using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using InvoiceApi.Models;

namespace InvoiceApi.Tests;

/// <summary>
/// Reglas de validacion que protegen el endpoint POST /invoice.
/// </summary>
/// <remarks>
/// Se ejercitan las anotaciones del modelo directamente, sin levantar la
/// aplicacion: son reglas declarativas y basta con comprobar que rechazan lo que
/// deben y aceptan lo valido. El requisito de la prueba tecnica es garantizar que
/// los datos recibidos sean correctos y completos, y estos casos cubren ambos
/// frentes: campos ausentes, valores fuera de rango y estados no permitidos.
/// </remarks>
public class ValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(CreateInvoiceRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static CreateInvoiceRequest ValidRequest() => new()
    {
        ClientName = "Juan Perez",
        Amount = 150000.50m,
        IssueDate = new DateTime(2026, 8, 18),
        Status = "PENDING"
    };

    [Fact]
    public void Validate_CompleteRequest_PassesValidation()
    {
        Validate(ValidRequest()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyClientName_IsRejected(string clientName)
    {
        var request = ValidRequest();
        request.ClientName = clientName;

        Validate(request).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(CreateInvoiceRequest.ClientName));
    }

    [Fact]
    public void Validate_ClientNameOverMaxLength_IsRejected()
    {
        var request = ValidRequest();
        request.ClientName = new string('A', 101);

        Validate(request).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(CreateInvoiceRequest.ClientName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-150000.50)]
    public void Validate_NonPositiveAmount_IsRejected(decimal amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        Validate(request).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(CreateInvoiceRequest.Amount));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("ENVIADA")]
    [InlineData("PENDING ")]
    public void Validate_StatusOutsideCatalog_IsRejected(string status)
    {
        var request = ValidRequest();
        request.Status = status;

        Validate(request).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(CreateInvoiceRequest.Status));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PAID")]
    [InlineData("CANCELLED")]
    public void Validate_StatusFromCatalog_IsAccepted(string status)
    {
        var request = ValidRequest();
        request.Status = status;

        Validate(request).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(1, 50, 1)]
    [InlineData(100, 50, 2)]
    [InlineData(101, 50, 3)]
    public void TotalPages_AnyRowCount_RoundsUp(long total, int pageSize, int expected)
    {
        var page = new PagedResult<InvoiceResponse> { PageSize = pageSize, TotalCount = total };

        page.TotalPages.Should().Be(expected);
    }
}
