#!/usr/bin/env bash
# check-domain-guards.sh — comprueba que el formato de los value types escalares de
# product-catalog se hace cumplir en alguna parte.
#
# build genera la clase <Tipo>Format con la regex del diseño; la LLAMADA es del agente, en el factory
# de la entidad o en el método de negocio que asigna el campo, siempre DESPUÉS de normalizar. Sin esa
# llamada el servicio acepta valores que el diseño declara imposibles, compila, arranca y pasa todos
# los escenarios que no lo miren.
#
# Es una comprobación ESTRUCTURAL: caza la ausencia, no juzga si el sitio elegido es el mejor.
#
# Uso (desde la raíz del proyecto; no necesita infraestructura ni compilar):
#   bash infra/check-domain-guards.sh
#
# Código de salida:
#   0  todos los campos con formato tienen quien lo haga cumplir
#   1  hay hallazgos → vuelven al agente de código
set -u

SRC="src"

if [ ! -d "$SRC" ]; then
  echo "Ejecuta el script desde la raíz del proyecto (no se encontró $SRC)." >&2
  exit 1
fi

findings=0
detail=""

note() {  # texto
  detail="$detail  $1\n"
  findings=$((findings + 1))
}

# Un campo con formato declarado: su clase existe y alguien la llama.
guard() {  # sujeto, clase, archivo de la clase, porqué
  local subject="$1" class="$2" declaration="$3" why="$4"
  # Autochequeo: si la clase no está, lo que falta es la generación, no el uso, y reportarlo como
  # uso ausente mandaría al agente a escribir una llamada a algo que no existe.
  if [ ! -f "$declaration" ]; then
    note "$subject: no existe $declaration — lo genera build desde el diseño; regenera el proyecto"
    return
  fi
  # La propia declaración fuera: la clase se nombra a sí misma y contaría como su uso. Y los
  # comentarios fuera: build deja en el factory un TODO que NOMBRA la llamada que falta, así que
  # mirando la prosa el gate saldría verde por su propio aviso.
  local caller=""
  local file code
  while IFS= read -r file; do
    [ -n "$file" ] || continue
    [ "$file" != "$declaration" ] || continue
    code="$(sed -e 's://.*::' -e '/^[[:space:]]*\*/d' -e '/^[[:space:]]*\/\*/d' -e '/^[[:space:]]*using /d' "$file")"
    if printf '%s' "$code" | grep -qE -- "$class[[:space:]]*\.[[:space:]]*(Validate|Matches)[[:space:]]*\("; then
      caller="$file"
      break
    fi
  done <<EOF
$(grep -rlF --include='*.cs' -- "$class." "$SRC" 2>/dev/null)
EOF
  [ -n "$caller" ] || note "$subject: nadie llama a $class.Validate(...) — $why"
}

guard 'Product.sku' 'SKUFormat' 'src/ProductCatalog.Domain/ValueObject/SKUFormat.cs' 'el tipo SKU declara un formato y este campo no lo hace cumplir en ninguna parte'

echo ""
echo "FORMATO DE LOS VALUE TYPES"
if [ "$findings" -eq 0 ]; then echo "  valueTypeFormat      OK"; else echo "  valueTypeFormat      KO"; fi

if [ "$findings" -gt 0 ]; then
  echo ""
  echo "HALLAZGOS ($findings)"
  printf '%b' "$detail"
  echo ""
  echo "El lector de la petición valida el formato heredado de un value type, pero lo que no entra por"
  echo "la API (un evento, una operación interna) no pasa por él. La clase <Tipo>Format lleva la regex"
  echo "del diseño; llámala en el factory de la entidad o en el método de negocio que asigna el campo."
  echo "Sin esa llamada el servicio acepta por esas puertas valores que el diseño declara imposibles,"
  echo "y ningún escenario que no lo mire lo delata."
  exit 1
fi

exit 0
