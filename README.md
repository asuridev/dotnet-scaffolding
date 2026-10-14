# product-catalog-dotnet

Gestiona el catálogo de productos publicables de la tienda.

Generado desde `specs/product-catalog` v1.1.0 con `keel-dotnet`. El diseño es la fuente de
verdad: un cambio funcional se hace en el workspace de diseño y se vuelve a ejecutar
`keel-dotnet build specs/product-catalog`.

Stack: PostgreSQL.

## Arrancar

Requiere el SDK de .NET 10 (`global.json` fija la banda; `dotnet --list-sdks` dice cuáles tienes).

```bash
dotnet restore                    # descarga los paquetes; la primera vez escribe packages.lock.json (commitéalos)
dotnet build                      # compila la solución ProductCatalog.slnx; un warning es un error
dotnet run --project src/ProductCatalog.Api   # PROFILE=local por defecto, puerto 8080
```

| Variable | Default | Qué controla |
|---|---|---|
| `PROFILE` | `local` | Perfil activo (varios separados por coma) |
| `SERVER_PORT` | `8080` | Puerto HTTP |
| `SERVER_ADDRESS` | `0.0.0.0` | Interfaz en la que escucha |
| `SHUTDOWN_TIMEOUT` | `30s` | Margen del apagado ordenado |

Sondas: `GET /livez` y `GET /readyz` → `{"status":"UP"}`; durante el apagado, `/readyz` responde
503 `{"status":"OUT_OF_SERVICE"}`.

## Completar el servicio

Build deja el proyecto compilando y arrancando, con la lógica de negocio por escribir (los handlers terminan en
`TODO`). La completa `/keel-generate-dotnet`, sin argumentos y desde esta raíz: orquesta los agentes de código,
infraestructura, pruebas, arbitraje y calidad hasta el 100% de los escenarios `FL-*` de
`specs/validation-scenarios.md` (`docs/keel/orchestration.md`).

## Pruebas

```bash
dotnet test                       # xUnit v3 sobre Microsoft.Testing.Platform, bajo el perfil test (sin infraestructura)
bash infra/up.sh                  # la infraestructura de prueba (docker o podman)
bash infra/score-scenarios.sh     # compila, y ejecuta y puntúa las pruebas de flujo FL-* contra el servidor real
```

Las pruebas de flujo (`tests/ProductCatalog.IntegrationTests`) están fuera de `dotnet test` a propósito: necesitan la
infraestructura. Son caja negra: no ven ningún tipo del servicio y lo arrancan como proceso aparte.

**Una ruta corta.** Windows limita las rutas a 260 caracteres y el compilador no lo dice: bajo un directorio muy
profundo, un proyecto compila sin dejar su DLL (`MSB3030`). Si aparece, mueve el proyecto a una ruta más corta.

## La solución

| Proyecto | Qué contiene | Ve a |
|---|---|---|
| `ProductCatalog.Domain` | el dominio, en C# puro: value objects, agregados, eventos, errores | nadie |
| `ProductCatalog.Application` | los casos de uso: mensajes, handlers, DTOs, mappers, contrato del cable | Domain |
| `ProductCatalog.Infrastructure` | configuración, mediator, persistencia, mensajería, sondas | Application |
| `ProductCatalog.Api` | el arranque y la API HTTP | Infrastructure |
| `ProductCatalog.UnitTests` | las pruebas del arranque, del cable y del cableado de los casos de uso | Api |
| `ProductCatalog.IntegrationTests` | las pruebas de flujo `FL-*`, contra el servidor arrancado | ninguno (caja negra) |

La frontera hexagonal la impone el compilador: un proyecto solo ve a los que referencia.

## Despliegue en producción

1. Compila y publica: `dotnet publish src/ProductCatalog.Api -c Release -o out`.
2. Define las variables obligatorias de la tabla siguiente (PostgreSQL accesible desde el servicio).
3. Arranca con `PROFILE=production` (`dotnet out/ProductCatalog.Api.dll`).
4. El esquema lo crean las **migraciones de EF Core al arrancar** (`database.schema: migrate`); no hay que preparar tablas a mano.
5. Comprueba `GET /livez` y `GET /readyz`.

Parámetros obligatorios (`${VAR}` sin default en `config/parameters/production/*.yaml`):

| Variable | Qué es |
|---|---|
| `DB_URL` | URL de conexión a PostgreSQL (`jdbc:postgresql://host:puerto/base`) |
| `DB_USERNAME` | Usuario de la base |
| `DB_PASSWORD` | Contraseña de la base |

Opcionales: `DB_POOL_MAX_SIZE` (10), `DB_POOL_CONNECTION_TIMEOUT_MS` (5000), `DB_POOL_MAX_LIFETIME_MS` (1800000),
`DB_TRANSACTION_TIMEOUT` (30s), `SERVER_PORT` (8080), `SERVER_ADDRESS` (0.0.0.0), `SHUTDOWN_TIMEOUT` (30s).
