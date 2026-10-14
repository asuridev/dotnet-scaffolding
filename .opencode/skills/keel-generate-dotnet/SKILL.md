---
name: keel-generate-dotnet
description: Completa la generación de este microservicio .NET a partir del diseño Keel incluido en specs/, orquestando los subagentes de código, infraestructura, pruebas de integración, arbitraje y calidad. Usar dentro de este proyecto, sin argumentos.
---

# /keel-generate-dotnet — completar product-catalog-dotnet

Este proyecto lo generó `keel-dotnet build` desde `specs/product-catalog` v1.1.0 y es **autosuficiente**.
Se invoca **sin argumentos, con el cwd en esta raíz**; todas las rutas son relativas a ella. Tú eres el
**orquestador**: el trabajo lo hacen los subagentes de `.opencode/agent`. El pipeline completo, con sus porqués:
`docs/keel/orchestration.md`.

## Proceso

0. **Precondiciones.** `specs/` es un snapshot que `keel-dotnet build` ya validó: no lo revalides ni lo edites
   —el canónico es `specs/product-catalog/` del workspace, y un cambio funcional lo hace **el diseñador** allí y
   re-ejecuta `keel-dotnet build`—. **Tú no editas ni este snapshot ni el workspace.** Un escenario que contradice al
   diseño es un `culprit: design`: se **propone** en `design-gaps.yaml`, no se aplica (`infra/score-scenarios.sh`
   comprueba el sello de `specs/` y sale con `2` si cambió). Comprueba solo: que existe
   `specs/validation-scenarios.md` (sin escenarios no hay contra qué validar: detente y pide cerrar el diseño con
   `/keel-design`), que esta raíz es un repo git (si no, `git init -b main`), que está el SDK de .NET 10
   (`dotnet --list-sdks`) y que los paquetes se restauran (`dotnet restore`). El stack está en
   `keel-stack.json`: respétalo.
1. **Fase 1 — en paralelo** (los tres lanzados a la vez, en un único mensaje):
   - `keel-dotnet-code`: «Completa el proyecto en `.`. Tu proceso, tu alcance y tu criterio de terminado son los de
     tu archivo de agente; `AGENTS.md` es el contexto del repo, no tu lista de tareas.» — hasta `dotnet build`
     en verde, sin warnings.
   - `keel-dotnet-infra`: «Levanta y valida la infraestructura de `.` (`infra/`). Déjala arriba y reporta.»
   - `keel-dotnet-tests`: «Traduce los escenarios `FL-*` de `specs/validation-scenarios.md` a pruebas en
     `tests/ProductCatalog.IntegrationTests/` de `.`.» — una clase por flujo, en caja negra y **sin leer `src/`**, hasta
     `bash infra/check-flows.sh` en verde.

   **Espera a los tres sin hacer nada más.** Gating sobre su bloque estructurado: sin docker/podman
   (`infra status: PENDIENTE`) → **detente** (compilado pero NO validado). Infra KO corregible → relanza infra una
   vez. `code` con `compiles: false` → relánzalo con sus `failures` (máx. 2 ciclos). `tests` en KO por causa
   propia → relánzalo; si su KO viene de `src/`, no es suyo. `blockers` en cualquiera → detente y repórtalo.
2. **Fase 2a — puntuación mecánica (tú, sin agente).** Con los tres en OK: `bash infra/score-scenarios.sh`. Compila
   la solución, arranca por el **humo del arnés** y solo con él en verde ejecuta la suite y compone la matriz
   `FL-* → OK | FALLO | OMITIDO | NO_EJERC` desde el XML JUnit. La salida de `dotnet` va a
   `build/keel-scenarios/run.log` y la del servidor a `build/keel-scenarios/server.log`: no las vuelques en tu
   contexto salvo para diagnosticar. Según el código: `0` → fase 3 **sin invocar a nadie**; `1` → fase 2b; `2` →
   **nada que arbitrar**: con `BUILD: KO`, relanza a quien sea dueño de los errores que imprime (`src/` →
   `keel-dotnet-code`, el proyecto de flujos → `keel-dotnet-tests`); con el humo rojo, la matriz vacía, la suite en
   rojo por pruebas que no son escenarios o un flujo que no arrancó, relanza `keel-dotnet-tests` (no consume cupo)
   — salvo que el defecto esté fuera del proyecto de flujos (`infra/`, la solución, `Directory.*.props`): eso es de
   build, lo corriges tú y va al informe como fix del generador.
3. **Fase 2b — arbitraje.** Lanza `keel-dotnet-validate` con la matriz y, por cada fallo, su clase de flujo y su
   volcado de `build/keel-failures/`, y **espera sin ejecutar nada** (una pasada tuya del script le borraría la
   evidencia). Según `culprit`: `code` → `keel-dotnet-code` con **exactamente** sus `failures` (con
   `evidence`); `test` → `keel-dotnet-tests`; `harness` → `keel-dotnet-tests` exigiendo verificación amplia y
   `harnessPatches`; `design` → detente y propón el cambio. Tras cualquier ciclo de fix se vuelve **siempre** a
   `bash infra/score-scenarios.sh`. Mezcla de `code` con `test`/`harness` → **en serie**, primero `code`
   (comparten la base de prueba). Cupo de ciclos `blocking: scoped`, por número de flujos `FL-*`: hasta 10, 2 (tope
   4); de 11 a 20, 3 (tope 5); más de 20, 4 (tope 6). No lo consumen los `systemic` ni los `test`/`harness`.
   Alcanzado el tope, reporta la matriz y detente.
4. **Fase 3 — calidad + baseline de migraciones.** Solo con **todos** los escenarios OK: lanza
   `keel-dotnet-quality` y **espera sin tocar el proyecto**. Hace el pase no-conductual, comprueba la no-regresión
   (la suite al 100%) y `dotnet test`, y produce el **baseline de migraciones**: revisa el DDL del modelo (`infra/export-schema.sh`), crea la migración (`dotnet ef migrations add Baseline --project src/ProductCatalog.Infrastructure --output-dir Persistence/Migrations`) y la **verifica en vivo** (`infra/verify-baseline.sh`). Exige `baseline: OK` y `baselineTested: OK`: sin ellos el servicio pasa sus escenarios (corren en `local`) pero no arranca en `develop` ni en `production`. `KO` → relánzalo una vez con su error exacto. `status: KO` o `scenarios: KO` → revierte o reporta; nunca hagas commit con algo en rojo. Al terminar, baja la
   infraestructura: `bash infra/down.sh`.
5. **Guía de despliegue.** Con todo en verde y **antes** del commit, añade al `README.md` una sección
   `## Despliegue en producción` con los pasos y la tabla de parámetros obligatorios: todo `${VAR}` sin default de
   `config/parameters/production/*.yaml`, y que el esquema lo crean las migraciones de EF Core al arrancar.
   No inventes parámetros.
6. **Informe de generación.** Escribe `INFORME-GENERACION.md`, que **abre con la matriz final de
   `infra/score-scenarios.sh` y su código de salida, literales**, y sigue con lo que es del generador
   (`harnessPatches`, fixes de `infra/` o de la solución, `culprit: harness`, `probes[].verdict: FALSO-NEGATIVO`),
   cada entrada diciendo de quién es. Y `design-gaps.yaml` con **cada** `designGap` (schema
   `design-gaps.schema.json` de keel-core, con el `service` y la `version` de `specs/service.keel.yaml` y
   `generator: keel-dotnet`): `keel-dotnet check` lo imprime desde el workspace. Sin huecos, no se escribe; sin nada
   que reportar, dilo en una línea.
7. **Cerrar.** Commit (`Generado desde specs/product-catalog v1.1.0`; los `packages.lock.json` incluidos) y
   resumen: matriz, estado de cada agente, ajustes de calidad y huecos del diseño.

## Conocimiento local

- `AGENTS.md` — contexto del repo (diseño, stack, verificación).
- `docs/keel/orchestration.md` — el pipeline: fases, códigos, agentes, handoffs y ciclos.
- `docs/keel/architecture.md` y `docs/keel/constitution.md` — la arquitectura y sus reglas inviolables.
- `docs/keel/conventions/` — mapeo diseño → código (`mapping.md`, estricto), estructura y comandos
  (`project-layout.md`), el dominio (`domain-modeling.md`), la auditoría de cada handler (`flow-fidelity.md`), las
  pruebas de los escenarios (`integration-tests.md`) y la infraestructura (`infra-validation.md`).
- Skills por tecnología (`.opencode/skills`, hermanas de esta), instaladas solo las aplicables a este servicio: `keel-dotnet-database`. Cada una trae `references/` que se leen bajo demanda.
- Un flujo suelto, con la solución compilada: `dotnet tests/ProductCatalog.IntegrationTests/bin/Debug/net10.0/ProductCatalog.IntegrationTests.dll -class "ProductCatalog.IntegrationTests.<Flujo>Flow"`; el humo,
  `dotnet tests/ProductCatalog.IntegrationTests/bin/Debug/net10.0/ProductCatalog.IntegrationTests.dll -class "ProductCatalog.IntegrationTests.HarnessSmoke"`.

**Un solo actor sobre el proyecto, en todo momento.** Mientras un subagente esté vivo, tú no ejecutas nada sobre
este directorio: el script sobrescribe la evidencia que el árbitro lee, el reset vacía la base que otro está
usando, y en Windows el servidor de los flujos retiene el ejecutable del Api. Y el trabajo de un agente **no lo
haces tú aunque sepas hacerlo**: sus restricciones —el de pruebas sin leer `src/`, el árbitro sin corregir, el de
calidad sin cambiar comportamiento— son del agente. Tu trabajo propio es la fase 2a y los pasos 5 a 7.

En corto, de la constitución: el diseño es la única fuente de verdad funcional, los `code` de error se copian
exactos, los importes son `decimal` (nunca `double`) con escala y redondeo explícitos, ningún paquete con licencia
comercial, y ante ambigüedad, diseño > conventions > criterio. No des la generación por terminada con la
compilación en rojo o un escenario fallando.
