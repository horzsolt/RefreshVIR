namespace RefreshVIR
{
    internal sealed class DetailedApplicationException : Exception
    {
        public DetailedApplicationException(
            string message,
            IReadOnlyDictionary<string, string>? context = null,
            string? responseBody = null,
            int? httpStatusCode = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Context = context ?? new Dictionary<string, string>();
            ResponseBody = responseBody;
            HttpStatusCode = httpStatusCode;
        }

        public IReadOnlyDictionary<string, string> Context { get; }

        public string? ResponseBody { get; }

        public int? HttpStatusCode { get; }
    }
}
