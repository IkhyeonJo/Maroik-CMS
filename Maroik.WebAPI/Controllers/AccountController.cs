﻿using Maroik.Common.DataAccess.Contracts;
using Maroik.WebAPI.Contracts;
using Maroik.WebAPI.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Maroik.WebAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        //private readonly ILogger<AccountController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IJwtAuthManager _jwtAuthManager;
        private readonly IAccountRepository _accountRepository;

        //public AccountController(ILogger<AccountController> logger, IUserService userService, IJwtAuthManager jwtAuthManager, IAccountRepository accountRepository)
        public AccountController(IUserRepository userRepository, IJwtAuthManager jwtAuthManager, IAccountRepository accountRepository)
        {
            //_logger = logger;
            _userRepository = userRepository;
            _jwtAuthManager = jwtAuthManager;
            _accountRepository = accountRepository;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (!await _userRepository.IsValidUserCredentialsAsync(request.Email, request.Password))
            {
                return Unauthorized();
            }

            Maroik.Common.DataAccess.Models.Account tempAccount = await _accountRepository.GetAccountByEmailAsync(request.Email);
            Claim[] claims = new[]
            {
                new Claim(ClaimTypes.Email, tempAccount.Email),
                new Claim(ClaimTypes.Name, tempAccount.Nickname),
                new Claim(ClaimTypes.Role, tempAccount.Role)
            };

            JwtAuthResult jwtResult = _jwtAuthManager.GenerateTokens(tempAccount.Nickname, claims, DateTime.UtcNow);
            //logger.LogInformation($"User [{tempAccount.Email}] logged in the system.");
            return Ok(new LoginResult
            {
                UserName = tempAccount.Nickname,
                Role = tempAccount.Role,
                AccessToken = jwtResult.AccessToken,
                RefreshToken = jwtResult.RefreshToken.TokenString
            });
        }

        [HttpGet("user")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            return Ok(new LoginResult
            {
                UserName = User.Identity?.Name,
                Role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty,
                OriginalUserName = User.FindFirst("OriginalUserName")?.Value
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // optionally "revoke" JWT token on the server side --> add the current token to a block-list
            // https://github.com/auth0/node-jsonwebtoken/issues/375

            string userName = User.Identity?.Name;
            _jwtAuthManager.RemoveRefreshTokenByUserName(userName);
            //logger.LogInformation($"User [{userName}] logged out the system.");
            return Ok();
        }

        [HttpPost("refresh-token")]
        [Authorize]
        public async Task<ActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                string userName = User.Identity?.Name;
                //logger.LogInformation($"User [{userName}] is trying to refresh JWT token.");

                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return Unauthorized();
                }

                string accessToken = await HttpContext.GetTokenAsync("Bearer", "access_token");
                JwtAuthResult jwtResult = _jwtAuthManager.Refresh(request.RefreshToken, accessToken, DateTime.UtcNow);
                //logger.LogInformation($"User [{userName}] has refreshed JWT token.");
                return Ok(new LoginResult
                {
                    UserName = userName,
                    Role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty,
                    AccessToken = jwtResult.AccessToken,
                    RefreshToken = jwtResult.RefreshToken.TokenString
                });
            }
            catch (SecurityTokenException e)
            {
                return Unauthorized(e.Message); // return 401 so that the client side can redirect the user to login page
            }
        }

        //[HttpPost("impersonation")]
        //[Authorize(Roles = Role.Admin)]
        //public ActionResult Impersonate([FromBody] ImpersonationRequest request)
        //{
        //    var userName = User.Identity?.Name;
        //    logger.LogInformation($"User [{userName}] is trying to impersonate [{request.UserName}].");

        //    var impersonatedRole = userService.GetUserRole(request.UserName);
        //    if (string.IsNullOrWhiteSpace(impersonatedRole))
        //    {
        //        logger.LogInformation($"User [{userName}] failed to impersonate [{request.UserName}] due to the target user not found.");
        //        return BadRequest($"The target user [{request.UserName}] is not found.");
        //    }
        //    if (impersonatedRole == UserRoles.Admin)
        //    {
        //        logger.LogInformation($"User [{userName}] is not allowed to impersonate another Admin.");
        //        return BadRequest("This action is not supported.");
        //    }

        //    var claims = new[]
        //    {
        //        new Claim(ClaimTypes.Name,request.UserName),
        //        new Claim(ClaimTypes.Role, impersonatedRole),
        //        new Claim("OriginalUserName", userName ?? string.Empty)
        //    };

        //    var jwtResult = jwtAuthManager.GenerateTokens(request.UserName, claims, DateTime.Now);
        //    logger.LogInformation($"User [{request.UserName}] is impersonating [{request.UserName}] in the system.");
        //    return Ok(new LoginResult
        //    {
        //        UserName = request.UserName,
        //        Role = impersonatedRole,
        //        OriginalUserName = userName,
        //        AccessToken = jwtResult.AccessToken,
        //        RefreshToken = jwtResult.RefreshToken.TokenString
        //    });
        //}

        //[HttpPost("stop-impersonation")]
        //public ActionResult StopImpersonation()
        //{
        //    var userName = User.Identity?.Name;
        //    var originalUserName = User.FindFirst("OriginalUserName")?.Value;
        //    if (string.IsNullOrWhiteSpace(originalUserName))
        //    {
        //        return BadRequest("You are not impersonating anyone.");
        //    }
        //    logger.LogInformation($"User [{originalUserName}] is trying to stop impersonate [{userName}].");

        //    var role = userService.GetUserRole(originalUserName);
        //    var claims = new[]
        //    {
        //        new Claim(ClaimTypes.Name,originalUserName),
        //        new Claim(ClaimTypes.Role, role)
        //    };

        //    var jwtResult = jwtAuthManager.GenerateTokens(originalUserName, claims, DateTime.Now);
        //    logger.LogInformation($"User [{originalUserName}] has stopped impersonation.");
        //    return Ok(new LoginResult
        //    {
        //        UserName = originalUserName,
        //        Role = role,
        //        OriginalUserName = null,
        //        AccessToken = jwtResult.AccessToken,
        //        RefreshToken = jwtResult.RefreshToken.TokenString
        //    });
        //}
    }

    public class LoginRequest
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [JsonPropertyName("Email")]
        public string Email { get; set; }

        [Required]
        [JsonPropertyName("Password")]
        public string Password { get; set; }
    }

    public class LoginResult
    {
        [JsonPropertyName("username")]
        public string UserName { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("originalUserName")]
        public string OriginalUserName { get; set; }

        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenRequest
    {
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }
    }

    public class ImpersonationRequest
    {
        [JsonPropertyName("username")]
        public string UserName { get; set; }
    }
}
