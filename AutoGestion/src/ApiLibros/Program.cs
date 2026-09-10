using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.OpenApi.Models;

LoadEnvFile();

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

var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("RedisConnection")
    ?? "redis-cache:6379";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
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
const string CacheKeyListadoLibros = "listado_libros";

app.MapGet("/api/libros", async (IDistributedCache cache) =>
{
    try
    {
        var cachedData = await cache.GetStringAsync(CacheKeyListadoLibros);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cachedLibros = JsonSerializer.Deserialize<List<Libro>>(cachedData);
            if (cachedLibros is not null)
            {
                return Results.Ok(cachedLibros);
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al consultar Redis Cache en Libros. Se ejecuta fallback a la fuente de datos.");
    }

    List<Libro> result;
    lock (syncLock)
    {
        result = libros.ToList();
    }

    try
    {
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        var serializedData = JsonSerializer.Serialize(result);
        await cache.SetStringAsync(CacheKeyListadoLibros, serializedData, cacheOptions);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al guardar el catálogo de libros en Redis Cache.");
    }

    return Results.Ok(result);
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

app.MapPost("/api/libros", async (Libro nuevoLibro, IDistributedCache cache) =>
{
    lock (syncLock)
    {
        nuevoLibro.Id = nextId++;
        libros.Add(nuevoLibro);
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoLibros);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras POST en Libros.");
    }

    return Results.Created($"/api/libros/{nuevoLibro.Id}", nuevoLibro);
})
.WithName("CrearLibro")
.WithSummary("Registra un nuevo libro en el catálogo");

app.MapPut("/api/libros/{id:int}", async (int id, Libro libroActualizado, IDistributedCache cache) =>
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
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoLibros);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras PUT en Libros.");
    }

    return Results.Ok(libroActualizado);
})
.WithName("ActualizarLibro")
.WithSummary("Actualiza los datos de un libro existente");

app.MapDelete("/api/libros/{id:int}", async (int id, IDistributedCache cache) =>
{
    Libro? eliminado = null;
    lock (syncLock)
    {
        var index = libros.FindIndex(l => l.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Libro con Id {id} no encontrado para eliminación" });
        }

        eliminado = libros[index];
        libros.RemoveAt(index);
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoLibros);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras DELETE en Libros.");
    }

    return Results.Ok(eliminado);
})
.WithName("EliminarLibro")
.WithSummary("Elimina un libro del catálogo");

app.Run();

static void LoadEnvFile()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        var envPath = Path.Combine(current.FullName, ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
            break;
        }
        current = current.Parent;
    }
}

public class Libro
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public int AnioPublicacion { get; set; }
}
