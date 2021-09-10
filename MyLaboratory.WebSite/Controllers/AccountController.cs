using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLaboratory.WebSite.Models.ViewModels.Account;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MyLaboratory.WebSite.Common;
using MyLaboratory.WebSite.Services;
using Newtonsoft.Json;
using System.Text;
using MyLaboratory.WebSite.Models;
using MyLaboratory.Common.DataAccess.Models;
using MyLaboratory.WebSite.Contracts;
using MyLaboratory.WebSite.Helpers;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using MyLaboratory.Common.DataAccess.Contracts;
using MyLaboratory.Common.DataAccess.Services;

namespace MyLaboratory.WebSite.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHtmlLocalizer<AccountController> localizer;
        private readonly ILogger<AccountController> logger;
        private readonly IAccountRepository accountRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IMailRepository mailRepository;

        public AccountController(IHtmlLocalizer<AccountController> localizer, ILogger<AccountController> logger, IAccountRepository accountRepository, ICategoryRepository categoryRepository, ISubCategoryRepository subCategoryRepository, IMailRepository mailRepository)
        {
            this.localizer = localizer;
            this.logger = logger;
            this.accountRepository = accountRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.mailRepository = mailRepository;
        }

        #region 언어 변경
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CultureManagement([FromBody] LoginInputViewModel loginInputViewModel)
        {
            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(loginInputViewModel.Culture)),
                new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30), Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });

            return Json(new { result = true });
        }
        #endregion

        #region 동의서
        [HttpGet]
        public IActionResult ConsentForm()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
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

        #region 로그인
        [HttpGet]
        public IActionResult Login()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
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
        public async Task<IActionResult> Login(LoginInputViewModel loginInputViewModel)
        {
            string userAgent = Request.Headers["User-Agent"].ToString();
            if (userAgent.Contains("MSIE") || userAgent.Contains("Trident")) // IsInternetExplorer (차단 이유: Nonfactor Data Grid에서 문제 생김...)
            {
                TempData["Error"] = localizer["BlockInternetExplorer"].Value;
                return View();
            }

            ModelState.Remove(nameof(loginInputViewModel.Password));
            ModelState.Remove(nameof(loginInputViewModel.FullName));

            if (ModelState.IsValid)
            {
                var tempAccount = await accountRepository.GetAccountAsync(loginInputViewModel.Email);

                if (tempAccount == null) // no account exist
                {
                    TempData["Error"] = localizer["Email or Password is wrong"].Value;
                    return View();
                }

                if (tempAccount.Deleted)
                {
                    TempData["Error"] = localizer["Your Account is Deleted, Please create your new account by clicking Sign up Button"].Value;
                    return View();
                }

                if (tempAccount.Locked)
                {
                    TempData["Error"] = localizer["Your Account is Locked, Please reset your password by clicking Forgot password Button"].Value;
                    return View();
                }

                if (!new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Verify(loginInputViewModel.Password ?? "", tempAccount.HashedPassword)) // wrong password
                {
                    tempAccount.LoginAttempt++;

                    if (tempAccount.LoginAttempt == ServerSetting.MaxLoginAttempt)
                    {
                        tempAccount.Locked = true;
                        tempAccount.Message = AccountMessage.AccountLocked;
                        await accountRepository.UpdateAccountAsync(tempAccount);
                        TempData["Error"] = localizer["Your Account is Locked, Please reset your password by clicking Forgot password Button"].Value;
                        return View();
                    }
                    else
                    {
                        await accountRepository.UpdateAccountAsync(tempAccount);
                        TempData["Error"] = localizer["Email or Password is wrong"].Value;
                        return View();
                    }
                }

                if (tempAccount.EmailConfirmed == true && tempAccount.AgreedServiceTerms == true)
                {
                    tempAccount.LoginAttempt = 0;
                    await accountRepository.UpdateAccountAsync(tempAccount);
                    tempAccount.HashedPassword = null;
                    HttpContext.Session.Set("AccountSession", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(tempAccount)));

                    #region DB Cache
                    if (tempAccount.Role == Role.Admin) // 로그인 권한이 Admin인 경우
                    {
                        DbCache.AdminCategories = await categoryRepository.GetCategoryByRoleAsync(Role.Admin);
                        DbCache.AdminSubCategories = await subCategoryRepository.GetSubCategoryByRoleAsync(Role.Admin);
                        return RedirectToAction("AdminIndex", "DashBoard");
                    }
                    else if (tempAccount.Role == Role.User) // 로그인 권한이 User인 경우
                    {
                        DbCache.UserCategories = await categoryRepository.GetCategoryByRoleAsync(Role.User);
                        DbCache.UserSubCategories = await subCategoryRepository.GetSubCategoryByRoleAsync(Role.User);
                        return RedirectToAction("UserIndex", "DashBoard");
                    }
                    else // 로그인 권한은 Admin 또는 User만 있음 이 경우는 버그 발생한 것임
                    {
                        return null;
                    }
                    #endregion
                }
                else
                {
                    if (tempAccount.EmailConfirmed == false)
                    {
                        TempData["Error"] = localizer["Email verification was not completed. Please try sign up again."].Value;
                        return View();
                    }
                    else if (tempAccount.AgreedServiceTerms == false)
                    {
                        TempData["Error"] = localizer["Agreed Service Terms was not checked. Please try sign up again and login again."].Value;
                        return View();
                    }
                    else
                    {
                        TempData["Error"] = localizer["Email verification was not completed and Agreed Service Terms was not checked. Please try sign up again."].Value;
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

        #region 로그아웃
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("AccountSession");
            return RedirectToAction("Login", "Account");
        }
        #endregion

        #region 계정 생성
        [HttpGet]
        public IActionResult Register()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
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
                        TempData["Error"] = localizer["Please check the consent form"].Value;
                        return View();
                    }

                    var account = await accountRepository.GetAccountAsync(loginInputViewModel.Email);

                    #region 새롭게 회원가입을 진행하는 경우
                    if (account == null) // 입력된 ID로 생성된 Record가 없다.
                    {
                        string registrationToken = GUIDToken.Generate();
                        loginInputViewModel.RegistrationToken = registrationToken;
                        try
                        {
                            await accountRepository.CreateAccountAsync
                                (
                                new Account()
                                {
                                    Email = loginInputViewModel.Email,
                                    HashedPassword = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(loginInputViewModel.Password), // 암호화
                                    FullName = loginInputViewModel.FullName,
                                    AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                                    Role = Role.User,
                                    Locked = false,
                                    LoginAttempt = 0,
                                    EmailConfirmed = false,
                                    AgreedServiceTerms = true,
                                    RegistrationToken = loginInputViewModel.RegistrationToken,
                                    ResetPasswordToken = null,
                                    Created = DateTime.UtcNow,
                                    Updated = DateTime.UtcNow,
                                    Message = AccountMessage.UserCreatedVerifyEmail,
                                    Deleted = false
                                }
                                );
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString()); // ActivityLog 등으로 로그 남겨야 한다..
                            TempData["Error"] = localizer["Error occurred while processing about account registration"].Value;
                            return View();
                        }
                        #region 메일 전송
                        Mail mail = new Mail();
                        mail.Subject = localizer["MyLaboratory Email Confirmation"].Value;
                        loginInputViewModel.RegistrationToken = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(loginInputViewModel.RegistrationToken); // 암호화
                        mail.Body = mailRepository.GetMailConfirmationBody(loginInputViewModel, localizer["Welcome to MyLaboratory WebSite"].Value, localizer["Click the link below to verify your Email"].Value, localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value);
                        mail.ToMailIds = new List<string>
                        {
                            loginInputViewModel.Email
                        };
                        string result = await mailRepository.SendMailAsync(mail);
                        if (result != AccountMessage.MailSent) // 메일 전송 오류 발생 했을 시..
                        {
                            Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                            loginInputViewModel.RegistrationToken = registrationToken;
                            try
                            {
                                await accountRepository.UpdateAccountAsync
                                    (
                                    new Account()
                                    {
                                        Email = loginInputViewModel.Email,
                                        HashedPassword = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(loginInputViewModel.Password), // 암호화
                                        FullName = loginInputViewModel.FullName,
                                        AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                                        Role = Role.User,
                                        Locked = false,
                                        LoginAttempt = 0,
                                        EmailConfirmed = false,
                                        AgreedServiceTerms = true,
                                        RegistrationToken = loginInputViewModel.RegistrationToken,
                                        ResetPasswordToken = null,
                                        Created = DateTime.UtcNow,
                                        Updated = DateTime.UtcNow,
                                        Message = AccountMessage.FailToMailSent,
                                        Deleted = false
                                    });
                            }
                            catch
                            {
                                TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            TempData["Error"] = localizer["Error occurred while processing about sending account authentication mail"].Value;
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
                            Mail mail = new Mail();
                            mail.Subject = localizer["MyLaboratory Email Confirmation"].Value;
                            if (string.IsNullOrEmpty(account.RegistrationToken))
                            {
                                account.RegistrationToken = GUIDToken.Generate();
                            }
                            mail.Body = mailRepository.GetMailConfirmationBody(new LoginInputViewModel() { RegistrationToken = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(account.RegistrationToken) }, localizer["Welcome to MyLaboratory WebSite"].Value, localizer["Click the link below to verify your Email"].Value, localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value);
                            mail.ToMailIds = new List<string>
                            {
                                account.Email
                            };
                            string result = await mailRepository.SendMailAsync(mail);
                            if (result != AccountMessage.MailSent) // 메일 전송 오류 발생 했을 시..
                            {
                                Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                                try
                                {
                                    account.Updated = DateTime.UtcNow;
                                    account.Message = AccountMessage.FailToMailSent;
                                    await accountRepository.UpdateAccountAsync(account);
                                }
                                catch
                                {
                                    TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
                                    return View();
                                }
                                TempData["Error"] = localizer["Error occurred while processing about sending account authentication mail"].Value;
                                return View();
                            }
                            #endregion
                            try
                            {
                                account.Message = AccountMessage.VerifyEmail;
                                account.Updated = DateTime.UtcNow;
                                await accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
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
                                account.Message = AccountMessage.UserAlreadyCreated;
                                account.Updated = DateTime.UtcNow;
                                account.AgreedServiceTerms = true;
                                await accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            TempData["Error"] = localizer[AccountMessage.UserAlreadyCreated].Value;
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
                ModelState.Remove(nameof(loginInputViewModel.Password));
                ModelState.Remove(nameof(loginInputViewModel.FullName));

                if (ModelState.IsValid) // 입력 값 검증
                {
                    var account = await accountRepository.GetAccountAsync(loginInputViewModel.Email);

                    #region 이메일 재전송 하는데 받은 Email로 DB 조회를 했는데 Record가 없다. (버그임)
                    if (account == null) // 입력된 ID로 생성된 Record 없다.
                    {
                        TempData["Error"] = localizer["Failed to resend email"].Value;
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
                            Mail mail = new Mail();
                            mail.Subject = localizer["MyLaboratory Email Confirmation"].Value;
                            mail.Body = mailRepository.GetMailConfirmationBody(new LoginInputViewModel() { RegistrationToken = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(account.RegistrationToken) }, localizer["Welcome to MyLaboratory WebSite"].Value, localizer["Click the link below to verify your Email"].Value, localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value);
                            mail.ToMailIds = new List<string>
                            {
                                account.Email
                            };
                            string result = await mailRepository.SendMailAsync(mail);
                            if (result != AccountMessage.MailSent) // 메일 전송 오류 발생 했을 시..
                            {
                                Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                                try
                                {
                                    account.Updated = DateTime.UtcNow;
                                    account.Message = AccountMessage.FailToMailSent;
                                    await accountRepository.UpdateAccountAsync(account);
                                }
                                catch
                                {
                                    TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
                                    return View();
                                }
                                TempData["Error"] = localizer["Error occurred while processing about sending account authentication mail"].Value;
                                return View();
                            }
                            #endregion
                            try
                            {
                                account.Message = AccountMessage.VerifyEmail;
                                account.Updated = DateTime.UtcNow;
                                await accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = localizer["Error occurred while processing about mail send account status"].Value;
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
                                account.Message = AccountMessage.UserAlreadyCreated;
                                account.Updated = DateTime.UtcNow;
                                await accountRepository.UpdateAccountAsync(account);
                            }
                            catch
                            {
                                TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
                                return View();
                            }
                            TempData["Error"] = localizer[AccountMessage.UserAlreadyCreated].Value;
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

        #region 이메일 확인
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string registrationToken)
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
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
                var rsaHelper = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);

                foreach (var tempAccount in await accountRepository.GetAllAsync())
                {
                    try
                    {
                        if (rsaHelper.Verify(tempAccount.RegistrationToken, registrationToken)) // 암호화된 토큰과 DB 사용자 토큰이 같다
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
                    account.Message = AccountMessage.Success;
                    try
                    {
                        await accountRepository.UpdateAccountAsync(account);
                    }
                    catch  // 2. Update 시, 예외 발생 했다
                    {
                        ViewBag.InvalidToken = true;
                        ViewBag.AccountCreated = true;
                        TempData["Error"] = localizer["Error occurred while processing about confirm email account status"].Value;
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
                    account.Message = AccountMessage.UserAlreadyCreated;
                    try
                    {
                        await accountRepository.UpdateAccountAsync(account);
                    }
                    catch // 2.	Update 시, 예외 발생 했다 [있을 수 없는 일].
                    {
                        ViewBag.InvalidToken = true;
                        ViewBag.AccountCreated = true;
                        TempData["Error"] = localizer["Error occurred while processing about account status"].Value;
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

        #region 비밀번호 초기화
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            bool isAccountSessionExist = HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
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
            ModelState.Remove(nameof(loginInputViewModel.FullName));
            ModelState.Remove(nameof(loginInputViewModel.Password));

            if (ModelState.IsValid)
            {
                var account = await accountRepository.GetAccountAsync(loginInputViewModel.Email);

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
                        Mail mail = new Mail();
                        mail.Subject = localizer["MyLaboratory Reset Password"].Value;
                        mail.Body = mailRepository.GetMailResetPasswordBody(new LoginInputViewModel() { ResetPasswordToken = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(resetPasswordToken) }, localizer["Welcome to MyLaboratory WebSite"].Value, localizer["Click the link below to reset your password"].Value, localizer["If this link does not work, please copy & paste this link to your Internet URL"].Value);
                        mail.ToMailIds = new List<string>
                            {
                                account.Email
                            };
                        string result = await mailRepository.SendMailAsync(mail);
                        if (result != AccountMessage.MailSent) // 메일 전송 오류 발생 했을 시..
                        {
                            Console.WriteLine(result); // ActivityLog 등으로 로그 남겨야 한다..
                            account.Updated = DateTime.UtcNow;
                            account.Message = AccountMessage.FailToMailSent;
                            await accountRepository.UpdateAccountAsync(account);
                            ViewBag.mailSent = true;
                            ViewBag.sentEmailAddress = loginInputViewModel.Email;
                            return View();
                        }
                        #endregion

                        account.ResetPasswordToken = resetPasswordToken;
                        account.Updated = DateTime.UtcNow;
                        account.Message = AccountMessage.ResetPasswordMail;
                        await accountRepository.UpdateAccountAsync(account);
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
            bool isAccountSessionExist = HttpContext.Session.TryGetValue("AccountSession", out byte[] resultByte);

            if (isAccountSessionExist) // 로그인 된 상태라면
            {
                var loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session
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
                var rsaHelper = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);

                foreach (var tempAccount in await accountRepository.GetAllAsync())
                {
                    try
                    {
                        if (rsaHelper.Verify(tempAccount.ResetPasswordToken, resetPasswordToken)) // 암호화된 토큰과 DB 사용자 토큰이 같다
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
            ModelState.Remove(nameof(loginInputViewModel.Email));
            ModelState.Remove(nameof(loginInputViewModel.FullName));
            ModelState.Remove(nameof(loginInputViewModel.Password));

            if (ModelState.IsValid)
            {
                Account account = null;

                #region 1.	입력된 token[암호화 됨]이랑 모든 계정에 저장된 토큰 해쉬화 후 비교해서 해당 계정 찾아낸다.
                var rsaHelper = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey);

                foreach (var tempAccount in await accountRepository.GetAllAsync())
                {
                    try
                    {
                        if (rsaHelper.Verify(tempAccount.ResetPasswordToken, loginInputViewModel.ResetPasswordToken)) // 암호화된 토큰과 DB 사용자 토큰이 같다
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
                    account.HashedPassword = new RSA(RSAType.RSA2, Encoding.UTF8, RSA.privateKey, RSA.publicKey).Sign(loginInputViewModel.Password); // 암호화
                    account.Locked = false; // 잠금 해제
                    account.LoginAttempt = 0; // 로그인 시도 횟수 초기화
                    account.Updated = DateTime.UtcNow;
                    account.Message = AccountMessage.SuccessToResetPassword;
                    try
                    {
                        await accountRepository.UpdateAccountAsync(account);
                    }
                    catch  // 2. Update 시, 예외 발생 했다
                    {
                        ViewBag.FailToResetPassword = true;
                        TempData["Error"] = localizer["Error occurred while processing about reset password"].Value;
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