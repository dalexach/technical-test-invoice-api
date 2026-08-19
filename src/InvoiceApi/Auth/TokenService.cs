using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceApi.Auth;

/// <summary>Valida credenciales de cliente y emite los tokens de acceso.</summary>
public interface ITokenService
{
    /// <summary>Comprueba las credenciales recibidas contra las configuradas.</summary>
    /// <param name="clientId">Identificador de cliente recibido.</param>
    /// <param name="clientSecret">Secreto recibido.</param>
    /// <returns><c>true</c> si ambos coinciden con los valores configurados.</returns>
    bool ValidateCredentials(string clientId, string clientSecret);

    /// <summary>Emite un token firmado para el cliente indicado.</summary>
    /// <param name="clientId">Cliente al que se emite el token.</param>
    /// <returns>El token y su vigencia en segundos.</returns>
    (string AccessToken, int ExpiresInSeconds) CreateToken(string clientId);
}

/// <summary>
/// Emisor de tokens firmados con clave simetrica (HMAC-SHA256).
/// </summary>
/// <remarks>
/// Las credenciales se comparan contra las de configuracion. En un sistema real
/// esta responsabilidad recae en un proveedor de identidad; aqui se resuelve
/// dentro del servicio para que la API sea autocontenida, manteniendo los valores
/// fuera del codigo.
/// </remarks>
public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    /// <param name="options">Opciones de emision, validadas al arrancar la aplicacion.</param>
    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    /// <inheritdoc />
    public bool ValidateCredentials(string clientId, string clientSecret)
    {
        // Comparacion en tiempo constante: evita distinguir credenciales validas
        // de invalidas midiendo cuanto tarda la respuesta.
        var idMatches = FixedTimeEquals(clientId, _options.Client.ClientId);
        var secretMatches = FixedTimeEquals(clientSecret, _options.Client.ClientSecret);

        return idMatches && secretMatches;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cada token incluye un identificador unico, lo que permitiria revocarlo de
    /// forma individual si mas adelante se añade una lista de revocacion.
    /// </remarks>
    public (string AccessToken, int ExpiresInSeconds) CreateToken(string clientId)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, clientId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return (accessToken, _options.ExpirationMinutes * 60);
    }

    /// <summary>
    /// Compara dos cadenas en tiempo constante.
    /// </summary>
    /// <remarks>
    /// Una comparacion normal se detiene en el primer caracter distinto, y el
    /// tiempo que tarda revela cuantos coincidian. Repitiendo el intento se puede
    /// reconstruir el secreto caracter a caracter. Esta comparacion siempre recorre
    /// la misma longitud, de modo que la duracion no aporta informacion.
    /// </remarks>
    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
}
