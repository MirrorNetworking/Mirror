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
