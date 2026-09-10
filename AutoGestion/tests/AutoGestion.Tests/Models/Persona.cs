using System.ComponentModel.DataAnnotations;

namespace AutoGestion.Tests.Models
{
    public class Persona : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del registro es de carácter obligatorio.")]
        [MinLength(2, ErrorMessage = "El nombre debe contener al menos 2 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DUI es obligatorio.")]
        [RegularExpression(@"^\d{8}-\d$", ErrorMessage = "El formato del DUI debe ser estrictamente 00000000-0.")]
        public string DUI { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Nombre))
            {
                yield return new ValidationResult(
                    "El nombre del registro es de carácter obligatorio.",
                    new[] { nameof(Nombre) }
                );
            }
        }
    }
}
