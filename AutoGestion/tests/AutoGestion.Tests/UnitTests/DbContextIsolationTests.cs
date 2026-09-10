using Microsoft.EntityFrameworkCore;
using AutoGestion.Tests.Data;
using AutoGestion.Tests.Helpers;
using AutoGestion.Tests.Models;

namespace AutoGestion.Tests.UnitTests
{
    public class DbContextIsolationTests : BaseTestFixture
    {
        [Fact]
        public async Task InstanciasSeparadas_PoseenBasesDeDatosInMemoryAisladas()
        {
            // Arrange: Context 1 (de la fixture) y Context 2 (creado independientemente)
            using var context2 = DbContextTestFactory.CreateInMemoryDbContext();

            var persona1 = new Persona { Id = 1, Nombre = "Juan Pérez", DUI = "01234567-8" };
            var persona2 = new Persona { Id = 1, Nombre = "María López", DUI = "08765432-1" };

            // Act: Guardar entidades con el mismo ID en ambos contextos
            Context.Personas.Add(persona1);
            await Context.SaveChangesAsync();

            context2.Personas.Add(persona2);
            await context2.SaveChangesAsync();

            // Assert: No hay colisión de llaves primarias ni estado compartido
            var resultado1 = await Context.Personas.FindAsync(1);
            var resultado2 = await context2.Personas.FindAsync(1);

            Assert.NotNull(resultado1);
            Assert.NotNull(resultado2);
            Assert.Equal("Juan Pérez", resultado1.Nombre);
            Assert.Equal("María López", resultado2.Nombre);
        }

        [Fact]
        public void DbContextInMemory_NoUtilizaCadenaDeConexionRelacional()
        {
            // Assert: El proveedor configurado en la factoría es InMemory
            var providerName = Context.Database.ProviderName;
            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", providerName);
        }
    }
}
