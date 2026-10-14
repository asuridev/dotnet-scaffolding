namespace ProductCatalog.IntegrationTests.Support;

using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

/// <summary>
/// Un escenario de un flujo: el título (<c>FL-PRD-001-A: …</c>) es el nombre del caso en el XML, y los escenarios
/// de un flujo se ejecutan en el orden en que están ESCRITOS (el de keel-spring con <c>@Order</c> y el de keel-nest
/// por declaración). xUnit, por defecto, los ordena por un hash.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScenarioAttribute : FactAttribute
{
    public ScenarioAttribute(string title, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        DisplayName = title;
        Line = sourceLineNumber;
    }

    /// <summary>La línea del escenario en su archivo: el orden de ejecución dentro del flujo.</summary>
    public int Line { get; }
}

/// <summary>
/// Ordena los escenarios de un flujo por la línea de su <see cref="ScenarioAttribute"/> (lo demás, al final). En
/// xUnit v3 el orden de los MÉTODOS de una clase es de un <see cref="ITestMethodOrderer"/>, no del de casos.
/// </summary>
public sealed class ScenarioOrderer : ITestMethodOrderer
{
    public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?> testMethods)
        where TTestMethod : notnull, ITestMethod =>
        testMethods.OrderBy(LineOf).ThenBy(testMethod => testMethod?.MethodName, StringComparer.Ordinal).ToList();

    private static int LineOf<TTestMethod>(TTestMethod? testMethod)
        where TTestMethod : notnull, ITestMethod =>
        testMethod is IXunitTestMethod xunit ? xunit.Method.GetCustomAttribute<ScenarioAttribute>()?.Line ?? int.MaxValue : int.MaxValue;
}
