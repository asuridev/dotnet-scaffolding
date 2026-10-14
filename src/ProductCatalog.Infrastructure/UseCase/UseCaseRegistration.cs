namespace ProductCatalog.Infrastructure.UseCase;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Commands;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Application.Mappers;
using ProductCatalog.Application.Queries;
using ProductCatalog.Application.UseCases;

/// <summary>
/// El ÚNICO sitio que registra los casos de uso en el contenedor: el mediator, los mappers y un handler por
/// operación del diseño, con el contrato que lo ata a SU mensaje. Lo genera build; si una operación no está
/// aquí, no se despacha.
/// </summary>
public static class UseCaseRegistration
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.TryAddScoped<IUseCaseTransactions, PassThroughUseCaseTransactions>();
        services.AddScoped<IUseCaseMediator, UseCaseMediator>();
        services.AddScoped<ProductApplicationMapper>();
        services.AddScoped<IReturningCommandHandler<CreateProductCommand, CreateProductResponseDto>, CreateProductCommandHandler>();
        services.AddScoped<IQueryHandler<GetProductQuery, GetProductResponseDto>, GetProductQueryHandler>();
        services.AddScoped<IQueryHandler<ListProductsQuery, PagedResponse<ListProductsResponseDto>>, ListProductsQueryHandler>();
        services.AddScoped<ICommandHandler<ActivateProductCommand>, ActivateProductCommandHandler>();
        services.AddScoped<ICommandHandler<RetireProductCommand>, RetireProductCommandHandler>();
        return services;
    }
}
