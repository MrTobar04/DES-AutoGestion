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
        [InlineData("12345678")]      // Sin guion y falta 1 dígito
        [InlineData("123456789")]     // 9 dígitos sin guion
        [InlineData("00000000-A")]    // Caracter alfabético en verificador
        [InlineData("ABCDEFGH-I")]    // Todo texto
        [InlineData("1234567-8")]     // 7 dígitos antes del guion
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
        }
    }
}
