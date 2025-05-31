using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.FileStorage.Contracts;
using System.Text;

namespace Maroik.FileStorage.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly RSA _rsa;
        private readonly ILogger<UserRepository> _logger;
        private readonly IAccountRepository _accountRepository;

        public UserRepository(ILogger<UserRepository> logger, IAccountRepository accountRepository)
        {
            _rsa = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);
            _logger = logger;
            _accountRepository = accountRepository;
        }

        public async Task<bool> IsValidUserCredentialsAsync(string email, string password)
        {
            _logger.LogInformation($"Validating user [{email}]");
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            Maroik.Common.DataAccess.Models.Account tempAccount = await _accountRepository.GetAccountByEmailAsync(email);

            if (tempAccount == null)
            {
                return false;
            }

            if (tempAccount.Deleted)
            {
                return false;
            }

            if (tempAccount.Locked)
            {
                return false;
            }

            if (!_rsa.Verify(password ?? "", tempAccount.HashedPassword)) // wrong password
            {
                tempAccount.LoginAttempt++;

                if (tempAccount.LoginAttempt == ServerSetting.MaxLoginAttempt)
                {
                    tempAccount.Locked = true;
                    tempAccount.Message = EnumHelper.GetDescription(AccountMessage.AccountLocked);
                    await _accountRepository.UpdateAccountAsync(tempAccount);
                    return false;
                }
                else
                {
                    await _accountRepository.UpdateAccountAsync(tempAccount);
                    return false;
                }
            }

            if (tempAccount.EmailConfirmed == true && tempAccount.AgreedServiceTerms == true)
            {
                tempAccount.LoginAttempt = 0;
                await _accountRepository.UpdateAccountAsync(tempAccount);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}