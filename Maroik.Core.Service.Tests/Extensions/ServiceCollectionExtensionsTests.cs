using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Maroik.Core.Service.Tests.Extensions;

/// <summary>
/// Tests of <see cref="ServiceCollectionExtensions.AddApplicationServices"/>: every application-layer contract is
/// registered to its implementation with the intended lifetime — per-request services scoped, stateless or
/// key-holding helpers singleton — and the clock is the system one unless the host already registered another.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>The registrations <see cref="ServiceCollectionExtensions.AddApplicationServices"/> must make.</summary>
    public static TheoryData<Type, Type, ServiceLifetime> Registrations => new()
    {
        { typeof(IPasswordService), typeof(PasswordService), ServiceLifetime.Scoped },
        { typeof(IAccountService), typeof(AccountService), ServiceLifetime.Scoped },
        { typeof(IAccountMailStatusService), typeof(AccountMailStatusService), ServiceLifetime.Scoped },
        { typeof(IAssetService), typeof(AssetService), ServiceLifetime.Scoped },
        { typeof(IAssetBalanceStore), typeof(AssetBalanceStore), ServiceLifetime.Scoped },
        { typeof(IIncomeService), typeof(IncomeService), ServiceLifetime.Scoped },
        { typeof(IExpenditureService), typeof(ExpenditureService), ServiceLifetime.Scoped },
        { typeof(IFixedIncomeService), typeof(FixedIncomeService), ServiceLifetime.Scoped },
        { typeof(IFixedExpenditureService), typeof(FixedExpenditureService), ServiceLifetime.Scoped },
        { typeof(IDashboardService), typeof(DashboardService), ServiceLifetime.Scoped },
        { typeof(IProfileService), typeof(ProfileService), ServiceLifetime.Scoped },
        { typeof(IManagementAccountService), typeof(ManagementAccountService), ServiceLifetime.Scoped },
        { typeof(IMenuService), typeof(MenuService), ServiceLifetime.Scoped },
        { typeof(IBoardService), typeof(BoardService), ServiceLifetime.Scoped },
        { typeof(ICalendarService), typeof(CalendarService), ServiceLifetime.Scoped },
        { typeof(IAttachmentContentService), typeof(AttachmentContentService), ServiceLifetime.Scoped },
        { typeof(IRsaService), typeof(RsaService), ServiceLifetime.Singleton },
        { typeof(IImageValidatorService), typeof(ImageValidatorService), ServiceLifetime.Singleton },
        { typeof(IHtmlContentSanitizerService), typeof(HtmlContentSanitizerService), ServiceLifetime.Singleton },
        { typeof(IHtmlParserService), typeof(HtmlParserService), ServiceLifetime.Singleton },
        { typeof(ITimeZoneCatalogService), typeof(TimeZoneCatalogService), ServiceLifetime.Singleton },
    };

    /// <summary>Each contract is registered once, to its implementation, with its lifetime.</summary>
    [Theory]
    [MemberData(nameof(Registrations))]
    public void AddApplicationServices_RegistersTheContract(Type contract, Type implementation, ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();

        ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == contract);
        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    /// <summary>Without a clock from the host, the system clock is registered as a singleton.</summary>
    [Fact]
    public void AddApplicationServices_RegistersTheSystemClock()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();

        ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == typeof(TimeProvider));
        Assert.Same(TimeProvider.System, descriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>A clock the host registered first (e.g. a test's fake clock) is kept.</summary>
    [Fact]
    public void AddApplicationServices_KeepsAClockTheHostAlreadyRegistered()
    {
        var services = new ServiceCollection();
        var fake = new FakeTimeProvider();
        services.AddSingleton<TimeProvider>(fake);

        services.AddApplicationServices();

        ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == typeof(TimeProvider));
        Assert.Same(fake, descriptor.ImplementationInstance);
    }
}
