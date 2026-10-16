using System.Diagnostics;
using System.Reflection;

namespace RefreshVIR
{
    internal static class AppVersion
    {
        internal static string GetDisplayString()
        {
            string? clickOnceVersion = Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion");
            if (!string.IsNullOrWhiteSpace(clickOnceVersion))
                return Format(clickOnceVersion);

            Assembly assembly = Assembly.GetExecutingAssembly();

            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return Format(StripBuildMetadata(informational));

            string? fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
                return Format(fileVersion);

            Version? version = assembly.GetName().Version;
            return version == null ? "v1.0.0" : Format(version.ToString());
        }

        private static string Format(string version)
        {
            version = version.Trim();
            return version.StartsWith('v') || version.StartsWith('V')
                ? version
                : $"v{version}";
        }

        private static string StripBuildMetadata(string version)
        {
            int plusIndex = version.IndexOf('+');
            return plusIndex >= 0 ? version[..plusIndex] : version;
        }
    }
}
