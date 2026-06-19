namespace RefreshVIR
{
    internal sealed class PowerBiSession : IAsyncDisposable, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<Guid, string> _workspaceAccessEmails = new();
        private PowerBiPipelineResolver? _pipelineResolver;

        internal HttpClient HttpClient => _httpClient;

        internal PowerBiPipelineResolver Pipeline =>
            _pipelineResolver ??= new PowerBiPipelineResolver(_httpClient);

        internal PowerBiSession(HttpClient httpClient) =>
            _httpClient = httpClient;

        internal async Task<string> GetWorkspaceAccessEmailAsync(
            Guid workspaceId,
            string? workspaceName = null)
        {
            if (_workspaceAccessEmails.TryGetValue(workspaceId, out string cached))
                return cached;

            string accessEmail = await PowerBiGroupApi.GetWorkspaceAccessEmailAsync(
                _httpClient,
                workspaceId,
                workspaceName);

            _workspaceAccessEmails[workspaceId] = accessEmail;
            return accessEmail;
        }

        public void Dispose() =>
            _httpClient.Dispose();

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
