// Los flujos comparten la base y el servidor: en serie, nunca a la vez. El reset es por flujo (clase), y dos
// flujos simultáneos se borrarían el Given el uno al otro.
using ProductCatalog.IntegrationTests.Support;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.None)]
[assembly: TestMethodOrderer(typeof(ScenarioOrderer))]
[assembly: AssemblyFixture(typeof(FlowServerShutdown))]
