// Math.tan, Math.log, Math.log2 and Math.cbrt as V8 computes them: fdlibm's k_tan.c, s_tan.c and e_log.c, and FreeBSD's
// e_log2.c and s_cbrt.c, as in V8's src/base/ieee754.cc. The C runtime behind .NET's Math rounds some inputs
// the other way (a quarter of all cube roots), and one pixel on the other side of a dither threshold breaks a frame's parity.
// Freely distributable fdlibm code, Copyright (C) 1993 by Sun Microsystems, Inc.; FreeBSD's additions are under the same terms.
using System;

namespace Pixelfx
{
    public static partial class Px
    {
        static double FromWords(int hi, uint lo) => BitConverter.Int64BitsToDouble(((long)hi << 32) | lo);
        static double WithLow(double x, uint lo) => FromWords(HighWord(x), lo);
        static double WithHigh(double x, int hi) => FromWords(hi, LowWord(x));

        // ---- tan

        static readonly double[] T =
        {
            3.33333333333334091986e-01, 1.33333333333201242699e-01, 5.39682539762260521377e-02, 2.18694882948595424599e-02,
            8.86323982359930005737e-03, 3.59207910759131235356e-03, 1.45620945432529025516e-03, 5.88041240820264096874e-04,
            2.46463134818469906812e-04, 7.81794442939557092300e-05, 7.14072491382608190305e-05, -1.85586374855275456654e-05,
            2.59073051863633712884e-05,
        };
        const double pio4 = 7.85398163397448278999e-01, pio4lo = 3.06161699786838301793e-17;

        static double KernelTan(double x, double y, int iy)
        {
            double z, r, v, w, s;
            int hx = HighWord(x), ix = hx & 0x7fffffff;
            if (ix < 0x3e300000 && (int)x == 0)
            {
                if (((ix | (int)LowWord(x)) | (iy + 1)) == 0) return 1 / Math.Abs(x);
                if (iy == 1) return x;
                // -1 / (x + y), carefully
                z = w = x + y;
                z = WithLow(z, 0);
                v = y - (z - x);
                double a = -1 / w, t = WithLow(a, 0);
                s = 1 + t * z;
                return t + a * (s + t * v);
            }
            if (ix >= 0x3FE59428)
            {
                // |x| >= 0.6744
                if (hx < 0)
                {
                    x = -x;
                    y = -y;
                }
                z = pio4 - x;
                w = pio4lo - y;
                x = z + w;
                y = 0.0;
            }
            z = x * x;
            w = z * z;
            r = T[1] + w * (T[3] + w * (T[5] + w * (T[7] + w * (T[9] + w * T[11]))));
            v = z * (T[2] + w * (T[4] + w * (T[6] + w * (T[8] + w * (T[10] + w * T[12])))));
            s = z * x;
            r = y + z * (s * (r + v) + y);
            r += T[0] * s;
            w = x + r;
            if (ix >= 0x3FE59428)
            {
                v = iy;
                return (1 - ((hx >> 30) & 2)) * (v - 2.0 * (x - (w * w / (w + v) - r)));
            }
            if (iy == 1) return w;
            {
                z = WithLow(w, 0);
                v = r - (z - x);
                double a = -1.0 / w, t = WithLow(a, 0);
                s = 1.0 + t * z;
                return t + a * (s + t * v);
            }
        }

        /// <summary>Math.tan as V8 computes it. Past 2^19 * pi/2 it falls back to the runtime's tangent, which no effect reaches.</summary>
        public static double Tan(double x)
        {
            int ix = HighWord(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelTan(x, 0, 1);
            if (ix >= 0x7ff00000) return x - x;
            if (!RemPio2(x, out int n, out double y0, out double y1)) return Math.Tan(x);
            return KernelTan(y0, y1, 1 - ((n & 1) << 1));
        }

        // ---- log, log2

        const double ln2_hi = 6.93147180369123816490e-01, ln2_lo = 1.90821492927058770002e-10, two54 = 1.80143985094819840000e+16;
        const double Lg1 = 6.666666666666735130e-01, Lg2 = 3.999999999940941908e-01, Lg3 = 2.857142874366239149e-01,
            Lg4 = 2.222219843214978396e-01, Lg5 = 1.818357216161805012e-01, Lg6 = 1.531383769920937332e-01, Lg7 = 1.479819860511658591e-01;

        /// <summary>Math.log as V8 computes it.</summary>
        public static double Log(double x)
        {
            double hfsq, f, s, z, R, w, t1, t2, dk;
            int k = 0, hx = HighWord(x), i, j;
            uint lx = LowWord(x);
            if (hx < 0x00100000)
            {
                if (((hx & 0x7fffffff) | (int)lx) == 0) return double.NegativeInfinity;
                if (hx < 0) return double.NaN;
                k -= 54;
                x *= two54;
                hx = HighWord(x);
            }
            if (hx >= 0x7ff00000) return x + x;
            k += (hx >> 20) - 1023;
            hx &= 0x000fffff;
            i = (hx + 0x95f64) & 0x100000;
            x = WithHigh(x, hx | (i ^ 0x3ff00000));
            k += i >> 20;
            f = x - 1.0;
            if ((0x000fffff & (2 + hx)) < 3)
            {
                // |f| < 2^-20
                if (f == 0)
                {
                    if (k == 0) return 0;
                    dk = k;
                    return dk * ln2_hi + dk * ln2_lo;
                }
                R = f * f * (0.5 - 0.33333333333333333 * f);
                if (k == 0) return f - R;
                dk = k;
                return dk * ln2_hi - ((R - dk * ln2_lo) - f);
            }
            s = f / (2.0 + f);
            dk = k;
            z = s * s;
            i = hx - 0x6147a;
            w = z * z;
            j = 0x6b851 - hx;
            t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
            t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
            i |= j;
            R = t2 + t1;
            if (i > 0)
            {
                hfsq = 0.5 * f * f;
                if (k == 0) return f - (hfsq - s * (hfsq + R));
                return dk * ln2_hi - ((hfsq - (s * (hfsq + R) + dk * ln2_lo)) - f);
            }
            if (k == 0) return f - s * (f - R);
            return dk * ln2_hi - ((s * (f - R) - dk * ln2_lo) - f);
        }

        const double ivln2hi = 1.44269504072144627571e+00, ivln2lo = 1.67517131648865118353e-10;

        /// <summary>Math.log2 as V8 computes it.</summary>
        public static double Log2(double x)
        {
            int k = 0, hx = HighWord(x), i;
            uint lx = LowWord(x);
            if (hx < 0x00100000)
            {
                if (((hx & 0x7fffffff) | (int)lx) == 0) return double.NegativeInfinity;
                if (hx < 0) return double.NaN;
                k -= 54;
                x *= two54;
                hx = HighWord(x);
            }
            if (hx >= 0x7ff00000) return x + x;
            if (hx == 0x3ff00000 && lx == 0) return 0;
            k += (hx >> 20) - 1023;
            hx &= 0x000fffff;
            i = (hx + 0x95f64) & 0x100000;
            x = WithHigh(x, hx | (i ^ 0x3ff00000));
            k += i >> 20;
            double y = k, f = x - 1.0, hfsq = 0.5 * f * f;
            // k_log1p(f)
            double s = f / (2.0 + f), z = s * s, w = z * z;
            double R = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7))) + w * (Lg2 + w * (Lg4 + w * Lg6));
            double r = s * (hfsq + R);
            double hi = WithLow(f - hfsq, 0);
            double lo = (f - hi) - hfsq + r;
            double val_hi = hi * ivln2hi, val_lo = (lo + hi) * ivln2lo + lo * ivln2hi;
            w = y + val_hi;
            val_lo += (y - w) + val_hi;
            val_hi = w;
            return val_lo + val_hi;
        }

        // ---- cbrt

        const uint B1 = 715094163, B2 = 696219795;
        const double P0 = 1.87595182427177009643, P1 = -1.88497979543377169875, P2 = 1.621429720105354466140,
            P3 = -0.758397934778766047437, P4 = 0.145996192886612446982;

        /// <summary>Math.cbrt as V8 computes it.</summary>
        public static double Cbrt(double x)
        {
            int hx = HighWord(x);
            uint low = LowWord(x), sign = (uint)hx & 0x80000000;
            hx = (int)((uint)hx ^ sign);
            if (hx >= 0x7ff00000) return x + x;
            double r, s, t, w;
            if (hx < 0x00100000)
            {
                if ((hx | (int)low) == 0) return x;
                t = FromWords(0x43500000, 0) * x;
                uint high = (uint)HighWord(t);
                t = FromWords((int)(sign | ((high & 0x7fffffff) / 3 + B2)), 0);
            }
            else t = FromWords((int)(sign | ((uint)hx / 3 + B1)), 0);
            r = (t * t) * (t / x);
            t = t * ((P0 + r * (P1 + r * P2)) + ((r * r) * r) * (P3 + r * P4));
            t = BitConverter.Int64BitsToDouble((long)(((ulong)BitConverter.DoubleToInt64Bits(t) + 0x80000000UL) & 0xffffffffc0000000UL));
            s = t * t;
            r = x / s;
            w = t + t;
            r = (r - t) / (w + r);
            return t + t * r;
        }
    }
}
