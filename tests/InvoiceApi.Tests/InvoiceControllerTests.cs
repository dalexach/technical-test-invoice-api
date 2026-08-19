using FluentAssertions;
using InvoiceApi.Controllers;
using InvoiceApi.Data;
using InvoiceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvoiceApi.Tests;

/// <summary>
/// Comportamiento del controlador con el acceso a datos sustituido por un doble.
/// </summary>
/// <remarks>
/// Al aislar el repositorio, estas pruebas verifican exclusivamente las decisiones
/// del controlador: que traduzca la ausencia de una factura en un 404, que emita
/// la cabecera Location al crear, que conserve la forma de la respuesta cuando la
/// busqueda no encuentra nada y que traslade el modo de coincidencia solicitado.
/// El doble se configura en modo estricto, de modo que cualquier llamada no
/// prevista al repositorio haga fallar la prueba en lugar de pasar inadvertida.
/// </remarks>
public class InvoiceControllerTests
{
    private readonly Mock<IInvoiceRepository> _repository = new(MockBehavior.Strict);

    private InvoiceController CreateSut() =>
        new(_repository.Object, NullLogger<InvoiceController>.Instance);

    private static InvoiceResponse SampleInvoice(int id = 1, string client = "Juan Perez") => new()
    {
        Id = id,
        ClientName = client,
        Amount = 150000.50m,
        IssueDate = new DateTime(2026, 8, 18),
        Status = "PENDING",
        CreatedAt = new DateTime(2026, 8, 18, 12, 0, 0)
    };

    [Fact]
    public async Task Create_ValidInvoice_ReturnsCreatedWithLocation()
    {
        var request = new CreateInvoiceRequest
        {
            ClientName = "Juan Perez",
            Amount = 150000.50m,
            IssueDate = new DateTime(2026, 8, 18),
            Status = "PENDING"
        };

        _repository
            .Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleInvoice(id: 42));

        var result = await CreateSut().Create(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(InvoiceController.GetById));
        created.RouteValues!["id"].Should().Be(42);
        created.Value.Should().BeOfType<InvoiceResponse>()
            .Which.Id.Should().Be(42);
    }

    [Fact]
    public async Task GetById_ExistingInvoice_ReturnsInvoice()
    {
        _repository
            .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleInvoice(id: 7));

        var result = await CreateSut().GetById(7, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<InvoiceResponse>().Which.Id.Should().Be(7);
    }

    [Fact]
    public async Task GetById_MissingInvoice_ReturnsNotFound()
    {
        _repository
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceResponse?)null);

        var result = await CreateSut().GetById(999, CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Be("Factura no encontrada.");
    }

    [Fact]
    public async Task Search_MatchingInvoices_ReturnsPagedResult()
    {
        var page = new PagedResult<InvoiceResponse>
        {
            Items = new[] { SampleInvoice(1), SampleInvoice(2) },
            Page = 1,
            PageSize = 50,
            TotalCount = 2
        };

        _repository
            .Setup(r => r.SearchByClientAsync("Juan Perez", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await CreateSut().SearchByClient("Juan Perez", 1, 50, ClientMatchMode.Exact, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<PagedResult<InvoiceResponse>>().Subject;
        body.Items.Should().HaveCount(2);
        body.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsOkWithEmptyList()
    {
        // La forma de la respuesta no cambia cuando no hay resultados:
        // el cliente siempre recibe la misma estructura paginada.
        var empty = new PagedResult<InvoiceResponse>
        {
            Items = Array.Empty<InvoiceResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0
        };

        _repository
            .Setup(r => r.SearchByClientAsync("Cliente Inexistente", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()))
            .ReturnsAsync(empty);

        var result = await CreateSut()
            .SearchByClient("Cliente Inexistente", 1, 50, ClientMatchMode.Exact, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var body = ok.Value.Should().BeOfType<PagedResult<InvoiceResponse>>().Subject;
        body.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);
        body.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Search_PrefixMode_PassesModeToRepository()
    {
        // El modo de coincidencia debe llegar al repositorio tal como se recibio:
        // es lo que decide entre igualdad y busqueda por prefijo en el procedimiento.
        var page = new PagedResult<InvoiceResponse>
        {
            Items = new[] { SampleInvoice(1, "Seguros Sura") },
            Page = 1,
            PageSize = 50,
            TotalCount = 1
        };

        _repository
            .Setup(r => r.SearchByClientAsync("Seguros", 1, 50, ClientMatchMode.Prefix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await CreateSut().SearchByClient("Seguros", 1, 50, ClientMatchMode.Prefix, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        _repository.Verify(
            r => r.SearchByClientAsync("Seguros", 1, 50, ClientMatchMode.Prefix, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_NoModeGiven_DefaultsToExactMatch()
    {
        var empty = new PagedResult<InvoiceResponse>
        {
            Items = Array.Empty<InvoiceResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0
        };

        _repository
            .Setup(r => r.SearchByClientAsync("Seguros", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()))
            .ReturnsAsync(empty);

        await CreateSut().SearchByClient("Seguros", 1, 50, ClientMatchMode.Exact, CancellationToken.None);

        _repository.Verify(
            r => r.SearchByClientAsync("Seguros", 1, 50, ClientMatchMode.Exact, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_ContainsMode_PassesModeToRepository()
    {
        // El modo de contenido permite encontrar el nombre en cualquier posicion,
        // que es lo que necesitan las razones sociales cuya parte distintiva no
        // va al principio ("Sura" dentro de "Seguros Sura").
        var page = new PagedResult<InvoiceResponse>
        {
            Items = new[] { SampleInvoice(1, "Seguros Sura") },
            Page = 1,
            PageSize = 50,
            TotalCount = 1
        };

        _repository
            .Setup(r => r.SearchByClientAsync("Sura", 1, 50, ClientMatchMode.Contains, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await CreateSut()
            .SearchByClient("Sura", 1, 50, ClientMatchMode.Contains, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        _repository.Verify(
            r => r.SearchByClientAsync("Sura", 1, 50, ClientMatchMode.Contains, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
