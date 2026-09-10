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
    public class ModelosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ModelosController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerModelos()
        {
            var modelos = await _context.Modelos.Include(m => m.Marca).ToListAsync();
            return Ok(modelos);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var modelo = await _context.Modelos.Include(m => m.Marca).FirstOrDefaultAsync(m => m.Id == id);
            if (modelo == null)
            {
                return NotFound(new { mensaje = $"Modelo con Id {id} no encontrado" });
            }
            return Ok(modelo);
        }

        [HttpPost]
        public async Task<IActionResult> CrearModelo([FromBody] Modelo modelo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Modelos.Add(modelo);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(ObtenerPorId), new { id = modelo.Id }, modelo);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarModelo(int id, [FromBody] Modelo modeloActualizado)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var modelo = await _context.Modelos.FindAsync(id);
            if (modelo == null)
            {
                return NotFound(new { mensaje = $"Modelo con Id {id} no encontrado" });
            }

            modelo.Nombre = modeloActualizado.Nombre;
            if (modeloActualizado.MarcaId > 0)
            {
                modelo.MarcaId = modeloActualizado.MarcaId;
            }

            await _context.SaveChangesAsync();
            return Ok(modelo);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarModelo(int id)
        {
            var modelo = await _context.Modelos.FindAsync(id);
            if (modelo == null)
            {
                return NotFound(new { mensaje = $"Modelo con Id {id} no encontrado" });
            }

            _context.Modelos.Remove(modelo);
            await _context.SaveChangesAsync();
            return Ok(modelo);
        }
    }
}
