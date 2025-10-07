using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class EmailRequest
    {
        [Required(ErrorMessage = "Email jest wymagany.")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail.")]
        public string? Email { get; set; }
    }
}

