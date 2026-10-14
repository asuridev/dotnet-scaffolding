namespace ProductCatalog.UnitTests;

using ProductCatalog.Application.Support;
using ProductCatalog.Domain.Idempotency;
using System.Security.Cryptography;
using Xunit;

public sealed class IdempotencyTests
{
    /// <summary>Un comando de muestra: el método del ámbito no es contenido y no entra en la firma.</summary>
    private sealed class SampleCommand
    {
        public required string Sku { get; init; }

        public string IdempotencyScope() => "sample";
    }

    private static string Sig(object value) => CommandSignature.Of(value);

    [Fact]
    public void LaFirmaNoDependeDelOrdenDeLasPropiedadesNiDeLosMetodos()
    {
        Assert.Equal(Sig(new { A = 1, B = "x" }), Sig(new { B = "x", A = 1 }));
        Assert.Equal(Sig(new SampleCommand { Sku = "ACM-0001" }), Sig(new { Sku = "ACM-0001" }));
        Assert.Matches("^[0-9a-f]{64}\\z", Sig(new { }));
    }

    [Fact]
    public void ElOrdenDeUnaListaEsContenidoYLaEscalaNo()
    {
        Assert.NotEqual(Sig(new { Items = new[] { 1, 2 } }), Sig(new { Items = new[] { 2, 1 } }));
        Assert.Equal(Sig(new { Amount = 1.50m }), Sig(new { Amount = 1.5m }));
        Assert.NotEqual(Sig(new { Amount = 1.50m }), Sig(new { Amount = 1.51m }));
    }

    [Fact]
    public void NingunContenidoImitaUnSeparadorNiANull()
    {
        Assert.NotEqual(Sig(new { Notes = (string?)null }), Sig(new { Notes = "~" }));
        Assert.NotEqual(Sig(new { A = "b,c" }), Sig(new { A = "b", C = "" }));
    }

    [Fact]
    public void UnBinarioEntraPorSuDigest()
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData("a"u8.ToArray()));
        Assert.Equal("{7:Content64:" + digest + "}", CommandSignature.Canonical(new { Content = "a"u8.ToArray() }));
    }

    [Theory]
    [InlineData("createProduct", "IDEMPOTENCY_KEY_IN_PROGRESS", "IDEMPOTENCY_KEY_REUSED")]
    public void CadaConflictoSaleConElCodeDeSuOperacion(string operation, string race, string reuse)
    {
        Assert.Equal(race, new IdempotencyConflictException(operation, "k").Code);
        Assert.Equal(reuse, new IdempotencyReuseException(operation, "k").Code);
        Assert.Equal(409, new IdempotencyConflictException(operation + ":particion", "k").HttpStatus);
    }

    [Fact]
    public async Task LaCabeceraLlegaAlContextoRecortadaYEnBlancoNoAbreNada()
    {
        Assert.Null(IdempotencyContext.Get());
        string? seen = "sin ejecutar";
        await IdempotencyContext.RunWith("  clave-1 ", () => { seen = IdempotencyContext.Get(); return Task.CompletedTask; });
        Assert.Equal("clave-1", seen);
        await IdempotencyContext.RunWith("   ", () => { seen = IdempotencyContext.Get(); return Task.CompletedTask; });
        Assert.Null(seen);
        Assert.Null(IdempotencyContext.Get());
    }
}
