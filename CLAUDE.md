# product-catalog-dotnet

Servicio .NET generado por `keel-dotnet build` desde `specs/product-catalog` v1.1.0. Es el equivalente
de los servidores de keel-spring y keel-nest del mismo diseño: mismo contrato HTTP, mismo cable, mismo esquema.

- **Diseño**: `specs/` es un snapshot del diseño Keel; el canónico vive en el workspace de diseño. No se edita aquí.
- **Capas declaradas**: api, persistence.
- **Stack** (`keel-stack.json`): PostgreSQL.
- **Arquitectura**: hexagonal + CQRS (`docs/keel/architecture.md`), un proyecto por capa (`ProductCatalog.Domain`, `ProductCatalog.Application`, `ProductCatalog.Infrastructure`, `ProductCatalog.Api`): la frontera la impone el compilador con las referencias entre proyectos. Reglas inviolables: `docs/keel/constitution.md`.
- **Configuración**: perfil por `PROFILE` (default `local`), `config/application.yaml` + `config/parameters/<perfil>/`; `${VAR}` es obligatoria y `${VAR:default}` opcional.
- **Casos de uso**: un mensaje y un handler por operación, despachados por `UseCaseMediator`, que abre la transacción; el registro de los handlers es generado (`UseCaseRegistration`). Los handlers nacen con las notas del diseño y terminan en `throw new NotImplementedException("TODO: <operación>")`.
- **Esquema**: en `local` lo crea EF Core desde el modelo (`EnsureCreated`); en `develop` y `production`, las migraciones de EF Core al arrancar.
- **Paquetes**: solo con licencia permisiva y versión exacta (`Directory.Packages.props`); nunca uno que haya pasado a licencia comercial (MediatR, AutoMapper, MassTransit, FluentAssertions…).

## Completar el servicio

`/keel-generate-dotnet`, sin argumentos y con el cwd en esta raíz: orquesta los agentes de código, infraestructura, pruebas,
arbitraje y calidad hasta el 100% de los escenarios `FL-*`. El pipeline: `docs/keel/orchestration.md`.

## Verificación

```bash
dotnet restore                     # la primera vez escribe packages.lock.json: commitéalos
dotnet build                       # la solución entera; un warning es un error
dotnet test                        # las pruebas de build (perfil test, sin infraestructura)
bash infra/up.sh && bash infra/validate-infra.sh   # la infraestructura de prueba
bash infra/score-scenarios.sh                      # compila + humo del arnés + flujos FL-* + matriz
```

`infra/check-domain-guards.sh` sale en ROJO recién generado a propósito: el formato de los value types escalares lo hace cumplir `<Tipo>Format.Validate(...)`, y esa llamada es del agente.
