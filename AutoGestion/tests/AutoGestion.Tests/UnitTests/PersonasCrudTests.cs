using Microsoft.AspNetCore.Mvc;
using AutoGestion.Tests.Controllers;
using AutoGestion.Tests.Helpers;
using AutoGestion.Tests.Models;

namespace AutoGestion.Tests.UnitTests
{
    public class PersonasCrudTests : BaseTestFixture
    {
        private readonly PersonasController _controller;

        public PersonasCrudTests()
        {
            _controller = new PersonasController(Context);
        }

        [Fact]
        public async Task CrearPersona_DatosValidos_GuardaExitosamenteYRetornaCreated()
        {
            // Arrange
            var nuevaPersona = new Persona
            {
                Nombre = "Carlos Eduardo Martínez",
                DUI = "01234567-9"
            };

            // Act
            var result = await _controller.CrearPersona(nuevaPersona);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);

            var personaGuardada = Assert.IsType<Persona>(createdResult.Value);
            Assert.True(personaGuardada.Id > 0);
            Assert.Equal("Carlos Eduardo Martínez", personaGuardada.Nombre);

            // Comprobación de persistencia efectiva en el contexto InMemory
            var personaEnDb = await Context.Personas.FindAsync(personaGuardada.Id);
            Assert.NotNull(personaEnDb);
            Assert.Equal("01234567-9", personaEnDb.DUI);
        }

        [Fact]
        public async Task ObtenerPersonas_RetornaListadoCompletoDePersonas()
        {
            // Arrange
            Context.Personas.AddRange(
                new Persona { Nombre = "Persona Uno", DUI = "11111111-1" },
                new Persona { Nombre = "Persona Dos", DUI = "22222222-2" }
            );
            await Context.SaveChangesAsync();

            // Act
            var result = await _controller.ObtenerPersonas();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var lista = Assert.IsAssignableFrom<IEnumerable<Persona>>(okResult.Value);
            Assert.Equal(2, lista.Count());
        }

        [Fact]
        public async Task ObtenerPorId_IdExistente_RetornaOkConPersona()
        {
            // Arrange
            var persona = new Persona { Nombre = "Ana Gómez", DUI = "09876543-2" };
            Context.Personas.Add(persona);
            await Context.SaveChangesAsync();

            // Act
            var result = await _controller.ObtenerPorId(persona.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var personaObtenida = Assert.IsType<Persona>(okResult.Value);
            Assert.Equal(persona.Id, personaObtenida.Id);
            Assert.Equal("Ana Gómez", personaObtenida.Nombre);
        }

        [Fact]
        public async Task ObtenerPorId_IdInexistente_RetornaNotFoundOBadRequest()
        {
            // Arrange
            int idInexistente = 99999;

            // Act
            var result = await _controller.ObtenerPorId(idInexistente);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task ActualizarPersona_IdExistente_ActualizaExitosamenteYRetornaOk()
        {
            // Arrange
            var persona = new Persona { Nombre = "Nombre Original", DUI = "12345678-9" };
            Context.Personas.Add(persona);
            await Context.SaveChangesAsync();

            var personaModificada = new Persona
            {
                Id = persona.Id,
                Nombre = "Nombre Actualizado",
                DUI = "98765432-1"
            };

            // Act
            var result = await _controller.ActualizarPersona(persona.Id, personaModificada);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var actualizadoEnDb = await Context.Personas.FindAsync(persona.Id);
            Assert.NotNull(actualizadoEnDb);
            Assert.Equal("Nombre Actualizado", actualizadoEnDb.Nombre);
            Assert.Equal("98765432-1", actualizadoEnDb.DUI);
        }

        [Fact]
        public async Task ActualizarPersona_IdInexistente_RetornaNotFoundOBadRequest()
        {
            // Arrange
            var personaParaActualizar = new Persona
            {
                Id = 88888,
                Nombre = "Nombre Fantasma",
                DUI = "00000000-0"
            };

            // Act
            var result = await _controller.ActualizarPersona(88888, personaParaActualizar);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task EliminarPersona_IdExistente_EliminaExitosamenteYRetornaOk()
        {
            // Arrange
            var persona = new Persona { Nombre = "Persona Para Eliminar", DUI = "55555555-5" };
            Context.Personas.Add(persona);
            await Context.SaveChangesAsync();

            // Act
            var result = await _controller.EliminarPersona(persona.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var eliminadoDeDb = await Context.Personas.FindAsync(persona.Id);
            Assert.Null(eliminadoDeDb);
        }

        [Fact]
        public async Task EliminarPersona_IdInexistente_RetornaNotFoundOBadRequest()
        {
            // Arrange
            int idInexistente = 77777;

            // Act
            var result = await _controller.EliminarPersona(idInexistente);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }
    }
}
