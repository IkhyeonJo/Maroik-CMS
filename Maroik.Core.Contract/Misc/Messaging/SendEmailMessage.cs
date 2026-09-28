namespace Maroik.Core.Contract.Misc.Messaging;

/// <summary>
/// Queue message carrying a fully-built email ready to be sent by a consumer.
/// Published by <see cref="Interfaces.IEmailPublisher"/> and consumed by Maroik.Worker.
/// </summary>
/// <param name="CorrelationId">
/// The originating request's ambient <see cref="System.Diagnostics.Activity.Current"/> id,
/// carried across the queue so Worker-side logs can be traced back to the Website request
/// that triggered them. Empty for messages published before this field existed.
/// </param>
public sealed record SendEmailMessage(string ToEmail, string Subject, string Body, string CorrelationId = "");
