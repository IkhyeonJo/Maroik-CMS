using Maroik.Core.Domain.Account;

namespace Maroik.Core.Domain.Tests.Account;

/// <summary>Tests of <see cref="LoginThrottlePolicy"/>: the wait grows by stage and is capped, never permanent.</summary>
public class LoginThrottlePolicyTests
{
    /// <summary>
    /// With a step of 3: no wait for the first two failures, then 1 minute from the 3rd, 5 from the 6th, 15 from the
    /// 9th and 60 from the 12th on — the cap, however many more failures follow.
    /// </summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(2, null)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 5)]
    [InlineData(8, 5)]
    [InlineData(9, 15)]
    [InlineData(11, 15)]
    [InlineData(12, 60)]
    [InlineData(1_000, 60)]
    public void DelayAfter_GrowsByStage_AndIsCappedAtAnHour(long failedAttempts, int? expectedMinutes)
    {
        TimeSpan? delay = LoginThrottlePolicy.DelayAfter(failedAttempts, step: 3);

        Assert.Equal(expectedMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null, delay);
    }

    /// <summary>The stages follow the configured step: with 5, the first wait starts at the 5th failure.</summary>
    [Theory]
    [InlineData(4, null)]
    [InlineData(5, 1)]
    [InlineData(10, 5)]
    public void DelayAfter_FollowsTheConfiguredStep(long failedAttempts, int? expectedMinutes)
    {
        Assert.Equal(expectedMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            LoginThrottlePolicy.DelayAfter(failedAttempts, step: 5));
    }

    /// <summary>A step below one is treated as one, so a misconfiguration cannot disable the throttle or divide by zero.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void DelayAfter_TreatsAStepBelowOneAsOne(int step)
    {
        Assert.Equal(TimeSpan.FromMinutes(1), LoginThrottlePolicy.DelayAfter(1, step));
    }
}
