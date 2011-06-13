using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using Monza.DotNetExtensions.iSynaptic;

namespace Monza.DotNetExtensions
{
    public struct DoubleComponents
    {
        public double Datum { get; set; }
        public bool IsNaN { get; set; }
        public bool Negative { get; set; }
        public int MathematicalBase2Exponent { get; set; }
        public long MathematicalBase2Mantissa { get; set; }
        public int RawExponentInt { get; set; }
        public long RawMantissaLong { get; set; }
        // TODO: Memoize these
        public long FloorLog10 { get { return Log10.Floor(); } }
        public double FracLog10 { get { return Log10 - FloorLog10; } }
        public double Log10 { get { return Datum.Log10(); } }
        public byte[] ExponentBits { get { return BitConverter.GetBytes(RawExponentInt); } }
        public byte[] MantissaBits { get { return BitConverter.GetBytes(RawMantissaLong); } }
    }
    /// <summary>
    /// Numerical routines.
    /// </summary>
    public static class Numerics
    {
        public static double Log10(this double d) { return Math.Log10(d); }
        public static double Exp10(this double d) { return Math.Pow(10d, d); }
        public static double LogB(this double d, double @base) { return Math.Log(d, @base); }
        public static double Log(this double d) { return Math.Log(d); }
        public static double Exp(this double d) { return Math.Exp(d); }
        public static double ExpB(this double d, double @base) { return Math.Pow(@base, d); }
        public static double Pow(this double d, double exponent) { return Math.Pow(d, exponent); }
        public static long Floor(this double d) { return ((long)(Math.Floor(d))); }
        public static long Ceiling(this double d) { return ((long)(Math.Ceiling(d))); }
        public static double DFloor(this double d) { return ((long)(Math.Floor(d))); }
        public static double DCeiling(this double d) { return ((long)(Math.Ceiling(d))); }
        public static double Round(this double d, int digits = 4) { return Math.Round(d, digits); }

        public static DoubleComponents Decompose(this double d)
        {
            // See http://msdn.microsoft.com/en-us/library/aa691146(VS.71).aspx 
            // and Steve Hollasch's http://steve.hollasch.net/cgindex/coding/ieeefloat.html
            // and PremK's http://blogs.msdn.com/b/premk/archive/2006/02/25/539198.aspx 

            var result = new DoubleComponents { Datum = d };

            long bits = BitConverter.DoubleToInt64Bits(d);
            bool fNegative = (bits < 0);
            int exponent = (int)((bits >> 52) & 0x7ffL);
            long mantissa = (bits & 0xfffffffffffffL);

            result.Negative = fNegative;
            result.RawExponentInt = exponent;
            result.RawMantissaLong = mantissa;

            if (exponent == 0x7ffL && mantissa != 0)
            {
                Contract.Assert(double.IsNaN(d));

                // The number is an NaN. Client must interpret.
                result.IsNaN = true;
                return result;
            }

            // The first bit of the mathematical mantissaBits is always 1, and it is not
            // represented in the stored mantissaBits bits. The following logic accounts for
            // this and restores the mantissaBits to its mathematical value.

            if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    // Returning either +0 or -0.
                    return result;
                }
                // Denormalized: A fool-proof detector for denormals is a zero exponentBits. 
                // Mantissae for denormals do not have an assumed leading 1-bit. Bump the 
                // exponentBits by one so that when we re-bias it by -1023, we have actually 
                // brought it back down by -1022, the way it should be. This increment merges 
                // the logic for normals and denormals.
                exponent++;
            }
            else
            {
                // Normalized: radix point (the binary point) is after the first non-zero digit (bit). 
                // Or-in the *assumed* leading 1 bit to restore the mathematical mantissaBits.
                mantissa = mantissa | (1L << 52);
            }

            // Re-bias the exponentBits by the IEEE 1023, which treats the mathematical mantissaBits
            // as a pure fraction, minus another 52, because we treat the mathematical mantissaBits
            // as a pure integer.
            exponent -= 1075;

            // Produce form with lowest possible whole-number mantissaBits.
            while ((mantissa & 1) == 0)
            {
                mantissa >>= 1;
                exponent++;
            }

            result.MathematicalBase2Exponent = exponent;
            result.MathematicalBase2Mantissa = mantissa;
            return result;
        }

        /// <summary>
        /// Compares two doubles for nearness in the absolute value within a given delta, also a double. Not realiable for infinities, epsilon, max_value, min_value, and NaNs.
        /// </summary>
        /// <param name="d1">The first double.</param>
        /// <param name="d2">The second double.</param>
        /// <param name="delta">The allowable absolute difference.</param>
        /// <returns></returns>
        public static bool DoublesWithinAbsoluteDifference(double d1, double d2, double delta)
        {
            return (Math.Abs(d1 - d2) <= Math.Abs(delta));
        }

        /// <summary>
        /// Return the bits of a double as a twos-complement long integer. IEEE 745 doubles are 
        /// lexicographically ordered in this representation.
        /// </summary>
        /// <param name="d">The double to convert.</param>
        /// <returns>A long containing the bits of the double in twos-complement.</returns>
        private static long TwosComplementBits(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            // Convert to 2-s complement, which are lexicographically ordered.
            if (bits < 0)
                bits = unchecked((long)(0x8000000000000000 - (ulong)bits));
            return bits;
        }

        /// <summary>
        /// Returns the number of IEEE 754 double-precision values separating a pair of doubles. Adjacent doubles
        /// will have a quanta-difference of 1. Returns -1L if either number is NaN. Reports that double.MaxValue
        /// is adjacent to double.PositiveInfinity and likewise for double.MinValue and double.NegativeInfinity.
        /// </summary>
        /// <param name="d1">The first double to compare.</param>
        /// <param name="d2">The second double to compare.</param>
        /// <returns>The number of IEEE 745 doubles separating the pair.</returns>
        public static long LexicographicQuantaDifference(double d1, double d2)
        {
            if (double.IsNaN(d1) || double.IsNaN(d2))
                return -1L;

            // See Bruce Dawson's http://www.cygnus-software.com/papers/comparingfloats/comparingfloats.htm
            // The major limitation of this technique is that double.MaxValue and double.PostiveInfinity will 
            // report almost equal. See the paper for mitigations.

            // Extract bits from the doubles.
            long bits1 = TwosComplementBits(d1);
            long bits2 = TwosComplementBits(d2);

            long diff = bits1 - bits2;
            if (diff < 0)
                diff = -diff;

            return diff;
        }

        /// <summary>
        /// Compare doubles for equality within a given number of possible discrete double-precision values. 
        /// Produces false if either number is NaN. Reports that double.MaxValue is almost equal to 
        /// double.PositiveInfinity; likewise for double.MinValue and double.NegativeInfinity.
        /// </summary>
        /// <param name="d1">The first double.</param>
        /// <param name="d2">The second double.</param>
        /// <param name="nQuanta">The number of discrete double-precision values permitted between d1 and d2. Must be >= 0.</param>
        /// <returns></returns>
        public static bool DoublesNearlyEqual(double d1, double d2, int nQuanta)
        {
            Contract.Assert(nQuanta >= 0, "nUnits");

            if (double.IsNaN(d1) || double.IsNaN(d2))
                return false;

            return LexicographicQuantaDifference(d1, d2) <= nQuanta;
        }
    }
}
