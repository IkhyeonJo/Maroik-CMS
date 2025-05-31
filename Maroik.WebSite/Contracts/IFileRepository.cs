namespace Maroik.WebSite.Contracts
{
    public interface IFileRepository
    {
        public Task<byte[]> DownloadAsync(string filePath);
        public Task<bool> UploadAsync(IFormFile formFile, string filePath);
        public Task DeleteAsync(string filePath);
    }
}