using System.ComponentModel.DataAnnotations;

namespace InvoiceApi.Auth;

/// <summary>Configuracion del emisor y validador de tokens (seccion "Jwt").</summary>
public class JwtOptions
{
    /// <summary>Nombre de la seccion de configuracion que alimenta estas opciones.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Clave simetrica de firma. Se inyecta por variable de entorno o user-secrets;
    /// nunca se versiona en el repositorio.
    /// </summary>
    [Required(ErrorMessage = "Jwt:Key es obligatorio. Configurelo por variable de entorno o user-secrets.")]
    [MinLength(32, ErrorMessage = "Jwt:Key debe tener al menos 32 caracteres (256 bits) para HMAC-SHA256.")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Quien emite el token. Se valida en cada peticion, de modo que un token
    /// firmado por otro sistema con la misma clave no sea aceptado.
    /// </summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Destinatario previsto del token. Impide reutilizar en esta API un token
    /// emitido para otro servicio.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Vigencia del token emitido, en minutos.</summary>
    [Range(1, 1440)]
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>Credenciales aceptadas por el endpoint de emision.</summary>
    public ApiClientCredentials Client { get; set; } = new();
}

/// <summary>
/// Credencial de servicio para obtener un token. En un entorno real esto vive en un
/// proveedor de identidad; aqui se mantiene fuera del codigo, en configuracion.
/// </summary>
public class ApiClientCredentials
{
    /// <summary>Identificador del cliente autorizado a solicitar tokens.</summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Secreto asociado al identificador. Se inyecta por variable de entorno y
    /// nunca se versiona en el repositorio.
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}
