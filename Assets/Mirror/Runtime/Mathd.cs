// 'double' precision variants for some of Unity's Mathf functions.
namespace Mirror
{
    public static class Mathd
    {
        /// <summary>Linearly interpolates between a and b by t with no limit to t.</summary>
        public static double LerpUnclamped(double a, double b, double t) =>
            a + (b - a) * t;

        /// <summary>Clamps value between 0 and 1 and returns value.</summary>
        public static double Clamp01(double value)
        {
            if (value < 0.0)
                return 0;
            return value > 1 ? 1 : value;
        }

        /// <summary>Calculates the linear parameter t that produces the interpolant value within the range [a, b].</summary>
        public static double InverseLerp(double a, double b, double value) =>
            a != b ? Clamp01((value - a) / (b - a)) : 0;
    }
}
