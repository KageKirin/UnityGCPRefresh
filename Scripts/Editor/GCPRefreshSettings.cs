using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;
using Unity.EditorCoroutines.Editor;

#nullable enable

namespace KageKirin.GCPRefresh
{
    [FilePath("GCPRefresh.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class GCPRefreshSettings : ScriptableSingleton<GCPRefreshSettings>
    {
#region properties
        [SerializeField]
        [ContextMenuItem("Set from environment", "SetGcloudPathFromEnv")]
        public string m_gcloudPath = String.Empty;

        [SerializeField]
        public string m_gcloudRegistry = String.Empty;

        [SerializeField]
        [Tooltip("Refresh rate in minutes")]
        public int m_tokenRefreshRate = 40;
#endregion // properties


        public void OnValidate()
        {
            if (String.IsNullOrWhiteSpace(m_gcloudRegistry))
            {
                Debug.LogError($"gcloud registry is empty. this is not valid.");
                return;
            }

            m_tokenRefreshRate = (int)Mathf.Clamp(m_tokenRefreshRate, 1, 60);
        }

#region global utility functions
        private void SetGcloudPathFromEnv()
        {
            m_gcloudPath = LocateGcloudTool();
        }

        public static string LocateGcloudTool()
        {
            try
            {
                // look in $PATH
                var PATH =
                    Environment.GetEnvironmentVariable("PATH")
                    ?? Environment.GetEnvironmentVariable("Path");

                if (!String.IsNullOrEmpty(PATH))
                {
#if UNITY_EDITOR_WIN
                    var pathes = PATH.Split(";");
#else
                    var pathes = PATH.Split(":");
#endif // UNITY_EDITOR_WIN

                    foreach (var path in pathes)
                    {
                        var gcloudPath = Path.Join(path, GCPRefreshConstants.GcloudExe);
                        if (File.Exists(gcloudPath))
                        {
                            return gcloudPath;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"locating gcloud: {e.Message}");
            }

            return GCPRefreshConstants.GcloudExe;
        }
#endregion // global utility functions
    }
} // namespace KageKirin.GCPRefresh
