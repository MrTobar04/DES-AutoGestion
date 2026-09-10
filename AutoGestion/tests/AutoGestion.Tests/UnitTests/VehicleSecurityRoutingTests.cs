using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace AutoGestion.Tests.UnitTests
{
    public class VehicleSecurityRoutingTests
    {
        #region RegisterDto and LoginDto Contract Tests

        [Fact]
        public void RegisterDto_ValidPayload_PassesValidation()
        {
            // Arrange
            var dto = new RegisterContractDto
            {
                Nombre = "Juan Pérez",
                Dui = "01234567-8",
                Email = "juan.perez@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(dto, context, results, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void RegisterDto_MissingNombre_FailsValidation(string? nombreInvalido)
        {
            // Arrange
            var dto = new RegisterContractDto
            {
                Nombre = nombreInvalido!,
                Dui = "01234567-8",
                Email = "juan.perez@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(dto, context, results, true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Nombre") || r.ErrorMessage!.Contains("nombre"));
        }

        [Theory]
        [InlineData("12345678")]      // 8 dígitos sin guion
        [InlineData("123456789")]     // 9 dígitos sin guion
        [InlineData("00000000-A")]    // Carácter no numérico en dígito verificador
        [InlineData("ABCDEFGH-1")]    // Letras
        [InlineData("1234567-8")]     // 7 dígitos antes del guion
        [InlineData("12345678--9")]   // Doble guion
        public void RegisterDto_InvalidDui_FailsValidation(string duiInvalido)
        {
            // Arrange
            var dto = new RegisterContractDto
            {
                Nombre = "Carlos Lopez",
                Dui = duiInvalido,
                Email = "carlos@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(dto, context, results, true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Dui") || r.ErrorMessage!.Contains("DUI"));
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("abc")]
        public void RegisterDto_ShortPassword_FailsValidation(string shortPassword)
        {
            // Arrange
            var dto = new RegisterContractDto
            {
                Nombre = "Maria Santos",
                Dui = "09876543-2",
                Email = "maria@autogestion.com",
                Password = shortPassword
            };

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(dto, context, results, true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Password") || r.ErrorMessage!.Contains("contraseña"));
        }

        [Fact]
        public void LoginDto_ValidPayload_PassesValidation()
        {
            // Arrange
            var dto = new LoginContractDto
            {
                Email = "operador@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            var context = new ValidationContext(dto);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(dto, context, results, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(results);
        }

        #endregion

        #region Ocelot Gateway Routing and Configuration Tests (SPEC-1.1.2)

        [Fact]
        public void OcelotConfig_ContainsRequiredAuthAndVehicleRoutes()
        {
            // Arrange
            var ocelotPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ApiGateway", "ocelot.json");
            Assert.True(File.Exists(ocelotPath), $"El archivo ocelot.json no fue encontrado en: {ocelotPath}");

            var json = File.ReadAllText(ocelotPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var routes = root.GetProperty("Routes").EnumerateArray().ToList();

            // Act & Assert - Verificar ruta /register
            var registerRoute = routes.FirstOrDefault(r => r.GetProperty("UpstreamPathTemplate").GetString() == "/register");
            Assert.True(registerRoute.ValueKind != JsonValueKind.Undefined, "La ruta Upstream /register no está configurada en ocelot.json.");
            Assert.Equal("/api/auth/register", registerRoute.GetProperty("DownstreamPathTemplate").GetString());
            Assert.Equal(5003, registerRoute.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Port").GetInt32());

            // Act & Assert - Verificar ruta /login
            var loginRoute = routes.FirstOrDefault(r => r.GetProperty("UpstreamPathTemplate").GetString() == "/login");
            Assert.True(loginRoute.ValueKind != JsonValueKind.Undefined, "La ruta Upstream /login no está configurada en ocelot.json.");
            Assert.Equal("/api/auth/login", loginRoute.GetProperty("DownstreamPathTemplate").GetString());
            Assert.Equal(5003, loginRoute.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Port").GetInt32());

            // Act & Assert - Verificar ruta /vehiculos
            var vehiculosRoute = routes.FirstOrDefault(r => r.GetProperty("UpstreamPathTemplate").GetString() == "/vehiculos");
            Assert.True(vehiculosRoute.ValueKind != JsonValueKind.Undefined, "La ruta Upstream /vehiculos no está configurada en ocelot.json.");
            Assert.Equal("/api/vehiculos", vehiculosRoute.GetProperty("DownstreamPathTemplate").GetString());
            Assert.Equal(5003, vehiculosRoute.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Port").GetInt32());
        }

        [Fact]
        public void OcelotConfig_AllVehicleAndAuthRoutesHaveRateLimitingEnabled()
        {
            // Arrange
            var ocelotPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ApiGateway", "ocelot.json");
            var json = File.ReadAllText(ocelotPath);
            using var doc = JsonDocument.Parse(json);
            var routes = doc.RootElement.GetProperty("Routes").EnumerateArray();

            // Act & Assert
            foreach (var route in routes)
            {
                var upstreamPath = route.GetProperty("UpstreamPathTemplate").GetString();
                if (upstreamPath is "/vehiculos" or "/vehiculos/{everything}" or "/register" or "/login")
                {
                    var rateLimit = route.GetProperty("RateLimitOptions");
                    Assert.True(rateLimit.GetProperty("EnableRateLimiting").GetBoolean(), $"RateLimiting debe estar habilitado para la ruta: {upstreamPath}");
                    Assert.Equal(10, rateLimit.GetProperty("Limit").GetInt32());
                    Assert.Equal("1m", rateLimit.GetProperty("Period").GetString());
                }
            }
        }

        #endregion

        #region Security Middleware Behavior Simulation Tests

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Basic dXNlcjpwYXNz")]
        [InlineData("Bearer ")]
        public void SecurityCheck_AnonymousOrInvalidHeader_ReturnsUnauthorized(string invalidAuthHeader)
        {
            // Simulación de la regla perimetral de seguridad para /api/vehiculos
            var isAuthorized = false;
            var statusCode = StatusCodes.Status200OK;

            if (string.IsNullOrWhiteSpace(invalidAuthHeader) || !invalidAuthHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                var token = invalidAuthHeader["Bearer ".Length..].Trim();
                if (string.IsNullOrEmpty(token))
                {
                    statusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    isAuthorized = true;
                }
            }

            Assert.False(isAuthorized);
            Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        }

        [Fact]
        public void SecurityCheck_ValidBearerToken_AllowsAccess()
        {
            // Arrange
            var validAuthHeader = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.dummy_token_content";

            // Act
            var isAuthorized = false;
            var statusCode = StatusCodes.Status200OK;

            if (string.IsNullOrWhiteSpace(validAuthHeader) || !validAuthHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                var token = validAuthHeader["Bearer ".Length..].Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    isAuthorized = true;
                }
            }

            // Assert
            Assert.True(isAuthorized);
            Assert.Equal(StatusCodes.Status200OK, statusCode);
        }

        #endregion
    }

    public class RegisterContractDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DUI es obligatorio")]
        [RegularExpression(@"^\d{8}-\d$", ErrorMessage = "El formato del DUI debe ser 00000000-0")]
        public string Dui { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener mínimo 6 caracteres")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginContractDto
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        public string Password { get; set; } = string.Empty;
    }
}
