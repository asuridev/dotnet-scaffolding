namespace ProductCatalog.Infrastructure.Health;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// El estado del apagado ordenado. En cuanto el host empieza a pararse, el servicio deja de estar
/// disponible (<c>/readyz</c> responde 503) aunque siga atendiendo lo que ya tenía en vuelo.
/// </summary>
public sealed class GracefulShutdown
{
    private int draining;

    /// <summary>¿Ha empezado el apagado?</summary>
    public bool Draining => Volatile.Read(ref draining) == 1;

    /// <summary>Deja de aceptar tráfico: <c>/readyz</c> pasa a 503. Separado de la señal para poder probarlo sin ella.</summary>
    public void StartDraining() => Interlocked.Exchange(ref draining, 1);

    /// <summary>Empieza a drenar cuando el host empieza a pararse, y lo deja escrito con el margen que tiene.</summary>
    public void Watch(IHostApplicationLifetime lifetime, ILogger logger, TimeSpan timeout)
    {
        lifetime.ApplicationStopping.Register(() =>
        {
            StartDraining();
            logger.LogInformation("Apagado ordenado: margen de {Timeout} para las peticiones en vuelo", timeout);
        });
    }
}
