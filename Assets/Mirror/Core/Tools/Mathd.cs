// 'double' precision variants for some of Unity's Mathf functions.
using System.Runtime.CompilerServices;

namespace Mirror
{
    public static class Mathd
    {
        // Unity 2020 doesn't have Math.Clamp yet.
        /// <summary>Clamps value between 0 and 1 and returns value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Clamps value between 0 and 1 and returns value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp01(double value) => Clamp(value, 0, 1);

        /// <summary>Calculates the linear parameter t that produces the interpolant value within the range [a, b].</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double InverseLerp(double a, double b, double value) =>
            a != b ? Clamp01((value - a) / (b - a)) : 0;

        /// <summary>Linearly interpolates between a and b by t with no limit to t.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double LerpUnclamped(double a, double b, double t) =>
            a + (b - a) * t;
    }
}
