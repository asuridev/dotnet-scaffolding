namespace ProductCatalog.Application.Commands;

using ProductCatalog.Application.Abstractions;

/// <summary>Publica un producto en borrador; solo con un precio mayor que cero.</summary>
public sealed class ActivateProductCommand : ICommand
{
    public required Guid Id { get; init; }
}
