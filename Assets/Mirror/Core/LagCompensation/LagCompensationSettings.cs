// snapshot interpolation settings struct.
// can easily be exposed in Unity inspectors.
using System;
using UnityEngine;

namespace Mirror
{
    // class so we can define defaults easily
    [Serializable]
    public class LagCompensationSettings
    {
        [Header("Buffering")]
        [Tooltip("Keep this many past snapshots in the buffer. The larger this is, the further we can rewind into the past.\nMaximum rewind time := historyAmount * captureInterval")]
        public int historyLimit = 6;

        [Tooltip("Capture state every 'captureInterval' seconds. Larger values will space out the captures more, which gives a longer history but with possible gaps inbetween.\nSmaller values will have fewer gaps, with shorter history.")]
        public float captureInterval = 0.100f; // 100 ms
    }
}
