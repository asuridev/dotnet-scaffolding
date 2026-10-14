namespace ProductCatalog.Application.Support;

using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// El contrato del cable (docs/keel/wire-contract.md del método): cómo viaja en JSON cada tipo del diseño.
/// Es contrato observable, el mismo que cumplen los servidores de keel-spring y keel-nest del mismo diseño;
/// <c>WireContractTests</c> lo ejecuta contra sus casos.
/// </summary>
public static class WireJson
{
    /// <summary>Unas opciones ya configuradas y de solo lectura, para quien serializa fuera de la API.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        options.MakeReadOnly();
        return options;
    }

    /// <summary>Aplica el contrato a unas opciones existentes (las de la API las crea ASP.NET Core).</summary>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        // Los DTO nombran cada propiedad con el nombre del diseño; esto es solo la red para lo que no lo hace.
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // El nombre de una propiedad se compara exacto, como en los otros dos servidores.
        options.PropertyNameCaseInsensitive = false;
        // unknown-ignored: una propiedad desconocida en la entrada se ignora.
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        // nulls-include: por defecto un campo sin valor viaja como null. Omitirlo es decisión del diseño, por tipo.
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        // decimal-input y long-exact: un número se lee también desde una cadena numérica ("2.50").
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        // string-utf8: sin escapar lo que no es ASCII. El codificador por defecto escapa además lo que es
        // peligroso DENTRO DE HTML (<, >, &, '); una respuesta JSON no se incrusta en HTML, y Jackson tampoco lo escapa.
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Converters.Add(new DecimalWireConverter());
        options.Converters.Add(new TimestampWireConverter());
        options.Converters.Add(new DateWireConverter());
        options.Converters.Add(new JsonWireConverter());
        // enum-value: el valor que declara el diseño ([JsonStringEnumMemberName]), nunca el número.
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(OmitNulls);
    }

    /// <summary>nulls-omit: un tipo marcado con <see cref="OmitNullsAttribute"/> no escribe sus propiedades sin valor.</summary>
    private static void OmitNulls(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Type.GetCustomAttribute<OmitNullsAttribute>() is null)
        {
            return;
        }
        foreach (var property in typeInfo.Properties)
        {
            property.ShouldSerialize = static (_, value) => value is not null;
        }
    }
}
