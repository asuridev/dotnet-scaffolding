namespace ProductCatalog.Infrastructure.UseCase;

/// <summary>
/// La frontera transaccional de un caso de uso: el mediator envuelve en ella cada despacho, en solo lectura las
/// consultas. Una transacción ya abierta se REUTILIZA (un caso de uso que despacha otro con Dispatch se une a la
/// del llamante).
/// </summary>
public interface IUseCaseTransactions
{
    Task<T> Run<T>(Func<Task<T>> work, bool readOnly, CancellationToken cancellationToken);
}

/// <summary>Sin persistencia no hay transacción que abrir: el trabajo se ejecuta tal cual.</summary>
public sealed class PassThroughUseCaseTransactions : IUseCaseTransactions
{
    public Task<T> Run<T>(Func<Task<T>> work, bool readOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        return work();
    }
}
