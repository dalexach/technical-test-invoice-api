using System.ComponentModel.DataAnnotations;
using InvoiceApi.Data;
using InvoiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceApi.Controllers;

/// <summary>Registro y consulta de facturas.</summary>
[ApiController]
[Route("invoice")]
[Authorize]
[Produces("application/json")]
public class InvoiceController : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly IInvoiceRepository _repository;
    private readonly ILogger<InvoiceController> _logger;

    /// <param name="repository">Acceso a datos de facturas.</param>
    /// <param name="logger">Registro de la aplicacion.</param>
    public InvoiceController(IInvoiceRepository repository, ILogger<InvoiceController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Registra una nueva factura.</summary>
    /// <response code="201">Factura creada. La cabecera Location apunta al recurso.</response>
    /// <response code="400">Los datos enviados no son validos o estan incompletos.</response>
    /// <response code="401">Falta el token de acceso o no es valido.</response>
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await _repository.CreateAsync(request, cancellationToken);

        _logger.LogInformation("Factura {InvoiceId} creada para el cliente {ClientName}.",
            invoice.Id, invoice.ClientName);

        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
    }

    /// <summary>Obtiene una factura por su identificador.</summary>
    /// <param name="id">Identificador de la factura.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <response code="200">Factura encontrada.</response>
    /// <response code="400">El identificador no es un entero positivo.</response>
    /// <response code="401">Falta el token de acceso o no es valido.</response>
    /// <response code="404">No existe una factura con ese identificador.</response>
    [HttpGet("{id:int:min(1)}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdAsync(id, cancellationToken);

        if (invoice is null)
        {
            _logger.LogInformation("No se encontro la factura {InvoiceId}.", id);

            return Problem(
                title: "Factura no encontrada.",
                detail: $"No existe una factura con el identificador {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(invoice);
    }

    /// <summary>Busca las facturas asociadas a un cliente.</summary>
    /// <remarks>
    /// El parametro <c>matchMode</c> decide como se compara el nombre:
    /// <c>Exact</c> exige coincidencia completa, <c>Prefix</c> busca los nombres que
    /// empiezan por el texto ("Seguros" encuentra "Seguros Sura") y <c>Contains</c>
    /// los que lo contienen en cualquier posicion ("Sura" encuentra "Seguros Sura").
    ///
    /// Los dos primeros se resuelven con el indice. <c>Contains</c> obliga a recorrer
    /// la tabla, por lo que su coste crece con el volumen; se ofrece porque hay
    /// busquedas legitimas que lo necesitan, pero no es el comportamiento por defecto.
    ///
    /// El resultado se entrega paginado para acotar el tamano de la respuesta.
    /// Una busqueda sin coincidencias devuelve 200 con la lista vacia.
    /// </remarks>
    /// <param name="client">Texto a buscar en el nombre del cliente.</param>
    /// <param name="page">Numero de pagina, desde 1.</param>
    /// <param name="pageSize">Cantidad de elementos por pagina (maximo 200).</param>
    /// <param name="matchMode">Forma de comparar el nombre: Exact, Prefix o Contains.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <response code="200">Pagina de resultados, posiblemente vacia.</response>
    /// <response code="400">Parametros de busqueda o paginacion invalidos.</response>
    /// <response code="401">Falta el token de acceso o no es valido.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<InvoiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<InvoiceResponse>>> SearchByClient(
        [FromQuery, Required(ErrorMessage = "El parametro 'client' es obligatorio.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "El parametro 'client' debe tener entre 1 y 100 caracteres.")]
        string client,
        [FromQuery, Range(1, int.MaxValue, ErrorMessage = "El parametro 'page' debe ser mayor o igual a 1.")]
        int page = 1,
        [FromQuery, Range(1, MaxPageSize, ErrorMessage = "El parametro 'pageSize' debe estar entre 1 y 200.")]
        int pageSize = DefaultPageSize,
        [FromQuery] ClientMatchMode matchMode = ClientMatchMode.Exact,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.SearchByClientAsync(
            client, page, pageSize, matchMode, cancellationToken);

        _logger.LogInformation(
            "Busqueda de cliente {ClientName} en modo {MatchMode}: {Returned} de {Total} facturas (pagina {Page}).",
            client, matchMode, result.Items.Count, result.TotalCount, page);

        return Ok(result);
    }
}
