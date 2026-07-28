namespace RefreshVIR.Tests;

public class ErrorOccurrenceTrackerTests
{
    [Fact]
    public void Register_ShowsFirstOccurrenceOnlyAndCountsRepeats()
    {
        ErrorOccurrenceTracker.Reset();

        bool first = ErrorOccurrenceTracker.Register("same-error", out int firstCount);
        bool second = ErrorOccurrenceTracker.Register("same-error", out int secondCount);
        bool third = ErrorOccurrenceTracker.Register("same-error", out int thirdCount);

        Assert.True(first);
        Assert.False(second);
        Assert.False(third);
        Assert.Equal(1, firstCount);
        Assert.Equal(2, secondCount);
        Assert.Equal(3, thirdCount);
    }
}
