using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace LetrerosHerreraSYNCTIMEapi.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public const string MensajePasswordInvalida = "La password debe tener entre 8 y 72 caracteres.";

    // BCrypt solo considera los primeros 72 bytes.
    public static bool EsPasswordValida(string? password) =>
        password is { Length: >= 8 and <= 72 };

    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password);

    public bool VerifyPassword(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);

    public string CreateTokenEmpleado(Models.Empleado empleado)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, empleado.IdEmpleado.ToString()),
            new Claim(ClaimTypes.NameIdentifier, empleado.IdEmpleado.ToString()),
            new Claim(ClaimTypes.Name, $"{empleado.Nombre} {empleado.Apellido}"),
            new Claim(ClaimTypes.Email, empleado.Email),
            new Claim(ClaimTypes.Role, empleado.Rol),
            new Claim("user_type", "empleado")
        };
        return GenerateToken(claims);
    }

    public string CreateTokenCliente(Models.Cliente cliente)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, cliente.IdCliente.ToString()),
            new Claim(ClaimTypes.NameIdentifier, cliente.IdCliente.ToString()),
            new Claim(ClaimTypes.Name, $"{cliente.Nombre} {cliente.Apellido}"),
            new Claim(ClaimTypes.Email, cliente.Email ?? ""),
            new Claim(ClaimTypes.Role, "cliente"),
            new Claim("user_type", "cliente")
        };
        return GenerateToken(claims);
    }

    private string GenerateToken(Claim[] claims)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        var expMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var m) ? m : 480;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
