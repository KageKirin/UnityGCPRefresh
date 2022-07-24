using System;
using System.IO;

namespace KageKirin.GCPRefresh
{
    internal static class GCPRefreshConstants
    {
#if UNITY_EDITOR_WIN
        public const string GcloudExe = "gcloud.exe";
#else
        public const string GcloudExe = "gcloud";
#endif // UNITY_EDITOR_WIN

        public static string UpmConfigTomlPath =>
            Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".upmconfig.toml"
            );
    }
}
