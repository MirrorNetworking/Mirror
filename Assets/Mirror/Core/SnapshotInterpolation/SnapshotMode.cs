// snapshot mode & color coding for debugging purposes only.
// this is not required for snapshot interpolation to work correctly.
using UnityEngine;

namespace Mirror
{
    // current interpolation mode is returned for debugging.
    public enum SnapshotMode
    {
        Normal,      // regular speed
        Catchup,     // little behind, catching up
        Slowdown,    // little ahead, slowing down
        ClampBehind, // so far behind that we clamp
        ClampAhead   // so far ahead that we clamp
    }

    public static class SnapshotModeUtils
    {
        public static Color ColorCode(SnapshotMode mode, Color defaultColor)
        {
            // color code the current snapshot interpolation mode.
            // colors comparable to temperature. red=hot/fast, blue=cold/slow.
            switch (mode)
            {
                case SnapshotMode.Normal:      return defaultColor;
                case SnapshotMode.Catchup:     return Color.yellow;
                case SnapshotMode.ClampBehind: return Color.red;
                case SnapshotMode.Slowdown:    return Color.cyan;
                case SnapshotMode.ClampAhead:  return Color.blue;
                default:                       return defaultColor;
            }
        }
    }
}
