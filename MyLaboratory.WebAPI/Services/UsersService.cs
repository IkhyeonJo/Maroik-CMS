using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyLaboratory.Common.DataAccess.Contracts;
using MyLaboratory.WebAPI.Common;
using MyLaboratory.WebAPI.Helpers;

namespace MyLaboratory.WebAPI.Services
{
    public interface IUserService
    {
        Task<bool> IsValidUserCredentials(string email, string password);
    }

    public class UserService : IUserService
    {
        private readonly ILogger<UserService> logger;
        private readonly IAccountRepository accountRepository;

        public UserService(ILogger<UserService> logger, IAccountRepository accountRepository)
        {
            this.logger = logger;
            this.accountRepository = accountRepository;
        }

        public async Task<bool> IsValidUserCredentials(string email, string password)
        {
            logger.LogInformation($"Validating user [{email}]");
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var tempAccount = await accountRepository.GetAccountAsync(email);

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

            if (!new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Verify(password ?? "", tempAccount.HashedPassword)) // wrong password
            {
                tempAccount.LoginAttempt++;

                if (tempAccount.LoginAttempt == ServerSetting.MaxLoginAttempt)
                {
                    tempAccount.Locked = true;
                    tempAccount.Message = AccountMessage.AccountLocked;
                    await accountRepository.UpdateAccountAsync(tempAccount);
                    return false;
                }
                else
                {
                    await accountRepository.UpdateAccountAsync(tempAccount);
                    return false;
                }
            }

            if (tempAccount.EmailConfirmed == true && tempAccount.AgreedServiceTerms == true)
            {
                tempAccount.LoginAttempt = 0;
                await accountRepository.UpdateAccountAsync(tempAccount);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}