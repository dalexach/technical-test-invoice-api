using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using InvoiceApi.Auth;
using Microsoft.Extensions.Options;

namespace InvoiceApi.Tests;

/// <summary>
/// Emision de tokens y validacion de credenciales.
/// </summary>
/// <remarks>
/// Cubre las dos responsabilidades del servicio: que solo las credenciales
/// configuradas sean aceptadas, y que el token emitido lleve emisor, audiencia,
/// sujeto y vencimiento correctos, ya que son los campos que la API valida
/// despues en cada peticion.
/// </remarks>
public class TokenServiceTests
{
    private static readonly JwtOptions Settings = new()
    {
        Key = "clave-de-pruebas-con-mas-de-treinta-y-dos-caracteres",
        Issuer = "invoice-api",
        Audience = "invoice-api-clients",
        ExpirationMinutes = 30,
        Client = new ApiClientCredentials
        {
            ClientId = "cliente-de-prueba",
            ClientSecret = "secreto-de-prueba"
        }
    };

    private static TokenService CreateSut() => new(Microsoft.Extensions.Options.Options.Create(Settings));

    [Fact]
    public void ValidateCredentials_CorrectCredentials_ReturnsTrue()
    {
        CreateSut().ValidateCredentials("cliente-de-prueba", "secreto-de-prueba")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("cliente-de-prueba", "secreto-incorrecto")]
    [InlineData("cliente-incorrecto", "secreto-de-prueba")]
    [InlineData("", "")]
    [InlineData("cliente-de-prueba", "secreto-de-prueba-mas-largo")]
    public void ValidateCredentials_WrongCredentials_ReturnsFalse(string clientId, string clientSecret)
    {
        CreateSut().ValidateCredentials(clientId, clientSecret).Should().BeFalse();
    }

    [Fact]
    public void CreateToken_AnyClient_SetsIssuerAudienceAndSubject()
    {
        var (accessToken, expiresIn) = CreateSut().CreateToken("cliente-de-prueba");

        expiresIn.Should().Be(30 * 60);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        token.Issuer.Should().Be("invoice-api");
        token.Audiences.Should().Contain("invoice-api-clients");
        token.Subject.Should().Be("cliente-de-prueba");
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void CreateToken_CalledTwice_ProducesUniqueIdentifiers()
    {
        var sut = CreateSut();
        var handler = new JwtSecurityTokenHandler();

        var first = handler.ReadJwtToken(sut.CreateToken("cliente-de-prueba").AccessToken);
        var second = handler.ReadJwtToken(sut.CreateToken("cliente-de-prueba").AccessToken);

        first.Id.Should().NotBe(second.Id);
    }
}
