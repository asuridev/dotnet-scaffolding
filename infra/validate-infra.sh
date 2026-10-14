#!/usr/bin/env bash
# validate-infra.sh — sondea la infraestructura de prueba de product-catalog.
# Un check por tecnología elegida en keel-stack.json; ejecuta cada CLI dentro
# del contenedor devtools (o del propio contenedor de la BD). Uso (desde la raíz
# del proyecto; con podman, exporta CONTAINER_RUNTIME=podman):
#   bash infra/up.sh && bash infra/validate-infra.sh
set -u

RUNTIME="${CONTAINER_RUNTIME:-}"
if [ -z "$RUNTIME" ]; then
  if command -v docker >/dev/null 2>&1; then RUNTIME=docker
  elif command -v podman >/dev/null 2>&1; then RUNTIME=podman
  else echo "No se encontró docker ni podman en el PATH." >&2; exit 2; fi
fi

fail=0
# Reintentos porque 'Up' no es 'listo': Keycloak en start-dev, Kafka y LocalStack
# publican su listener bastante después de que el contenedor arranque, y un solo
# intento a los pocos segundos de 'up -d' da un FALLO que a la segunda pasada es
# verde — un falso negativo que hace perder el tiempo buscando en la infra lo que
# no está roto. Con la infra sana el primer intento acierta y esto no cuesta nada.
RETRIES="${KEEL_CHECK_RETRIES:-5}"
DELAY="${KEEL_CHECK_DELAY:-5}"
check() {
  label="$1"; container="$2"; cmd="$3"
  attempt=1
  while [ "$attempt" -le "$RETRIES" ]; do
    if $RUNTIME exec "$container" sh -c "$cmd" >/dev/null 2>&1; then
      echo "  OK     $label"
      return
    fi
    attempt=$((attempt + 1))
    [ "$attempt" -le "$RETRIES" ] && sleep "$DELAY"
  done
  echo "  FALLO  $label (tras $RETRIES intentos)"
  fail=$((fail + 1))
}

echo "Validando infraestructura vía '$RUNTIME exec'…"
check 'PostgreSQL (db)' 'product-catalog-devtools' 'PGPASSWORD='\''changeme'\'' psql -h db -U product_catalog -d product_catalog -c '\''SELECT 1'\'' -q -t'

# Procesos ajenos a la validación que comparten esta infraestructura. Un
# 'dotnet run' olvidado escribe en la misma BD que la suite y contamina la matriz
# con datos que ningún escenario creó — un fallo que parece de negocio y no lo es.
# Es aviso, no error: el puerto ocupado no impide correr las pruebas (el arnés
# arranca el servidor en un puerto libre), solo explica resultados imposibles.
listeners=""
if command -v netstat >/dev/null 2>&1; then
  listeners=$(netstat -ano 2>/dev/null | grep -E "[:.]8080 " | grep -Ei "listen" || true)
elif command -v ss >/dev/null 2>&1; then
  listeners=$(ss -ltn 2>/dev/null | grep -E "[:.]8080 " || true)
fi
if [ -n "$listeners" ]; then
  echo "  AVISO  algo escucha en el puerto 8080: ¿un 'dotnet run' de otra sesión?"
  echo "         Comparte BD y broker con la suite: ciérralo antes de ejecutar bash infra/score-scenarios.sh."
fi

if [ "$fail" -ne 0 ]; then
  echo "$fail comprobación(es) fallaron. ¿Está la infraestructura arriba ('bash infra/up.sh') y lista?" >&2
  exit 1
fi
echo "Infraestructura OK."
