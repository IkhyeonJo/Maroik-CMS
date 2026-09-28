using System.Collections.Concurrent;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Messaging;

namespace Maroik.Website.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for <see cref="IEmailPublisher"/>, registered by
/// <see cref="MaroikWebApplicationFactory"/> in place of the real RabbitMQ-backed
/// implementation (which needs a live broker) so that <c>AccountService</c> flows that publish
/// an email (Register, ForgotPassword, ...) can be exercised for real over HTTP without one.
/// <c>RabbitMqEmailPublisher</c> itself already has dedicated unit tests in
/// <c>Maroik.Core.Client.Tests</c> — this fake exists purely so Website-level tests can assert
/// an email *would have been sent* (recipient/subject/body), not to re-verify queue delivery.
/// </summary>
public sealed class FakeEmailPublisher : IEmailPublisher
{
    private readonly ConcurrentBag<SendEmailMessage> _published = [];

    /// <summary>All messages published so far, across every test that shares this factory instance.</summary>
    public IReadOnlyCollection<SendEmailMessage> PublishedMessages => _published;

    /// <summary>Publish async.</summary>
    public Task PublishAsync(SendEmailMessage message, CancellationToken ct = default)
    {
        _published.Add(message);
        return Task.CompletedTask;
    }
}
