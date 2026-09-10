using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiVehiculos.Data;
using ApiVehiculos.Models;

namespace ApiVehiculos.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MarcasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MarcasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerMarcas()
        {
            var marcas = await _context.Marcas.Include(m => m.Modelos).ToListAsync();
            return Ok(marcas);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var marca = await _context.Marcas.Include(m => m.Modelos).FirstOrDefaultAsync(m => m.Id == id);
            if (marca == null)
            {
                return NotFound(new { mensaje = $"Marca con Id {id} no encontrada" });
            }
            return Ok(marca);
        }

        [HttpPost]
        public async Task<IActionResult> CrearMarca([FromBody] Marca marca)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Marcas.Add(marca);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(ObtenerPorId), new { id = marca.Id }, marca);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarMarca(int id, [FromBody] Marca marcaActualizada)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var marca = await _context.Marcas.FindAsync(id);
            if (marca == null)
            {
                return NotFound(new { mensaje = $"Marca con Id {id} no encontrada" });
            }

            marca.Nombre = marcaActualizada.Nombre;
            await _context.SaveChangesAsync();
            return Ok(marca);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarMarca(int id)
        {
            var marca = await _context.Marcas.FindAsync(id);
            if (marca == null)
            {
                return NotFound(new { mensaje = $"Marca con Id {id} no encontrada" });
            }

            _context.Marcas.Remove(marca);
            await _context.SaveChangesAsync();
            return Ok(marca);
        }
    }
}
