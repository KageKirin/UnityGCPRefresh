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
    public class GCPRefreshWindow : EditorWindow
    {
        private EditorCoroutine? coroutine = null;
        private bool isActive => coroutine != null;

        void StartCoroutine()
        {
            coroutine = EditorCoroutineUtility.StartCoroutine(GCPRefreshCoroutine.Run(), this);
        }

        void StopCoroutine()
        {
            if (coroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(coroutine);
                coroutine = null;
            }
        }

        void OnGUI()
        {
            bool settingsOk = !String.IsNullOrWhiteSpace(GCPRefreshSettings.instance.m_gcloudPath);

            if (!settingsOk)
            {
                GUILayout.Box("Please configure GCPRefresh in Project Settings.");
            }
            else
            {
                var playBtn = EditorGUIUtility.IconContent("d_PlayButton@2x", "start");
                var stopBtn = EditorGUIUtility.IconContent("d_PauseButton@2x", "stop");
                GUI.enabled = !isActive;
                if (GUI.Button(new Rect(10, 10, 40, 25), playBtn))
                {
                    StartCoroutine();
                }
                GUI.enabled = isActive;
                if (GUI.Button(new Rect(50, 10, 40, 25), stopBtn))
                {
                    StopCoroutine();
                }
                GUI.enabled = true;

                EditorGUILayout.Space(50.0f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(
                    $"Current Auth Token for {GCPRefreshSettings.instance.m_gcloudRegistry}"
                );
                EditorGUI.indentLevel++;
                EditorGUILayout.TextField(GCPRefreshCoroutine.Token);
                EditorGUILayout.LabelField(
                    $"Last refreshed: {(GCPRefreshCoroutine.LastRefreshTime == DateTime.UnixEpoch ? "never" : GCPRefreshCoroutine.LastRefreshTime.ToString())}"
                );
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        IEnumerator LogTimeSinceStartup()
        {
            while (true)
            {
                Debug.LogFormat("Time since startup: {0} s", Time.realtimeSinceStartup);
                yield return null;
            }
        }

        [MenuItem("GCP/Refresh GCP Token")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            GCPRefreshWindow window = (GCPRefreshWindow)EditorWindow.GetWindow(
                typeof(GCPRefreshWindow)
            );
            window.Show();
        }
    }
} //namespace KageKirin.GCPRefresh
