#!/usr/bin/env bash
# reset-db.sh — deja el estado de prueba de product-catalog como recién arrancado:
# vacía los datos de la BD (esquema intacto).
# Ejecutar antes de cada flujo FL-* de specs/validation-scenarios.md: los Given
# asumen estado limpio. Uso (desde la raíz; con podman, exporta CONTAINER_RUNTIME=podman):
#   bash infra/reset-db.sh
#   bash infra/reset-db.sh --schema   # además, RECREA el esquema
#
# --schema es para después de regenerar el mapeo: el EnsureCreated del perfil local
# solo crea el esquema cuando la base no tiene ninguna tabla, así que una columna
# nueva no llega nunca a una base que ya existe, y el primer flujo falla con un error
# del motor que no apunta a la fila. Recrear el esquema es la salida; el volumen no se
# toca.
set -u

MODE=data
case "${1:-}" in
  --schema) MODE=schema ;;
  "") ;;
  *) echo "Uso: bash infra/reset-db.sh [--schema]" >&2; exit 2 ;;
esac

RUNTIME="${CONTAINER_RUNTIME:-}"
if [ -z "$RUNTIME" ]; then
  if command -v docker >/dev/null 2>&1; then RUNTIME=docker
  elif command -v podman >/dev/null 2>&1; then RUNTIME=podman
  else echo "No se encontró docker ni podman en el PATH." >&2; exit 2; fi
fi

if [ "$MODE" = schema ]; then
  if $RUNTIME exec 'product-catalog-devtools' sh -c 'PGPASSWORD='\''changeme'\'' psql -h db -U product_catalog -d product_catalog -v ON_ERROR_STOP=1 -q -c '\''DROP SCHEMA public CASCADE; CREATE SCHEMA public;'\'''; then
    echo "Esquema recreado (PostgreSQL): lo vuelve a crear EF Core al arrancar la app (EnsureCreated del perfil local)."
  else
    echo "FALLO al recrear el esquema. ¿Está la infraestructura arriba ('bash infra/up.sh')?" >&2
    exit 1
  fi
else
  if $RUNTIME exec 'product-catalog-devtools' sh -c 'PGPASSWORD='\''changeme'\'' psql -h db -U product_catalog -d product_catalog -v ON_ERROR_STOP=1 -q -c "DO \$\$ DECLARE stmt text; BEGIN SELECT '\''TRUNCATE TABLE '\'' || string_agg(quote_ident(tablename), '\'', '\'') || '\'' RESTART IDENTITY CASCADE'\'' INTO stmt FROM pg_tables WHERE schemaname = '\''public'\'' AND tablename <> '\''ef_migrations_history'\''; IF stmt IS NOT NULL THEN EXECUTE stmt; END IF; END \$\$;"'; then
    echo "Datos reseteados (PostgreSQL)."
  else
    echo "FALLO al resetear los datos. ¿Está la infraestructura arriba ('bash infra/up.sh')?" >&2
    exit 1
  fi
fi
