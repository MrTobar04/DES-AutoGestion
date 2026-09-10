using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiVehiculos.Controllers;
using ApiVehiculos.Data;
using ApiVehiculos.Models;

namespace AutoGestion.Tests.UnitTests
{
    public class VehicleAuthorizationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly VehiculosController _vehiculosController;
        private readonly MarcasController _marcasController;
        private readonly ModelosController _modelosController;

        public VehicleAuthorizationTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            _vehiculosController = new VehiculosController(_context);
            _marcasController = new MarcasController(_context);
            _modelosController = new ModelosController(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Scenario 1: Bloqueo de Acceso Anónimo con Atributo [Authorize] a Nivel de Clase (100% de Endpoints)

        [Fact]
        public void VehiculosController_HasAuthorizeAttributeAtClassLevel()
        {
            // Assert
            var authorizeAttribute = typeof(VehiculosController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttribute);
        }

        [Fact]
        public void MarcasController_HasAuthorizeAttributeAtClassLevel()
        {
            // Assert
            var authorizeAttribute = typeof(MarcasController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttribute);
        }

        [Fact]
        public void ModelosController_HasAuthorizeAttributeAtClassLevel()
        {
            // Assert
            var authorizeAttribute = typeof(ModelosController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorizeAttribute);
        }

        [Theory]
        [InlineData(typeof(VehiculosController))]
        [InlineData(typeof(MarcasController))]
        [InlineData(typeof(ModelosController))]
        public void AllActions_InheritClassLevelAuthorize_AndNoneHaveAllowAnonymous(Type controllerType)
        {
            // Arrange & Act
            var publicMethods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType) ||
                            (m.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(m.ReturnType)));

            // Assert
            foreach (var method in publicMethods)
            {
                var allowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>();
                Assert.Null(allowAnonymous); // Ningún método individual debe abrir una brecha anónima
            }
        }

        #endregion

        #region Scenario 2: Acceso Autorizado y Operaciones CRUD en VehiculosController

        [Fact]
        public async Task ObtenerVehiculos_AuthenticatedUser_ReturnsOkWithVehiclesList()
        {
            // Arrange
            SetAuthenticatedUser(_vehiculosController);

            // Act
            var result = await _vehiculosController.ObtenerVehiculos();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var list = Assert.IsAssignableFrom<IEnumerable<Vehiculo>>(okResult.Value);
            Assert.NotEmpty(list);
        }

        [Fact]
        public async Task ObtenerPorId_ExistingId_ReturnsOkWithVehiculo()
        {
            // Arrange
            SetAuthenticatedUser(_vehiculosController);

            // Act
            var result = await _vehiculosController.ObtenerPorId(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var vehiculo = Assert.IsType<Vehiculo>(okResult.Value);
            Assert.Equal(1, vehiculo.Id);
        }

        [Fact]
        public async Task ObtenerPorId_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            SetAuthenticatedUser(_vehiculosController);

            // Act
            var result = await _vehiculosController.ObtenerPorId(99999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task CrearVehiculo_ValidPayload_ReturnsCreatedAtActionAndPersists()
        {
            // Arrange
            SetAuthenticatedUser(_vehiculosController);
            var nuevoVehiculo = new Vehiculo
            {
                ModeloId = 1,
                Marca = "Toyota",
                Modelo = "Yaris Sedan",
                Anio = 2024,
                Placa = "P999-888",
                Precio = 19500.00m
            };

            // Act
            var result = await _vehiculosController.CrearVehiculo(nuevoVehiculo);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(201, createdResult.StatusCode);

            var vehiculoCreado = Assert.IsType<Vehiculo>(createdResult.Value);
            Assert.True(vehiculoCreado.Id > 0);

            var enDb = await _context.Vehiculos.FindAsync(vehiculoCreado.Id);
            Assert.NotNull(enDb);
            Assert.Equal("P999-888", enDb.Placa);
        }

        [Fact]
        public async Task ActualizarVehiculo_ExistingId_UpdatesAndReturnsOk()
        {
            // Arrange
            SetAuthenticatedUser(_vehiculosController);
            var vehiculoActualizado = new Vehiculo
            {
                Marca = "Toyota",
                Modelo = "Corolla Actualizado",
                Anio = 2023,
                Placa = "P123-999",
                Precio = 20000.00m
            };

            // Act
            var result = await _vehiculosController.ActualizarVehiculo(1, vehiculoActualizado);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var enDb = await _context.Vehiculos.FindAsync(1);
            Assert.NotNull(enDb);
            Assert.Equal("Corolla Actualizado", enDb.Modelo);
            Assert.Equal("P123-999", enDb.Placa);
        }

        [Fact]
        public async Task EliminarVehiculo_ExistingId_RemovesAndReturnsOk()
        {
            // Arrange
            SetAuthenticatedUser(_vehiculosController);

            // Act
            var result = await _vehiculosController.EliminarVehiculo(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var enDb = await _context.Vehiculos.FindAsync(1);
            Assert.Null(enDb);
        }

        #endregion

        #region Scenario 3: Operaciones en MarcasController y ModelosController

        [Fact]
        public async Task MarcasController_ObtenerMarcas_ReturnsOkList()
        {
            // Arrange
            SetAuthenticatedUser(_marcasController);

            // Act
            var result = await _marcasController.ObtenerMarcas();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
            var marcas = Assert.IsAssignableFrom<IEnumerable<Marca>>(okResult.Value);
            Assert.NotEmpty(marcas);
        }

        [Fact]
        public async Task ModelosController_ObtenerModelos_ReturnsOkList()
        {
            // Arrange
            SetAuthenticatedUser(_modelosController);

            // Act
            var result = await _modelosController.ObtenerModelos();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
            var modelos = Assert.IsAssignableFrom<IEnumerable<Modelo>>(okResult.Value);
            Assert.NotEmpty(modelos);
        }

        #endregion

        private static void SetAuthenticatedUser(ControllerBase controller)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "usr-test-123"),
                new Claim(ClaimTypes.Email, "operador@autogestion.com"),
                new Claim("nombre", "Operador de Pruebas"),
                new Claim("dui", "01234567-8")
            };
            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }
    }
}
