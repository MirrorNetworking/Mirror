using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mirror.Tests.WindowScripts
{
    public class PerformanceTestsWindow : EditorWindow
    {
#if UNITY_2019_2_OR_NEWER
        static string resultPath => Path.Combine(Application.persistentDataPath, "TestResults.xml");
#else
        static string resultPath => Path.Combine(Application.streamingAssetsPath, "TestResults.xml");
#endif
        static string settingsPath => Path.Combine(Application.persistentDataPath, "PerformanceTestsWindowSettings.json");

        [SerializeField] RunSettings settings;

        SerializedObject so;
        SerializedProperty dotnetProp;
        SerializedProperty reporterProp;
        SerializedProperty outputProp;
        SerializedProperty baselineProp;
        SerializedProperty resultsProp;

        void OnEnable()
        {
            Load();
        }
        void OnDisable()
        {
            Save();
        }
        void Load()
        {
            try
            {
                string json = File.ReadAllText(settingsPath);
                settings = JsonUtility.FromJson<RunSettings>(json);
                so = new SerializedObject(this);
                SerializedProperty settingProp = so.FindProperty(nameof(settings));

                dotnetProp = settingProp.FindPropertyRelative(nameof(RunSettings.dotnet));
                reporterProp = settingProp.FindPropertyRelative(nameof(RunSettings.reporter));
                outputProp = settingProp.FindPropertyRelative(nameof(RunSettings.output));
                baselineProp = settingProp.FindPropertyRelative(nameof(RunSettings.baseLine));
                resultsProp = settingProp.FindPropertyRelative(nameof(RunSettings.results));
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }



        void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(settings);
                File.WriteAllText(settingsPath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnGUI()
        {
            if (so == null)
            {
                GUILayout.Label("Failed to load");
                return;
            }
            so.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Settings"))
                {
                    Save();
                }
                if (GUILayout.Button("Load Settings"))
                {
                    Load();
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(dotnetProp);
            EditorGUILayout.PropertyField(reporterProp);
            EditorGUILayout.PropertyField(outputProp);

            DrawResultsLine(baselineProp);
            using (new EditorGUI.IndentLevelScope())
            {
                resultsProp.arraySize = EditorGUILayout.IntField(new GUIContent("results array size"), resultsProp.arraySize);
                for (int i = 0; i < resultsProp.arraySize; i++)
                {
                    DrawResultsLine(resultsProp.GetArrayElementAtIndex(i));
                }
            }

            if (GUILayout.Button("Build Report"))
            {
                BuildReport();
            }
            if (GUILayout.Button("Open Report folder"))
            {
                OpenReport();
            }

            so.ApplyModifiedProperties();
        }

        void BuildReport()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                Debug.Log("Build report started");
                StringBuilder argBuilder = new StringBuilder();
                argBuilder.Append(settings.reporter);
                argBuilder.Append(" --baseline=");
                argBuilder.Append(settings.baseLine);
                argBuilder.Append(" --reportdirpath=");
                argBuilder.Append(settings.output);

                foreach (string result in settings.results)
                {
                    argBuilder.Append(" --results=");
                    argBuilder.Append(result);
                }

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = settings.dotnet;
                process.StartInfo.Arguments = argBuilder.ToString();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler((sender, e) =>
                {
                    // Prepend line numbers to each line of the output.
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.Log(e.Data);
                    }
                });

                process.Start();

                process.BeginOutputReadLine();

                Debug.Log("Build report Finished");

                process.WaitForExit();
                process.Close();
            });
        }

        void OpenReport()
        {
            System.Diagnostics.Process.Start(string.Format("{0}/UnityPerformanceBenchmark", settings.output));
        }

        static void DrawResultsLine(SerializedProperty prop)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(prop);
                if (GUILayout.Button("move"))
                {
                    MoveResults(prop.stringValue);
                }
            }
        }

        static void MoveResults(string path)
        {
            File.Copy(resultPath, path, true);
        }

        [MenuItem("Window/Tools/Performance Benchmark Report Builder")]
        public static void ShowWindow()
        {
            PerformanceTestsWindow window = GetWindow<PerformanceTestsWindow>();
            window.titleContent = new GUIContent("Performance Benchmark Report Builder");
            window.Show();
        }

        [System.Serializable]
        public class RunSettings
        {
            public string dotnet;
            public string reporter;
            public string output;
            public string baseLine;
            public string[] results;
        }
    }
}
