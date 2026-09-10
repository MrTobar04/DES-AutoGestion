using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ApiVehiculos.Controllers;
using ApiVehiculos.Data;
using ApiVehiculos.Models;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

// Configuración de Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Vehículos y Autenticación",
        Version = "v1",
        Description = "Microservicio de Flotilla de Vehículos y Gestión de Identidad (ASP.NET Core Identity + JWT)"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese su token JWT (Swagger antepondrá automáticamente 'Bearer ')."
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

// Configuración de Entity Framework Core con SQL Server y Resiliencia ante Fallos Transitorios
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? "Server=(localdb)\\mssqllocaldb;Database=AutoGestionVehiculosDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        );
    });
});

// Configuración de ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configuración de Autenticación JWT Bearer
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? AuthController.DefaultJwtKey;

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? AuthController.DefaultIssuer;

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? AuthController.DefaultAudience;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configuración de Redis Cache
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("RedisConnection")
    ?? "redis-cache:6379";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

var app = builder.Build();

// Aplicar migraciones automáticamente en inicio de la aplicación
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Aviso: No se pudo ejecutar context.Database.Migrate() automáticamente en el arranque.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Vehículos v1");
    });
}

// Middleware de seguridad Bearer para aislamiento de Vehículos (SPEC-1.3.1, SPEC-1.1.2, SPEC-4.2.1)
app.Use(async (context, next) =>
{
    // Permitir acceso libre a Swagger y Endpoints de Autenticación
    if (context.Request.Path.StartsWithSegments("/swagger") ||
        context.Request.Path.StartsWithSegments("/api/auth"))
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers.Authorization.ToString().Trim();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        authHeader = context.Request.Headers["Authorization"].ToString().Trim();
    }

    string token = string.Empty;
    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        token = authHeader["Bearer ".Length..].Trim();
    }
    else if (authHeader.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
    {
        token = authHeader["Bearer".Length..].Trim();
    }
    else if (!string.IsNullOrEmpty(authHeader) && authHeader.Split('.').Length == 3)
    {
        token = authHeader;
    }

    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { mensaje = "Acceso no autorizado. Se requiere cabecera 'Authorization: Bearer <token>' para acceder al módulo de Vehículos." });
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Mapeo de Controladores (incluye AuthController para /api/auth/register, /api/auth/login)
app.MapControllers();

// In-memory data store for autonomous testing and caching
var vehiculos = new List<Vehiculo>
{
    new() { Id = 1, ModeloId = 1, Marca = "Toyota", Modelo = "Corolla LE", Anio = 2022, Placa = "P123-456", Precio = 18500.00m },
    new() { Id = 2, ModeloId = 2, Marca = "Nissan", Modelo = "Sentra Advance", Anio = 2023, Placa = "P654-321", Precio = 21000.00m },
    new() { Id = 3, ModeloId = 3, Marca = "Honda", Modelo = "Civic Touring", Anio = 2024, Placa = "P789-012", Precio = 26500.00m }
};

var nextId = vehiculos.Max(v => v.Id) + 1;
var syncLock = new object();
const string CacheKeyListadoVehiculos = "listado_vehiculos";

app.MapGet("/api/vehiculos", async (IDistributedCache cache) =>
{
    try
    {
        var cachedData = await cache.GetStringAsync(CacheKeyListadoVehiculos);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cachedVehiculos = JsonSerializer.Deserialize<List<Vehiculo>>(cachedData);
            if (cachedVehiculos is not null)
            {
                return Results.Ok(cachedVehiculos);
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al consultar Redis Cache en Vehículos. Se ejecuta fallback a la fuente de datos.");
    }

    List<Vehiculo> result;
    lock (syncLock)
    {
        result = vehiculos.ToList();
    }

    try
    {
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        var serializedData = JsonSerializer.Serialize(result);
        await cache.SetStringAsync(CacheKeyListadoVehiculos, serializedData, cacheOptions);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al guardar el inventario de vehículos en Redis Cache.");
    }

    return Results.Ok(result);
})
.WithName("ObtenerVehiculos")
.WithSummary("Obtiene el inventario completo de vehículos");

app.MapGet("/api/vehiculos/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var vehiculo = vehiculos.FirstOrDefault(v => v.Id == id);
        return vehiculo is not null ? Results.Ok(vehiculo) : Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado" });
    }
})
.WithName("ObtenerVehiculoPorId")
.WithSummary("Obtiene un vehículo por su identificador");

app.MapPost("/api/vehiculos", async (Vehiculo nuevoVehiculo, IDistributedCache cache) =>
{
    lock (syncLock)
    {
        nuevoVehiculo.Id = nextId++;
        vehiculos.Add(nuevoVehiculo);
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoVehiculos);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras POST en Vehículos.");
    }

    return Results.Created($"/api/vehiculos/{nuevoVehiculo.Id}", nuevoVehiculo);
})
.WithName("CrearVehiculo")
.WithSummary("Registra un nuevo vehículo en el inventario");

app.MapPut("/api/vehiculos/{id:int}", async (int id, Vehiculo vehiculoActualizado, IDistributedCache cache) =>
{
    lock (syncLock)
    {
        var index = vehiculos.FindIndex(v => v.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para actualización" });
        }

        vehiculoActualizado.Id = id;
        vehiculos[index] = vehiculoActualizado;
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoVehiculos);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras PUT en Vehículos.");
    }

    return Results.Ok(vehiculoActualizado);
})
.WithName("ActualizarVehiculo")
.WithSummary("Actualiza los datos de un vehículo existente");

app.MapDelete("/api/vehiculos/{id:int}", async (int id, IDistributedCache cache) =>
{
    Vehiculo? eliminado = null;
    lock (syncLock)
    {
        var index = vehiculos.FindIndex(v => v.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para eliminación" });
        }

        eliminado = vehiculos[index];
        vehiculos.RemoveAt(index);
    }

    try
    {
        await cache.RemoveAsync(CacheKeyListadoVehiculos);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Error al invalidar Redis Cache tras DELETE en Vehículos.");
    }

    return Results.Ok(eliminado);
})
.WithName("EliminarVehiculo")
.WithSummary("Elimina un vehículo del inventario");

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
