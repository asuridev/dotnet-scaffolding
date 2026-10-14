namespace ProductCatalog.Infrastructure.UseCase;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Domain.Errors;
using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Fachada única de despacho de casos de uso: resuelve el handler del mensaje en el contenedor y lo invoca.
/// </summary>
/// <remarks>
/// Es también el único punto por el que pasan TODAS las operaciones —las peticiones HTTP, los barridos
/// programados y los mensajes consumidos—, y por eso es aquí donde vive el LOG DE FRONTERA de cada caso de
/// uso: operación, resultado y duración. Solo nombres y códigos, nunca los valores del mensaje: un comando
/// lleva datos de quien llama. Niveles fijos: Debug si salió bien, Information si el dominio lo rechazó —un
/// 4xx es un resultado esperado— y Error si falló. La pila de un fallo NO va aquí: la imprime el adaptador
/// por el que entró, y con las dos cada fallo saldría duplicado.
/// </remarks>
public sealed partial class UseCaseMediator(IServiceProvider services, IUseCaseTransactions transactions, ILogger<UseCaseMediator> logger) : IUseCaseMediator
{
    // Un envoltorio por tipo de mensaje, construido una vez: cierra los genéricos del handler a partir del tipo
    // concreto del mensaje, que el método solo conoce como su interfaz.
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task Dispatch(ICommand command, CancellationToken cancellationToken = default) =>
        Run(command, () => transactions.Run(() => Handled(command, cancellationToken), readOnly: false, cancellationToken));

    public Task<TResult> Dispatch<TResult>(IReturningCommand<TResult> command, CancellationToken cancellationToken = default) =>
        Run(command, () => transactions.Run(() => Handled(command, cancellationToken), readOnly: false, cancellationToken));

    public Task<TResult> Dispatch<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        Run(query, () => transactions.Run(() => Wrapper<ResultWrapper<TResult>>(typeof(QueryWrapper<,>), query, typeof(TResult)).Handle(query, services, cancellationToken), readOnly: true, cancellationToken));

    public Task DispatchWithoutTransaction(ICommand command, CancellationToken cancellationToken = default) =>
        Run(command, () => Handled(command, cancellationToken));

    public Task<TResult> DispatchWithoutTransaction<TResult>(IReturningCommand<TResult> command, CancellationToken cancellationToken = default) =>
        Run(command, () => Handled(command, cancellationToken));

    private async Task<bool> Handled(ICommand command, CancellationToken cancellationToken)
    {
        await Wrapper<CommandWrapper>(typeof(CommandWrapper<>), command).Handle(command, services, cancellationToken);
        return true;
    }

    private Task<TResult> Handled<TResult>(IReturningCommand<TResult> command, CancellationToken cancellationToken) =>
        Wrapper<ResultWrapper<TResult>>(typeof(ReturningCommandWrapper<,>), command, typeof(TResult)).Handle(command, services, cancellationToken);

    private static T Wrapper<T>(Type open, IMessage message, Type? result = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        var type = message.GetType();
        return (T)Wrappers.GetOrAdd(type, _ => Activator.CreateInstance(open.MakeGenericType(result is null ? [type] : [type, result]))!);
    }

    private async Task Run(IMessage message, Func<Task> work) =>
        await Run(message, async () =>
        {
            await work();
            return true;
        });

    private async Task<T> Run<T>(IMessage message, Func<Task<T>> work)
    {
        var operation = message.GetType().Name;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await work();
            LogOk(operation, Elapsed(started));
            return result;
        }
        catch (DomainException error)
        {
            LogRejected(operation, error.Code ?? error.GetType().Name, Elapsed(started));
            throw;
        }
        catch (Exception error)
        {
            LogFailed(operation, error.GetType().Name, Elapsed(started));
            throw;
        }
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Caso de uso {Operation}: ok ({Elapsed} ms)")]
    private partial void LogOk(string operation, long elapsed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Caso de uso {Operation}: rechazado ({Code}, {Elapsed} ms)")]
    private partial void LogRejected(string operation, string code, long elapsed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Caso de uso {Operation}: falló con {Type} ({Elapsed} ms)")]
    private partial void LogFailed(string operation, string type, long elapsed);

    private abstract class CommandWrapper
    {
        public abstract Task Handle(IMessage command, IServiceProvider services, CancellationToken cancellationToken);
    }

    private abstract class ResultWrapper<TResult>
    {
        public abstract Task<TResult> Handle(IMessage message, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class CommandWrapper<TCommand> : CommandWrapper
        where TCommand : ICommand
    {
        public override Task Handle(IMessage command, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<ICommandHandler<TCommand>>().Handle((TCommand)command, cancellationToken);
    }

    private sealed class ReturningCommandWrapper<TCommand, TResult> : ResultWrapper<TResult>
        where TCommand : IReturningCommand<TResult>
    {
        public override Task<TResult> Handle(IMessage message, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<IReturningCommandHandler<TCommand, TResult>>().Handle((TCommand)message, cancellationToken);
    }

    private sealed class QueryWrapper<TQuery, TResult> : ResultWrapper<TResult>
        where TQuery : IQuery<TResult>
    {
        public override Task<TResult> Handle(IMessage message, IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<IQueryHandler<TQuery, TResult>>().Handle((TQuery)message, cancellationToken);
    }
}
