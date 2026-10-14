namespace ProductCatalog.Application.UseCases;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Commands;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Domain.Errors;

/// <summary>Publica un producto en borrador; solo con un precio mayor que cero.</summary>
public sealed class ActivateProductCommandHandler(IProductRepository productRepository) : ICommandHandler<ActivateProductCommand>
{
    private readonly IProductRepository productRepository = productRepository;

    public async Task Handle(ActivateProductCommand command, CancellationToken cancellationToken)
    {
        // Existe -> draft -> precio > 0: el orden lo impone Product.Activate().
        var product = await productRepository.FindById(command.Id, cancellationToken)
            ?? throw new ProductNotFoundError($"No existe el producto {command.Id}");
        product.Activate();
        await productRepository.Save(product, cancellationToken);
    }
}
