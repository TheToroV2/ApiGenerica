using System;
using System.IdentityModel.Tokens.Jwt; // Importa la funcionalidad para generar y manejar tokens JWT.
using System.Security.Claims; // Importa la funcionalidad para manejar claims en la autenticación.
using System.Text; // Importa el espacio de nombres necesario para la codificación de texto.
using Microsoft.Extensions.Configuration; // Importa la configuración de la aplicación.
using Microsoft.IdentityModel.Tokens; // Importa la funcionalidad para la validación y firma de tokens.

namespace csharpapi.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuracion; // Configuración de la aplicación.

        // Constructor que recibe la configuración de la aplicación.
        public TokenService(IConfiguration configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        // Método para generar un token JWT.
        public string GenerarToken(string usuario)
        {
            // Obtiene la clave JWT desde la configuración y valida que no sea null.
            var claveJwt = _configuracion["Jwt:Key"]
                ?? throw new InvalidOperationException("La clave JWT no está configurada correctamente.");

            // Convierte la clave en un arreglo de bytes para ser utilizada en la firma.
            var claveSecreta = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveJwt));

            // Crea las credenciales de firma usando la clave secreta y el algoritmo HMAC SHA256.
            var credenciales = new SigningCredentials(claveSecreta, SecurityAlgorithms.HmacSha256);

            // Define los claims del token (información que llevará dentro).
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario), // Identificador del usuario.
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Identificador único del token.
            };

            // Crea el token con los claims, la firma y la duración definida.
            var tokenDescriptor = new JwtSecurityToken(
                issuer: _configuracion["Jwt:Issuer"], // Emisor del token.
                audience: _configuracion["Jwt:Audience"], // Audiencia del token.
                claims: claims, // Claims definidos anteriormente.
                expires: DateTime.UtcNow.AddHours(2), // Expiración del token en 2 horas.
                signingCredentials: credenciales // Credenciales de firma.
            );

            // Convierte el token en una cadena y lo devuelve.
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}
