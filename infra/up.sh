#!/usr/bin/env bash
# up.sh — levanta la infraestructura de prueba de product-catalog.
#
# Uso (desde la raíz del proyecto):
#   bash infra/up.sh && bash infra/validate-infra.sh
#
# Resuelve el runtime (docker o podman, o $CONTAINER_RUNTIME) y el frontend de
# compose. Levantar NO es estar listo: Kafka, Keycloak y LocalStack publican su
# listener bastante después de que el contenedor esté 'Up', así que quien decide si
# se puede empezar es validate-infra.sh, que reintenta.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
COMPOSE_FILE="$HERE/docker-compose.yaml"

RUNTIME="${CONTAINER_RUNTIME:-}"
if [ -z "$RUNTIME" ]; then
  if command -v docker >/dev/null 2>&1; then RUNTIME=docker
  elif command -v podman >/dev/null 2>&1; then RUNTIME=podman
  else echo "No se encontró docker ni podman en el PATH." >&2; exit 2; fi
fi

hostpath() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

COMPOSE=()
if [ "$RUNTIME" = "podman" ] && ! podman compose ls >/dev/null 2>&1; then
  # El frontend delegado no alcanza el motor: podman-compose es un binario aparte
  # que habla con podman directamente.
  if command -v podman-compose >/dev/null 2>&1; then
    COMPOSE=(podman-compose -f "$(hostpath "$COMPOSE_FILE")")
  else
    echo "podman no puede ejecutar compose ('podman compose ls' falla) y no encuentro podman-compose." >&2
    echo "Instala podman-compose ('pip install podman-compose') o arranca la máquina ('podman machine start')." >&2
    exit 2
  fi
else
  COMPOSE=("$RUNTIME" compose -f "$(hostpath "$COMPOSE_FILE")")
fi

echo "Levantando la infraestructura de product-catalog con '${COMPOSE[*]}'…"
"${COMPOSE[@]}" up -d

echo ""
echo "Contenedores arriba. Comprueba que están LISTOS antes de usarlos:"
echo "  bash infra/validate-infra.sh"
