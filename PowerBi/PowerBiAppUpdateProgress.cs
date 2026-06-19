namespace RefreshVIR
{
    internal sealed class AppUpdateProgressReport
    {
        public required string Message { get; init; }
        public int Percent { get; init; }
    }

    internal sealed class AppUpdateProgressTracker
    {
        private readonly IProgress<AppUpdateProgressReport>? _progress;
        private readonly int[] _phaseWeights = { 5, 5, 15, 10, 30, 5, 25, 5 };
        private int _phaseIndex;
        private int _phasePercent;

        internal AppUpdateProgressTracker(IProgress<AppUpdateProgressReport>? progress) =>
            _progress = progress;

        internal void ReportPhaseStart(string message)
        {
            _phasePercent = 0;
            Report(message);
        }

        internal void ReportPhaseComplete(string message) =>
            Report(message, 100);

        internal void ReportSubStep(string message, int completed, int total)
        {
            int percent = total <= 0
                ? 0
                : Math.Clamp(completed * 100 / total, 0, 100);
            Report(message, percent);
        }

        internal void Report(string message, int phasePercent = -1)
        {
            if (phasePercent >= 0)
                _phasePercent = Math.Clamp(phasePercent, 0, 100);

            _progress?.Report(new AppUpdateProgressReport
            {
                Message = message,
                Percent = CalculateOverallPercent()
            });
        }

        internal void AdvancePhase()
        {
            if (_phaseIndex < _phaseWeights.Length - 1)
                _phaseIndex++;
            _phasePercent = 0;
        }

        private int CalculateOverallPercent()
        {
            int completedWeight = 0;
            for (int i = 0; i < _phaseIndex; i++)
                completedWeight += _phaseWeights[i];

            int currentWeight = _phaseWeights[Math.Min(_phaseIndex, _phaseWeights.Length - 1)];
            int currentContribution = currentWeight * _phasePercent / 100;
            int totalWeight = _phaseWeights.Sum();

            return Math.Clamp((completedWeight + currentContribution) * 100 / totalWeight, 0, 100);
        }
    }
}
