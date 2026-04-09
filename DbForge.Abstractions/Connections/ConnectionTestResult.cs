namespace DbForge.Abstractions.Connections
{
    public class ConnectionTestResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ServerVersion { get; set; }
        public long LatencyMs { get; set; }

        public static ConnectionTestResult Success ( string serverVersion, long latencyMs ) =>
            new() { IsSuccess = true, ServerVersion = serverVersion, LatencyMs = latencyMs };

        public static ConnectionTestResult Failure ( string error ) =>
            new() { IsSuccess = false, ErrorMessage = error };
    }
}
