using System.ComponentModel.DataAnnotations;

namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class NotificationTemplateInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Template name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        [Display(Name = "Template Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Body is required.")]
        [Display(Name = "Body")]
        public string Body { get; set; } = string.Empty;
    }
}
