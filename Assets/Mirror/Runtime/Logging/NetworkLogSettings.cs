using UnityEngine;

namespace Mirror.Logging
{
    /// <summary>
    /// Used to load LogSettings in build
    /// </summary>
    public class NetworkLogSettings : MonoBehaviour
    {
        [SerializeField] internal LogSettings settings;

        void Awake()
        {
            RefreshDictionary();
        }

        void OnValidate()
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
