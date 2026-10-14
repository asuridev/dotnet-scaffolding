#!/usr/bin/env bash
# export-schema.sh — el DDL que el modelo de EF Core de product-catalog crearía, para REVISARLO
# antes de escribir el baseline de migraciones. No necesita base: lo deriva del modelo.
#
# Uso (desde la raíz del proyecto):
#   bash infra/export-schema.sh      → build/schema/model.sql
# Después, el baseline (pase de calidad):
#   dotnet ef migrations add Baseline --project src/ProductCatalog.Infrastructure --output-dir Persistence/Migrations
#   bash infra/verify-baseline.sh
set -u
cd "$(dirname "$0")/.."
mkdir -p build/schema

if ! dotnet tool restore >build/schema/tool-restore.log 2>&1; then
  echo "dotnet tool restore falló (¿red, o el SDK?):" >&2
  tail -20 build/schema/tool-restore.log >&2
  exit 1
fi
if ! dotnet ef dbcontext script --project src/ProductCatalog.Infrastructure --output build/schema/model.sql >build/schema/export.log 2>&1; then
  echo "dotnet ef no pudo derivar el DDL del modelo (¿compila la solución?):" >&2
  tail -30 build/schema/export.log >&2
  exit 1
fi
echo "DDL del modelo → build/schema/model.sql"
