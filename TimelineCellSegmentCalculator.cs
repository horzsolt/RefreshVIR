namespace RefreshVIR
{
    internal static class TimelineCellSegmentCalculator
    {
        internal const double MinWidthFraction = 0.02;

        internal static List<TimelineCellExecutionSegment> BuildSegments(
            DateTime slotStart,
            DateTime slotEnd,
            IEnumerable<JobExecution> overlappingRuns)
        {
            List<TimelineCellExecutionSegment> segments = new List<TimelineCellExecutionSegment>();

            foreach (JobExecution run in overlappingRuns)
            {
                DateTime visibleStart = run.StartTime < slotStart ? slotStart : run.StartTime;
                DateTime visibleFinish = run.FinishTime > slotEnd ? slotEnd : run.FinishTime;

                if (visibleFinish <= visibleStart)
                    continue;

                segments.Add(new TimelineCellExecutionSegment
                {
                    VisibleStart = visibleStart,
                    VisibleFinish = visibleFinish,
                    RunStatus = run.RunStatus
                });
            }

            return segments;
        }

        internal static (double StartFraction, double WidthFraction) GetBarFractions(
            DateTime slotStart,
            DateTime slotEnd,
            DateTime visibleStart,
            DateTime visibleFinish)
        {
            DateTime clampedStart = visibleStart < slotStart ? slotStart : visibleStart;
            DateTime clampedFinish = visibleFinish > slotEnd ? slotEnd : visibleFinish;

            if (clampedFinish <= clampedStart)
                return (0, 0);

            double slotSeconds = Math.Max(1, (slotEnd - slotStart).TotalSeconds);
            double startFraction = (clampedStart - slotStart).TotalSeconds / slotSeconds;
            double widthFraction = (clampedFinish - clampedStart).TotalSeconds / slotSeconds;

            startFraction = Math.Clamp(startFraction, 0, 1);
            widthFraction = Math.Clamp(widthFraction, 0, 1 - startFraction);

            if (widthFraction > 0 && widthFraction < MinWidthFraction)
                widthFraction = MinWidthFraction;

            return (startFraction, widthFraction);
        }
    }
}
