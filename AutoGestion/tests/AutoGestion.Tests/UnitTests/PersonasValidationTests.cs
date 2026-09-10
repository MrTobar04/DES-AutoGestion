using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using AutoGestion.Tests.Controllers;
using AutoGestion.Tests.Helpers;
using AutoGestion.Tests.Models;

namespace AutoGestion.Tests.UnitTests
{
    public class PersonasValidationTests : BaseTestFixture
    {
        private readonly PersonasController _controller;

        public PersonasValidationTests()
        {
            _controller = new PersonasController(Context);
        }

        [Theory]
        [InlineData("12345")]         // Longitud corta sin guion
        [InlineData("123-4")]         // Longitud corta con guion
        [InlineData("12345678")]      // 8 dígitos sin guion (falta dígito verificador)
        [InlineData("123456789")]     // 9 dígitos sin guion
        [InlineData("000000000-0")]   // 9 dígitos antes del guion (longitud mayor)
        [InlineData("00000000-A")]    // Carácter alfabético en verificador
        [InlineData("12345678-X")]    // Carácter especial/letra en verificador
        [InlineData("ABCDEFGH-I")]    // Solo caracteres alfabéticos
        [InlineData("1234567-8")]     // 7 dígitos antes del guion (longitud menor)
        [InlineData("12345678--9")]   // Doble guion
        public async Task CrearPersona_DuiInvalido_RetornaBadRequest(string duiInvalido)
        {
            // Arrange
            var persona = new Persona { Nombre = "Juan Pérez", DUI = duiInvalido };
            _controller.ModelState.AddModelError("DUI", "El formato del DUI debe ser estrictamente 00000000-0.");

            // Act
            var result = await _controller.CrearPersona(persona);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var error = Assert.IsAssignableFrom<SerializableError>(badRequestResult.Value);
            Assert.True(error.ContainsKey("DUI"));
            var errorMessages = Assert.IsAssignableFrom<string[]>(error["DUI"]);
            Assert.Contains("El formato del DUI debe ser estrictamente 00000000-0.", errorMessages);

            // Verificar que no se guardó ningún registro en InMemory
            Assert.Empty(Context.Personas);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task CrearPersona_NombreVacioONulo_RetornaBadRequest(string? nombreInvalido)
        {
            // Arrange
            var persona = new Persona { Nombre = nombreInvalido ?? string.Empty, DUI = "02345678-9" };
            _controller.ModelState.AddModelError("Nombre", "El nombre del registro es de carácter obligatorio.");

            // Act
            var result = await _controller.CrearPersona(persona);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            var error = Assert.IsAssignableFrom<SerializableError>(badRequestResult.Value);
            Assert.True(error.ContainsKey("Nombre"));
            var errorMessages = Assert.IsAssignableFrom<string[]>(error["Nombre"]);
            Assert.Contains("El nombre del registro es de carácter obligatorio.", errorMessages);

            // Verificar que no se guardó ningún registro en InMemory
            Assert.Empty(Context.Personas);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("123-4")]
        [InlineData("12345678")]
        [InlineData("123456789")]
        [InlineData("000000000-0")]
        [InlineData("00000000-A")]
        [InlineData("12345678-X")]
        [InlineData("ABCDEFGH-I")]
        [InlineData("1234567-8")]
        [InlineData("12345678--9")]
        public void ValidacionModelo_DuiInvalido_FallaValidacionDataAnnotations(string duiInvalido)
        {
            // Arrange
            var persona = new Persona { Nombre = "María López", DUI = duiInvalido };
            var validationContext = new ValidationContext(persona);
            var validationResults = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(persona, validationContext, validationResults, validateAllProperties: true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(validationResults, v => v.MemberNames.Contains("DUI") && v.ErrorMessage == "El formato del DUI debe ser estrictamente 00000000-0.");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ValidacionModelo_NombreInvalido_FallaValidacionDataAnnotations(string? nombreInvalido)
        {
            // Arrange
            var persona = new Persona { Nombre = nombreInvalido ?? string.Empty, DUI = "01234567-8" };
            var validationContext = new ValidationContext(persona);
            var validationResults = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(persona, validationContext, validationResults, validateAllProperties: true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(validationResults, v => v.MemberNames.Contains("Nombre"));
        }

        [Fact]
        public void ValidacionModelo_DatosValidos_PasaValidacionDataAnnotations()
        {
            // Arrange
            var persona = new Persona { Nombre = "Carlos Hernández", DUI = "01234567-8" };
            var validationContext = new ValidationContext(persona);
            var validationResults = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(persona, validationContext, validationResults, validateAllProperties: true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
    }
}
