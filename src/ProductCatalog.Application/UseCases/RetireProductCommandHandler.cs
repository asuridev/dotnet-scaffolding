namespace ProductCatalog.Application.UseCases;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Commands;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Domain.Errors;

/// <summary>Retira un producto del catálogo de forma definitiva.</summary>
public sealed class RetireProductCommandHandler(IProductRepository productRepository) : ICommandHandler<RetireProductCommand>
{
    private readonly IProductRepository productRepository = productRepository;

    public async Task Handle(RetireProductCommand command, CancellationToken cancellationToken)
    {
        var product = await productRepository.FindById(command.Id, cancellationToken)
            ?? throw new ProductNotFoundError($"No existe el producto {command.Id}");
        product.Retire();
        await productRepository.Save(product, cancellationToken);
    }
}
