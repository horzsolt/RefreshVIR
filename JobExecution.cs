
namespace RefreshVIR
{
    public class JobExecution
    {
        public string JobName { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime FinishTime { get; set; }

        public int RunStatus { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
