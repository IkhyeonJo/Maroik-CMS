namespace Maroik.WebSite.Contracts
{
    public interface IClamavRepository
    {
        public Task<bool> ScanWithClamavAsync(Stream fileStream);
    }
}