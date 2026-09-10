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
        Title = "AutoGestion - API Productos",
        Version = "v1",
        Description = "Microservicio de Catálogo de Productos para pruebas previas al acoplamiento con Gateway Ocelot"
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Productos v1");
    });
}

// In-memory data store for autonomous testing
var productos = new List<Producto>
{
    new() { Id = 1, Nombre = "Filtro de Aceite Sintético", Precio = 14.99m, Stock = 50, Categoria = "Mantenimiento" },
    new() { Id = 2, Nombre = "Pastillas de Freno Cerámicas", Precio = 45.50m, Stock = 30, Categoria = "Frenos" },
    new() { Id = 3, Nombre = "Batería 12V 65Ah Alto Rendimiento", Precio = 110.00m, Stock = 15, Categoria = "Eléctrico" }
};

var nextId = productos.Max(p => p.Id) + 1;
var syncLock = new object();
const string CacheKeyListadoProductos = "listado_productos";

app.MapGet("/api/productos", async (IDistributedCache cache) =>
{
    try
    {
        var cachedData = await cache.GetStringAsync(CacheKeyListadoProductos);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cachedProductos = JsonSerializer.Deserialize<List<Producto>>(cachedData);
            if (cachedProductos is not null)
            {
                return Results.Ok(cachedProductos);
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al consultar Redis Cache. Se ejecuta fallback a la fuente de datos.");
    }

    List<Producto> result;
    lock (syncLock)
    {
        result = productos.ToList();
    }

    try
    {
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        var serializedData = JsonSerializer.Serialize(result);
        await cache.SetStringAsync(CacheKeyListadoProductos, serializedData, cacheOptions);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al guardar el listado de productos en Redis Cache.");
    }

    return Results.Ok(result);
})
.WithName("ObtenerProductos")
.WithSummary("Obtiene el listado completo de productos");

app.MapGet("/api/productos/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var producto = productos.FirstOrDefault(p => p.Id == id);
        return producto is not null ? Results.Ok(producto) : Results.NotFound(new { mensaje = $"Producto con Id {id} no encontrado" });
    }
})
.WithName("ObtenerProductoPorId")
.WithSummary("Obtiene un producto por su identificador");

app.MapPost("/api/productos", async (Producto nuevoProducto, IDistributedCache cache) =>
{
    lock (syncLock)
    {
        nuevoProducto.Id = nextId++;
        productos.Add(nuevoProducto);
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoProductos);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras POST.");
    }

    return Results.Created($"/api/productos/{nuevoProducto.Id}", nuevoProducto);
})
.WithName("CrearProducto")
.WithSummary("Registra un nuevo producto en el catálogo");

app.MapPut("/api/productos/{id:int}", async (int id, Producto productoActualizado, IDistributedCache cache) =>
{
    lock (syncLock)
    {
        var index = productos.FindIndex(p => p.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Producto con Id {id} no encontrado para actualización" });
        }

        productoActualizado.Id = id;
        productos[index] = productoActualizado;
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoProductos);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras PUT.");
    }

    return Results.Ok(productoActualizado);
})
.WithName("ActualizarProducto")
.WithSummary("Actualiza los datos de un producto existente");

app.MapDelete("/api/productos/{id:int}", async (int id, IDistributedCache cache) =>
{
    Producto? eliminado = null;
    lock (syncLock)
    {
        var index = productos.FindIndex(p => p.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Producto con Id {id} no encontrado para eliminación" });
        }

        eliminado = productos[index];
        productos.RemoveAt(index);
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoProductos);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras DELETE.");
    }

    return Results.Ok(eliminado);
})
.WithName("EliminarProducto")
.WithSummary("Elimina un producto del catálogo");

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

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string Categoria { get; set; } = string.Empty;
}
