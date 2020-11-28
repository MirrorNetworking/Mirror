using System;
using UnityEngine;

namespace Mirror
{
    enum ComponentType : uint
    {
        X = 0,
        Y = 1,
        Z = 2,
        W = 3
    }

    /// <summary>
    ///     Credit to this man for converting gaffer games c code to c#
    ///     https://gist.github.com/fversnel/0497ad7ab3b81e0dc1dd
    /// </summary>
    public static class Compression
    {
        private const float Minimum = -1.0f / 1.414214f; // note: 1.0f / sqrt(2)
        private const float Maximum = +1.0f / 1.414214f;

        internal static uint Compress(Quaternion quaternion)
        {
            float absX = Mathf.Abs(quaternion.x),
                      absY = Mathf.Abs(quaternion.y),
                      absZ = Mathf.Abs(quaternion.z),
                      absW = Mathf.Abs(quaternion.w);

            ComponentType largestComponent = ComponentType.X;
            float largestAbs = absX;
            float largest = quaternion.x;

            if (absY > largestAbs)
            {
                largestAbs = absY;
                largestComponent = ComponentType.Y;
                largest = quaternion.y;
            }
            if (absZ > largestAbs)
            {
                largestAbs = absZ;
                largestComponent = ComponentType.Z;
                largest =quaternion.z;
            }
            if (absW > largestAbs)
            {
                largestComponent = ComponentType.W;
                largest = quaternion.w;
            }

            float a = 0;
            float b = 0;
            float c = 0;
            switch (largestComponent)
            {
                case ComponentType.X:
                    a = quaternion.y;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Y:
                    a = quaternion.x;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Z:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.w;
                    break;
                case ComponentType.W:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.z;
                    break;
            }

            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            float normalizedA = (a - Minimum) / (Maximum - Minimum),
                normalizedB = (b - Minimum) / (Maximum - Minimum),
                normalizedC = (c - Minimum) / (Maximum - Minimum);

            uint integerA = (uint)Mathf.RoundToInt(normalizedA * 1024.0f),
                integerB = (uint)Mathf.RoundToInt(normalizedB * 1024.0f),
                integerC = (uint)Mathf.RoundToInt(normalizedC * 1024.0f);

            return (((uint)largestComponent) << 30) | (integerA << 20) | (integerB << 10) | integerC;
        }

        internal static Quaternion Decompress(uint compressed)
        {
            var largestComponentType = (ComponentType)(compressed >> 30);
            uint integerA = (compressed >> 20) & ((1 << 10) - 1),
                integerB = (compressed >> 10) & ((1 << 10) - 1),
                integerC = compressed & ((1 << 10) - 1);

            float a = integerA / 1024.0f * (Maximum - Minimum) + Minimum,
                b = integerB / 1024.0f * (Maximum - Minimum) + Minimum,
                c = integerC / 1024.0f * (Maximum - Minimum) + Minimum;

            Quaternion rotation;
            switch (largestComponentType)
            {
                case ComponentType.X:
                    // (?) y z w
                    rotation.y = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.x = Mathf.Sqrt(1 - rotation.y * rotation.y
                                               - rotation.z * rotation.z
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.Y:
                    // x (?) z w
                    rotation.x = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.y = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.z * rotation.z
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.Z:
                    // x y (?) w
                    rotation.x = a;
                    rotation.y = b;
                    rotation.w = c;
                    rotation.z = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.y * rotation.y
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.W:
                    // x y z (?)
                    rotation.x = a;
                    rotation.y = b;
                    rotation.z = c;
                    rotation.w = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.y * rotation.y
                                               - rotation.z * rotation.z);
                    break;
                default:
                    // Should never happen!
                    throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                          largestComponentType);
            }

            return rotation;
        }
    }
}
