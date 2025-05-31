namespace Maroik.FileStorage.Contracts
{
    public interface IClamavRepository
    {
        public Task<bool> ScanWithClamavAsync(Stream fileStream);
    }
}