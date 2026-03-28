using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefreshVIR
{
    public class Configuration
    {
        public static string connectionString = $"Server={Environment.GetEnvironmentVariable("VIR_SQL_SERVER_NAME")};" +
                          $"Database=qad;" +
                          $"User Id={Environment.GetEnvironmentVariable("VIR_SQL_USER")};" +
                          $"Password={Environment.GetEnvironmentVariable("VIR_SQL_PASSWORD")};" +
                          "Connection Timeout=500;Trust Server Certificate=true";

        public static Dictionary<string, string> jobs = new Dictionary<string, string>
            {
                { "QAD_GL_frissites", "Főkönyv teljes frissítés" },
                { "QAD_GL_INC_frissites", "Főkönyv növekményes frissítés" },
                { "QAD_VIR_2025_ejszakai_frissites", "QAD 2025 frissítés 1." },
                { "QAD_VIR_2025_ejszakai_frissites_2", "QAD 2025 frissítés 2." },
                { "QAD_VIR_2025_ejszakai_frissites_3", "QAD 2025 frissítés 3." },
                { "Refresh_Scriptor_1", "Scriptor frissítés 1." },
                { "Refresh_Scriptor_2", "Scriptor frissítés 2." },
                { "Refresh_Scriptor_3", "Scriptor frissítés 3." },
                { "Refresh_Scriptor_4", "Scriptor frissítés 4." },
                { "QAD_VIR_2025_ejszakai_frissites", "QAD 2025 frissítés 1." },
                { "QAD_VIR_2025_ejszakai_frissites_2", "QAD 2025 frissítés 2." },
                { "QAD_VIR_2025_ejszakai_frissites_3", "QAD 2025 frissítés 3." },
            };
    }
}
