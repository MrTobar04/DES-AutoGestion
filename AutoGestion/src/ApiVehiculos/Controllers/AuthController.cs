using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ApiVehiculos.Models;

namespace ApiVehiculos.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController>? _logger;

        public const string DefaultJwtKey = "AutoGestion_Desafio2_SecretKey_2026_Secure_JWT_Key_32bytes!";
        public const string DefaultIssuer = "AutoGestion";
        public const string DefaultAudience = "AutoGestion";

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger<AuthController>? logger = null)
        {
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
                if (existingUser != null)
                {
                    return BadRequest(new
                    {
                        mensaje = "El correo electrónico ya se encuentra registrado.",
                        errores = new[] { "Email already in use" }
                    });
                }

                var user = new ApplicationUser
                {
                    UserName = registerDto.Email,
                    Email = registerDto.Email,
                    Nombre = registerDto.Nombre,
                    Dui = registerDto.Dui
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        mensaje = "Error en el registro del usuario.",
                        errores = result.Errors.Select(e => e.Description)
                    });
                }

                return Ok(new
                {
                    mensaje = "Usuario registrado exitosamente",
                    email = user.Email,
                    nombre = user.Nombre,
                    dui = user.Dui
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Excepción no controlada durante el registro de usuario en Identity/SQL Server.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    mensaje = "Error interno del servidor al procesar el registro de usuario.",
                    detalle = ex.Message
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user == null)
                {
                    return Unauthorized(new { mensaje = "Credenciales inválidas" });
                }

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
                if (!isPasswordValid)
                {
                    return Unauthorized(new { mensaje = "Credenciales inválidas" });
                }

                var (token, expiration) = GenerateJwtToken(user);

                return Ok(new AuthResponseDto
                {
                    Token = token,
                    Expiration = expiration,
                    Email = user.Email ?? loginDto.Email
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Excepción no controlada durante el inicio de sesión en Identity/SQL Server.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    mensaje = "Error interno del servidor al procesar la autenticación.",
                    detalle = ex.Message
                });
            }
        }

        private (string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user)
        {
            var jwtKey = _configuration["Jwt:Key"]
                ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? DefaultJwtKey;

            var jwtIssuer = _configuration["Jwt:Issuer"]
                ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
                ?? DefaultIssuer;

            var jwtAudience = _configuration["Jwt:Audience"]
                ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                ?? DefaultAudience;

            var expirationMinutes = 60;
            if (int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var parsedMinutes) && parsedMinutes > 0)
            {
                expirationMinutes = parsedMinutes;
            }

            var expiration = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("nombre", user.Nombre),
                new("Nombre", user.Nombre),
                new("dui", user.Dui),
                new("Dui", user.Dui)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: expiration,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiration);
        }
    }
}
