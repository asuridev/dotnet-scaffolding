namespace ProductCatalog.Application.Commands;

using ProductCatalog.Application.Abstractions;

/// <summary>Retira un producto del catálogo de forma definitiva.</summary>
public sealed class RetireProductCommand : ICommand
{
    public required Guid Id { get; init; }
}
