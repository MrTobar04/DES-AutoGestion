using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using ApiVehiculos.Data;
using ApiVehiculos.Models;

namespace ApiVehiculos.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class VehiculosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache? _cache;
        private readonly ILogger<VehiculosController>? _logger;

        public const string CacheKeyListadoVehiculos = "listado_vehiculos";

        public VehiculosController(
            ApplicationDbContext context,
            IDistributedCache? cache = null,
            ILogger<VehiculosController>? logger = null)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerVehiculos()
        {
            if (_cache != null)
            {
                try
                {
                    var cachedData = await _cache.GetStringAsync(CacheKeyListadoVehiculos);
                    if (!string.IsNullOrEmpty(cachedData))
                    {
                        var cachedVehiculos = JsonSerializer.Deserialize<List<Vehiculo>>(cachedData);
                        if (cachedVehiculos is not null)
                        {
                            return Ok(cachedVehiculos);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error al consultar Redis Cache en Vehículos. Se ejecuta fallback a base de datos.");
                }
            }

            var vehiculos = await _context.Vehiculos.ToListAsync();

            if (_cache != null)
            {
                try
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    };
                    var serializedData = JsonSerializer.Serialize(vehiculos);
                    await _cache.SetStringAsync(CacheKeyListadoVehiculos, serializedData, cacheOptions);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error al guardar el inventario de vehículos en Redis Cache.");
                }
            }

            return Ok(vehiculos);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var vehiculo = await _context.Vehiculos.FindAsync(id);
            if (vehiculo == null)
            {
                return NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado" });
            }
            return Ok(vehiculo);
        }

        [HttpPost]
        public async Task<IActionResult> CrearVehiculo([FromBody] Vehiculo vehiculo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Vehiculos.Add(vehiculo);
            await _context.SaveChangesAsync();

            if (_cache != null)
            {
                try
                {
                    await _cache.RemoveAsync(CacheKeyListadoVehiculos);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error al invalidar Redis Cache tras POST en Vehículos.");
                }
            }

            return CreatedAtAction(nameof(ObtenerPorId), new { id = vehiculo.Id }, vehiculo);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarVehiculo(int id, [FromBody] Vehiculo vehiculoActualizado)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var vehiculo = await _context.Vehiculos.FindAsync(id);
            if (vehiculo == null)
            {
                return NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para actualización" });
            }

            vehiculo.Marca = vehiculoActualizado.Marca;
            vehiculo.Modelo = vehiculoActualizado.Modelo;
            vehiculo.Anio = vehiculoActualizado.Anio;
            vehiculo.Precio = vehiculoActualizado.Precio;
            vehiculo.Placa = vehiculoActualizado.Placa;
            if (vehiculoActualizado.ModeloId > 0)
            {
                vehiculo.ModeloId = vehiculoActualizado.ModeloId;
            }

            await _context.SaveChangesAsync();

            if (_cache != null)
            {
                try
                {
                    await _cache.RemoveAsync(CacheKeyListadoVehiculos);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error al invalidar Redis Cache tras PUT en Vehículos.");
                }
            }

            return Ok(vehiculo);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarVehiculo(int id)
        {
            var vehiculo = await _context.Vehiculos.FindAsync(id);
            if (vehiculo == null)
            {
                return NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para eliminación" });
            }

            _context.Vehiculos.Remove(vehiculo);
            await _context.SaveChangesAsync();

            if (_cache != null)
            {
                try
                {
                    await _cache.RemoveAsync(CacheKeyListadoVehiculos);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error al invalidar Redis Cache tras DELETE en Vehículos.");
                }
            }

            return Ok(vehiculo);
        }
    }
}
