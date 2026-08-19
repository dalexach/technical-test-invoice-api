using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceApi.Infrastructure;

/// <summary>
/// Traduce cualquier excepcion no controlada a una respuesta ProblemDetails.
/// El detalle tecnico queda en el log del servidor y nunca viaja al cliente:
/// exponerlo revelaria estructura interna, rutas o datos de conexion.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    /// <param name="logger">Registro donde queda el detalle tecnico del error.</param>
    /// <param name="problemDetailsService">
    /// Servicio que escribe la respuesta en el formato estandar de errores.
    /// </param>
    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    /// <summary>Convierte una excepcion no controlada en una respuesta 500.</summary>
    /// <param name="httpContext">Contexto de la peticion que fallo.</param>
    /// <param name="exception">Excepcion capturada.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>
    /// <c>true</c> si la excepcion se tradujo en respuesta; <c>false</c> para dejarla
    /// pasar al comportamiento por defecto, como ocurre cuando el cliente aborto la
    /// peticion y ya no hay a quien responder.
    /// </returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Una peticion abortada por el cliente no es un error del servidor.
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        _logger.LogError(
            exception,
            "Error no controlado procesando {Method} {Path}.",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor.",
                Detail = "Ocurrio un error procesando la solicitud. "
                       + "Consulte el identificador de traza para el seguimiento."
            }
        });
    }
}
