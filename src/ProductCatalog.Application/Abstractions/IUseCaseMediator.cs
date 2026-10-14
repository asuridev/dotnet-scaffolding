namespace ProductCatalog.Application.Abstractions;

/// <summary>
/// Fachada única de despacho de casos de uso. Los controladores, los listeners y el reloj dependen solo de
/// ella, nunca de un handler.
/// </summary>
public interface IUseCaseMediator
{
    Task Dispatch(ICommand command, CancellationToken cancellationToken = default);

    Task<TResult> Dispatch<TResult>(IReturningCommand<TResult> command, CancellationToken cancellationToken = default);

    Task<TResult> Dispatch<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha SIN transacción abarcadora: las abre el adaptador de repositorio en cada llamada, así que
    /// quien despacha controla dónde cae cada commit. Es para los barridos que llaman a un proveedor EN MEDIO
    /// de su trabajo. Sin persistencia es el mismo despacho que <see cref="Dispatch(ICommand, CancellationToken)"/>.
    /// </summary>
    Task DispatchWithoutTransaction(ICommand command, CancellationToken cancellationToken = default);

    Task<TResult> DispatchWithoutTransaction<TResult>(IReturningCommand<TResult> command, CancellationToken cancellationToken = default);
}
