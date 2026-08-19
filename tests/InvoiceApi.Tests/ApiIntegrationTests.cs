using System.Net;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using InvoiceApi.Data;
using InvoiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace InvoiceApi.Tests;

/// <summary>
/// Arranca la aplicacion en memoria para las pruebas de integracion.
/// </summary>
/// <remarks>
/// Sustituye el repositorio por un doble y aporta una configuracion propia, de
/// modo que las pruebas se ejecuten en cualquier maquina sin depender de SQL
/// Server. Lo que se verifica aqui es lo que solo existe cuando la aplicacion
/// esta montada: el ruteo, el enlace de parametros, la autorizacion y el manejo
/// global de errores.
/// </remarks>
public class InvoiceApiFactory : WebApplicationFactory<Program>
{
    public Mock<IInvoiceRepository> Repository { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=Test;Integrated Security=True;",
                ["Jwt:Key"] = "clave-de-pruebas-con-mas-de-treinta-y-dos-caracteres",
                ["Jwt:Issuer"] = "invoice-api",
                ["Jwt:Audience"] = "invoice-api-clients",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:Client:ClientId"] = "cliente-de-prueba",
                ["Jwt:Client:ClientSecret"] = "secreto-de-prueba"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInvoiceRepository>();
            services.AddScoped(_ => Repository.Object);
        });

        return base.CreateHost(builder);
    }
}

/// <summary>
/// Recorrido de la API a traves de peticiones HTTP reales contra la aplicacion
/// levantada en memoria.
/// </summary>
public class ApiIntegrationTests : IClassFixture<InvoiceApiFactory>
{
    private readonly InvoiceApiFactory _factory;

    public ApiIntegrationTests(InvoiceApiFactory factory) => _factory = factory;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/token", new
        {
            clientId = "cliente-de-prueba",
            clientSecret = "secreto-de-prueba"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await response.Content.ReadFromJsonAsync<TokenPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        return client;
    }

    private sealed record TokenPayload(string AccessToken, string TokenType, int ExpiresIn);

    [Theory]
    [InlineData("/invoice/1")]
    [InlineData("/invoice/search?client=Juan")]
    public async Task InvoiceEndpoints_WithoutToken_ReturnsUnauthorized(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostInvoice_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/invoice", new
        {
            clientName = "Juan Perez",
            amount = 1000m,
            issueDate = "2026-08-18T00:00:00",
            status = "PENDING"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthToken_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/token", new
        {
            clientId = "cliente-de-prueba",
            clientSecret = "secreto-equivocado"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchInvoices_RequestedByPath_DoesNotMatchIdRoute()
    {
        // Regresion: con una plantilla {id} sin restriccion de tipo, la peticion
        // a /invoice/search puede resolverse contra el endpoint por identificador.
        _factory.Repository
            .Setup(r => r.SearchByClientAsync("Juan Perez", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<InvoiceResponse>
            {
                Items = Array.Empty<InvoiceResponse>(),
                Page = 1,
                PageSize = 50,
                TotalCount = 0
            });

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/invoice/search?client=Juan%20Perez");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Repository.Verify(
            r => r.SearchByClientAsync("Juan Perez", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchInvoices_MissingClientParameter_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/invoice/search");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostInvoice_InvalidBody_ReturnsBadRequestPerRule()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/invoice", new
        {
            clientName = "",
            amount = -5m,
            issueDate = "2026-08-18T00:00:00",
            status = "DESCONOCIDO"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // La respuesta enumera las tres reglas incumplidas, no solo la primera.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.Should().BeEquivalentTo("ClientName", "Amount", "Status");
    }

    [Fact]
    public async Task GetInvoiceById_MissingInvoice_ReturnsNotFoundWithoutInternals()
    {
        _factory.Repository
            .Setup(r => r.GetByIdAsync(4321, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceResponse?)null);

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/invoice/4321");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Factura no encontrada.");
    }

    [Fact]
    public async Task GetInvoiceById_RepositoryThrows_HidesExceptionDetail()
    {
        _factory.Repository
            .Setup(r => r.GetByIdAsync(500, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Login failed for user 'sa' en Server=produccion;Password=secreto"));

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/invoice/500");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Password").And.NotContain("Login failed");
        body.Should().Contain("Error interno del servidor.");
    }

    [Theory]
    [InlineData("Exact", ClientMatchMode.Exact)]
    [InlineData("Prefix", ClientMatchMode.Prefix)]
    [InlineData("Contains", ClientMatchMode.Contains)]
    [InlineData("contains", ClientMatchMode.Contains)]
    [InlineData("2", ClientMatchMode.Contains)]
    public async Task SearchInvoices_MatchModeInQueryString_BindsToEnum(string value, ClientMatchMode expected)
    {
        // El modo se admite por nombre, sin distinguir mayusculas, y por su valor
        // numerico, que es como lo envian algunos clientes generados.
        // El fixture comparte el doble entre casos, asi que se captura el modo
        // recibido en lugar de contar invocaciones.
        ClientMatchMode? receivedMode = null;

        _factory.Repository
            .Setup(r => r.SearchByClientAsync(
                "Sura", 1, 50, It.IsAny<ClientMatchMode>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, int, ClientMatchMode, CancellationToken>(
                (_, _, _, modo, _) => receivedMode = modo)
            .ReturnsAsync(new PagedResult<InvoiceResponse>
            {
                Items = Array.Empty<InvoiceResponse>(),
                Page = 1,
                PageSize = 50,
                TotalCount = 0
            });

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"/invoice/search?client=Sura&matchMode={value}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        receivedMode.Should().Be(expected);
    }

    [Theory]
    [InlineData("Inventado")]
    [InlineData("99")]
    public async Task SearchInvoices_UnknownMatchMode_ReturnsBadRequest(string value)
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/invoice/search?client=Sura&matchMode={value}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchInvoices_NoMatchModeGiven_UsesExactMatch()
    {
        // El modo mas barato es el predeterminado: quien no elige, no paga el
        // recorrido de tabla que implica la busqueda por contenido.
        _factory.Repository
            .Setup(r => r.SearchByClientAsync("Seguros Sura", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<InvoiceResponse>
            {
                Items = Array.Empty<InvoiceResponse>(),
                Page = 1,
                PageSize = 50,
                TotalCount = 0
            });

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/invoice/search?client=Seguros%20Sura");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Repository.Verify(
            r => r.SearchByClientAsync("Seguros Sura", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("amount", "\"cien\"", "100")]
    [InlineData("issueDate", "\"no-es-fecha\"", "\"2026-08-15T00:00:00\"")]
    public async Task PostInvoice_WrongValueType_ExplainsWithoutInternals(
        string field, string invalidValue, string _)
    {
        // El deserializador genera mensajes en ingles que nombran el tipo de .NET
        // y la posicion del byte donde fallo. Ni el idioma ni esos detalles le
        // sirven a quien consume la API.
        var body = field == "amount"
            ? $$"""{"clientName":"Test","amount":{{invalidValue}},"issueDate":"2026-08-15T00:00:00","status":"PAID"}"""
            : $$"""{"clientName":"Test","amount":100,"issueDate":{{invalidValue}},"status":"PAID"}""";

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsync(
            "/invoice", new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("System.")
             .And.NotContain("could not be converted")
             .And.NotContain("BytePositionInLine");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey(field);
    }

    [Fact]
    public async Task PostInvoice_ModelRuleViolation_KeepsOriginalMessage()
    {
        // La sustitucion solo afecta a los mensajes del deserializador: los que
        // vienen de las anotaciones ya estan redactados y deben llegar intactos.
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/invoice", new
        {
            clientName = "Test",
            amount = -5m,
            issueDate = "2026-08-15T00:00:00",
            status = "PAID"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors["Amount"].Should().Contain("El monto debe ser mayor a 0.");
    }
}
