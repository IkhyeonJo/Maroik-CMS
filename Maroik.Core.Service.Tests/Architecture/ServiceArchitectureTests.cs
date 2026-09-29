using System.Reflection;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Services;
using NetArchTest.Rules;

namespace Maroik.Core.Service.Tests.Architecture;

/// <summary>
/// Architecture test enforcing that Maroik.Core.Service does not reach past
/// Maroik.Core.Contract into any infrastructure layer or outer application, and stays a sibling
/// of — not a dependent of — every other layer that itself depends on Service.
/// </summary>
public class ServiceArchitectureTests
{
    /// <summary>The assembly containing <c>AccountService</c>, referenced by these architecture rules.</summary>
    private static readonly Assembly _serviceAssembly = typeof(AccountService).Assembly;

    /// <summary>Service should not depend on any infrastructure layer or outer application.</summary>
    [Theory]
    [InlineData("Maroik.Core.Client")]
    [InlineData("Maroik.Core.Repository")]
    [InlineData("Maroik.Core.PostgreSQL")]
    [InlineData("Maroik.Website")]
    [InlineData("Maroik.Worker")]
    [InlineData("Maroik.FileStorage")]
    public void Service_ShouldNot_DependOnInfrastructureOrOuterLayer(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_serviceAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Service must not reference {forbiddenNamespace} directly. " +
            "Depend on abstractions in Maroik.Core.Contract instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Third-party libraries that a service is allowed to use in-process for pure computation
    /// (hashing, HTML sanitizing/parsing, image inspection) — but never for talking to an external
    /// <em>system</em>. Reaching a database, an SMTP server or a message broker is infrastructure
    /// work: it happens in Maroik.Core.Repository / Maroik.Core.Client behind a Contract interface,
    /// so the service layer must not reference EF Core, the Postgres driver, the mail client or the
    /// RabbitMQ client at all.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("MailKit")]
    [InlineData("MimeKit")]
    [InlineData("RabbitMQ.Client")]
    public void Service_ShouldNot_DependOn_ExternalSystemClientLibrary(string forbiddenNamespace)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_serviceAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Maroik.Core.Service must not reference {forbiddenNamespace}. Talking to an external " +
            "system (database, SMTP, message broker) is infrastructure work — do it in " +
            "Maroik.Core.Repository / Maroik.Core.Client behind a Maroik.Core.Contract interface " +
            "and inject that interface into the service.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// <see cref="BoardComment"/>, <see cref="BoardAttachedFile"/>, <see cref="CalendarEventReminder"/>,
    /// <see cref="CalendarEventAttachedFile"/>, <see cref="CalendarShared"/> and
    /// <see cref="OtherCalendar"/> are persisted independently through their own repositories, but
    /// each one still sits inside the consistency boundary of a parent aggregate (Board or Calendar)
    /// and must only be written after that parent's own domain methods have had their say. Their
    /// repositories expose Create/UpdateEntityAsync via IGenericRepository, the same shape as a
    /// top-level aggregate repository, so nothing in the type system stops another service from
    /// injecting one and persisting a mutation that skipped the parent. This test is the guard rail
    /// instead: only the service that owns the parent aggregate may reference each repository's
    /// interface at all.
    /// </summary>
    [Theory]
    [InlineData("BoardService", "IBoardCommentRepository")]
    [InlineData("BoardService", "IBoardAttachedFileRepository")]
    [InlineData("CalendarService", "ICalendarEventReminderRepository")]
    [InlineData("CalendarService", "ICalendarEventAttachedFileRepository")]
    [InlineData("CalendarService", "ICalendarSharedRepository")]
    [InlineData("CalendarService", "IOtherCalendarRepository")]
    public void OnlyOwningAggregateService_ShouldDependOn_ChildEntityRepository(string owningServiceName, string childRepositoryInterfaceName)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(_serviceAssembly)
            .That()
            .DoNotHaveName(owningServiceName)
            .ShouldNot()
            .HaveDependencyOn(childRepositoryInterfaceName)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Only {owningServiceName} may depend on {childRepositoryInterfaceName} — it persists a " +
            "child entity that must only be mutated after going through its aggregate root's own " +
            "domain methods. Route the operation through the aggregate root's service instead.\n" +
            "Failing types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
