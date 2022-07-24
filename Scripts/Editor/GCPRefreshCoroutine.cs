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
    internal static class GCPRefreshCoroutine
    {
        internal static IEnumerator Run()
        {
            while (true)
            {
                Token = GetRefreshedToken();
                RefreshCount++;
                LastRefreshTime = DateTime.Now;

                var waitForOneMinute = new EditorWaitForSeconds(60.0f);
                for (int count = 0; count < GCPRefreshSettings.instance.m_tokenRefreshRate; count++)
                {
                    yield return waitForOneMinute;
                }
            }
        }

        static public int RefreshCount { get; private set; }
        static public DateTime LastRefreshTime { get; private set; } = DateTime.UnixEpoch;

        static public string Token
        {
            get
            {
                var tomlString = File.ReadAllText(GCPRefreshConstants.UpmConfigTomlPath);
                var tomlData = Toml.ToModel(tomlString);
                var npmAuthTable = (TomlTable)tomlData["npmAuth"];

                var npmAuthRegistry = (TomlTable)npmAuthTable[
                    GCPRefreshSettings.instance.m_gcloudRegistry
                ];
                return (string)npmAuthRegistry["token"];
            }
            private set
            {
                var tomlString = File.ReadAllText(GCPRefreshConstants.UpmConfigTomlPath);
                var tomlData = Toml.ToModel(tomlString);
                var npmAuthTable = (TomlTable)tomlData["npmAuth"];

                var npmAuthRegistry = (TomlTable)npmAuthTable[
                    GCPRefreshSettings.instance.m_gcloudRegistry
                ];
                npmAuthRegistry["token"] = value;

                tomlString = Toml.FromModel(tomlData);
                File.WriteAllText(GCPRefreshConstants.UpmConfigTomlPath, tomlString);
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
    }
} //namespace KageKirin.GCPRefresh
