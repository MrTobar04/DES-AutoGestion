using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoGestion.Tests.Data;
using AutoGestion.Tests.Models;

namespace AutoGestion.Tests.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PersonasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPersonas()
        {
            var personas = await _context.Personas.ToListAsync();
            return Ok(personas);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var persona = await _context.Personas.FindAsync(id);
            if (persona == null)
            {
                return NotFound(new { mensaje = $"Persona con Id {id} no encontrada." });
            }
            return Ok(persona);
        }

        [HttpPost]
        public async Task<IActionResult> CrearPersona([FromBody] Persona persona)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Personas.Add(persona);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(ObtenerPorId), new { id = persona.Id }, persona);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarPersona(int id, [FromBody] Persona persona)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existente = await _context.Personas.FindAsync(id);
            if (existente == null)
            {
                return NotFound(new { mensaje = $"Persona con Id {id} no encontrada para actualización." });
            }

            existente.Nombre = persona.Nombre;
            existente.DUI = persona.DUI;

            await _context.SaveChangesAsync();
            return Ok(existente);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarPersona(int id)
        {
            var existente = await _context.Personas.FindAsync(id);
            if (existente == null)
            {
                return NotFound(new { mensaje = $"Persona con Id {id} no encontrada para eliminación." });
            }

            _context.Personas.Remove(existente);
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Persona eliminada exitosamente." });
        }
    }
}
