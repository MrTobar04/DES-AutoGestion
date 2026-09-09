using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Productos",
        Version = "v1",
        Description = "Microservicio de Catálogo de Productos para AutoGestion S.A."
    });
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

app.MapGet("/api/productos", () =>
{
    lock (syncLock)
    {
        return Results.Ok(productos.ToList());
    }
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

app.MapPost("/api/productos", (Producto nuevoProducto) =>
{
    lock (syncLock)
    {
        nuevoProducto.Id = nextId++;
        productos.Add(nuevoProducto);
        return Results.Created($"/api/productos/{nuevoProducto.Id}", nuevoProducto);
    }
})
.WithName("CrearProducto")
.WithSummary("Registra un nuevo producto en el catálogo");

app.MapPut("/api/productos/{id:int}", (int id, Producto productoActualizado) =>
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
        return Results.Ok(productoActualizado);
    }
})
.WithName("ActualizarProducto")
.WithSummary("Actualiza los datos de un producto existente");

app.MapDelete("/api/productos/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var index = productos.FindIndex(p => p.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Producto con Id {id} no encontrado para eliminación" });
        }

        var eliminado = productos[index];
        productos.RemoveAt(index);
        return Results.Ok(eliminado);
    }
})
.WithName("EliminarProducto")
.WithSummary("Elimina un producto del catálogo");

app.Run();

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string Categoria { get; set; } = string.Empty;
}
