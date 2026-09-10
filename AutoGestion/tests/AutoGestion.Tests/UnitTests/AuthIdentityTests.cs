using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ApiVehiculos.Controllers;
using ApiVehiculos.Models;

namespace AutoGestion.Tests.UnitTests
{
    public class AuthIdentityTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly IConfiguration _configuration;
        private readonly AuthController _controller;

        public AuthIdentityTests()
        {
            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object,
                Mock.Of<IOptions<IdentityOptions>>(),
                Mock.Of<IPasswordHasher<ApplicationUser>>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                Mock.Of<ILookupNormalizer>(),
                Mock.Of<IdentityErrorDescriber>(),
                Mock.Of<IServiceProvider>(),
                Mock.Of<ILogger<UserManager<ApplicationUser>>>()
            );

            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "AutoGestion_Desafio2_SecretKey_2026_Secure_JWT_Key_32bytes!" },
                { "Jwt:Issuer", "AutoGestion" },
                { "Jwt:Audience", "AutoGestion" },
                { "Jwt:ExpirationMinutes", "60" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemoryConfig)
                .Build();

            _controller = new AuthController(_userManagerMock.Object, _configuration);
        }

        #region Scenario 1: Registro Exitoso de Nuevo Usuario con Nombre y DUI Válido

        [Fact]
        public async Task Register_ValidPayload_ReturnsOkResultAndPersistsData()
        {
            // Arrange
            var registerDto = new RegisterDto
            {
                Nombre = "Juan Pérez",
                Dui = "01234567-8",
                Email = "operador@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync(registerDto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), registerDto.Password))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<ApplicationUser, string>((user, pass) =>
                {
                    user.Id = "usr-12345";
                });

            // Act
            var result = await _controller.Register(registerDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            _userManagerMock.Verify(m => m.CreateAsync(
                It.Is<ApplicationUser>(u =>
                    u.Nombre == "Juan Pérez" &&
                    u.Dui == "01234567-8" &&
                    u.Email == "operador@autogestion.com" &&
                    u.UserName == "operador@autogestion.com"),
                registerDto.Password),
                Times.Once);
        }

        #endregion

        #region Scenario 2: Rechazo de Registro por Formato Inválido de DUI o Falta de Nombre / Duplicados

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void RegisterDto_MissingNombre_FailsValidation(string? nombreInvalido)
        {
            // Arrange
            var dto = new RegisterDto
            {
                Nombre = nombreInvalido!,
                Dui = "01234567-8",
                Email = "operador@autogestion.com",
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
            var dto = new RegisterDto
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
            var dto = new RegisterDto
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
        public async Task Register_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var dto = new RegisterDto
            {
                Nombre = "",
                Dui = "12345678",
                Email = "email_invalido",
                Password = "123"
            };
            _controller.ModelState.AddModelError("Nombre", "El nombre es obligatorio");
            _controller.ModelState.AddModelError("Dui", "El formato del DUI debe ser 00000000-0");

            // Act
            var result = await _controller.Register(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task Register_ExistingEmail_ReturnsBadRequest()
        {
            // Arrange
            var dto = new RegisterDto
            {
                Nombre = "Juan Perez",
                Dui = "01234567-8",
                Email = "existente@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            var existingUser = new ApplicationUser
            {
                Id = "usr-existing",
                Email = dto.Email,
                UserName = dto.Email,
                Nombre = "Usuario Existente",
                Dui = "09876543-1"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync(dto.Email))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _controller.Register(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public async Task Register_IdentityCreationFailure_ReturnsBadRequestWithErrors()
        {
            // Arrange
            var dto = new RegisterDto
            {
                Nombre = "Juan Perez",
                Dui = "01234567-8",
                Email = "error@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync(dto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            var error = new IdentityError { Code = "PasswordTooShort", Description = "Password is too short." };
            _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Failed(error));

            // Act
            var result = await _controller.Register(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        #endregion

        #region Scenario 3: Autenticación Exitosa y Generación de Token JWT con Claims

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOkWithValidJwtTokenAnd60MinExpiration()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "operador@autogestion.com",
                Password = "PasswordSeguro123!"
            };

            var user = new ApplicationUser
            {
                Id = "user-guid-9988",
                Email = loginDto.Email,
                UserName = loginDto.Email,
                Nombre = "Juan Pérez",
                Dui = "01234567-8"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.CheckPasswordAsync(user, loginDto.Password))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var authResponse = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.NotNull(authResponse);
            Assert.Equal(user.Email, authResponse.Email);
            Assert.False(string.IsNullOrWhiteSpace(authResponse.Token));

            // Validar expiración cercana a 60 minutos (59 - 61 min)
            var timeDifference = authResponse.Expiration - DateTime.UtcNow;
            Assert.InRange(timeDifference.TotalMinutes, 58, 62);

            // Decodificar y validar el token JWT y sus claims
            var handler = new JwtSecurityTokenHandler();
            Assert.True(handler.CanReadToken(authResponse.Token));

            var jwtToken = handler.ReadJwtToken(authResponse.Token);
            Assert.Equal("HS256", jwtToken.Header.Alg);

            var claims = jwtToken.Claims.ToList();
            Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "user-guid-9988");
            Assert.Contains(claims, c => (c.Type == JwtRegisteredClaimNames.Email || c.Type == ClaimTypes.Email) && c.Value == loginDto.Email);
            Assert.Contains(claims, c => c.Type.Equals("nombre", StringComparison.OrdinalIgnoreCase) && c.Value == "Juan Pérez");
            Assert.Contains(claims, c => c.Type.Equals("dui", StringComparison.OrdinalIgnoreCase) && c.Value == "01234567-8");
            Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Jti && !string.IsNullOrEmpty(c.Value));
        }

        #endregion

        #region Scenario 4 & 5: Rechazo de Autenticación con Contraseña Incorrecta o Usuario Inexistente

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "operador@autogestion.com",
                Password = "ContraseñaIncorrecta!"
            };

            var user = new ApplicationUser
            {
                Id = "user-guid-1",
                Email = loginDto.Email,
                UserName = loginDto.Email,
                Nombre = "Juan Pérez",
                Dui = "01234567-8"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync(user);

            _userManagerMock.Setup(m => m.CheckPasswordAsync(user, loginDto.Password))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
        }

        [Fact]
        public async Task Login_NonExistentUser_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = new LoginDto
            {
                Email = "fantasma@autogestion.com",
                Password = "CualquierPassword123!"
            };

            _userManagerMock.Setup(m => m.FindByEmailAsync(loginDto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _controller.Login(loginDto);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
        }

        #endregion
    }
}
