using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ApiVehiculos.Models;

namespace ApiVehiculos.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Marca> Marcas => Set<Marca>();
        public DbSet<Modelo> Modelos => Set<Modelo>();
        public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // CONFIGURACION Y SEED: MARCAS
            // =========================
            modelBuilder.Entity<Vehiculo>()
                .Property(v => v.Precio)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Marca>().HasData(
                new Marca { Id = 1, Nombre = "Toyota" },
                new Marca { Id = 2, Nombre = "Honda" },
                new Marca { Id = 3, Nombre = "Ford" },
                new Marca { Id = 4, Nombre = "Chevrolet" }
            );

            // =========================
            // CONFIGURACION Y SEED: MODELOS
            // =========================
            modelBuilder.Entity<Modelo>().HasData(
                new Modelo { Id = 1, MarcaId = 1, Nombre = "Corolla" },
                new Modelo { Id = 2, MarcaId = 1, Nombre = "Hilux" },
                new Modelo { Id = 3, MarcaId = 2, Nombre = "Civic" },
                new Modelo { Id = 4, MarcaId = 2, Nombre = "CR-V" },
                new Modelo { Id = 5, MarcaId = 3, Nombre = "Mustang" },
                new Modelo { Id = 6, MarcaId = 3, Nombre = "Ranger" },
                new Modelo { Id = 7, MarcaId = 4, Nombre = "Camaro" },
                new Modelo { Id = 8, MarcaId = 4, Nombre = "Silverado" }
            );

            // =========================
            // CONFIGURACION Y SEED: VEHICULOS
            // =========================
            modelBuilder.Entity<Vehiculo>().HasData(
                new Vehiculo { Id = 1, ModeloId = 1, Marca = "Toyota", Modelo = "Corolla LE", Anio = 2020, Placa = "P123-456", Precio = 18500.00m },
                new Vehiculo { Id = 2, ModeloId = 1, Marca = "Toyota", Modelo = "Hilux 4x4", Anio = 2022, Placa = "P234-567", Precio = 28000.00m },
                new Vehiculo { Id = 3, ModeloId = 2, Marca = "Toyota", Modelo = "Hilux SRV", Anio = 2021, Placa = "P345-678", Precio = 32000.00m },
                new Vehiculo { Id = 4, ModeloId = 3, Marca = "Honda", Modelo = "Civic EX", Anio = 2019, Placa = "P456-789", Precio = 17500.00m },
                new Vehiculo { Id = 5, ModeloId = 3, Marca = "Honda", Modelo = "Civic Touring", Anio = 2023, Placa = "P567-890", Precio = 26500.00m },
                new Vehiculo { Id = 6, ModeloId = 4, Marca = "Honda", Modelo = "CR-V Turbo", Anio = 2022, Placa = "P678-901", Precio = 29500.00m },
                new Vehiculo { Id = 7, ModeloId = 5, Marca = "Ford", Modelo = "Mustang GT", Anio = 2021, Placa = "P789-012", Precio = 38000.00m },
                new Vehiculo { Id = 8, ModeloId = 6, Marca = "Ford", Modelo = "Ranger XLT", Anio = 2020, Placa = "P890-123", Precio = 24000.00m },
                new Vehiculo { Id = 9, ModeloId = 7, Marca = "Chevrolet", Modelo = "Camaro SS", Anio = 2018, Placa = "P901-234", Precio = 34000.00m },
                new Vehiculo { Id = 10, ModeloId = 8, Marca = "Chevrolet", Modelo = "Silverado LT", Anio = 2023, Placa = "P012-345", Precio = 42000.00m }
            );
        }
    }
}
