using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.Management
{
    public class BoardInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Must be between 1 and 100 characters.", MinimumLength = 1)]
        public string Title { get; set; }

        public string Content { get; set; }

        public bool Noticed { get; set; }

        public bool Locked { get; set; }

        public IFormFile UploadedFile { get; set; }
    }
}
