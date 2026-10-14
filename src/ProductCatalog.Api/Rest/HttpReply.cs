namespace ProductCatalog.Api.Rest;

using Microsoft.AspNetCore.Http;
using ProductCatalog.Application.Support;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>Cómo responde un endpoint: el status del diseño y, si lo hay, el cuerpo con el contrato del cable.</summary>
public static partial class HttpReply
{
    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateParameter();

    public static Task Json<T>(HttpContext http, int status, T body)
    {
        ArgumentNullException.ThrowIfNull(http);
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(body, WireJson.Options, http.RequestAborted);
    }

    public static void Status(HttpContext http, int status)
    {
        ArgumentNullException.ThrowIfNull(http);
        http.Response.StatusCode = status;
    }

    /// <summary>
    /// URL absoluta de <c>Location</c>: el esquema y el host de la petición, y la plantilla de la ruta de lectura
    /// expandida con el valor (como ServletUriComponentsBuilder.fromCurrentContextPath() en keel-spring).
    /// </summary>
    public static string Location(HttpRequest request, string template, object? value)
    {
        ArgumentNullException.ThrowIfNull(request);
        var segment = Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
        return $"{request.Scheme}://{request.Host}{TemplateParameter().Replace(template, segment, 1)}";
    }
}
