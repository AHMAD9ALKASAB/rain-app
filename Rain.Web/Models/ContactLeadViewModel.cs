using System.ComponentModel.DataAnnotations;

namespace Rain.Web.Models
{
    public class ContactLeadViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Company { get; set; }

        [Required]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;
    }
}
