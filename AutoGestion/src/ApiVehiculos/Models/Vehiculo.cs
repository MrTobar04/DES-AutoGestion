namespace ApiVehiculos.Models
{
    public class Marca
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public ICollection<Modelo> Modelos { get; set; } = new List<Modelo>();
    }

    public class Modelo
    {
        public int Id { get; set; }
        public int MarcaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public Marca Marca { get; set; } = null!;
        public ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
    }

    public class Vehiculo
    {
        public int Id { get; set; }
        public int ModeloId { get; set; }
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public int Anio { get; set; }
        public decimal Precio { get; set; }
        public string Placa { get; set; } = string.Empty;
        public Modelo? ModeloRelacion { get; set; }
    }
}
