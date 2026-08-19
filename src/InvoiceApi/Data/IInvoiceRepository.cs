using InvoiceApi.Models;

namespace InvoiceApi.Data;

/// <summary>
/// Acceso a datos de facturas. Toda consulta se resuelve mediante procedimientos
/// almacenados, sin Entity Framework.
/// </summary>
/// <remarks>
/// La interfaz existe para que el controlador dependa de una abstraccion y no de
/// la implementacion concreta sobre SQL Server. Es lo que permite que las pruebas
/// de integracion levanten la aplicacion completa sustituyendo esta pieza por un
/// doble, sin necesidad de una base de datos real.
/// </remarks>
public interface IInvoiceRepository
{
    /// <summary>Registra una factura y devuelve la fila creada.</summary>
    /// <param name="request">Datos validados de la factura a registrar.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>
    /// La factura almacenada, incluidos el identificador asignado por la base de
    /// datos y la marca de tiempo de creacion.
    /// </returns>
    Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Obtiene una factura por su identificador.</summary>
    /// <param name="id">Identificador de la factura.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>La factura, o <c>null</c> si no existe ninguna con ese identificador.</returns>
    /// <remarks>
    /// Devolver <c>null</c> en lugar de lanzar una excepcion es deliberado: que una
    /// factura no exista es un resultado posible de la consulta, no un fallo. Es el
    /// controlador quien decide traducirlo a una respuesta 404.
    /// </remarks>
    Task<InvoiceResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Busca las facturas de un cliente, con resultado paginado.</summary>
    /// <param name="clientName">
    /// Texto a buscar en el nombre del cliente. Se interpreta segun
    /// <paramref name="matchMode"/>.
    /// </param>
    /// <param name="page">Numero de pagina, empezando en 1.</param>
    /// <param name="pageSize">Cantidad de elementos por pagina.</param>
    /// <param name="matchMode">
    /// Forma de comparar el nombre: exacta, por prefijo o por contenido.
    /// </param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>
    /// La pagina solicitada junto con el total de coincidencias. Si no hay ninguna,
    /// devuelve una pagina vacia en lugar de <c>null</c>, de modo que quien consume
    /// el resultado no necesite distinguir ambos casos.
    /// </returns>
    Task<PagedResult<InvoiceResponse>> SearchByClientAsync(
        string clientName,
        int page,
        int pageSize,
        ClientMatchMode matchMode = ClientMatchMode.Exact,
        CancellationToken cancellationToken = default);
}
