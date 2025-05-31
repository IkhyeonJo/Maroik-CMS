namespace Maroik.FileStorage.Contracts
{
    public interface IUserRepository
    {
        public Task<bool> IsValidUserCredentialsAsync(string email, string password);
    }
}