namespace ProductCatalog.UnitTests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Commands;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Application.Mappers;
using ProductCatalog.Application.Queries;
using ProductCatalog.Application.UseCases;
using ProductCatalog.Domain.Errors;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.UseCase;
using Xunit;

public sealed class UseCasesTests
{
    private static ServiceProvider Provider(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUseCases();
        services.AddPersistence(DatabaseSettings.Disabled);
        extra?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void ElContenedorConstruyeTodosLosCasosDeUso()
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUseCaseMediator>());
    }

    [Theory]
    [InlineData(typeof(IReturningCommandHandler<CreateProductCommand, CreateProductResponseDto>), typeof(CreateProductCommandHandler))]
    [InlineData(typeof(IQueryHandler<GetProductQuery, GetProductResponseDto>), typeof(GetProductQueryHandler))]
    [InlineData(typeof(IQueryHandler<ListProductsQuery, PagedResponse<ListProductsResponseDto>>), typeof(ListProductsQueryHandler))]
    [InlineData(typeof(ICommandHandler<ActivateProductCommand>), typeof(ActivateProductCommandHandler))]
    [InlineData(typeof(ICommandHandler<RetireProductCommand>), typeof(RetireProductCommandHandler))]
    public void CadaMensajeLlegaASuHandler(Type contract, Type handler)
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();
        Assert.IsType(handler, scope.ServiceProvider.GetRequiredService(contract));
    }

    public sealed class Ping : IQuery<string>
    {
        public required string Text { get; init; }
    }

    public sealed class PingHandler : IQueryHandler<Ping, string>
    {
        public Task<string> Handle(Ping query, CancellationToken cancellationToken) => Task.FromResult($"{query.Text}-pong");
    }

    public sealed class Create : IReturningCommand<int>;

    public sealed class CreateHandler : IReturningCommandHandler<Create, int>
    {
        public Task<int> Handle(Create command, CancellationToken cancellationToken) => Task.FromResult(42);
    }

    public sealed class Touch : ICommand;

    public sealed class TouchHandler : ICommandHandler<Touch>
    {
        public int Count { get; private set; }

        public Task Handle(Touch command, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    public sealed class Refuse : ICommand;

    public sealed class RefuseHandler : ICommandHandler<Refuse>
    {
        public Task Handle(Refuse command, CancellationToken cancellationToken) => throw new ConflictException("ya existe", code: "ALREADY_EXISTS");
    }

    [Fact]
    public async Task ElMediatorDespachaLasTresClasesDeMensajeYDejaPasarElRechazoDelDominio()
    {
        var touch = new TouchHandler();
        using var provider = Provider(services =>
        {
            services.AddScoped<IQueryHandler<Ping, string>, PingHandler>();
            services.AddScoped<IReturningCommandHandler<Create, int>, CreateHandler>();
            services.AddSingleton<ICommandHandler<Touch>>(touch);
            services.AddScoped<ICommandHandler<Refuse>, RefuseHandler>();
            // Sin base: la transacción de este despacho es el paso directo, no la de EF.
            services.Replace(ServiceDescriptor.Scoped<IUseCaseTransactions, PassThroughUseCaseTransactions>());
        });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IUseCaseMediator>();
        var cancellation = TestContext.Current.CancellationToken;
        Assert.Equal("x-pong", await mediator.Dispatch(new Ping { Text = "x" }, cancellation));
        Assert.Equal(42, await mediator.Dispatch(new Create(), cancellation));
        await mediator.Dispatch(new Touch(), cancellation);
        await mediator.DispatchWithoutTransaction(new Touch(), cancellation);
        Assert.Equal(2, touch.Count);
        var error = await Assert.ThrowsAsync<ConflictException>(() => mediator.Dispatch(new Refuse(), cancellation));
        Assert.Equal("ALREADY_EXISTS", error.Code);
    }
}
