using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Libros",
        Version = "v1",
        Description = "Microservicio de Catálogo de Libros y Manuales Técnicos para pruebas previas al acoplamiento con Gateway Ocelot"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese 'Bearer' [espacio] y luego su token JWT en el campo."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Libros v1");
    });
}

// In-memory data store for autonomous testing
var libros = new List<Libro>
{
    new() { Id = 1, Titulo = "Manual de Taller y Mecánica Automotriz", Autor = "Alonso Pérez", Isbn = "978-0123456789", AnioPublicacion = 2022 },
    new() { Id = 2, Titulo = "Sistemas de Inyección Electrónica Avanzada", Autor = "Roberto Gómez", Isbn = "978-9876543210", AnioPublicacion = 2024 },
    new() { Id = 3, Titulo = "Diagnóstico Computarizado OBD-II", Autor = "Carlos Mendoza", Isbn = "978-1122334455", AnioPublicacion = 2023 }
};

var nextId = libros.Max(l => l.Id) + 1;
var syncLock = new object();

app.MapGet("/api/libros", () =>
{
    lock (syncLock)
    {
        return Results.Ok(libros.ToList());
    }
})
.WithName("ObtenerLibros")
.WithSummary("Obtiene el catálogo de libros y manuales");

app.MapGet("/api/libros/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var libro = libros.FirstOrDefault(l => l.Id == id);
        return libro is not null ? Results.Ok(libro) : Results.NotFound(new { mensaje = $"Libro con Id {id} no encontrado" });
    }
})
.WithName("ObtenerLibroPorId")
.WithSummary("Obtiene un libro por su identificador");

app.MapPost("/api/libros", (Libro nuevoLibro) =>
{
    lock (syncLock)
    {
        nuevoLibro.Id = nextId++;
        libros.Add(nuevoLibro);
        return Results.Created($"/api/libros/{nuevoLibro.Id}", nuevoLibro);
    }
})
.WithName("CrearLibro")
.WithSummary("Registra un nuevo libro en el catálogo");

app.MapPut("/api/libros/{id:int}", (int id, Libro libroActualizado) =>
{
    lock (syncLock)
    {
        var index = libros.FindIndex(l => l.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Libro con Id {id} no encontrado para actualización" });
        }

        libroActualizado.Id = id;
        libros[index] = libroActualizado;
        return Results.Ok(libroActualizado);
    }
})
.WithName("ActualizarLibro")
.WithSummary("Actualiza los datos de un libro existente");

app.MapDelete("/api/libros/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var index = libros.FindIndex(l => l.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Libro con Id {id} no encontrado para eliminación" });
        }

        var eliminado = libros[index];
        libros.RemoveAt(index);
        return Results.Ok(eliminado);
    }
})
.WithName("EliminarLibro")
.WithSummary("Elimina un libro del catálogo");

app.Run();

public class Libro
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public int AnioPublicacion { get; set; }
}
