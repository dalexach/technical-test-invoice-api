using System.ComponentModel.DataAnnotations;

namespace InvoiceApi.Models;

/// <summary>Datos que acepta <c>POST /invoice</c> para registrar una factura.</summary>
public class CreateInvoiceRequest
{
    /// <summary>Nombre del cliente al que se factura.</summary>
    /// <example>Juan Perez</example>
    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre del cliente debe tener entre 1 y 100 caracteres.")]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Valor total de la factura. Debe ser mayor a cero.</summary>
    /// <example>150000.50</example>
    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(0.01, 99999999999999.99, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal Amount { get; set; }

    /// <summary>Fecha de emision de la factura.</summary>
    /// <example>2026-08-18T00:00:00</example>
    [Required(ErrorMessage = "La fecha de emision es obligatoria.")]
    public DateTime IssueDate { get; set; }

    /// <summary>Estado de la factura: PENDING, PAID o CANCELLED.</summary>
    /// <example>PENDING</example>
    [Required(ErrorMessage = "El estado es obligatorio.")]
    [RegularExpression("^(PENDING|PAID|CANCELLED)$", ErrorMessage = "El estado debe ser PENDING, PAID o CANCELLED.")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>Representacion de una factura almacenada.</summary>
public class InvoiceResponse
{
    /// <summary>Identificador asignado por la base de datos al registrarse.</summary>
    public int Id { get; set; }

    /// <summary>Nombre del cliente al que se factura.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Valor total de la factura.</summary>
    public decimal Amount { get; set; }

    /// <summary>Fecha de emision indicada al registrar la factura.</summary>
    public DateTime IssueDate { get; set; }

    /// <summary>Estado actual: PENDING, PAID o CANCELLED.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Momento en que la factura quedo registrada, en horario universal. Lo asigna
    /// la base de datos, no el cliente, para que refleje el registro real.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Pagina de resultados devuelta por <c>GET /invoice/search</c>.</summary>
/// <typeparam name="T">Tipo de los elementos de la pagina.</typeparam>
public class PagedResult<T>
{
    /// <summary>Elementos de la pagina actual. Vacio si no hubo coincidencias.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Numero de la pagina devuelta, empezando en 1.</summary>
    public int Page { get; set; }

    /// <summary>Cantidad maxima de elementos por pagina.</summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total de elementos que coinciden con la busqueda, no solo los de esta pagina.
    /// Permite al cliente saber cuantos hay sin recorrerlas todas.
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>Numero de paginas necesarias para recorrer todas las coincidencias.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
