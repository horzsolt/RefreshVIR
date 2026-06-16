
namespace RefreshVIR
{
    public class Configuration
    {
        public static string connectionString =
            $"Server={Environment.GetEnvironmentVariable("VIR_SQL_SERVER_NAME")};" +
            $"Database=VIR;" +
            $"User Id={Environment.GetEnvironmentVariable("VIR_SQL_USER")};" +
            $"Password={Environment.GetEnvironmentVariable("VIR_SQL_PASSWORD")};" +
            "Connection Timeout=500;Trust Server Certificate=true";

        public const string DefaultPowerBiTenantId = "ba5e5692-d1f7-435a-b0c2-6cc74d8e102f";

        public static string PowerBiTenantId =
            Environment.GetEnvironmentVariable("VIR_POWERBI_TENANT_ID")
            ?? DefaultPowerBiTenantId;

        public static string PowerBiClientId =
            Environment.GetEnvironmentVariable("VIR_POWERBI_CLIENT_ID") ?? "";

        public static string PowerBiUser =
            Environment.GetEnvironmentVariable("VIR_POWERBI_USER") ?? "";

        public static string PowerBiPassword =
            Environment.GetEnvironmentVariable("VIR_POWERBI_PASSWORD") ?? "";

        public static string? PowerBiAppUpdatePipelineId =
            Environment.GetEnvironmentVariable("VIR_POWERBI_APP_UPDATE_PIPELINE_ID");

        public static bool IsPowerBiConfigured =>
            !string.IsNullOrWhiteSpace(PowerBiClientId)
            && !string.IsNullOrWhiteSpace(PowerBiUser)
            && !string.IsNullOrWhiteSpace(PowerBiPassword);

        public static string? GetPowerBiConfigurationError()
        {
            if (string.IsNullOrWhiteSpace(PowerBiClientId))
                return "A VIR_POWERBI_CLIENT_ID környezeti változó nincs beállítva.";

            if (string.IsNullOrWhiteSpace(PowerBiUser))
                return "A VIR_POWERBI_USER környezeti változó nincs beállítva.";

            if (string.IsNullOrWhiteSpace(PowerBiPassword))
                return "A VIR_POWERBI_PASSWORD környezeti változó nincs beállítva.";

            return null;
        }

        public static Dictionary<string, string> jobs = new Dictionary<string, string>
            {
                { "QAD_GL_hajnali_frissites", "Főkönyv teljes frissítés" },
                { "QAD_GL_INC_frissites", "Főkönyv növekményes frissítés" },
                { "QAD_VIR_2025_ejszakai_frissites", "QAD 2025 frissítés 1." },
                { "QAD_VIR_2025_ejszakai_frissites_2", "QAD 2025 frissítés 2." },
                { "QAD_VIR_2025_ejszakai_frissites_3", "QAD 2025 frissítés 3." },
                { "Refresh_Scriptor_1", "Scriptor frissítés 1." },
                { "Refresh_Scriptor_2", "Scriptor frissítés 2." },
                { "Refresh_Scriptor_3", "Scriptor frissítés 3." },
                { "Refresh_Scriptor_4", "Scriptor frissítés 4." },
                { "QAD_VIR_2026_frissites", "QAD 2026 frissítés 1." },
                { "QAD_VIR_2026_frissites_2", "QAD 2026 frissítés 2." },
                { "QAD_VIR_2026_frissites_3", "QAD 2026 frissítés 3." },
                { "QAD_VIR_ejszakai_frissites", "QAD VIR teljes, ejszakai frissítés 3." }
            };
    }
}
