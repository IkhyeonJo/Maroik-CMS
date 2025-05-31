using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.Forum
{
    public class BoardOutputViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Must be between 1 and 100 characters.", MinimumLength = 1)]
        public string Title { get; set; }
        public string Content { get; set; }
        public bool Locked { get; set; }
        public string Writer { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public long Views { get; set; }
        public string BoardAttachedFileName { get; set; }
        public string BoardAttachedFileBase64Data { get; set; }
        public string BoardAttachedFileContentType { get; set; }
        public string BoardAttachedFileExtension { get; set; }
        public long BoardAttachedFileSize { get; set; }
        public string BoardAttachedFilePath { get; set; }
        public bool IsImgTagIncluded { get; set; }
    }
}
