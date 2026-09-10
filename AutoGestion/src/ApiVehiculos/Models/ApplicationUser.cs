using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ApiVehiculos.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DUI es obligatorio")]
        public string Dui { get; set; } = string.Empty;
    }
}
