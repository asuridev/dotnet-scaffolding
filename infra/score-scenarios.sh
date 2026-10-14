#!/usr/bin/env bash
# score-scenarios.sh — compila la solución, ejecuta las pruebas de flujo de product-catalog y
# puntúa los escenarios FL-* contra specs/validation-scenarios.md.
#
# La matriz sale del XML JUnit que escribe xUnit v3, sin criterio de por medio: el título de
# cada caso ([Scenario("FL-PRD-001-A: …")]) lleva el id delante de los dos puntos. Arbitrar de
# quién es la culpa de un fallo NO es trabajo de este script — para eso está el agente de
# validación, que se invoca solo si aquí sale algo en rojo.
#
# Uso (desde la raíz del proyecto, con la infraestructura arriba):
#   bash infra/score-scenarios.sh           # compila + humo del arnés + suite + matriz
#   bash infra/score-scenarios.sh --score   # solo re-puntúa el XML ya existente
#
# Salida: la matriz por stdout (la de dotnet va al log). Código de salida:
#   0  todos los escenarios en OK
#   1  hay FALLO o NO_EJERCITADO → hay algo que arbitrar
#   2  precondición o arnés roto Y NADA QUE ARBITRAR: la solución no compila, la suite no se
#      ejecutó, o un flujo no llegó a arrancar sin dejar ni un FL-* en FALLO. Si además hay
#      escenarios en FALLO, sale 1: esos se arbitran.
#   (3, entorno bloqueado, es del script de keel-spring: dotnet no deja workers que sostengan
#   el directorio de resultados entre corridas.)
set -u

SOLUTION="ProductCatalog.slnx"
RESULTS="build/test-results/integration"
LOG_DIR="build/keel-scenarios"
LOG="$LOG_DIR/run.log"
EVIDENCE="build/keel-failures"
SCENARIOS="specs/validation-scenarios.md"
FLOWS="tests/ProductCatalog.IntegrationTests/bin/Debug/net10.0/ProductCatalog.IntegrationTests.dll"

if [ ! -f "$SOLUTION" ]; then
  echo "Ejecuta el script desde la raíz del proyecto (no se encontró $SOLUTION)." >&2
  exit 2
fi
mkdir -p "$LOG_DIR"

# La evidencia de la corrida ANTERIOR se borra antes de empezar: un volcado que sobrevive a
# una corrida verde se lee igual que uno recién escrito, y engaña.
rm -rf "$EVIDENCE"

# El snapshot del diseño tiene que ser el que build escribió. `specs.sha256` es su sello: una
# línea «<sha256>  <ruta>» por archivo de specs/, calculada SIN retornos de carro para que un
# checkout con autocrlf no la rompa. Si no casa, alguien editó el diseño desde aquí, y un
# escenario corregido por quien tiene la suite delante deja de medir lo que el diseño dijo:
# eso es un culpable 'design' que se PROPONE (design-gaps.yaml), no se aplica. En la corrida
# `catalog` (2026-09-21) se aplicó, la suite cerró en verde y el hueco no volvió al método.
if [ -f "specs.sha256" ]; then
  if command -v sha256sum >/dev/null 2>&1; then sha_cmd="sha256sum"; else sha_cmd="shasum -a 256"; fi
  seal_broken=""
  while read -r expected file; do
    [ -n "$file" ] || continue
    if [ ! -f "$file" ]; then seal_broken="$seal_broken $file(falta)"; continue; fi
    actual="$(tr -d '\r' < "$file" | $sha_cmd | cut -d' ' -f1)"
    [ "$actual" = "$expected" ] || seal_broken="$seal_broken $file"
  done < "specs.sha256"
  if [ -n "$seal_broken" ]; then
    echo "DISEÑO: el snapshot specs/ no es el que escribió keel-dotnet build:$seal_broken"
    echo "  La suite NO se ejecuta contra un diseño editado desde el proyecto generado."
    echo "  Si un escenario contradice al diseño, es un hueco del DISEÑO: va a design-gaps.yaml y al"
    echo "  informe, y lo corrige el diseñador en el workspace re-ejecutando keel-dotnet build."
    echo "  Para deshacer la edición: git checkout -- specs/"
    exit 2
  fi
fi

score_only=0
[ "${1:-}" = "--score" ] && score_only=1
suite_failed=0

# Los casos de las pruebas de flujo, con su reporte JUnit. El ejecutable es el propio proyecto
# de pruebas, con el runner de xUnit v3 (no Microsoft.Testing.Platform: el proyecto está fuera
# de dotnet test a propósito).
flows() {
  local report="$1"
  shift
  mkdir -p "$RESULTS"
  dotnet "$FLOWS" -noLogo -noColor -result-junit "$RESULTS/$report" "$@"
}

if [ "$score_only" -eq 0 ]; then
  # Re-puntuar (--score) solo lee XML; compilar y ejecutar sí necesita el SDK.
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "No está el SDK de .NET (dotnet) en el PATH: instálalo (global.json dice qué banda)." >&2
    exit 2
  fi
  rm -rf "$RESULTS"
  echo "Compilando la solución…"
  if ! dotnet build "$SOLUTION" -c Debug --nologo >"$LOG" 2>&1; then
    echo ""
    echo "BUILD: KO — la solución no compila; la suite NO se ejecutó."
    grep -E 'error [A-Z]+[0-9]+' "$LOG" | sed 's/^/  /' | sort -u | head -20
    echo "  log: $LOG"
    exit 2
  fi
  # Humo del arnés primero: son segundos y comprueba la fontanería de la que dependen
  # TODOS los flujos. En rojo no se ejecuta la suite.
  echo "Humo del arnés (HarnessSmoke)…"
  if ! flows smoke.xml -class "ProductCatalog.IntegrationTests.HarnessSmoke" >>"$LOG" 2>&1 \
     || grep -qE '<(failure|error|skipped)[ />]' "$RESULTS/smoke.xml" 2>/dev/null; then
    echo ""
    echo "HARNESS: KO — la suite NO se ejecutó."
    echo "  El defecto está en el andamiaje que generó build (tests/ProductCatalog.IntegrationTests/Support/,"
    echo "  HarnessSmoke.cs) o falta infraestructura (bash infra/up.sh && bash infra/validate-infra.sh)."
    if ls "$EVIDENCE"/*.json >/dev/null 2>&1; then
      echo "  evidencia: $EVIDENCE/"
    fi
    echo "  log del servidor: build/keel-scenarios/server.log"
    echo "  log: $LOG"
    exit 2
  fi
  echo "Humo del arnés: OK."
  rm -rf "$EVIDENCE" "$RESULTS"
  echo "Ejecutando la suite completa…"
  # El código de salida se GUARDA: la matriz solo mira los FL-*, y una prueba en rojo que no
  # sea un escenario no aparecería en ninguna fila.
  flows junit.xml >>"$LOG" 2>&1 || suite_failed=1
fi

if ! ls "$RESULTS"/*.xml >/dev/null 2>&1; then
  echo ""
  echo "No hay resultados en $RESULTS: la suite no llegó a ejecutarse."
  echo "  log: $LOG"
  exit 2
fi

# Matriz desde el XML de JUnit (los mismos programas que puntúan el de keel-spring y keel-nest).
matrix="$(awk '
  BEGIN { RS = "<testcase " }
  NR == 1 { next }
  {
    rec = $0
    close_tag = index(rec, "</testcase>")
    self_tag = index(rec, "/>")
    if (close_tag > 0 && (self_tag == 0 || close_tag < self_tag)) seg = substr(rec, 1, close_tag)
    else if (self_tag > 0) seg = substr(rec, 1, self_tag)
    else seg = rec

    name = ""; cls = ""
    # El atributo EXACTO: un /name=/ a secas casa dentro de classname="…", y el orden de los
    # dos lo decide el runner (Gradle escribe name primero; Vitest, classname).
    if (match(" " seg, /[ \t\n]name="[^"]*"/)) name = substr(" " seg, RSTART + 7, RLENGTH - 8)
    if (match(seg, /classname="[^"]*"/)) cls = substr(seg, RSTART + 11, RLENGTH - 12)
    if (name == "") next

    # Sin classname no es un test: es el nodo contenedor que algunos runners
    # emiten para la clase. Su @DisplayName suele empezar por el id del flujo, y
    # colarlo duplica la fila y falsea el recuento (9 escenarios donde hay 6).
    if (cls == "") next

    id = name
    if (index(id, ":") > 0) id = substr(id, 1, index(id, ":") - 1)
    gsub(/^[ \t]+|[ \t]+$/, "", id)
    # El id es un token, nunca una frase: un @DisplayName de clase como
    # "FL-RES-001 · alta de reserva" no es un escenario.
    if (id !~ /^FL-[A-Za-z0-9-]+$/) next

    sub(/^.*\./, "", cls)
    if (seg ~ /<(failure|error)[ >]/) print "FALLO\t" id "\t" cls
    else if (seg ~ /<skipped[ \/>]/) print "OMITIDO\t" id "\t" cls
    else print "OK\t" id "\t" cls
  }
' "$RESULTS"/*.xml 2>/dev/null | sort -k2,2)"

# Escenarios que el documento declara y ninguna prueba ejercita (cobertura que falta), y pruebas
# FL-* que el documento no declara (no puntúan): keel-core/gen/junit-scoring, el mismo cruce que en
# keel-spring.
uncovered=""
foreign=""
foreign_rows=""
foreign_failures=""
if [ -f "$SCENARIOS" ]; then
  declared="$(grep -oE '^#{1,6}[[:space:]]*FL-[A-Za-z0-9-]+' "$SCENARIOS" \
              | grep -oE 'FL-[A-Za-z0-9-]+' | sort -u)"
  covered="$(printf '%s\n' "$matrix" | cut -f2 | sort -u)"
  for id in $declared; do
    printf '%s\n' "$covered" | grep -qE "^${id}(-|$)" || uncovered="$uncovered $id"
  done
  for id in $covered; do
    owned=0
    for declared_id in $declared; do
      case "$id" in "$declared_id"|"$declared_id"-*) owned=1; break ;; esac
    done
    [ "$owned" -eq 1 ] || foreign="$foreign $id"
  done
  if [ -n "$foreign" ]; then
    foreign_rows="$(printf '%s\n' "$matrix" | awk -F '\t' -v ids="$foreign" \
      'BEGIN { n = split(ids, list, " "); for (i = 1; i <= n; i++) skip[list[i]] = 1 } ($2 in skip)')"
    matrix="$(printf '%s\n' "$matrix" | awk -F '\t' -v ids="$foreign" \
      'BEGIN { n = split(ids, list, " "); for (i = 1; i <= n; i++) skip[list[i]] = 1 } NF > 0 && !($2 in skip)')"
    foreign_failures="$(printf '%s\n' "$foreign_rows" | awk -F '\t' \
      '$1 == "FALLO" { printf "    %s  (%s)\n      el documento de escenarios no declara %s\n", $2, $3, $2 }')"
  fi
fi

echo ""
echo "MATRIZ"
printf '%s\n' "$matrix" | while IFS="$(printf '\t')" read -r result id cls; do
  [ -n "${id:-}" ] || continue
  if [ "$result" = "FALLO" ]; then
    printf '  %-8s %-20s %-28s %s\n' "$result" "$id" "$cls" "$EVIDENCE/$id.json"
  else
    printf '  %-8s %-20s %s\n' "$result" "$id" "$cls"
  fi
done
for id in $uncovered; do
  printf '  %-8s %s\n' "NO_EJERC" "$id"
done
if [ -n "$foreign" ]; then
  printf '%s\n' "$foreign_rows" | while IFS="$(printf '\t')" read -r result id cls; do
    [ -n "${id:-}" ] || continue
    printf '  %-8s %-20s %-28s %s\n' "AJENO" "$id" "$cls" "($result: el documento no lo declara; no puntúa)"
  done
fi

ok=$(printf '%s\n' "$matrix" | grep -c '^OK')
ko=$(printf '%s\n' "$matrix" | grep -c '^FALLO')
sk=$(printf '%s\n' "$matrix" | grep -c '^OMITIDO')
nc=0
for id in $uncovered; do nc=$((nc + 1)); done

# Las pruebas en rojo que NO son escenarios: la matriz solo conoce los FL-*.
non_scenario_failures() {
  awk '
    BEGIN { RS = "<testcase " }
    NR == 1 { next }
    {
      rec = $0
      close_tag = index(rec, "</testcase>")
      self_tag = index(rec, "/>")
      if (close_tag > 0 && (self_tag == 0 || close_tag < self_tag)) seg = substr(rec, 1, close_tag)
      else if (self_tag > 0) seg = substr(rec, 1, self_tag)
      else seg = rec
      name = ""; cls = ""
      if (match(" " seg, /[ \t\n]name="[^"]*"/)) name = substr(" " seg, RSTART + 7, RLENGTH - 8)
      if (match(seg, /classname="[^"]*"/)) cls = substr(seg, RSTART + 11, RLENGTH - 12)
      if (name == "" || cls == "") next
      if (seg !~ /<(failure|error)[ >]/) next
      id = name
      if (index(id, ":") > 0) id = substr(id, 1, index(id, ":") - 1)
      gsub(/^[ \t]+|[ \t]+$/, "", id)
      if (id ~ /^FL-[A-Za-z0-9-]+$/) next
      sub(/.*\./, "", cls)
      # El MENSAJE, que es lo único que convierte esta línea en accionable. Estaba aquí desde
      # siempre —el XML de JUnit lo guarda entero— y se descartaba: por stdout salía solo la clase de la
      # excepción y la línea donde saltó, y a diagnosticarlo se le iba un ciclo completo. Se colapsa a
      # una línea y se acota: quien lee esto es la sesión más larga del pipeline.
      msg = ""
      if (match(seg, /<(failure|error)[^>]*message="[^"]*"/)) {
        frag = substr(seg, RSTART, RLENGTH)
        if (match(frag, /message="[^"]*"/)) msg = substr(frag, RSTART + 9, RLENGTH - 10)
      }
      # El salto de línea en decimal (Gradle, Vitest) o en hexadecimal (xUnit v3), y la comilla que
      # xUnit escapa además con una barra (\&quot;): antes que &quot; a secas, o la barra se queda.
      gsub(/&#10;/, " ", msg); gsub(/&#13;/, " ", msg)
      gsub(/&#[xX][aA];/, " ", msg); gsub(/&#[xX][dD];/, " ", msg)
      gsub(/\\&quot;/, "\"", msg)
      gsub(/&quot;/, "\"", msg)
      gsub(/&lt;/, "<", msg); gsub(/&gt;/, ">", msg)
      gsub(/&amp;/, "\\&", msg)
      gsub(/  +/, " ", msg)
      if (length(msg) > 400) msg = substr(msg, 1, 400) " [...]"
      printf "    %s  (%s)\n", name, cls
      if (msg != "") printf "      %s\n", msg
    }
  ' "$RESULTS"/*.xml 2>/dev/null
}

# Los flujos que no llegaron a arrancar. La base da sus casos por OMITIDOS, no por fallidos —el
# gemelo del initializationError de JUnit—, y deja el volcado <Flujo>-init.json. Sin esto,
# «omitido» diría «alguien lo saltó» cuando lo que hubo fue un rojo del arranque.
init_failures() {
  for file in "$EVIDENCE"/*-init.json; do
    [ -f "$file" ] || continue
    flow="$(basename "$file" -init.json)"
    msg="$(sed -n 's/^  "assertion": "\(.*\)",\{0,1\}$/\1/p' "$file" | sed 's/\\n/ /g' | cut -c1-400)"
    echo "    $flow  (arranque del flujo)"
    [ -n "$msg" ] && echo "      $msg"
  done
}

broken="$(non_scenario_failures)$(init_failures)"
# Las pruebas en rojo con un id FL-* que el documento no declara tampoco salen en la matriz.
if [ -n "$foreign_failures" ]; then broken="$(printf '%s\n%s' "$broken" "$foreign_failures")"; fi

echo ""
if [ "$ko" -eq 0 ] && [ "$sk" -eq 0 ] && [ "$nc" -eq 0 ] && [ "$ok" -gt 0 ]; then
  if [ "$suite_failed" -ne 0 ]; then
    echo "RESULTADO: KO — los $ok escenario(s) FL-* están en OK, pero la suite falló."
    echo "  Hay pruebas en rojo que NO son escenarios y por eso no salen en la matriz:"
    printf '%s\n' "$broken"
    echo "  log completo de dotnet: $LOG"
    echo "  Son del agente de pruebas, no del diseño: o las arregla o las retira."
    echo "  Sale con 2 y NO con 1: no hay ningún FL-* en FALLO, así que no hay ningún Then"
    echo "  que arbitrar."
    exit 2
  fi
  echo "RESULTADO: OK — $ok escenario(s) al 100%."
  exit 0
fi

echo "RESULTADO: KO — $ok OK · $ko FALLO · $sk omitido(s) · $nc no ejercitado(s)."
echo "  evidencia por fallo: $EVIDENCE/<FL-id>.json (request, response y aserción)"
echo "  log del servidor: build/keel-scenarios/server.log"
echo "  log completo de dotnet: $LOG"
if [ -n "$broken" ]; then
  echo ""
  echo "  ARNÉS: hay pruebas en rojo que NO son escenarios, o flujos que no llegaron a arrancar."
  printf '%s\n' "$broken"
  echo "  Un flujo que no arrancó deja sus FL-* como OMITIDO: no es falta de cobertura ni un"
  echo "  salto. La causa va debajo de cada uno; el volcado, en $EVIDENCE/<Flujo>-init.json."
  if [ "$ko" -eq 0 ]; then
    echo "  No hay ningún escenario en FALLO: no hay nada que arbitrar, así que esto sale"
    echo "  con 2 (arnés roto) y no con 1: se arregla el flujo y se vuelve a puntuar."
    exit 2
  fi
  echo "  PERO hay $ko escenario(s) FL-* en FALLO, y eso SÍ se arbitra: esto sale con 1."
fi

# La matriz VACÍA no es un rojo que arbitrar: la suite corrió y no ejercitó ni un FL-*.
if [ "$ko" -eq 0 ] && [ "$sk" -eq 0 ] && [ "$nc" -eq 0 ]; then
  echo ""
  echo "  ARNÉS: la matriz está VACÍA — la suite no ejercitó ni un escenario FL-*."
  echo "  No hay nada que arbitrar: sale con 2 y vuelve al agente de pruebas."
  exit 2
fi
exit 1
