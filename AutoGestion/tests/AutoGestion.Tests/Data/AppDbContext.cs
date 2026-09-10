using Microsoft.EntityFrameworkCore;
using AutoGestion.Tests.Models;

namespace AutoGestion.Tests.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Persona> Personas => Set<Persona>();
    }
}
