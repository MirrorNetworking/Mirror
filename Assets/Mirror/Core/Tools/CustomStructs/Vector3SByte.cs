#pragma warning disable CS0659 // 'Vector3SByte' overrides Object.Equals(object o) but does not override Object.GetHashCode()
#pragma warning disable CS0661 // 'Vector3SByte' defines operator == or operator != but does not override Object.GetHashCode()

// Vector3SByte by mischa (based on game engine project)
using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public struct Vector3SByte
    {
        public sbyte x;
        public sbyte y;
        public sbyte z;

        public static readonly Vector3SByte zero = new Vector3SByte(0, 0, 0);
        public static readonly Vector3SByte one = new Vector3SByte(1, 1, 1);
        public static readonly Vector3SByte forward = new Vector3SByte(0, 0, 1);
        public static readonly Vector3SByte back = new Vector3SByte(0, 0, -1);
        public static readonly Vector3SByte left = new Vector3SByte(-1, 0, 0);
        public static readonly Vector3SByte right = new Vector3SByte(1, 0, 0);
        public static readonly Vector3SByte up = new Vector3SByte(0, 1, 0);
        public static readonly Vector3SByte down = new Vector3SByte(0, -1, 0);

        // constructor /////////////////////////////////////////////////////////
        public Vector3SByte(sbyte x, sbyte y, sbyte z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        // operators ///////////////////////////////////////////////////////////
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3SByte operator +(Vector3SByte a, Vector3SByte b) =>
            new Vector3SByte((sbyte)(a.x + b.x), (sbyte)(a.y + b.y), (sbyte)(a.z + b.z));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3SByte operator -(Vector3SByte a, Vector3SByte b) =>
            new Vector3SByte((sbyte)(a.x - b.x), (sbyte)(a.y - b.y), (sbyte)(a.z - b.z));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3SByte operator -(Vector3SByte v) =>
            new Vector3SByte((sbyte)-v.x, (sbyte)-v.y, (sbyte)-v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3SByte operator *(Vector3SByte a, sbyte n) =>
            new Vector3SByte((sbyte)(a.x * n), (sbyte)(a.y * n), (sbyte)(a.z * n));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3SByte operator *(sbyte n, Vector3SByte a) =>
            new Vector3SByte((sbyte)(a.x * n), (sbyte)(a.y * n), (sbyte)(a.z * n));

        // == returns true if exactly equal
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3SByte a, Vector3SByte b) =>
            a.x == b.x &&
            a.y == b.y &&
            a.z == b.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3SByte a, Vector3SByte b) => !(a == b);

        // [i] component index. useful for iterating all components etc.
        public sbyte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException($"Vector3SByte[{index}] out of range.");
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
                    default: throw new IndexOutOfRangeException($"Vector3SByte[{index}] out of range.");
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
        public bool Equals(Vector3SByte other) =>
            x == other.x && y == other.y && z == other.z;

        // Equals(object) can reuse Equals(Vector3SByte)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other) =>
            other is Vector3SByte vector3 && Equals(vector3);

#if UNITY_2021_3_OR_NEWER
        // Unity 2019/2020 don't have HashCode.Combine yet.
        // this is only to avoid reflection. without defining, it works too.
        // default generated by rider
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(x, y, z);
#endif
    }
}