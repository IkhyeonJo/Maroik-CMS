using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Extensions;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Contracts;
using Maroik.WebSite.Models.ViewModels.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;

namespace Maroik.WebSite.Controllers
{
    public class AccountController : Controller
    {
        private readonly RSA _rsa;
        private readonly IHtmlLocalizer<AccountController> _localizer;
        private readonly ILogger<AccountController> _logger;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IMailRepository _mailRepository;

        public AccountController(IHtmlLocalizer<AccountController> localizer, ILogger<AccountController> logger, IAccountRepository accountRepository, ICategoryRepository categoryRepository, ISubCategoryRepository subCategoryRepository, IMailRepository mailRepository)
        {
            _rsa = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);
            _localizer = localizer;
            _logger = logger;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _mailRepository = mailRepository;
        }

        #region ConsentForm
        [HttpGet]
        public IActionResult ConsentForm()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
                if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                {
                    return RedirectToAction("AdminIndex", "DashBoard");
                }
                else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                {
                    return RedirectToAction("UserIndex", "DashBoard");
                }
                else // 로그인한 사용자 권한이 Admin도 아니고 User도 아니면 DB Insert 오류임
                {
                    return null; // 잘못된 값을 넣으면 잘못된 값이 나옴
                }
            }
            else // 로그인 안 된 상태라면
            {
                return View();
            }
        }
        #endregion

        #region Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] //To Prevent CSRF attack
        public async Task<IActionResult> Login(LoginInputViewModel loginInputViewModel)
        {
            string userAgent = Request.Headers["User-Agent"].ToString();
            if (userAgent.Contains("MSIE") || userAgent.Contains("Trident")) // IsInternetExplorer (차단 이유: Nonfactor Data Grid에서 문제 생김...)
            {
                TempData["Error"] = _localizer["BlockInternetExplorer"].Value;
                return View();
            }

            _ = ModelState.Remove(nameof(loginInputViewModel.Password));
            _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));

            if (ModelState.IsValid)
            {
                Account tempAccount = await _accountRepository.GetAccountByEmailAsync(loginInputViewModel.Email);

                if (tempAccount == null) // no account exist
                {
                    TempData["Error"] = _localizer["Email or Password is wrong"].Value;
                    return View();
                }

                if (tempAccount.Deleted)
                {
                    TempData["Error"] = _localizer["Your Account is Deleted, Please create your new account by clicking Sign up Button"].Value;
                    return View();
                }

                if (tempAccount.Locked)
                {
                    TempData["Error"] = _localizer["Your Account is Locked, Please reset your password by clicking Forgot password Button"].Value;
                    return View();
                }

                if (!_rsa.Verify(loginInputViewModel.Password ?? "", tempAccount.HashedPassword)) // wrong password
                {
                    tempAccount.LoginAttempt++;

                    if (tempAccount.LoginAttempt == ServerSetting.MaxLoginAttempt)
                    {
                        tempAccount.Locked = true;
                        tempAccount.Message = EnumHelper.GetDescription(AccountMessage.AccountLocked);
                        await _accountRepository.UpdateAccountAsync(tempAccount);
                        TempData["Error"] = _localizer["Your Account is Locked, Please reset your password by clicking Forgot password Button"].Value;
                        return View();
                    }
                    else
                    {
                        await _accountRepository.UpdateAccountAsync(tempAccount);
                        TempData["Error"] = _localizer["Email or Password is wrong"].Value;
                        return View();
                    }
                }

                if (tempAccount.EmailConfirmed == true && tempAccount.AgreedServiceTerms == true)
                {
                    tempAccount.LoginAttempt = 0;
                    await _accountRepository.UpdateAccountAsync(tempAccount);
                    tempAccount.HashedPassword = null;
                    HttpContext.Session.Set(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tempAccount)));

                    #region DB Cache
                    return tempAccount.Role == Role.Admin
                        ? RedirectToAction("AdminIndex", "DashBoard")
                        : tempAccount.Role == Role.User ? RedirectToAction("UserIndex", "DashBoard") : (IActionResult)null;
                    #endregion
                }
                else
                {
                    if (tempAccount.EmailConfirmed == false)
                    {
                        TempData["Error"] = _localizer["Email verification was not completed. Please try sign up again."].Value;
                        return View();
                    }
                    else if (tempAccount.AgreedServiceTerms == false)
                    {
                        TempData["Error"] = _localizer["Agreed Service Terms was not checked. Please try sign up again and login again."].Value;
                        return View();
                    }
                    else
                    {
                        TempData["Error"] = _localizer["Email verification was not completed and Agreed Service Terms was not checked. Please try sign up again."].Value;
                        return View();
                    }
                }
            }
            else
            {
                return View();
            }
        }
        #endregion

        #region Logout
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account));
            return RedirectToAction("AnonymousIndex", "DashBoard");
        }
        #endregion

        #region Register
        [HttpGet]
        public IActionResult Register()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
                if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                {
                    return RedirectToAction("AdminIndex", "DashBoard");
                }
                else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                {
                    return RedirectToAction("UserIndex", "DashBoard");
                }
                else // 로그인한 사용자 권한이 Admin도 아니고 User도 아니면 DB Insert 오류임
                {
                    return null; // 잘못된 값을 넣으면 잘못된 값이 나옴
                }
            }
            else // 로그인 안 된 상태라면
            {
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] //To Prevent CSRF attack
        public async Task<IActionResult> Register(LoginInputViewModel loginInputViewModel)
        {
            if (!string.IsNullOrEmpty(loginInputViewModel.Password)) // 회원 가입
            {
                if (ModelState.IsValid) // 입력 값 검증
                {
                    if (!loginInputViewModel.AgreedServiceTerms) // 계약서 동의 하지 않을 시,
                    {
                        TempData["Error"] = _localizer["Please check the consent form"].Value;
                        return View();
                    }

                    if (string.IsNullOrEmpty(loginInputViewModel.TimeZoneIanaId)) // TimeZone 선택 하지 않을 시,
                    {
                        TempData["Error"] = _localizer["Please select time zone"].Value;
                        return View();
                    }

                    Account account = await _accountRepository.GetAccountByEmailAsync(loginInputViewModel.Email);

                    #region 새롭게 회원가입을 진행하는 경우
                    if (account == null) // 입력된 ID로 생성된 Record가 없다.
                    {
                        string registrationToken = GUIDToken.Generate();
                        loginInputViewModel.RegistrationToken = registrationToken;
                        try
                        {
                            await _accountRepository.CreateAccountAsync
                                (
                                new Account()
                                {
                                    Email = loginInputViewModel.Email,
                                    HashedPassword = _rsa.Sign(loginInputViewModel.Password), // 암호화
                                    Nickname = loginInputViewModel.Nickname,
                                    AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                                    Role = Role.User,
                                    TimeZoneIanaId = loginInputViewModel.TimeZoneIanaId,
                                    Locked = false,
                                    LoginAttempt = 0,
                                    EmailConfirmed = false,
                                    AgreedServiceTerms = true,
                                    RegistrationToken = loginInputViewModel.RegistrationToken,
                                    ResetPasswordToken = null,
                                    Created = DateTime.UtcNow,
                                    Updated = DateTime.UtcNow,
                                    Message = EnumHelper.GetDescription(AccountMessage.UserCreatedVerifyEmail),
                                    Deleted = false
                                }
                                );
                        }
                        catch (DbUpdateException e)
                        {
                            if (((Npgsql.NpgsqlException)e.InnerException).SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
                            {
                                Console.WriteLine(_localizer["Input is invalid"].Value); // ActivityLog 등으로 로그 남겨야 한다..
                                TempData["Error"] = $"'{loginInputViewModel.Nickname}' {_localizer["is a Nickname that already exists."].Value} {_localizer["Please enter another Nickname."].Value}";
                                return View();
                            }
                            else
                            {
                                Console.WriteLine(_localizer["Input is invalid"].Value); // ActivityLog 등으로 로그 남겨야 한다..
                                TempData["Error"] = _localizer["Error occurred while processing about account registration"].Value;
                                return View();
                            }
                        }
                        catch
                        {
                            Console.WriteLine(_localizer["Input is invalid"].Value); // ActivityLog 등으로 로그 남겨야 한다..
                            TempData["Error"] = _localizer["Error occurred while processing about account registration"].Value;
                            return View();
                        }
                        #region 메일 전송
                        Mail mail = new()
                        {
                            Subject = _localizer["Maroik Email Confirmation"].Value
                        };
                        loginInputViewModel.RegistrationToken = _rsa.Sign(loginInputViewModel.RegistrationToken); // 암호화
                        mail.Body = _mailRepository.GetMailConfirmationBody(loginInputViewModel, _localizer["Welcome to Maroik"].Value, _localizer["Click the link below to verify your Email"].Value, _localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value);
                        mail.ToMailIds =
                        [
                            loginInputViewModel.Email
                        ];
                        string result = await _mailRepository.SendMailAsync(mail);
                        if (result != EnumHelper.GetDescription(AccountMessage.MailSent)) // 메일 전송 오류 발생 했을 시..
                        {
                            Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                            loginInputViewModel.RegistrationToken = registrationToken;
                            try
                            {
                                await _accountRepository.UpdateAccountAsync
                                    (
                                    new Account()
                                    {
                                        Email = loginInputViewModel.Email,
                                        HashedPassword = _rsa.Sign(loginInputViewModel.Password), // 암호화
                                        Nickname = loginInputViewModel.Nickname,
                                        AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                                        Role = Role.User,
                                        TimeZoneIanaId = loginInputViewModel.TimeZoneIanaId,
                                        Locked = false,
                                        LoginAttempt = 0,
                                        EmailConfirmed = false,
                                        AgreedServiceTerms = true,
                                        RegistrationToken = loginInputViewModel.RegistrationToken,
                                        ResetPasswordToken = null,
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Message = EnumHelper.GetDescription(AccountMessage.FailToMailSent),
                                        Deleted = false
                                    });
                            }
                            catch
                            {
                                TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            TempData["Error"] = _localizer["Error occurred while processing about sending account authentication mail"].Value;
                            return View();
                        }
                        #endregion
                        ViewBag.ResendEmail = true; // 이메일 재전송 폼 활성화
                        ViewBag.ResentEmailAddress = loginInputViewModel.Email; // 이메일 재전송 폼 이메일 주소 표시용
                        return View();
                    }
                    #endregion

                    #region 회원가입이 된 경우
                    else // 입력된 ID로 생성된 Record 있다.
                    {
                        #region 이메일 인증이 안 된 경우
                        if (!account.EmailConfirmed) // 생성은 되었으나 Email 확인 안 함
                        {
                            #region 메일 전송
                            Mail mail = new()
                            {
                                Subject = _localizer["Maroik Email Confirmation"].Value
                            };
                            if (string.IsNullOrEmpty(account.RegistrationToken))
                            {
                                account.RegistrationToken = GUIDToken.Generate();
                            }
                            mail.Body = _mailRepository.GetMailConfirmationBody(new LoginInputViewModel() { RegistrationToken = _rsa.Sign(account.RegistrationToken) }, _localizer["Welcome to Maroik"].Value, _localizer["Click the link below to verify your Email"].Value, _localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value);
                            mail.ToMailIds =
                            [
                                account.Email
                            ];
                            string result = await _mailRepository.SendMailAsync(mail);
                            if (result != EnumHelper.GetDescription(AccountMessage.MailSent)) // 메일 전송 오류 발생 했을 시..
                            {
                                Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                                try
                                {
                                    account.Updated = DateTime.UtcNow;
                                    account.Message = EnumHelper.GetDescription(AccountMessage.FailToMailSent);
                                    await _accountRepository.UpdateAccountAsync(account);
                                }
                                catch
                                {
                                    TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                                    return View();
                                }
                                TempData["Error"] = _localizer["Error occurred while processing about sending account authentication mail"].Value;
                                return View();
                            }
                            #endregion
                            try
                            {
                                account.Message = EnumHelper.GetDescription(AccountMessage.VerifyEmail);
                                account.Updated = DateTime.UtcNow;
                                await _accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            ViewBag.ResendEmail = true; // 이메일 재전송 폼 활성화
                            ViewBag.ResentEmailAddress = account.Email; // 이메일 재전송 폼 이메일 주소 표시용
                            return View();
                        }
                        #endregion

                        #region 이메일 인증이 된 경우
                        else // 계정 생성 및 이메일 확인 완료
                        {
                            try
                            {
                                account.Message = EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated);
                                account.Updated = DateTime.UtcNow;
                                account.AgreedServiceTerms = true;
                                await _accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            TempData["Error"] = _localizer[EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated)].Value;
                            ViewBag.ResendEmail = false; // 로그인 폼 활성화
                            return View();
                        }
                        #endregion
                    }
                    #endregion
                }
                else // 입력 값 잘못 됨
                {
                    return RedirectToAction("Register", "Account");
                }
            }
            else // 이메일 재전송
            {
                _ = ModelState.Remove(nameof(loginInputViewModel.Password));
                _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));

                if (ModelState.IsValid) // 입력 값 검증
                {
                    Account account = await _accountRepository.GetAccountByEmailAsync(loginInputViewModel.Email);

                    #region 이메일 재전송 하는데 받은 Email로 DB 조회를 했는데 Record가 없다. (버그임)
                    if (account == null) // 입력된 ID로 생성된 Record 없다.
                    {
                        TempData["Error"] = _localizer["Failed to resend email"].Value;
                        ViewBag.ResendEmail = true; // 이메일 재전송 폼 활성화
                        return View();
                    }
                    #endregion

                    #region 이메일 재전송
                    else // 입력된 ID로 생성된 Record 있다.
                    {
                        #region 생성은 되었으나 Email 확인 안 함
                        if (!account.EmailConfirmed) // 생성은 되었으나 Email 확인 안 함.
                        {
                            #region 메일 전송
                            Mail mail = new()
                            {
                                Subject = _localizer["Maroik Email Confirmation"].Value,
                                Body = _mailRepository.GetMailConfirmationBody(new LoginInputViewModel() { RegistrationToken = _rsa.Sign(account.RegistrationToken) }, _localizer["Welcome to Maroik"].Value, _localizer["Click the link below to verify your Email"].Value, _localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value),
                                ToMailIds =
                                [
                                    account.Email
                                ]
                            };
                            string result = await _mailRepository.SendMailAsync(mail);
                            if (result != EnumHelper.GetDescription(AccountMessage.MailSent)) // 메일 전송 오류 발생 했을 시..
                            {
                                Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                                try
                                {
                                    account.Updated = DateTime.UtcNow;
                                    account.Message = EnumHelper.GetDescription(AccountMessage.FailToMailSent);
                                    await _accountRepository.UpdateAccountAsync(account);
                                }
                                catch
                                {
                                    TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                                    return View();
                                }
                                TempData["Error"] = _localizer["Error occurred while processing about sending account authentication mail"].Value;
                                return View();
                            }
                            #endregion
                            try
                            {
                                account.Message = EnumHelper.GetDescription(AccountMessage.VerifyEmail);
                                account.Updated = DateTime.UtcNow;
                                await _accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = _localizer["Error occurred while processing about mail send account status"].Value;
                                return View();
                            }
                            ViewBag.ResendEmail = true; // 이메일 재전송 폼 활성화
                            ViewBag.ResentEmailAddress = account.Email; // 이메일 재전송 폼 이메일 주소 표시용
                            ViewBag.RepeatEmailSend = true; // 이메일 재발송 확인
                            return View();
                        }
                        #endregion

                        #region 생성되고 Email 확인함
                        else // 생성되고 Email 확인함
                        {
                            try
                            {
                                account.Message = EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated);
                                account.Updated = DateTime.UtcNow;
                                await _accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            TempData["Error"] = _localizer[EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated)].Value;
                            ViewBag.ResendEmail = false; // 로그인 폼 활성화
                            return View();
                        }
                        #endregion
                    }
                    #endregion
                }

                else // 입력 값 잘못 됨
                {
                    return RedirectToAction("Register", "Account");
                }
            }
        }
        #endregion

        #region ConfirmEmail
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string registrationToken)
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
                if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                {
                    return RedirectToAction("AdminIndex", "DashBoard");
                }
                else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                {
                    return RedirectToAction("UserIndex", "DashBoard");
                }
                else // 로그인한 사용자 권한이 Admin도 아니고 User도 아니면 DB Insert 오류임
                {
                    return null; // 잘못된 값을 넣으면 잘못된 값이 나옴
                }
            }
            else // 로그인 안 된 상태라면
            {
                Account account = null;

                #region 1.	입력된 token[암호화 됨]이랑 모든 계정에 저장된 토큰 해쉬화 후 비교해서 해당 계정 찾아낸다.
                foreach (Account tempAccount in await _accountRepository.GetAllAsync())
                {
                    try
                    {
                        if (_rsa.Verify(tempAccount.RegistrationToken, registrationToken)) // 암호화된 토큰과 DB 사용자 토큰이 같다
                        {
                            account = tempAccount;
                            break;
                        }
                    }
                    catch
                    {

                    }
                }
                #endregion

                #region 2.	해당 계정을 Token으로 찾지 못했다.
                if (account == null)
                {
                    ViewBag.InvalidToken = true;
                    ViewBag.AccountCreated = false;
                    return View();
                }
                #endregion

                #region 3.	해당 계정의 Token 만료 되었다.
                if (!GUIDToken.IsTokenAlive(account.RegistrationToken))
                {
                    ViewBag.InvalidToken = true;
                    ViewBag.AccountCreated = false;
                    return View();
                }
                #endregion

                #region 4.	해당 계정의 EmailConfirmd가 false이다. (이메일 인증을 하지 않았다.)
                if (!account.EmailConfirmed)
                {
                    account.RegistrationToken = null;
                    account.EmailConfirmed = true;
                    account.Updated = DateTime.UtcNow;
                    account.Message = EnumHelper.GetDescription(AccountMessage.Success);
                    try
                    {
                        await _accountRepository.UpdateAccountAsync(account);
                    }
                    catch  // 2. Update 시, 예외 발생 했다
                    {
                        ViewBag.InvalidToken = true;
                        ViewBag.AccountCreated = true;
                        TempData["Error"] = _localizer["Error occurred while processing about confirm email account status"].Value;
                        return View();
                    }
                    ViewBag.InvalidToken = false;
                    ViewBag.AccountCreated = true;
                    return View();
                }
                #endregion

                #region 5.	해당 계정의 EmailConfirmd가 true이다. (이메일 인증을 하였다.)
                else
                {
                    account.Updated = DateTime.UtcNow;
                    account.Message = EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated);
                    try
                    {
                        await _accountRepository.UpdateAccountAsync(account);
                    }
                    catch // 2.	Update 시, 예외 발생 했다 [있을 수 없는 일].
                    {
                        ViewBag.InvalidToken = true;
                        ViewBag.AccountCreated = true;
                        TempData["Error"] = _localizer["Error occurred while processing about account status"].Value;
                        return View();
                    }
                    ViewBag.InvalidToken = false;
                    ViewBag.AccountCreated = false;
                    return View();
                }
                #endregion
            }
        }
        #endregion

        #region ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
                if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                {
                    return RedirectToAction("AdminIndex", "DashBoard");
                }
                else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                {
                    return RedirectToAction("UserIndex", "DashBoard");
                }
                else // 로그인한 사용자 권한이 Admin도 아니고 User도 아니면 DB Insert 오류임
                {
                    return null; // 잘못된 값을 넣으면 잘못된 값이 나옴
                }
            }
            else // 로그인 안 된 상태라면
            {
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] //To Prevent CSRF attack
        public async Task<IActionResult> ForgotPassword(LoginInputViewModel loginInputViewModel)
        {
            _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));
            _ = ModelState.Remove(nameof(loginInputViewModel.Password));

            if (ModelState.IsValid)
            {
                Account account = await _accountRepository.GetAccountByEmailAsync(loginInputViewModel.Email);

                if (account == null) // 입력 된 Email로 생성된 Record 없다.
                {
                    ViewBag.mailSent = true;
                    ViewBag.sentEmailAddress = loginInputViewModel.Email;
                    return View();
                }

                if (!account.EmailConfirmed) // 이메일 인증 안 됨
                {
                    ViewBag.mailSent = true;
                    ViewBag.sentEmailAddress = loginInputViewModel.Email;
                    return View();
                }

                if (account.EmailConfirmed) // 이메일 인증 됨
                {
                    string resetPasswordToken = GUIDToken.Generate();
                    try
                    {
                        #region 메일 전송
                        Mail mail = new()
                        {
                            Subject = _localizer["Maroik Reset Password"].Value,
                            Body = _mailRepository.GetMailResetPasswordBody(new LoginInputViewModel() { ResetPasswordToken = _rsa.Sign(resetPasswordToken) }, _localizer["Welcome to Maroik"].Value, _localizer["Click the link below to reset your password"].Value, _localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value),
                            ToMailIds =
                            [
                                account.Email
                            ]
                        };
                        string result = await _mailRepository.SendMailAsync(mail);
                        if (result != EnumHelper.GetDescription(AccountMessage.MailSent)) // 메일 전송 오류 발생 했을 시..
                        {
                            Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                            account.Updated = DateTime.UtcNow;
                            account.Message = EnumHelper.GetDescription(AccountMessage.FailToMailSent);
                            await _accountRepository.UpdateAccountAsync(account);
                            ViewBag.mailSent = true;
                            ViewBag.sentEmailAddress = loginInputViewModel.Email;
                            return View();
                        }
                        #endregion

                        account.ResetPasswordToken = resetPasswordToken;
                        account.Updated = DateTime.UtcNow;
                        account.Message = EnumHelper.GetDescription(AccountMessage.ResetPasswordMail);
                        await _accountRepository.UpdateAccountAsync(account);
                        ViewBag.mailSent = true;
                        ViewBag.sentEmailAddress = loginInputViewModel.Email;
                        return View();
                    }
                    catch
                    {
                        ViewBag.mailSent = true;
                        ViewBag.sentEmailAddress = loginInputViewModel.Email;
                        return View();
                    }
                }
                ViewBag.mailSent = true;
                ViewBag.sentEmailAddress = loginInputViewModel.Email;
                return View();
            }
            else // 입력 값 잘못 됨
            {
                return RedirectToAction("ForgotPassword", "Account");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string resetPasswordToken)
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
                if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                {
                    return RedirectToAction("AdminIndex", "DashBoard");
                }
                else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                {
                    return RedirectToAction("UserIndex", "DashBoard");
                }
                else // 로그인한 사용자 권한이 Admin도 아니고 User도 아니면 DB Insert 오류임
                {
                    return null; // 잘못된 값을 넣으면 잘못된 값이 나옴
                }
            }
            else // 로그인 안 된 상태라면
            {
                Account account = null;

                #region 1.	입력된 token[암호화 됨]이랑 모든 계정에 저장된 토큰 해쉬화 후 비교해서 해당 계정 찾아낸다.
                foreach (Account tempAccount in await _accountRepository.GetAllAsync())
                {
                    try
                    {
                        if (_rsa.Verify(tempAccount.ResetPasswordToken, resetPasswordToken)) // 암호화된 토큰과 DB 사용자 토큰이 같다
                        {
                            account = tempAccount;
                            break;
                        }
                    }
                    catch
                    {

                    }
                }
                #endregion

                #region 2.	해당 계정을 Token으로 찾지 못했다.
                if (account == null)
                {
                    ViewBag.FailToResetPassword = true;
                    return View();
                }
                #endregion

                #region 3.	해당 계정의 Token 만료 되었다.
                if (!GUIDToken.IsTokenAlive(account.ResetPasswordToken))
                {
                    ViewBag.FailToResetPassword = true;
                    return View();
                }
                #endregion

                #region 4.	해당 계정의 EmailConfirmd가 false이다. (이메일 인증을 하지 않았다.)
                if (!account.EmailConfirmed)
                {
                    ViewBag.FailToResetPassword = true;
                    return View();
                }
                #endregion

                #region 5.	해당 계정의 EmailConfirmd가 true이다. (이메일 인증을 하였다.)
                else
                {
                    ViewBag.FailToResetPassword = false;
                    ViewBag.ResetPasswordToken = resetPasswordToken;
                    return View();
                }
                #endregion
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] //To Prevent CSRF attack
        public async Task<IActionResult> ResetPassword(LoginInputViewModel loginInputViewModel)
        {
            _ = ModelState.Remove(nameof(loginInputViewModel.Email));
            _ = ModelState.Remove(nameof(loginInputViewModel.Nickname));
            _ = ModelState.Remove(nameof(loginInputViewModel.Password));

            if (ModelState.IsValid)
            {
                Account account = null;

                #region 1.	입력된 token[암호화 됨]이랑 모든 계정에 저장된 토큰 해쉬화 후 비교해서 해당 계정 찾아낸다.
                foreach (Account tempAccount in await _accountRepository.GetAllAsync())
                {
                    try
                    {
                        if (_rsa.Verify(tempAccount.ResetPasswordToken, loginInputViewModel.ResetPasswordToken)) // 암호화된 토큰과 DB 사용자 토큰이 같다
                        {
                            account = tempAccount;
                            break;
                        }
                    }
                    catch
                    {

                    }
                }
                #endregion

                #region 2.	해당 계정을 Token으로 찾지 못했다.
                if (account == null)
                {
                    ViewBag.FailToResetPassword = true;
                    return View();
                }
                #endregion

                #region 3.	해당 계정의 Token 만료 되었다.
                if (!GUIDToken.IsTokenAlive(account.ResetPasswordToken))
                {
                    ViewBag.FailToResetPassword = true;
                    return View();
                }
                #endregion

                #region 4.	해당 계정의 EmailConfirmd가 false이다. (이메일 인증을 하지 않았다.)
                if (!account.EmailConfirmed)
                {
                    ViewBag.FailToResetPassword = true;
                    return View();
                }
                #endregion

                #region 5.	해당 계정의 EmailConfirmd가 true이다. (이메일 인증을 하였다.)
                else
                {
                    account.ResetPasswordToken = null;
                    account.HashedPassword = _rsa.Sign(loginInputViewModel.Password); // 암호화
                    account.Locked = false; // 잠금 해제
                    account.LoginAttempt = 0; // 로그인 시도 횟수 초기화
                    account.Updated = DateTime.UtcNow;
                    account.Message = EnumHelper.GetDescription(AccountMessage.SuccessToResetPassword);
                    try
                    {
                        await _accountRepository.UpdateAccountAsync(account);
                    }
                    catch  // 2. Update 시, 예외 발생 했다
                    {
                        ViewBag.FailToResetPassword = true;
                        TempData["Error"] = _localizer["Error occurred while processing about reset password"].Value;
                        return View();
                    }
                    ViewBag.resetPasswordComplete = true;
                    return View();
                }
                #endregion
            }
            else // 입력 값 잘못 됨
            {
                return RedirectToAction("ResetPassword", "Account");
            }
        }
        #endregion
    }
}