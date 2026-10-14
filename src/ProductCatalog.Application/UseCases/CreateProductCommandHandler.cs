namespace ProductCatalog.Application.UseCases;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Commands;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Application.Mappers;
using ProductCatalog.Domain.Idempotency;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Application.Support;
using ProductCatalog.Domain.Aggregate;
using ProductCatalog.Domain.Errors;

/// <summary>Da de alta un producto en estado draft.</summary>
public sealed class CreateProductCommandHandler(ProductApplicationMapper productApplicationMapper, IProductRepository productRepository, IIdempotencyStore idempotencyStore) : IReturningCommandHandler<CreateProductCommand, CreateProductResponseDto>
{
    private readonly ProductApplicationMapper productApplicationMapper = productApplicationMapper;
    private readonly IProductRepository productRepository = productRepository;
    private readonly IIdempotencyStore idempotencyStore = idempotencyStore;

    public async Task<CreateProductResponseDto> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var key = IdempotencyContext.Get();
        if (key is null)
        {
            return await CreateNew(command, Guid.CreateVersion7(), cancellationToken);
        }
        var scope = command.IdempotencyScope();
        var signature = CommandSignature.Of(command);
        var previous = await idempotencyStore.Find(scope, key, cancellationToken);
        if (previous is not null)
        {
            if (previous.Signature != signature)
            {
                throw new IdempotencyReuseException(scope, key);
            }
            var existing = await productRepository.FindById(Guid.Parse(previous.ResourceId!), cancellationToken);
            return productApplicationMapper.ToCreateProductResponseDto(existing!);
        }
        // Reclamar primero: el id del recurso queda nombrado por el registro.
        var id = Guid.CreateVersion7();
        await idempotencyStore.Save(scope, key, signature, id.ToString(), 86400, cancellationToken);
        return await CreateNew(command, id, cancellationToken);
    }

    private async Task<CreateProductResponseDto> CreateNew(CreateProductCommand command, Guid id, CancellationToken cancellationToken)
    {
        if (await productRepository.FindBySku(command.Sku, cancellationToken) is not null)
        {
            throw new SkuAlreadyExistsError($"Ya existe un producto con el sku {command.Sku}");
        }
        var product = Product.Create(id, command.Sku, command.Name, command.Notes, command.Price, command.ApiToken);
        var saved = await productRepository.Save(product, cancellationToken);
        return productApplicationMapper.ToCreateProductResponseDto(saved);
    }
}
