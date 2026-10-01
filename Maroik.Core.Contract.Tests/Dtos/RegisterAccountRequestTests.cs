using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Tests.Dtos;

/// <summary>
/// Pins the shape of <see cref="RegisterAccountRequest"/>: the self-registration DTO must never
/// carry a field that grants privileges or admin-only account state, so a form bound straight to
/// it cannot escalate.
/// </summary>
public class RegisterAccountRequestTests
{
    /// <summary>Self-registration exposes only what a visitor may choose for their own account.</summary>
    [Fact]
    public void RegisterAccountRequest_ExposesOnlySelfServiceFields()
    {
        string[] properties = [.. typeof(RegisterAccountRequest).GetProperties().Select(p => p.Name).Order()];

        Assert.Equal(["AgreedServiceTerms", "Email", "Nickname", "PlainPassword", "TimeZoneIanaId"], properties);
    }
}
