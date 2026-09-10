using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Vehículos",
        Version = "v1",
        Description = "Microservicio de Flotilla e Inventario de Vehículos para pruebas previas al acoplamiento con Gateway Ocelot"
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Vehículos v1");
    });
}

// Middleware de seguridad Bearer para aislamiento de Vehículos (SPEC-1.3.1 y SPEC-1.1.2)
app.Use(async (context, next) =>
{
    // Permitir acceso libre a Swagger
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { mensaje = "Acceso no autorizado. Se requiere cabecera 'Authorization: Bearer <token>' para acceder al módulo de Vehículos." });
        return;
    }

    var token = authHeader["Bearer ".Length..].Trim();
    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { mensaje = "Token Bearer inválido o vacío." });
        return;
    }

    await next();
});

// In-memory data store for autonomous testing
var vehiculos = new List<Vehiculo>
{
    new() { Id = 1, Marca = "Toyota", Modelo = "Corolla LE", Anio = 2022, Placa = "P123-456", Precio = 18500.00m },
    new() { Id = 2, Marca = "Nissan", Modelo = "Sentra Advance", Anio = 2023, Placa = "P654-321", Precio = 21000.00m },
    new() { Id = 3, Marca = "Honda", Modelo = "Civic Touring", Anio = 2024, Placa = "P789-012", Precio = 26500.00m }
};

var nextId = vehiculos.Max(v => v.Id) + 1;
var syncLock = new object();

app.MapGet("/api/vehiculos", () =>
{
    lock (syncLock)
    {
        return Results.Ok(vehiculos.ToList());
    }
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

app.MapPost("/api/vehiculos", (Vehiculo nuevoVehiculo) =>
{
    lock (syncLock)
    {
        nuevoVehiculo.Id = nextId++;
        vehiculos.Add(nuevoVehiculo);
        return Results.Created($"/api/vehiculos/{nuevoVehiculo.Id}", nuevoVehiculo);
    }
})
.WithName("CrearVehiculo")
.WithSummary("Registra un nuevo vehículo en el inventario");

app.MapPut("/api/vehiculos/{id:int}", (int id, Vehiculo vehiculoActualizado) =>
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
        return Results.Ok(vehiculoActualizado);
    }
})
.WithName("ActualizarVehiculo")
.WithSummary("Actualiza los datos de un vehículo existente");

app.MapDelete("/api/vehiculos/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var index = vehiculos.FindIndex(v => v.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para eliminación" });
        }

        var eliminado = vehiculos[index];
        vehiculos.RemoveAt(index);
        return Results.Ok(eliminado);
    }
})
.WithName("EliminarVehiculo")
.WithSummary("Elimina un vehículo del inventario");

app.Run();

public class Vehiculo
{
    public int Id { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Anio { get; set; }
    public string Placa { get; set; } = string.Empty;
    public decimal Precio { get; set; }
}
