#!/usr/bin/env bash
# down.sh — para la infraestructura de prueba de product-catalog.
#
# Uso (desde la raíz del proyecto):
#   bash infra/down.sh           para los contenedores, CONSERVA los volúmenes
#   bash infra/down.sh --volumes los borra también (la BD vuelve a nacer vacía)
#
# Para limpiar el ESTADO entre flujos de validación no se usa esto: es
# infra/reset-db.sh, que vacía datos sin tirar los contenedores ni el historial de
# migraciones. Borrar los volúmenes obliga a rearrancar y resembrar la topología.
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

VOLUMES=""
if [ "${1:-}" = "--volumes" ] || [ "${1:-}" = "-v" ]; then
  VOLUMES="--volumes"
fi

echo "Parando la infraestructura de product-catalog…"
if [ -n "$VOLUMES" ]; then
  "${COMPOSE[@]}" down --volumes
  echo "Contenedores y volúmenes borrados."
else
  "${COMPOSE[@]}" down
  echo "Contenedores parados; los volúmenes siguen ahí (usa --volumes para borrarlos)."
fi
