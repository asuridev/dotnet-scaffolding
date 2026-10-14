#!/usr/bin/env bash
# check-flows.sh — compila las pruebas de flujo (tests/ProductCatalog.IntegrationTests/) sin el código del servicio.
#
# El proyecto de flujos no ve ningún tipo del servicio (caja negra): compila solo, y aquí se
# compila SIN construir el Api, así que un src/ a medio escribir no lo pone en rojo. Además
# comprueba que nadie le añadió una referencia que rompa la caja negra.
#
# Uso (desde la raíz; no necesita infraestructura):
#   bash infra/check-flows.sh
# Sale con 0 si las pruebas de flujo compilan y respetan la caja negra, y con 1 si no.
set -u
cd "$(dirname "$0")/.."
mkdir -p build

PROJECT="tests/ProductCatalog.IntegrationTests/ProductCatalog.IntegrationTests.csproj"
fail=0

# La caja negra: la única referencia a un proyecto del servicio es la que ordena la compilación
# del Api sin ver sus tipos.
visible="$(grep -E '<ProjectReference' "$PROJECT" | grep -v 'ReferenceOutputAssembly="false"' || true)"
if [ -n "$visible" ]; then
  echo "Caja negra rota: el proyecto de flujos referencia un proyecto del servicio y ve sus tipos:"
  printf '%s\n' "$visible" | sed 's/^ */  /'
  fail=1
fi

if ! dotnet build "$PROJECT" -c Debug --nologo -p:BuildProjectReferences=false >build/keel-flows-build.log 2>&1; then
  echo "Las pruebas de flujo no compilan:"
  grep -E '(error|warning) [A-Z]+[0-9]+' build/keel-flows-build.log | sed 's/^/  /' | sort -u | head -30
  fail=1
fi
[ "$fail" -eq 0 ] && echo "Pruebas de flujo: compilan y respetan la caja negra."
exit "$fail"
