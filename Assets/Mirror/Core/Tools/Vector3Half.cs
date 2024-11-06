#pragma warning disable CS0659 // 'Vector3Half' overrides Object.Equals(object o) but does not override Object.GetHashCode()
#pragma warning disable CS0661 // 'Vector3Half' defines operator == or operator != but does not override Object.GetHashCode()

// Vector3Half by mischa (based on game engine project)
using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public struct Vector3Half
    {
        public Half x;
        public Half y;
        public Half z;

        public static readonly Vector3Half zero = new Vector3Half((Half)0, (Half)0, (Half)0);
        public static readonly Vector3Half one = new Vector3Half((Half)1, (Half)1, (Half)1);
        public static readonly Vector3Half forward = new Vector3Half((Half)0, (Half)0, (Half)1);
        public static readonly Vector3Half back = new Vector3Half((Half)0, (Half)0, (Half)(-1));
        public static readonly Vector3Half left = new Vector3Half((Half)(-1), (Half)0, (Half)0);
        public static readonly Vector3Half right = new Vector3Half((Half)1, (Half)0, (Half)0);
        public static readonly Vector3Half up = new Vector3Half((Half)0, (Half)1, (Half)0);
        public static readonly Vector3Half down = new Vector3Half((Half)0, (Half)(-1), (Half)0);

        // constructor /////////////////////////////////////////////////////////
        public Vector3Half(Half x, Half y, Half z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        // operators ///////////////////////////////////////////////////////////
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Half operator +(Vector3Half a, Vector3Half b) =>
            new Vector3Half(a.x + b.x, a.y + b.y, a.z + b.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Half operator -(Vector3Half a, Vector3Half b) =>
            new Vector3Half(a.x - b.x, a.y - b.y, a.z - b.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Half operator -(Vector3Half v) =>
            new Vector3Half(-v.x, -v.y, -v.z);

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static Vector3Half operator *(Vector3Half a, long n) =>
        //     new Vector3Half(a.x * n, a.y * n, a.z * n);

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static Vector3Half operator *(long n, Vector3Half a) =>
        //     new Vector3Half(a.x * n, a.y * n, a.z * n);

        // == returns true if approximately equal (with epsilon).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3Half a, Vector3Half b) =>
            a.x == b.x &&
            a.y == b.y &&
            a.z == b.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3Half a, Vector3Half b) => !(a == b);

        // NO IMPLICIT System.Numerics.Vector3Half conversion because double<->float
        // would silently lose precision in large worlds.

        // [i] component index. useful for iterating all components etc.
        public Half this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException($"Vector3Half[{index}] out of range.");
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    case 2:
                        z = value;
                        break;
                    default: throw new IndexOutOfRangeException($"Vector3Half[{index}] out of range.");
                }
            }
        }

        // instance functions //////////////////////////////////////////////////
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"({x} {y} {z})";

        // equality ////////////////////////////////////////////////////////////
        // implement Equals & HashCode explicitly for performance.
        // calling .Equals (instead of "==") checks for exact equality.
        // (API compatibility)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vector3Half other) =>
            x == other.x && y == other.y && z == other.z;

        // Equals(object) can reuse Equals(Vector4)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other) =>
            other is Vector3Half vector4 && Equals(vector4);

#if UNITY_2021_3_OR_NEWER
        // Unity 2019/2020 don't have HashCode.Combine yet.
        // this is only to avoid reflection. without defining, it works too.
        // default generated by rider
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(x, y, z);
#endif
    }
}
