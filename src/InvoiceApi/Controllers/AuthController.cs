using InvoiceApi.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceApi.Controllers;

/// <summary>Emision de tokens de acceso para consumir la API de facturas.</summary>
[ApiController]
[Route("auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    /// <param name="tokenService">Servicio que valida credenciales y emite tokens.</param>
    /// <param name="logger">Registro de la aplicacion.</param>
    public AuthController(ITokenService tokenService, ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>Obtiene un token JWT a partir de las credenciales de cliente.</summary>
    /// <remarks>
    /// El token resultante se envia en la cabecera <c>Authorization: Bearer {token}</c>
    /// al resto de endpoints. En Swagger, use el boton "Authorize".
    /// </remarks>
    /// <response code="200">Token emitido correctamente.</response>
    /// <response code="400">La solicitud no incluye las credenciales requeridas.</response>
    /// <response code="401">Credenciales invalidas.</response>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> CreateToken([FromBody] TokenRequest request)
    {
        if (!_tokenService.ValidateCredentials(request.ClientId, request.ClientSecret))
        {
            // Se registra el clientId pero nunca el secreto.
            _logger.LogWarning("Intento de autenticacion fallido para clientId {ClientId}.", request.ClientId);

            return Problem(
                title: "Credenciales invalidas.",
                detail: "El clientId o el clientSecret no son correctos.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var (accessToken, expiresIn) = _tokenService.CreateToken(request.ClientId);

        _logger.LogInformation("Token emitido para clientId {ClientId}.", request.ClientId);

        return Ok(new TokenResponse { AccessToken = accessToken, ExpiresIn = expiresIn });
    }
}
