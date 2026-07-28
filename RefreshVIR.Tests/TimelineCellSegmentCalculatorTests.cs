namespace RefreshVIR.Tests;

public class TimelineCellSegmentCalculatorTests
{
    [Fact]
    public void GetBarFractions_FiveMinuteRunWithinHour_ShowsShortBar()
    {
        DateTime slotStart = new DateTime(2026, 7, 20, 14, 0, 0);
        DateTime slotEnd = slotStart.AddHours(1);
        DateTime runStart = new DateTime(2026, 7, 20, 14, 5, 0);
        DateTime runFinish = new DateTime(2026, 7, 20, 14, 10, 0);

        (double startFraction, double widthFraction) = TimelineCellSegmentCalculator.GetBarFractions(
            slotStart,
            slotEnd,
            runStart,
            runFinish);

        Assert.Equal(5.0 / 60.0, startFraction, 3);
        Assert.Equal(5.0 / 60.0, widthFraction, 3);
    }

    [Fact]
    public void GetBarFractions_RunSpanningTwoHours_IsClampedPerSlot()
    {
        DateTime firstSlotStart = new DateTime(2026, 7, 20, 14, 0, 0);
        DateTime firstSlotEnd = firstSlotStart.AddHours(1);
        DateTime runStart = new DateTime(2026, 7, 20, 14, 45, 0);
        DateTime runFinish = new DateTime(2026, 7, 20, 15, 15, 0);

        (double firstStart, double firstWidth) = TimelineCellSegmentCalculator.GetBarFractions(
            firstSlotStart,
            firstSlotEnd,
            runStart,
            runFinish);

        DateTime secondSlotStart = firstSlotEnd;
        DateTime secondSlotEnd = secondSlotStart.AddHours(1);

        (double secondStart, double secondWidth) = TimelineCellSegmentCalculator.GetBarFractions(
            secondSlotStart,
            secondSlotEnd,
            runStart,
            runFinish);

        Assert.Equal(45.0 / 60.0, firstStart, 3);
        Assert.Equal(15.0 / 60.0, firstWidth, 3);
        Assert.Equal(0, secondStart, 3);
        Assert.Equal(15.0 / 60.0, secondWidth, 3);
    }

    [Fact]
    public void BuildSegments_UsesVisibleOverlapInsideSlot()
    {
        DateTime slotStart = new DateTime(2026, 7, 20, 9, 0, 0);
        DateTime slotEnd = slotStart.AddHours(1);

        List<TimelineCellExecutionSegment> segments = TimelineCellSegmentCalculator.BuildSegments(
            slotStart,
            slotEnd,
            new[]
            {
                new JobExecution
                {
                    JobName = "Refresh_Scriptor_1",
                    StartTime = new DateTime(2026, 7, 20, 9, 2, 0),
                    FinishTime = new DateTime(2026, 7, 20, 9, 6, 0),
                    RunStatus = 1
                }
            });

        Assert.Single(segments);
        Assert.Equal(new DateTime(2026, 7, 20, 9, 2, 0), segments[0].VisibleStart);
        Assert.Equal(new DateTime(2026, 7, 20, 9, 6, 0), segments[0].VisibleFinish);
    }
}
