using System;
using System.Collections.Generic;
using System.IO;
using Mirage.NetworkProfiler.ModuleGUI.Messages;
using Mirage.NetworkProfiler.ModuleGUI.UITable;
using UnityEditor;
using UnityEngine;

namespace Mirage.NetworkProfiler.ModuleGUI
{
    [Serializable]
    internal class SavedData
    {
        /// <summary>
        /// Message from each frame so they can survive domain reload
        /// </summary>
        public Frames Frames;

        /// <summary>
        /// Active sort header
        /// </summary>
        public string SortHeader;

        public SortMode SortMode;

        /// <summary>
        /// Which Message groups are expanded
        /// </summary>
        public List<string> Expanded;

        public bool GroupMessages = true;

        public SavedData()
        {
            Frames = new Frames();

            Expanded = new List<string>();
        }

        public (ColumnInfo, SortMode) GetSortHeader(Columns columns)
        {
            foreach (var c in columns)
            {
                if (SortHeader == c.Header)
                {
                    return (c, SortMode);
                }
            }

            return (null, SortMode.None);
        }

        public void SetSortHeader(SortHeader header)
        {
            if (header == null)
            {
                SortHeader = "";
            }
            else
            {
                SortHeader = header.Info.Header;
                SortMode = header.SortMode;
            }
        }

        public void Clear()
        {
            foreach (var frame in Frames)
            {
                frame.Bytes = 0;
                frame.Messages.Clear();
            }
        }
    }

    internal class SaveDataLoader
    {
        private SavedData _receiveData;
        private SavedData _sentData;
        private static SaveDataLoader instance;
        private static bool isQuitting;

        // private so only we can create one
        private SaveDataLoader()
        {
            NetworkProfilerRecorder.AfterSample += AfterSample;
            EditorApplication.quitting += Quitting;
        }

        private void Quitting()
        {
            Console.WriteLine("[Mirage.Profiler] quitting");
            // save and clear references when quitting,
            // this is needed because finialize is called after unity dll unloads so causes crash
            SaveBoth();
            _receiveData = null;
            _sentData = null;
            isQuitting = true;
        }

        ~SaveDataLoader()
        {
            NetworkProfilerRecorder.AfterSample -= AfterSample;

            // dont save after quitting, unity might unload their dll and cause crash
            if (isQuitting)
                return;

            SaveBoth();
        }

        private void SaveBoth()
        {
            if (_receiveData != null)
                Save(GetFullPath("Receive"), _receiveData);

            if (_sentData != null)
                Save(GetFullPath("Sent"), _sentData);
        }

        private static void AfterSample(int tick)
        {
            SetFrame(tick, ReceiveData, NetworkProfilerRecorder._receivedCounter);
            SetFrame(tick, SentData, NetworkProfilerRecorder._sentCounter);
        }

        private static void SetFrame(int tick, SavedData data, CountRecorder counter)
        {
            var saveFrame = data.Frames.GetFrame(tick);

            // clear old data
            saveFrame.Bytes = 0;
            saveFrame.Messages.Clear();

            if (counter == null)
                return;

            var counterFrame = counter._frames.GetFrame(tick);

            saveFrame.Bytes = counterFrame.Bytes;
            saveFrame.Messages.AddRange(counterFrame.Messages);
        }

        public static SavedData ReceiveData
        {
            get
            {
                // dont load on quit, it might cause crash if unity dll unloads while savedata is save/loading
                if (isQuitting)
                    return null;

                if (instance == null)
                    instance = new SaveDataLoader();

                if (instance._receiveData == null)
                {
                    instance._receiveData = Load(GetFullPath("Receive"));
                }
                return instance._receiveData;
            }
        }

        public static SavedData SentData
        {
            get
            {
                // dont load on quit, it might cause crash if unity dll unloads while savedata is save/loading
                if (isQuitting)
                    return null;

                if (instance == null)
                    instance = new SaveDataLoader();

                if (instance._sentData == null)
                {
                    instance._sentData = Load(GetFullPath("Sent"));
                }
                return instance._sentData;
            }
        }

        public static string GetFullPath(string name)
        {
            var userSettingsFolder = Path.GetFullPath("UserSettings");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            return Path.Join(userSettingsFolder, "Mirage.Profiler", $"{name}.json");
        }

        public static void Save(string path, SavedData data)
        {
            Console.WriteLine($"[Mirage.Profiler] Save {path}");
            CheckDir(path);

            var text = JsonUtility.ToJson(data);
            File.WriteAllText(path, text);
        }

        public static SavedData Load(string path)
        {
            Console.WriteLine($"[Mirage.Profiler] Load {path}");
            CheckDir(path);

            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SavedData>(text);
                Validate(data);
                return data;
            }
            else
            {
                return new SavedData();
            }
        }

        private static void Validate(SavedData data)
        {
            data.Frames.ValidateSize();
        }

        private static void CheckDir(string path)
        {
            // check dir exists
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
