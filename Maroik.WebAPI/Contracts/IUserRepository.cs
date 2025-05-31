namespace Maroik.WebAPI.Contracts
{
    public interface IUserRepository
    {
        public Task<bool> IsValidUserCredentialsAsync(string email, string password);
    }
}