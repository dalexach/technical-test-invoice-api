using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InvoiceApi.Infrastructure;

/// <summary>
/// Construye la respuesta 400 de los errores de validacion.
/// </summary>
/// <remarks>
/// El comportamiento por defecto arrastra los mensajes que genera el
/// deserializador de JSON, que estan en ingles y mencionan el tipo interno de
/// .NET y la posicion del byte donde fallo el analisis. Eso rompe la coherencia
/// con el resto de mensajes de la API y revela detalles de implementacion que no
/// le sirven a quien consume el servicio.
///
/// Aqui esos mensajes se sustituyen por uno propio que indica que el valor no
/// corresponde al tipo esperado. Los errores de las anotaciones del modelo se
/// respetan tal cual, porque ya vienen redactados en el propio modelo.
/// </remarks>
public static class InvalidModelStateResponse
{
    private const string TipoInvalido =
        "El valor no tiene el formato esperado para este campo.";

    private const string CuerpoRequerido =
        "El cuerpo de la solicitud es obligatorio y debe ser un JSON valido.";

    /// <summary>
    /// Genera la respuesta 400 a partir del estado de validacion del modelo.
    /// </summary>
    /// <param name="context">Contexto de la accion que no supero la validacion.</param>
    /// <returns>Respuesta con el detalle de cada campo que incumple una regla.</returns>
    public static IActionResult Build(ActionContext context)
    {
        var errores = new ModelStateDictionary();

        foreach (var (campo, entrada) in context.ModelState)
        {
            foreach (var error in entrada.Errors)
            {
                errores.AddModelError(NormalizarCampo(campo), Traducir(campo, error));
            }
        }

        var problema = new ValidationProblemDetails(errores)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "La solicitud contiene datos invalidos.",
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        };

        return new BadRequestObjectResult(problema);
    }

    /// <summary>
    /// Sustituye los mensajes del deserializador por uno propio y deja intactos
    /// los que provienen de las anotaciones del modelo.
    /// </summary>
    private static string Traducir(string campo, ModelError error)
    {
        // Los campos con notacion de ruta JSON ("$.amount") solo aparecen cuando
        // falla la deserializacion, asi que el mensaje viene del serializador y no
        // del modelo. Lo mismo cuando el error llega como excepcion.
        if (campo.StartsWith("$", StringComparison.Ordinal)
            || error.Exception is not null
            || string.IsNullOrWhiteSpace(error.ErrorMessage))
        {
            return string.IsNullOrEmpty(campo) ? CuerpoRequerido : TipoInvalido;
        }

        // Estos dos los genera el enlazador cuando falta el cuerpo por completo.
        if (error.ErrorMessage.Contains("A non-empty request body is required", StringComparison.OrdinalIgnoreCase)
            || error.ErrorMessage.Contains("The request field is required", StringComparison.OrdinalIgnoreCase))
        {
            return CuerpoRequerido;
        }

        // Cualquier mensaje que mencione tipos internos se sustituye: son de
        // .NET, estan en ingles y no le dicen nada util a quien consume la API.
        if (error.ErrorMessage.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
            || error.ErrorMessage.Contains("System.", StringComparison.Ordinal))
        {
            return TipoInvalido;
        }

        return error.ErrorMessage;
    }

    /// <summary>
    /// El deserializador nombra los campos con notacion de ruta JSON ("$.amount").
    /// Se deja solo el nombre para que coincida con el del resto de errores.
    /// </summary>
    private static string NormalizarCampo(string campo) =>
        campo.StartsWith("$.", StringComparison.Ordinal) ? campo[2..] : campo;
}
