using UnityEngine;

namespace Mirror.Logging
{
    /// <summary>
    /// Used to enable log settings in build
    /// </summary>
    [ExecuteInEditMode]
    public class LogSettingsConfig : MonoBehaviour
    {
        [SerializeField] internal LogSettings settings;

#if UNITY_EDITOR
        // called when component is added to GameObject
        private void Reset()
        {
            LogSettings existingSettings = EditorLogSettingsLoader.FindLogSettings();
            if (existingSettings != null)
            {
                settings = existingSettings;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }
#endif


        void Start()
        {
            RefreshDictionary();
        }

        public void OnValidate()
        {
            // if settings field is changed
            RefreshDictionary();
        }

        void RefreshDictionary()
        {
            settings.LoadIntoDictionary(LogFactory.loggers);
        }
    }
}
