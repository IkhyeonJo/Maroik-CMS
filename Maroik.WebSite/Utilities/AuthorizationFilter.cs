using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Models;
using Maroik.Common.Miscellaneous.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;

namespace Maroik.WebSite.Utilities
{
    public class AuthorizationFilter : ActionFilterAttribute
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;

        public AuthorizationFilter(ICategoryRepository categoryRepository, ISubCategoryRepository subCategoryRepository)
        {
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
        }

        // Do something before the action executes.
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            IEnumerable<Category> categories = _categoryRepository.GetCategory();
            IEnumerable<SubCategory> subCategories = _subCategoryRepository.GetSubCategory();

            DbCache.AdminCategories = from adminCategory in categories
                                      where adminCategory.Role == Role.Admin
                                      select adminCategory;

            DbCache.AdminSubCategories = from adminSubCategory in subCategories
                                         where adminSubCategory.Role == Role.Admin
                                         select adminSubCategory;

            DbCache.UserCategories = from userCategory in categories
                                     where userCategory.Role == Role.User
                                     select userCategory;

            DbCache.UserSubCategories = from userSubCategory in subCategories
                                        where userSubCategory.Role == Role.User
                                        select userSubCategory;

            DbCache.AnonymousCategories = from anonymousCategory in categories
                                          where anonymousCategory.Role == Role.Anonymous
                                          select anonymousCategory;

            DbCache.AnonymousSubCategories = from anonymousSubCategory in subCategories
                                             where anonymousSubCategory.Role == Role.Anonymous
                                             select anonymousSubCategory;


            string currentControllerName = context.ActionDescriptor.RouteValues["controller"].ToString(); // 실행 전 Controller 이름
            string currentActionName = context.ActionDescriptor.RouteValues["action"].ToString(); // 실행 전 Action 이름
            string currentRequestMethod = context.HttpContext.Request.Method; // GET HTTP METHOD
            bool isAccountSessionExist = context.HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out _); // 로그인이 되어 계정 세션이 생겼는지 확인

            if (currentRequestMethod.ToUpper() == "GET") // GET 방식 (페이지 접근만 차단!!)
            {
                if (isAccountSessionExist == false) // 로그인이 안 된 상태이면
                {
                    if (currentControllerName == "Exception" && currentActionName == "Error") // 예외처리 자원 접근 허용 (Error)
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "Login") // 로그인 페이지인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "Register") // 회원가입 페이지인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "ConfirmEmail") // 이메일 인증 페이지인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "ConsentForm") // 동의서 페이지인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "ForgotPassword") // 비밀번호 찾기 페이지인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "ResetPassword") // 비밀번호 초기화 페이지인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "Logout") // 로그아웃인 경우
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }

                    IEnumerable<Category> anonymousCategories = DbCache.AnonymousCategories;
                    IEnumerable<SubCategory> anonymousSubCategories = DbCache.AnonymousSubCategories;

                    #region SubCategory가 없는 Category일 경우
                    IEnumerable<Category> singleCategories = from anonymousCategory in anonymousCategories
                                                             where !string.IsNullOrEmpty(anonymousCategory.Action)
                                                             select anonymousCategory; // 서브 카테고리 없는 카테고리 모음.

                    foreach (Category singleCategory in singleCategories)
                    {
                        if (singleCategory.Controller == currentControllerName && singleCategory.Action == currentActionName)
                        {
                            base.OnActionExecuting(context); // Do Action!!
                            return;
                        }
                    }
                    #endregion

                    #region SubCategory가 있는 Category일 경우
                    IEnumerable<Category> nonSingleCategories = from anonymousCategory in anonymousCategories
                                                                where string.IsNullOrEmpty(anonymousCategory.Action)
                                                                select anonymousCategory; // 서브 카테고리 있는 카테고리 모음.

                    foreach (Category nonSingleCategory in nonSingleCategories)
                    {
                        IEnumerable<SubCategory> nonSingleCategorysSubCategories = from anonymousSubCategory in anonymousSubCategories
                                                                                   where anonymousSubCategory.CategoryId == nonSingleCategory.Id
                                                                                   select anonymousSubCategory;
                        foreach (SubCategory nonSingleCategorysSubCategory in nonSingleCategorysSubCategories)
                        {
                            if (nonSingleCategory.Controller == currentControllerName && nonSingleCategorysSubCategory.Action == currentActionName)
                            {
                                base.OnActionExecuting(context); // Do Action!!
                                return;
                            }
                        }
                    }
                    #endregion
                    //context.Result = new UnauthorizedObjectResult("User is unauthorized");
                    context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                    return;
                }
                else if (isAccountSessionExist == true) // 로그인이 된 상태라면
                {
                    if (currentControllerName == "Exception" && currentActionName == "Error") // 예외처리 자원 접근 허용 (Error)
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "Account" && currentActionName == "Logout") // 로그아웃
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }
                    else if (currentControllerName == "DashBoard" && currentActionName == "AnonymousIndex") // 초기 화면
                    {
                        base.OnActionExecuting(context); // Do Action!!
                        return;
                    }

                    _ = context.HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);
                    Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session

                    if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                    {
                        IEnumerable<Category> adminCategories = DbCache.AdminCategories;
                        IEnumerable<SubCategory> adminSubCategories = DbCache.AdminSubCategories;

                        #region SubCategory가 없는 Category일 경우
                        IEnumerable<Category> singleCategories = from adminCategory in adminCategories
                                                                 where !string.IsNullOrEmpty(adminCategory.Action)
                                                                 select adminCategory; // 서브 카테고리 없는 카테고리 모음.

                        foreach (Category singleCategory in singleCategories)
                        {
                            if (singleCategory.Controller == currentControllerName && singleCategory.Action == currentActionName)
                            {
                                base.OnActionExecuting(context); // Do Action!!
                                return;
                            }
                        }
                        #endregion

                        #region SubCategory가 있는 Category일 경우
                        IEnumerable<Category> nonSingleCategories = from adminCategory in adminCategories
                                                                    where string.IsNullOrEmpty(adminCategory.Action)
                                                                    select adminCategory; // 서브 카테고리 있는 카테고리 모음.

                        foreach (Category nonSingleCategory in nonSingleCategories)
                        {
                            IEnumerable<SubCategory> nonSingleCategorysSubCategories = from adminSubCategory in adminSubCategories
                                                                                       where adminSubCategory.CategoryId == nonSingleCategory.Id
                                                                                       select adminSubCategory;
                            foreach (SubCategory nonSingleCategorysSubCategory in nonSingleCategorysSubCategories)
                            {
                                if (nonSingleCategory.Controller == currentControllerName && nonSingleCategorysSubCategory.Action == currentActionName)
                                {
                                    base.OnActionExecuting(context); // Do Action!!
                                    return;
                                }
                            }
                        }
                        #endregion
                        //context.Result = new UnauthorizedObjectResult("User is unauthorized"); // 다 검사했는데도 나오지 않은 경우는 DB에 없는 경우이다.
                        context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                        return;
                        //DB Category & SubCategory 다 뽑아 현재 Access한 Controller & Action과 다 맞는지 비교하고 맞으면 넘어가고 아니면 UnAuthroizedObjectReslut 출력 (Action 취소!!)
                    }
                    else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                    {
                        IEnumerable<Category> userCategories = DbCache.UserCategories;
                        IEnumerable<SubCategory> userSubCategories = DbCache.UserSubCategories;

                        #region SubCategory가 없는 Category일 경우
                        IEnumerable<Category> singleCategories = from userCategory in userCategories
                                                                 where !string.IsNullOrEmpty(userCategory.Action)
                                                                 select userCategory; // 서브 카테고리 없는 카테고리 모음.

                        foreach (Category singleCategory in singleCategories)
                        {
                            if (singleCategory.Controller == currentControllerName && singleCategory.Action == currentActionName)
                            {
                                base.OnActionExecuting(context); // Do Action!!
                                return;
                            }
                        }
                        #endregion

                        #region SubCategory가 있는 Category일 경우
                        IEnumerable<Category> nonSingleCategories = from userCategory in userCategories
                                                                    where string.IsNullOrEmpty(userCategory.Action)
                                                                    select userCategory; // 서브 카테고리 있는 카테고리 모음.

                        foreach (Category nonSingleCategory in nonSingleCategories)
                        {
                            IEnumerable<SubCategory> nonSingleCategorysSubCategories = from userSubCategory in userSubCategories
                                                                                       where userSubCategory.CategoryId == nonSingleCategory.Id
                                                                                       select userSubCategory;
                            foreach (SubCategory nonSingleCategorysSubCategory in nonSingleCategorysSubCategories)
                            {
                                if (nonSingleCategory.Controller == currentControllerName && nonSingleCategorysSubCategory.Action == currentActionName)
                                {
                                    base.OnActionExecuting(context); // Do Action!!
                                    return;
                                }
                            }
                        }
                        #endregion
                        //context.Result = new UnauthorizedObjectResult("User is unauthorized"); // 다 검사했는데도 나오지 않은 경우는 DB에 없는 경우이다.
                        context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                        return;
                        //DB Category & SubCategory 다 뽑아 현재 Access한 Controller & Action과 다 맞는지 비교하고 맞으면 넘어가고 아니면 UnAuthroizedObjectReslut 출력 (Action 취소!!)
                    }
                }
            }
            else if (currentRequestMethod.ToUpper() == "POST") // POST 방식
            {
                IEnumerable<Type> existingControllers = Assembly.GetExecutingAssembly().GetTypes()
                    .Where(type => typeof(Controller).IsAssignableFrom(type)); //filter controllers

                if (isAccountSessionExist == false) // 로그인이 안 된 상태이면
                {
                    IEnumerable<MethodInfo> existingHttpPostMethods = existingControllers
                    .Where(type => type.Name == "AccountController") // AccountController 체크
                    .SelectMany(type => type.GetMethods()) // Method 들을 구한다
                    .Where(method => method.IsPublic && !method.IsDefined(typeof(NonActionAttribute)) && method.IsDefined(typeof(HttpPostAttribute)) && method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute))); // Method에서 접근 제한자가 Public이고 Attribute가 NonAction으로 되지 않고, HttpPost 및 ValidateAntiForgeryToken으로 된 메소드들을 구한다.

                    foreach (MethodInfo existingHttpPostMethod in existingHttpPostMethods)
                    {
                        if (currentControllerName == existingHttpPostMethod.DeclaringType.Name.Replace("Controller", "") && currentActionName == existingHttpPostMethod.Name) // (POST로 보낸) 현 Controller 명과 Action 명이 Account Controller의 NonAction Attribute가 없고 HttpPost & ValidateAntiForgeryToken Attribute를 가진 Method(Action) 명과 같다면
                        {
                            base.OnActionExecuting(context); // Do Action!!
                            return;
                        }
                    }

                    IEnumerable<Category> anonymousCategories = DbCache.AnonymousCategories;
                    foreach (Category anonymousCategory in anonymousCategories)
                    {
                        existingHttpPostMethods = existingControllers
                        .Where(type => type.Name == $"{anonymousCategory.Controller}Controller") // 권한 AnonymousCategory일 때 등록 된 Category의 Controller 이름만 접근 가능
                        .SelectMany(type => type.GetMethods()) // Method 들을 구한다
                        .Where(method => method.IsPublic && !method.IsDefined(typeof(NonActionAttribute)) && method.IsDefined(typeof(HttpPostAttribute)) && method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)) && method.IsDefined(typeof(RequiredHttpPostAccessAttribute))); // Method에서 접근 제한자가 Public이고 Attribute가 NonAction으로 되지 않고, HttpPost 및 ValidateAntiForgeryToken가 있고 RequiredHttpPostAccess로 된 메소드들을 구한다.

                        foreach (MethodInfo existingHttpPostMethod in existingHttpPostMethods)
                        {
                            if (currentControllerName == existingHttpPostMethod.DeclaringType.Name.Replace("Controller", "") && currentActionName == existingHttpPostMethod.Name) // (POST로 보낸) 현 Controller 명과 Action 명이 Account Controller의 NonAction Attribute가 없고 HttpPost & ValidateAntiForgeryToken Attribute를 가진 Method(Action) 명과 같다면
                            {
                                foreach (CustomAttributeData customAttribute in existingHttpPostMethod.CustomAttributes)
                                {
                                    if (customAttribute.AttributeType.FullName == typeof(RequiredHttpPostAccessAttribute).ToString())
                                    {
                                        foreach (CustomAttributeNamedArgument namedArgument in customAttribute.NamedArguments)
                                        {
                                            if ((namedArgument.MemberName == nameof(RequiredHttpPostAccessAttribute.Role)) && ((namedArgument.TypedValue.Value as string) == Role.Anonymous))
                                            {
                                                base.OnActionExecuting(context); // Do Action!!
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //context.Result = new UnauthorizedObjectResult("Access is not allowed"); // 다 검사 했는데도 없으면 실행 불가!
                    context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                    return;
                }
                else if (isAccountSessionExist == true) // 로그인이 된 상태라면
                {
                    _ = context.HttpContext.Session.TryGetValue(Maroik.Common.Miscellaneous.Extensions.EnumHelper.GetDescription(Maroik.Common.Miscellaneous.Utilities.Session.Account), out byte[] resultByte);
                    Account loginedAccount = JsonConvert.DeserializeObject<Account>(Encoding.Default.GetString(resultByte)); // Get Session

                    if (loginedAccount.Role == Role.Admin) // 로그인한 사용자 권한이 Admin일 경우
                    {
                        IEnumerable<Category> adminCategories = DbCache.AdminCategories;
                        foreach (Category adminCategory in adminCategories)
                        {
                            IEnumerable<MethodInfo> existingHttpPostMethods = existingControllers
                            .Where(type => type.Name == $"{adminCategory.Controller}Controller") // 권한 Admin일 때 등록 된 Category의 Controller 이름만 접근 가능
                            .SelectMany(type => type.GetMethods()) // Method 들을 구한다
                            .Where(method => method.IsPublic && !method.IsDefined(typeof(NonActionAttribute)) && method.IsDefined(typeof(HttpPostAttribute)) && method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)) && method.IsDefined(typeof(RequiredHttpPostAccessAttribute))); // Method에서 접근 제한자가 Public이고 Attribute가 NonAction으로 되지 않고, HttpPost 및 ValidateAntiForgeryToken가 있고 RequiredHttpPostAccess로 된 메소드들을 구한다.

                            foreach (MethodInfo existingHttpPostMethod in existingHttpPostMethods)
                            {
                                if (currentControllerName == existingHttpPostMethod.DeclaringType.Name.Replace("Controller", "") && currentActionName == existingHttpPostMethod.Name) // (POST로 보낸) 현 Controller 명과 Action 명이 Account Controller의 NonAction Attribute가 없고 HttpPost & ValidateAntiForgeryToken Attribute를 가진 Method(Action) 명과 같다면
                                {
                                    foreach (CustomAttributeData customAttribute in existingHttpPostMethod.CustomAttributes)
                                    {
                                        if (customAttribute.AttributeType.FullName == typeof(RequiredHttpPostAccessAttribute).ToString())
                                        {
                                            foreach (CustomAttributeNamedArgument namedArgument in customAttribute.NamedArguments)
                                            {
                                                if ((namedArgument.MemberName == nameof(RequiredHttpPostAccessAttribute.Role)) && ((namedArgument.TypedValue.Value as string) == loginedAccount.Role))
                                                {
                                                    base.OnActionExecuting(context); // Do Action!!
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        //context.Result = new UnauthorizedObjectResult("Access is not allowed"); // 다 검사 했는데도 없으면 실행 불가!
                        context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                        return;
                    }
                    else if (loginedAccount.Role == Role.User) // 로그인한 사용자 권한이 User일 경우
                    {
                        IEnumerable<Category> userCategories = DbCache.UserCategories;
                        foreach (Category userCategory in userCategories)
                        {
                            IEnumerable<MethodInfo> existingHttpPostMethods = existingControllers
                            .Where(type => type.Name == $"{userCategory.Controller}Controller") // 권한 User일 때 등록 된 Category의 Controller 이름만 접근 가능
                            .SelectMany(type => type.GetMethods()) // Method 들을 구한다
                            .Where(method => method.IsPublic && !method.IsDefined(typeof(NonActionAttribute)) && method.IsDefined(typeof(HttpPostAttribute)) && method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)) && method.IsDefined(typeof(RequiredHttpPostAccessAttribute))); // Method에서 접근 제한자가 Public이고 Attribute가 NonAction으로 되지 않고, HttpPost 및 ValidateAntiForgeryToken가 있고 RequiredHttpPostAccess로 된 메소드들을 구한다.

                            foreach (MethodInfo existingHttpPostMethod in existingHttpPostMethods)
                            {
                                if (currentControllerName == existingHttpPostMethod.DeclaringType.Name.Replace("Controller", "") && currentActionName == existingHttpPostMethod.Name) // (POST로 보낸) 현 Controller 명과 Action 명이 Account Controller의 NonAction Attribute가 없고 HttpPost & ValidateAntiForgeryToken Attribute를 가진 Method(Action) 명과 같다면
                                {
                                    foreach (CustomAttributeData customAttribute in existingHttpPostMethod.CustomAttributes)
                                    {
                                        if (customAttribute.AttributeType.FullName == typeof(RequiredHttpPostAccessAttribute).ToString())
                                        {
                                            foreach (CustomAttributeNamedArgument namedArgument in customAttribute.NamedArguments)
                                            {
                                                if ((namedArgument.MemberName == nameof(RequiredHttpPostAccessAttribute.Role)) && ((namedArgument.TypedValue.Value as string) == loginedAccount.Role))
                                                {
                                                    base.OnActionExecuting(context); // Do Action!!
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        //context.Result = new UnauthorizedObjectResult("Access is not allowed"); // 다 검사 했는데도 없으면 실행 불가!
                        context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                        return;
                    }
                }
            }
            else // (PATCH / DELETE 등 방식은 다 거부)
            {
                //base.OnActionExecuting(context); // Do Action!!
                //context.Result = new UnauthorizedObjectResult("Access is not allowed [Only Allowed GET/POST Method]");
                context.Result = new RedirectResult("/DashBoard/AnonymousIndex");
                return;
            }
            // Do something before the action executes.
            //        ...
            //if (needToRedirect)
            //        {
            //            ...
            //   filterContext.Result = new RedirectResult(url);
            //            return;
            //        }
            //        ...

            //var controllerName = routeData.Values["controller"];
            //var actionName = routeData.Values["action"];
            //var message = String.Format("{0} controller:{1} action:{2}", methodName, controllerName, actionName);
            //Debug.WriteLine(message, "Action Filter Log");

            //string actionName = context.ActionDescriptor.ac;
            //string controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName
            //base.OnActionExecuting(filterContext);
        }

        // Do something after the action executes.
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            // Do something after the action executes.

        }
    }
}