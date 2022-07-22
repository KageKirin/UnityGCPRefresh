using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;

namespace KageKirin.GCPRefresh
{
    [Serializable]
    public struct GCPRegistrySettings
    {
        public string token;
        public string email;
        public bool alwaysAuth;
    }

    [FilePath("GCPRefresh.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class GCPRefreshSettings : ScriptableSingleton<GCPRefreshSettings>
    {
#region constants
#if UNITY_EDITOR_WIN
        private const string k_gcloudExe = "gcloud.exe";
#else
        private const string k_gcloudExe = "gcloud";
#endif // UNITY_EDITOR_WIN

        private static string k_upmConfigPath =>
            Path.Join(
                Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".upmconfig.toml"
            );

        private static string k_upmConfigTomlDefaultContents =
            @$"[npmAuth.""{0}""]
token = ""invalid_token""
email = ""invalid_email@somewhere.com""
alwaysAuth = true
";
#endregion // constants

#region values
        [SerializeField]
        [ContextMenuItem("Set from environment", "SetGcloudPathFromEnv")]
        public string m_gcloudPath;

        [SerializeField]
        [OnValueChanged("OnRegistryChanged")]
        public string m_gcloudRegistry;

        [SerializeField]
        [ContextMenuItem("Create .upmconfig.toml", "CreateDefaultUpmConfigToml")]
        public string m_upmConfigTomlContents = "";

        [SerializeField]
        [OnValueChanged("OnRegistrySettingsChanged")]
        public GCPRegistrySettings m_registrySettings;

        [SerializeField]
        [Tooltip("Refresh rate in minutes")]
        public int m_tokenRefreshRate = 40;

#endregion // values
        private Task? m_tokenRefreshTask = null;

        public void Awake()
        {
            if (File.Exists(k_upmConfigPath))
            {
                m_upmConfigTomlContents = File.ReadAllText(k_upmConfigPath);
            }
            else
            {
                m_upmConfigTomlContents = string.Format(
                    k_upmConfigTomlDefaultContents,
                    m_gcloudRegistry
                );
            }

            m_tokenRefreshTask = StartTokenRefreshTask();
        }

        public void OnValidate()
        {
            if (String.IsNullOrWhiteSpace(m_upmConfigTomlContents))
            {
                if (File.Exists(k_upmConfigPath))
                {
                    m_upmConfigTomlContents = File.ReadAllText(k_upmConfigPath);
                }
                else
                {
                    CreateDefaultUpmConfigToml();
                }
            }
            else
            {
                m_registrySettings = LoadRegistrySettings(m_gcloudRegistry);
                FlushUpmConfigToml(m_upmConfigTomlContents);
            }

            Debug.Log($"running task {m_tokenRefreshTask.Id}");
        }

        internal static SerializedObject GetSerializedSettings()
        {
            instance.Save(true);
            return new SerializedObject(instance);
        }

        private void SetGcloudPathFromEnv()
        {
            m_gcloudPath = LocateGcloudTool();
        }

        private void CreateDefaultUpmConfigToml()
        {
            CreateUpmConfigToml(m_gcloudRegistry);
            m_upmConfigTomlContents = File.ReadAllText(k_upmConfigPath);
        }

        private GCPRegistrySettings LoadRegistrySettings(string registry)
        {
            var registrySettings = new GCPRegistrySettings();

            if (!String.IsNullOrWhiteSpace(m_upmConfigTomlContents))
            {
                var tomlData = Toml.ToModel(m_upmConfigTomlContents);
                var npmAuthTable = (TomlTable)tomlData["npmAuth"];
                var npmAuthRegistry = (TomlTable)npmAuthTable[registry];
                registrySettings.alwaysAuth = (bool)npmAuthRegistry["alwaysAuth"];
                registrySettings.email = (string)npmAuthRegistry["email"];
                registrySettings.token = (string)npmAuthRegistry["token"];
            }

            return registrySettings;
        }

        private void SaveRegistrySettings(string registry, GCPRegistrySettings registrySettings)
        {
            if (String.IsNullOrWhiteSpace(m_upmConfigTomlContents))
            {
                m_upmConfigTomlContents = String.Format(k_upmConfigTomlDefaultContents, registry);
            }

            var tomlData = Toml.ToModel(m_upmConfigTomlContents);
            var npmAuthTable = (TomlTable)tomlData["npmAuth"];
            var npmAuthRegistry = (TomlTable)npmAuthTable[registry];
            npmAuthRegistry["alwaysAuth"] = registrySettings.alwaysAuth;
            npmAuthRegistry["email"] = registrySettings.email;
            //npmAuthRegistry["token"] = registrySettings.token; //< never update this
            m_upmConfigTomlContents = Toml.FromModel(tomlData);

            FlushUpmConfigToml(m_upmConfigTomlContents);
        }

        private void OnRegistryChanged()
        {
            m_registrySettings = LoadRegistrySettings(m_gcloudRegistry);
        }

        private void OnRegistrySettingsChanged()
        {
            SaveRegistrySettings(m_gcloudRegistry, m_registrySettings);
        }

        private void OnTokenRefreshRateChanged()
        {
            if (m_tokenRefreshTask != null)
            {
                m_tokenRefreshTask.Dispose();
            }
            m_tokenRefreshTask = StartTokenRefreshTask();
        }

        private Task StartTokenRefreshTask()
        {
            var task = new Task(RefreshToken);
            task.Start();
            return task;
        }

        private static async void RefreshToken()
        {
            while (true)
            {
                var tomlString = File.ReadAllText(k_upmConfigPath);
                var tomlData = Toml.ToModel(tomlString);
                var npmAuthTable = (TomlTable)tomlData["npmAuth"];
                var npmAuthRegistry = (TomlTable)npmAuthTable[
                    GCPRefreshSettings.instance.m_gcloudRegistry
                ];
                npmAuthRegistry["token"] = GetRefreshedToken();
                tomlString = Toml.FromModel(tomlData);
                File.WriteAllText(k_upmConfigPath, tomlString);
                await Task.Delay(
                    TimeSpan.FromMinutes(GCPRefreshSettings.instance.m_tokenRefreshRate)
                );
            }
        }

        private static string GetRefreshedToken()
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.FileName = GCPRefreshSettings.instance.m_gcloudPath;
                    // process.StartInfo.WorkingDirectory = RootPath;
                    process.StartInfo.Arguments = "auth print-access-token";

                    process.Start();
                    process.WaitForExit();

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();

                    if (!String.IsNullOrEmpty(stdout))
                        Debug.Log($"gcloud auth: {stdout}");

                    if (!String.IsNullOrEmpty(stderr))
                        Debug.LogError($"gcloud auth: {stderr}");

                    return stdout;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"gcloud auth: {e.Message}");
            }

            return "invalid token after error";
        }

        private static void FlushUpmConfigToml(string contents)
        {
            File.WriteAllText(k_upmConfigPath, contents);
        }

        public static void CreateUpmConfigToml(string registry)
        {
            if (!File.Exists(k_upmConfigPath))
            {
                FlushUpmConfigToml(string.Format(k_upmConfigTomlDefaultContents, registry));
            }
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
                        var gcloudPath = Path.Join(path, k_gcloudExe);
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

            return k_gcloudExe;
        }
    }
} // namespace KageKirin.GCPRefresh
