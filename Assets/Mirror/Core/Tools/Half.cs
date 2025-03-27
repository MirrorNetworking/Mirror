// half float from .NET 5:
// https://devblogs.microsoft.com/dotnet/introducing-the-half-type/
//
// drop in from dotnet/runtime source:
// https://github.com/dotnet/runtime/blob/e188d6ac90fe56320cca51c53709ef1c72f063d5/src/libraries/System.Private.CoreLib/src/System/Half.cs#L17
// removing all the stuff that's not in Unity though.



// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace System
{
    // Portions of the code implemented below are based on the 'Berkeley SoftFloat Release 3e' algorithms.

    /// <summary>
    /// Represents a half-precision floating-point number.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Half
        : IComparable,
          IComparable<Half>,
          IEquatable<Half>
    {
        private const NumberStyles DefaultParseStyle = NumberStyles.Float | NumberStyles.AllowThousands;

        // Constants for manipulating the private bit-representation

        internal const ushort SignMask = 0x8000;
        internal const int SignShift = 15;
        internal const byte ShiftedSignMask = SignMask >> SignShift;

        internal const ushort BiasedExponentMask = 0x7C00;
        internal const int BiasedExponentShift = 10;
        internal const int BiasedExponentLength = 5;
        internal const byte ShiftedBiasedExponentMask = BiasedExponentMask >> BiasedExponentShift;

        internal const ushort TrailingSignificandMask = 0x03FF;

        internal const byte MinSign = 0;
        internal const byte MaxSign = 1;

        internal const byte MinBiasedExponent = 0x00;
        internal const byte MaxBiasedExponent = 0x1F;

        internal const byte ExponentBias = 15;

        internal const sbyte MinExponent = -14;
        internal const sbyte MaxExponent = +15;

        internal const ushort MinTrailingSignificand = 0x0000;
        internal const ushort MaxTrailingSignificand = 0x03FF;

        internal const int TrailingSignificandLength = 10;
        internal const int SignificandLength = TrailingSignificandLength + 1;

        // Constants representing the private bit-representation for various default values

        private const ushort PositiveZeroBits = 0x0000;
        private const ushort NegativeZeroBits = 0x8000;

        private const ushort EpsilonBits = 0x0001;

        private const ushort PositiveInfinityBits = 0x7C00;
        private const ushort NegativeInfinityBits = 0xFC00;

        private const ushort PositiveQNaNBits = 0x7E00;
        private const ushort NegativeQNaNBits = 0xFE00;

        private const ushort MinValueBits = 0xFBFF;
        private const ushort MaxValueBits = 0x7BFF;

        private const ushort PositiveOneBits = 0x3C00;
        private const ushort NegativeOneBits = 0xBC00;

        private const ushort SmallestNormalBits = 0x0400;

        private const ushort EBits = 0x4170;
        private const ushort PiBits = 0x4248;
        private const ushort TauBits = 0x4648;

        // Well-defined and commonly used values

        public static Half Epsilon => new Half(EpsilonBits);                        //  5.9604645E-08

        public static Half PositiveInfinity => new Half(PositiveInfinityBits);      //  1.0 / 0.0;

        public static Half NegativeInfinity => new Half(NegativeInfinityBits);      // -1.0 / 0.0

        public static Half NaN => new Half(NegativeQNaNBits);                       //  0.0 / 0.0

        public static Half MinValue => new Half(MinValueBits);                      // -65504

        public static Half MaxValue => new Half(MaxValueBits);                      //  65504

        internal readonly ushort _value;                                            // internal representation

        internal Half(ushort value)
        {
            _value = value;
        }

        private Half(bool sign, ushort exp, ushort sig) => _value = (ushort)(((sign ? 1 : 0) << SignShift) + (exp << BiasedExponentShift) + sig);

        internal byte BiasedExponent
        {
            get
            {
                ushort bits = _value;
                return ExtractBiasedExponentFromBits(bits);
            }
        }

        internal sbyte Exponent
        {
            get
            {
                return (sbyte)(BiasedExponent - ExponentBias);
            }
        }

        internal ushort Significand
        {
            get
            {
                return (ushort)(TrailingSignificand | ((BiasedExponent != 0) ? (1U << BiasedExponentShift) : 0U));
            }
        }

        internal ushort TrailingSignificand
        {
            get
            {
                ushort bits = _value;
                return ExtractTrailingSignificandFromBits(bits);
            }
        }

        internal static byte ExtractBiasedExponentFromBits(ushort bits)
        {
            return (byte)((bits >> BiasedExponentShift) & ShiftedBiasedExponentMask);
        }

        internal static ushort ExtractTrailingSignificandFromBits(ushort bits)
        {
            return (ushort)(bits & TrailingSignificandMask);
        }

        public static bool operator <(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is unordered with respect to everything, including itself.
                return false;
            }

            bool leftIsNegative = IsNegative(left);

            if (leftIsNegative != IsNegative(right))
            {
                // When the signs of left and right differ, we know that left is less than right if it is
                // the negative value. The exception to this is if both values are zero, in which case IEEE
                // says they should be equal, even if the signs differ.
                return leftIsNegative && !AreZero(left, right);
            }

            return (left._value != right._value) && ((left._value < right._value) ^ leftIsNegative);
        }

        public static bool operator >(Half left, Half right)
        {
            return right < left;
        }

        public static bool operator <=(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is unordered with respect to everything, including itself.
                return false;
            }

            bool leftIsNegative = IsNegative(left);

            if (leftIsNegative != IsNegative(right))
            {
                // When the signs of left and right differ, we know that left is less than right if it is
                // the negative value. The exception to this is if both values are zero, in which case IEEE
                // says they should be equal, even if the signs differ.
                return leftIsNegative || AreZero(left, right);
            }

            return (left._value == right._value) || ((left._value < right._value) ^ leftIsNegative);
        }

        public static bool operator >=(Half left, Half right)
        {
            return right <= left;
        }

        public static bool operator ==(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is not equal to anything, including itself.
                return false;
            }

            // IEEE defines that positive and negative zero are equivalent.
            return (left._value == right._value) || AreZero(left, right);
        }

        public static bool operator !=(Half left, Half right)
        {
            return !(left == right);
        }

        /// <summary>Determines whether the specified value is finite (zero, subnormal, or normal).</summary>
        /// <remarks>This effectively checks the value is not NaN and not infinite.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Half value)
        {
            uint bits = value._value;
            return (~bits & PositiveInfinityBits) != 0;
        }

        /// <summary>Determines whether the specified value is infinite.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(Half value)
        {
            uint bits = value._value;
            return (bits & ~SignMask) == PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is NaN.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(Half value)
        {
            uint bits = value._value;
            return (bits & ~SignMask) > PositiveInfinityBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsNaNOrZero(Half value)
        {
            uint bits = value._value;
            return ((bits - 1) & ~SignMask) >= PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is negative.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegative(Half value)
        {
            return (short)(value._value) < 0;
        }

        /// <summary>Determines whether the specified value is negative infinity.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegativeInfinity(Half value)
        {
            return value._value == NegativeInfinityBits;
        }

        /// <summary>Determines whether the specified value is normal (finite, but not zero or subnormal).</summary>
        /// <remarks>This effectively checks the value is not NaN, not infinite, not subnormal, and not zero.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormal(Half value)
        {
            uint bits = value._value;
            return (ushort)((bits & ~SignMask) - SmallestNormalBits) < (PositiveInfinityBits - SmallestNormalBits);
        }

        /// <summary>Determines whether the specified value is positive infinity.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPositiveInfinity(Half value)
        {
            return value._value == PositiveInfinityBits;
        }

        /// <summary>Determines whether the specified value is subnormal (finite, but not zero or normal).</summary>
        /// <remarks>This effectively checks the value is not NaN, not infinite, not normal, and not zero.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSubnormal(Half value)
        {
            uint bits = value._value;
            return (ushort)((bits & ~SignMask) - 1) < MaxTrailingSignificand;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsZero(Half value)
        {
            uint bits = value._value;
            return (bits & ~SignMask) == 0;
        }

        private static bool AreZero(Half left, Half right)
        {
            // IEEE defines that positive and negative zero are equal, this gives us a quick equality check
            // for two values by or'ing the private bits together and stripping the sign. They are both zero,
            // and therefore equivalent, if the resulting value is still zero.
            return ((left._value | right._value) & ~SignMask) == 0;
        }

        /// <summary>
        /// Compares this object to another object, returning an integer that indicates the relationship.
        /// </summary>
        /// <returns>A value less than zero if this is less than <paramref name="obj"/>, zero if this is equal to <paramref name="obj"/>, or a value greater than zero if this is greater than <paramref name="obj"/>.</returns>
        public int CompareTo(object obj)
        {
            if (obj is Half other)
            {
                return CompareTo(other);
            }
            return (obj is null) ? 1 : throw new ArgumentException("SR.Arg_MustBeHalf");
        }

        /// <summary>
        /// Compares this object to another object, returning an integer that indicates the relationship.
        /// </summary>
        /// <returns>A value less than zero if this is less than <paramref name="other"/>, zero if this is equal to <paramref name="other"/>, or a value greater than zero if this is greater than <paramref name="other"/>.</returns>
        public int CompareTo(Half other)
        {
            if (this < other)
            {
                return -1;
            }

            if (this > other)
            {
                return 1;
            }

            if (this == other)
            {
                return 0;
            }

            if (IsNaN(this))
            {
                return IsNaN(other) ? 0 : -1;
            }

            return 1;
        }

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified <paramref name="obj"/>.
        /// </summary>
        public override bool Equals(object obj)
        {
            return (obj is Half other) && Equals(other);
        }

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified <paramref name="other"/> value.
        /// </summary>
        public bool Equals(Half other)
        {
            return _value == other._value
                || AreZero(this, other)
                || (IsNaN(this) && IsNaN(other));
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            uint bits = _value;

            if (IsNaNOrZero(this))
            {
                // Ensure that all NaNs and both zeros have the same hash code
                bits &= PositiveInfinityBits;
            }

            return (int)bits;
        }

        /// <summary>
        /// Returns a string representation of the current value.
        /// </summary>
        public override string ToString()
        {
            return ((float)this).ToString();
        }

        //
        // Explicit Convert To Half
        //

        /// <summary>Explicitly converts a <see cref="char" /> value to its nearest representable half-precision floating-point value.</summary>
        public static explicit operator Half(char value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="decimal" /> value to its nearest representable half-precision floating-point value.</summary>
        public static explicit operator Half(decimal value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="short" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(short value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="int" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(int value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="long" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(long value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="float" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(float value)
        {
            // Unity implement this!
            return new Half(Mathf.FloatToHalf(value));
        }

        /// <summary>Explicitly converts a <see cref="ushort" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(ushort value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="uint" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(uint value) => (Half)(float)value;

        /// <summary>Explicitly converts a <see cref="ulong" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static explicit operator Half(ulong value) => (Half)(float)value;

        //
        // Explicit Convert From Half
        //

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="byte" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="byte" /> value.</returns>
        public static explicit operator byte(Half value) => (byte)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="char" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="char" /> value.</returns>
        public static explicit operator char(Half value) => (char)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="decimal" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="decimal" /> value.</returns>
        public static explicit operator decimal(Half value) => (decimal)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="short" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="short" /> value.</returns>
        public static explicit operator short(Half value) => (short)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="int" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="int" /> value.</returns>
        public static explicit operator int(Half value) => (int)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="long" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="long" /> value.</returns>
        public static explicit operator long(Half value) => (long)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="sbyte" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="sbyte" /> value.</returns>
        public static explicit operator sbyte(Half value) => (sbyte)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="ushort" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="ushort" /> value.</returns>
        public static explicit operator ushort(Half value) => (ushort)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="uint" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="uint" /> value.</returns>
        public static explicit operator uint(Half value) => (uint)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="ulong" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="ulong" /> value.</returns>
        public static explicit operator ulong(Half value) => (ulong)(float)value;

        //
        // Implicit Convert To Half
        //

        /// <summary>Implicitly converts a <see cref="byte" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static implicit operator Half(byte value) => (Half)(float)value;

        /// <summary>Implicitly converts a <see cref="sbyte" /> value to its nearest representable half-precision floating-point value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable half-precision floating-point value.</returns>
        public static implicit operator Half(sbyte value) => (Half)(float)value;

        /// <summary>Explicitly converts a half-precision floating-point value to its nearest representable <see cref="float" /> value.</summary>
        /// <returns><paramref name="value" /> converted to its nearest representable <see cref="float" /> value.</returns>
        public static explicit operator float(Half value)
        {
            return Mathf.HalfToFloat(value._value);
        }

        // IEEE 754 specifies NaNs to be propagated
        internal static Half Negate(Half value)
        {
            return IsNaN(value) ? value : new Half((ushort)(value._value ^ SignMask));
        }

        public static Half operator +(Half left, Half right) => (Half)((float)left + (float)right);

        //
        // IDecrementOperators
        //

        public static Half operator --(Half value)
        {
            var tmp = (float)value;
            --tmp;
            return (Half)tmp;
        }

        //
        // IDivisionOperators
        //

        public static Half operator /(Half left, Half right) => (Half)((float)left / (float)right);

        //
        // IExponentialFunctions
        //

        public static Half Exp(Half x) => (Half)Math.Exp((float)x);

        //
        // IFloatingPoint
        //

        public static Half Ceiling(Half x) => (Half)Math.Ceiling((float)x);

        public static Half Floor(Half x) => (Half)Math.Floor((float)x);

        public static Half Round(Half x) => (Half)Math.Round((float)x);

        public static Half Round(Half x, int digits) => (Half)Math.Round((float)x, digits);

        public static Half Round(Half x, MidpointRounding mode) => (Half)Math.Round((float)x, mode);

        public static Half Round(Half x, int digits, MidpointRounding mode) => (Half)Math.Round((float)x, digits, mode);

        public static Half Truncate(Half x) => (Half)Math.Truncate((float)x);

        //
        // IFloatingPointConstants
        //

        public static Half E => new Half(EBits);

        public static Half Pi => new Half(PiBits);

        public static Half Tau => new Half(TauBits);

        //
        // IFloatingPointIeee754
        //

        public static Half NegativeZero => new Half(NegativeZeroBits);

        public static Half Atan2(Half y, Half x) => (Half)Math.Atan2((float)y, (float)x);

        public static Half Lerp(Half value1, Half value2, Half amount) => (Half)Mathf.Lerp((float)value1, (float)value2, (float)amount);

        //
        // IHyperbolicFunctions
        //

        public static Half Cosh(Half x) => (Half)Math.Cosh((float)x);

        public static Half Sinh(Half x) => (Half)Math.Sinh((float)x);

        public static Half Tanh(Half x) => (Half)Math.Tanh((float)x);

        //
        // IIncrementOperators
        //

        public static Half operator ++(Half value)
        {
            var tmp = (float)value;
            ++tmp;
            return (Half)tmp;
        }

        //
        // ILogarithmicFunctions
        //

        public static Half Log(Half x) => (Half)Math.Log((float)x);

        public static Half Log(Half x, Half newBase) => (Half)Math.Log((float)x, (float)newBase);

        //
        // IModulusOperators
        //

        public static Half operator %(Half left, Half right) => (Half)((float)left % (float)right);

        //
        // IMultiplicativeIdentity
        //

        public static Half MultiplicativeIdentity => new Half(PositiveOneBits);

        //
        // IMultiplyOperators
        //

        public static Half operator *(Half left, Half right) => (Half)((float)left * (float)right);

        //
        // INumber
        //

        public static Half Clamp(Half value, Half min, Half max) => (Half)Mathf.Clamp((float)value, (float)min, (float)max);

        public static Half CopySign(Half value, Half sign)
        {
            // This method is required to work for all inputs,
            // including NaN, so we operate on the raw bits.
            uint xbits = value._value;
            uint ybits = sign._value;

            // Remove the sign from x, and remove everything but the sign from y
            // Then, simply OR them to get the correct sign
            return new Half((ushort)((xbits & ~SignMask) | (ybits & SignMask)));
        }

        public static Half Max(Half x, Half y) => (Half)Math.Max((float)x, (float)y);

        public static Half MaxNumber(Half x, Half y)
        {
            // This matches the IEEE 754:2019 `maximumNumber` function
            //
            // It does not propagate NaN inputs back to the caller and
            // otherwise returns the larger of the inputs. It
            // treats +0 as larger than -0 as per the specification.

            if (x != y)
            {
                if (!IsNaN(y))
                {
                    return y < x ? x : y;
                }

                return x;
            }

            return IsNegative(y) ? x : y;
        }

        public static Half Min(Half x, Half y) => (Half)Math.Min((float)x, (float)y);

        public static Half MinNumber(Half x, Half y)
        {
            // This matches the IEEE 754:2019 `minimumNumber` function
            //
            // It does not propagate NaN inputs back to the caller and
            // otherwise returns the larger of the inputs. It
            // treats +0 as larger than -0 as per the specification.

            if (x != y)
            {
                if (!IsNaN(y))
                {
                    return x < y ? x : y;
                }

                return x;
            }

            return IsNegative(x) ? x : y;
        }

        public static int Sign(Half value)
        {
            if (IsNaN(value))
            {
                throw new ArithmeticException("SR.Arithmetic_NaN");
            }

            if (IsZero(value))
            {
                return 0;
            }
            else if (IsNegative(value))
            {
                return -1;
            }

            return +1;
        }

        //
        // INumberBase
        //

        public static Half One => new Half(PositiveOneBits);

        public static Half Zero => new Half(PositiveZeroBits);

        public static Half Abs(Half value) => new Half((ushort)(value._value & ~SignMask));

        public static bool IsPositive(Half value) => (short)(value._value) >= 0;

        public static bool IsRealNumber(Half value)
        {
            // A NaN will never equal itself so this is an
            // easy and efficient way to check for a real number.

#pragma warning disable CS1718
            return value == value;
#pragma warning restore CS1718
        }

        //
        // IPowerFunctions
        //

        public static Half Pow(Half x, Half y) => (Half)Math.Pow((float)x, (float)y);

        //
        // IRootFunctions
        //

        public static Half Sqrt(Half x) => (Half)Math.Sqrt((float)x);

        //
        // ISignedNumber
        //

        public static Half NegativeOne => new Half(NegativeOneBits);

        //
        // ISubtractionOperators
        //

        public static Half operator -(Half left, Half right) => (Half)((float)left - (float)right);

        //
        // ITrigonometricFunctions
        //

        public static Half Acos(Half x) => (Half)Math.Acos((float)x);

        public static Half Asin(Half x) => (Half)Math.Asin((float)x);

        public static Half Atan(Half x) => (Half)Math.Atan((float)x);

        public static Half Cos(Half x) => (Half)Math.Cos((float)x);

        public static Half Sin(Half x) => (Half)Math.Sin((float)x);

        public static Half Tan(Half x) => (Half)Math.Tan((float)x);

        //
        // IUnaryNegationOperators
        //

        public static Half operator -(Half value) => (Half)(-(float)value);

        //
        // IUnaryPlusOperators
        //

        public static Half operator +(Half value) => value;
    }
}
