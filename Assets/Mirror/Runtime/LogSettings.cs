using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [ExecuteInEditMode]
    [AddComponentMenu("Network/LogSettings")]
    public class LogSettings : MonoBehaviour
    {
        [Serializable]
        public struct Level
        {
            public string Name;
            public LogType level;
        };

        [SerializeField]
        public List<Level> Levels = new List<Level>();

        // Start is called before the first frame update
        void Awake()
        {
            SetLogLevels();
        }

        public void SetLogLevels()
        {
            foreach (Level setting in Levels)
            {
                var logger = LogFactory.GetLogger(setting.Name);
                logger.filterLogType = setting.level;
            }
        }
    }
}
