using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Core.Service.Extensions;

/// <summary>
/// Extension methods for registering all application-layer services into the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers all application service implementations as scoped (or singleton where appropriate)
        /// so they are available for constructor injection throughout the application.
        /// </summary>
        public void AddApplicationServices()
        {
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IAccountMailStatusService, AccountMailStatusService>();
            services.AddScoped<IAssetService, AssetService>();
            services.AddScoped<IAssetBalanceDomainService, AssetBalanceDomainService>();
            services.AddScoped<IIncomeService, IncomeService>();
            services.AddScoped<IExpenditureService, ExpenditureService>();
            services.AddScoped<IFixedIncomeService, FixedIncomeService>();
            services.AddScoped<IFixedExpenditureService, FixedExpenditureService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IManagementAccountService, ManagementAccountService>();
            services.AddScoped<IMenuService, MenuService>();
            services.AddScoped<IBoardService, BoardService>();
            services.AddScoped<ICalendarService, CalendarService>();
            services.AddScoped<IAttachmentContentService, AttachmentContentService>();

            services.AddSingleton<IRsaService, RsaService>();
            services.AddSingleton<IImageValidatorService, ImageValidatorService>();
            services.AddSingleton<IHtmlContentSanitizerService, HtmlContentSanitizerService>();
            services.AddSingleton<IHtmlParserService, HtmlParserService>();
            services.AddSingleton<ITimeZoneCatalogService, TimeZoneCatalogService>();
        }
    }
}
