using System.Data;
using Dapper;
using InvoiceApi.Models;
using Microsoft.Data.SqlClient;

namespace InvoiceApi.Data;

/// <summary>
/// Implementacion sobre SQL Server con Dapper. Cada operacion invoca un
/// procedimiento almacenado con parametros tipados.
/// </summary>
/// <remarks>
/// Dos consecuencias de pasar siempre por parametros y nunca por concatenacion:
/// el motor reutiliza el plan de ejecucion ya compilado, y el valor que envia el
/// usuario no puede alterar la estructura de la consulta, lo que descarta la
/// inyeccion SQL por construccion.
///
/// Se declara el tipo y el tamano de cada parametro de forma explicita. Si se
/// dejara inferir, un desajuste entre el tipo del parametro y el de la columna
/// obligaria al motor a convertir cada fila antes de comparar, lo que impide usar
/// el indice y convierte una busqueda directa en un recorrido completo.
/// </remarks>
public class InvoiceRepository : IInvoiceRepository
{
    private readonly string _connectionString;

    /// <summary>
    /// Resuelve la cadena de conexion al construirse. Si no esta configurada, falla
    /// de inmediato en lugar de esperar a la primera consulta.
    /// </summary>
    /// <param name="configuration">Configuracion de la aplicacion.</param>
    /// <exception cref="InvalidOperationException">
    /// Si no existe la cadena de conexion 'DefaultConnection'.
    /// </exception>
    public InvoiceRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No se encontro la cadena de conexion 'DefaultConnection'.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// El procedimiento devuelve la fila recien insertada mediante la clausula
    /// OUTPUT, lo que evita una segunda consulta para releerla.
    /// </remarks>
    public async Task<InvoiceResponse> CreateAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@ClientName", request.ClientName, DbType.String, size: 100);
        parameters.Add("@Amount", request.Amount, DbType.Decimal);
        parameters.Add("@IssueDate", request.IssueDate, DbType.DateTime2);
        parameters.Add("@Status", request.Status, DbType.AnsiString, size: 20);

        var command = new CommandDefinition(
            "SP_InsertInvoice",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<InvoiceResponse>(command);
    }

    /// <inheritdoc />
    public async Task<InvoiceResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@Id", id, DbType.Int32);

        var command = new CommandDefinition(
            "SP_GetInvoiceById",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<InvoiceResponse>(command);
    }

    /// <inheritdoc />
    /// <remarks>
    /// El procedimiento devuelve dos conjuntos de resultados en una sola ida a la
    /// base: primero el total de coincidencias y despues la pagina solicitada.
    /// Resolverlo en una unica llamada evita que ambos numeros se calculen sobre
    /// estados distintos de la tabla.
    /// </remarks>
    public async Task<PagedResult<InvoiceResponse>> SearchByClientAsync(
        string clientName,
        int page,
        int pageSize,
        ClientMatchMode matchMode = ClientMatchMode.Exact,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@ClientName", clientName, DbType.String, size: 100);
        parameters.Add("@Page", page, DbType.Int32);
        parameters.Add("@PageSize", pageSize, DbType.Int32);
        parameters.Add("@MatchMode", (byte)matchMode, DbType.Byte);

        var command = new CommandDefinition(
            "SP_SearchInvoicesByClient",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        // El SP devuelve dos result sets: primero el total, luego la pagina.
        await using var multi = await connection.QueryMultipleAsync(command);

        var totalCount = await multi.ReadSingleAsync<long>();
        var items = (await multi.ReadAsync<InvoiceResponse>()).AsList();

        return new PagedResult<InvoiceResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
