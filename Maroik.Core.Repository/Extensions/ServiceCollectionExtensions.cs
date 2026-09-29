using Maroik.Core.Contract.Interfaces;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Core.Repository.Extensions;

/// <summary>
/// Extension methods for registering the EF Core database context and all repository
/// implementations into the DI container.
/// Uses the C# 14 "extension" block syntax to add methods directly to <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures the PostgreSQL <see cref="ApplicationDbContext"/> using the
        /// "DefaultConnection" connection string from <paramref name="configuration"/>.
        /// Enables Npgsql's legacy timestamp behavior: a <see cref="DateTime"/> of any
        /// <see cref="DateTime.Kind"/> maps to <c>timestamp without time zone</c> and is read back as
        /// <see cref="DateTimeKind.Unspecified"/> (the application stores UTC values there).
        /// Retry-on-failure is disabled (count = 0) — transient errors are expected to be handled
        /// by the application tier.
        /// </summary>
        /// <param name="configuration">
        /// The application configuration that contains the "DefaultConnection" connection string.
        /// </param>
        public void AddRepositoryContext(IConfiguration configuration)
        {
            // Tell Npgsql to write DateTime values of any Kind to "timestamp without time zone" (no
            // UTC-offset metadata, no Kind check), preserving the behavior of older Npgsql versions.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    // Disable automatic retry so transient failures surface immediately to the caller.
                    npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(0)
                )//.EnableSensitiveDataLogging()
            );
        }

        /// <summary>
        /// Registers all repository and unit-of-work implementations as scoped services.
        /// Each repository is bound to its corresponding interface defined in Maroik.Core.Contract.
        /// </summary>
        public void AddRepositoryServices()
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
            services.AddScoped<IAssetRepository, AssetRepository>();
            services.AddScoped<IIncomeRepository, IncomeRepository>();
            services.AddScoped<IExpenditureRepository, ExpenditureRepository>();
            services.AddScoped<IFixedIncomeRepository, FixedIncomeRepository>();
            services.AddScoped<IFixedExpenditureRepository, FixedExpenditureRepository>();
            services.AddScoped<IBoardRepository, BoardRepository>();
            services.AddScoped<IBoardAttachedFileRepository, BoardAttachedFileRepository>();
            services.AddScoped<IBoardCommentRepository, BoardCommentRepository>();
            services.AddScoped<ICalendarRepository, CalendarRepository>();
            services.AddScoped<IOtherCalendarRepository, OtherCalendarRepository>();
            services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
            services.AddScoped<ICalendarEventAttachedFileRepository, CalendarEventAttachedFileRepository>();
            services.AddScoped<ICalendarEventReminderRepository, CalendarEventReminderRepository>();
            services.AddScoped<ICalendarSharedRepository, CalendarSharedRepository>();
        }
    }
}
