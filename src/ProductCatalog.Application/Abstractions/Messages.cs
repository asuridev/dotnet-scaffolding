namespace ProductCatalog.Application.Abstractions;

/// <summary>Un mensaje que despacha el mediator: un comando o una consulta.</summary>
public interface IMessage;

/// <summary>Un comando que cambia estado y no devuelve nada.</summary>
public interface ICommand : IMessage;

/// <summary>Un comando que cambia estado y devuelve <typeparamref name="TResult"/> (el recurso creado, por ejemplo).</summary>
public interface IReturningCommand<TResult> : IMessage;

/// <summary>Una consulta: no cambia estado y devuelve <typeparamref name="TResult"/>.</summary>
public interface IQuery<TResult> : IMessage;
