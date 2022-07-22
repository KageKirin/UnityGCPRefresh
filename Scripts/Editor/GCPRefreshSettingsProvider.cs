using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Tomlyn;
using Tomlyn.Model;

#nullable enable


namespace KageKirin.GCPRefresh
{
    public class GCPRefreshSettingsProvider : SettingsProvider
    {
        private SerializedObject? m_GCPRefreshSettings = null;

        private string m_UpmConfigToml = String.Empty;
        private TomlTable? m_tomlData = null;
        private TomlTable? m_npmAuthData = null;
        private string[]? m_npmAuthRegistries = null;

        class Styles
        {
            /// explainer text
            public static GUIContent gcloudExplainer = new GUIContent(
                "Using GCPRefresh for Unity requires having the gcloud tool installed and available in the $PATH environment variable."
                    + "\nOnce installed, click the button below to automatically set the correct path to `gcloud`."
            );

            /// formatting
            public static GUIStyle gcloudExplainerStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperLeft,
            };

            public static GUIContent gcloudPathString = new GUIContent("gcloud install path");
            public static GUIContent gcloudSetPathButton = new GUIContent(
                "Set path from environment"
            );
            public static GUILayoutOption[] gcloudSetPathButtonOptions = new GUILayoutOption[]
            {
                GUILayout.Width(EditorGUIUtility.labelWidth),
                GUILayout.MaxWidth(EditorGUIUtility.labelWidth + 20),
            };

            public static GUIContent upmConfigToml = new GUIContent(".upmconfig.toml");
            public static GUIContent upmConfigTomlButton = new GUIContent("Create .upmconfig.toml");

            public static GUILayoutOption[] upmConfigTomlOptions = new GUILayoutOption[]
            {
                GUILayout.Height(400),
                GUILayout.Width(EditorGUIUtility.labelWidth),
                GUILayout.MaxWidth(1200),
            };

            public static GUILayoutOption[] upmConfigTomlButtonSpaceOptions = new GUILayoutOption[]
            {
                GUILayout.Width(EditorGUIUtility.labelWidth),
                GUILayout.MaxWidth(1200),
            };

            public static GUIContent tokenRefreshRate = new GUIContent(
                "token refresh rate (minutes)"
            );
            public static GUIContent gcloudRegistry = new GUIContent("gcloud registry");
        }

        public GCPRefreshSettingsProvider(
            string path,
            SettingsScope scopes,
            IEnumerable<string>? keywords = null
        ) : base(path, scopes, keywords) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_GCPRefreshSettings = new SerializedObject(GCPRefreshSettings.instance);

            m_UpmConfigToml = File.ReadAllText(GCPRefreshConstants.UpmConfigTomlPath);
            m_tomlData = (TomlTable)Toml.ToModel(m_UpmConfigToml);
            m_npmAuthData = (TomlTable)m_tomlData["npmAuth"];
            m_npmAuthRegistries = m_npmAuthData.Keys.ToArray();
        }

        public override void OnGUI(string searchContext)
        {
            using (CreateSettingsWindowGUIScope())
            {
                GUILayout.Box(Styles.gcloudExplainer, Styles.gcloudExplainerStyle);
                GUILayout.Space(20.0f);

                if (m_GCPRefreshSettings != null)
                {
                    var pathRect = EditorGUILayout.BeginHorizontal();
                    var gcloudPath = m_GCPRefreshSettings.FindProperty("m_gcloudPath");
                    gcloudPath.stringValue = EditorGUILayout.TextField(
                        Styles.gcloudPathString,
                        gcloudPath.stringValue
                    );
                    if (
                        GUILayout.Button(
                            Styles.gcloudSetPathButton,
                            Styles.gcloudSetPathButtonOptions
                        )
                    )
                    {
                        var path = GCPRefreshSettings.LocateGcloudTool();
                        if (!string.IsNullOrEmpty(path))
                        {
                            gcloudPath.stringValue = path;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    var tokenRefreshRate = m_GCPRefreshSettings.FindProperty("m_tokenRefreshRate");
                    tokenRefreshRate.intValue = EditorGUILayout.IntSlider(
                        Styles.tokenRefreshRate,
                        tokenRefreshRate.intValue,
                        1,
                        60
                    );

                    var gcloudRegistry = m_GCPRefreshSettings.FindProperty("m_gcloudRegistry");
                    if (gcloudRegistry != null && m_npmAuthRegistries != null)
                    {
                        int selection = 0;
                        if (!String.IsNullOrWhiteSpace(gcloudRegistry.stringValue))
                        {
                            selection = Array.IndexOf(
                                m_npmAuthRegistries,
                                gcloudRegistry.stringValue
                            );
                        }
                        selection = EditorGUILayout.Popup(
                            Styles.gcloudRegistry,
                            selection,
                            m_npmAuthRegistries.Select(x => x.Replace("https://", "")).ToArray()
                        );
                        gcloudRegistry.stringValue = m_npmAuthRegistries[selection];
                    }

                    m_GCPRefreshSettings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateGCPRefreshSettingsProvider()
        {
            var provider = new GCPRefreshSettingsProvider(
                "Project/GCPRefresh",
                SettingsScope.Project,
                GetSearchKeywordsFromGUIContentProperties<Styles>()
            );
            return provider;
        }

        private IDisposable? CreateSettingsWindowGUIScope()
        {
            var unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
            var type = unityEditorAssembly.GetType("UnityEditor.SettingsWindow+GUIScope");
            return Activator.CreateInstance(type) as IDisposable;
        }
    }
} // namespace KageKirin.GCPRefresh
