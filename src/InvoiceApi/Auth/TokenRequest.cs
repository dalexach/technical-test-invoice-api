using System.ComponentModel.DataAnnotations;

namespace InvoiceApi.Auth;

/// <summary>Credenciales enviadas a <c>POST /auth/token</c>.</summary>
public class TokenRequest
{
    /// <summary>Identificador del cliente que solicita el token.</summary>
    [Required(ErrorMessage = "clientId es obligatorio.")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Secreto asociado al identificador.</summary>
    [Required(ErrorMessage = "clientSecret es obligatorio.")]
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>Token emitido por <c>POST /auth/token</c>.</summary>
public class TokenResponse
{
    /// <summary>Token firmado que autoriza el acceso al resto de endpoints.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Esquema con el que enviar el token en la cabecera Authorization.
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Segundos que el token sigue siendo valido desde su emision.</summary>
    public int ExpiresIn { get; set; }
}
