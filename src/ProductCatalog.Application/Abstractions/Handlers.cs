namespace ProductCatalog.Application.Abstractions;

/// <summary>El caso de uso de un comando sin resultado. Uno por comando, y solo uno.</summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>El caso de uso de un comando con resultado.</summary>
public interface IReturningCommandHandler<in TCommand, TResult>
    where TCommand : IReturningCommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>El caso de uso de una consulta.</summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
