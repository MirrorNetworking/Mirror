// People should be able to see and report errors to the developer very easily.
//
// Unity's Developer Console only works in development builds and it only shows
// errors. This class provides a console that works in all builds and also shows
// log and warnings in development builds.
//
// Note: we don't include the stack trace, because that can also be grabbed from
// the log files if needed.
//
// Note: there is no 'hide' button because we DO want people to see those errors
// and report them back to us.
//
// Note: normal Debug.Log messages can be shown by building in Debug/Development
//       mode.
using UnityEngine;
using System.Collections.Generic;

namespace Mirror
{
    struct LogEntry
    {
        public string message;
        public LogType type;

        public LogEntry(string message, LogType type)
        {
            this.message = message;
            this.type = type;
        }
    }

    public class GUIConsole : MonoBehaviour
    {
        public int height = 150;

        // only keep the recent 'n' entries. otherwise memory would grow forever
        // and drawing would get slower and slower.
        public int maxLogCount = 50;

        // Unity Editor has the Console window, we don't need to show it there.
        // unless for testing, so keep it as option.
        public bool showInEditor = false;

        // log as queue so we can remove the first entry easily
        readonly Queue<LogEntry> log = new Queue<LogEntry>();

        // hotkey to show/hide at runtime for easier debugging
        // (sometimes we need to temporarily hide/show it)
        // => F12 makes sense. nobody can find ^ in other games.
        public KeyCode hotKey = KeyCode.F12;

        // GUI
        bool visible;
        Vector2 scroll = Vector2.zero;

        // only show at runtime, or if showInEditor is enabled
        bool show => !Application.isEditor || showInEditor;

        void Awake()
        {
            // only show at runtime, or if showInEditor is enabled
            if (show)
                Application.logMessageReceived += OnLog;
        }

        // OnLog logs everything, even Debug.Log messages in release builds
        // => this makes a lot of things easier. e.g. addon initialization logs.
        // => it's really better to have than not to have those
        void OnLog(string message, string stackTrace, LogType type)
        {
            // is this important?
            // => always show exceptions & errors
            // => usually a good idea to show warnings too, otherwise it's too
            //    easy to miss OnDeserialize warnings etc. in builds
            bool isImportant = type == LogType.Error || type == LogType.Exception || type == LogType.Warning;

            // use stack trace only if important
            // (otherwise users would have to find and search the log file.
            //  seeing it in the console directly is way easier to deal with.)
            // => only add \n if stack trace is available (only in debug builds)
            if (isImportant && !string.IsNullOrWhiteSpace(stackTrace))
                message += $"\n{stackTrace}";

            // add to queue
            log.Enqueue(new LogEntry(message, type));

            // respect max entries
            if (log.Count > maxLogCount)
                log.Dequeue();

            // become visible if it was important
            // (no need to become visible for regular log. let the user decide.)
            if (isImportant)
                visible = true;

            // auto scroll
            scroll.y = float.MaxValue;
        }

        void Update()
        {
            if (show && Input.GetKeyDown(hotKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;

            scroll = GUILayout.BeginScrollView(scroll, "Box", GUILayout.Width(Screen.width), GUILayout.Height(height));
            foreach (LogEntry entry in log)
            {
                if (entry.type == LogType.Error || entry.type == LogType.Exception)
                    GUI.color = Color.red;
                else if (entry.type == LogType.Warning)
                    GUI.color = Color.yellow;

                GUILayout.Label(entry.message);
                GUI.color = Color.white;
            }
            GUILayout.EndScrollView();
        }
    }
}
