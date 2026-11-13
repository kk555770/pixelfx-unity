// Math.sin, Math.cos, Math.asin and Math.acos as V8 computes them: fdlibm's k_sin.c, k_cos.c, e_rem_pio2.c, e_asin.c and
// e_acos.c, as in V8's src/base/ieee754.cc. The C runtime behind .NET's Math.Sin and friends rounds about one input in 150
// the other way, and one pixel that lands on the other side of a dither threshold is enough to break a frame's parity.
// Freely distributable fdlibm code, Copyright (C) 1993 by Sun Microsystems, Inc.
using System;

namespace Pixelfx
{
    public static partial class Px
    {
        const double S1 = -1.66666666666666324348e-01, S2 = 8.33333333332248946124e-03, S3 = -1.98412698298579493134e-04,
            S4 = 2.75573137070700676789e-06, S5 = -2.50507602534068634195e-08, S6 = 1.58969099521155010221e-10;
        const double C1 = 4.16666666666666019037e-02, C2 = -1.38888888888741095749e-03, C3 = 2.48015872894767294178e-05,
            C4 = -2.75573143513906633035e-07, C5 = 2.08757232129817482790e-09, C6 = -1.13596475577881948265e-11;
        const double invpio2 = 6.36619772367581382433e-01, pio2_1 = 1.57079632673412561417e+00, pio2_1t = 6.07710050650619224932e-11,
            pio2_2 = 6.07710050630396597660e-11, pio2_2t = 2.02226624879595063154e-21, pio2_3 = 2.02226624871116645580e-21,
            pio2_3t = 8.47842766036889956997e-32;

        static double KernelSin(double x, double y, bool tail)
        {
            if ((HighWord(x) & 0x7fffffff) < 0x3e400000 && (int)x == 0) return x;
            double z = x * x, v = z * x, r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
            if (!tail) return x + v * (S1 + z * r);
            return x - ((z * (0.5 * y - v * r) - y) - v * S1);
        }

        static double KernelCos(double x, double y)
        {
            int ix = HighWord(x) & 0x7fffffff;
            if (ix < 0x3e400000 && (int)x == 0) return 1;
            double z = x * x, r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
            if (ix < 0x3FD33333) return 1 - (0.5 * z - (z * r - x * y));
            double qx = ix > 0x3fe90000 ? 0.28125 : BitConverter.Int64BitsToDouble((long)(ix - 0x00200000) << 32);
            double hz = 0.5 * z - qx, a = 1 - qx;
            return a - (hz - (z * r - x * y));
        }

        // x - n * pi/2 as y0 + y1, for |x| up to 2^19 * pi/2; false beyond that
        static bool RemPio2(double x, out int n, out double y0, out double y1)
        {
            int hx = HighWord(x), ix = hx & 0x7fffffff;
            if (ix < 0x4002d97c)
            {
                // |x| < 3pi/4: n is +-1
                double z;
                if (hx > 0)
                {
                    z = x - pio2_1;
                    if (ix != 0x3ff921fb)
                    {
                        y0 = z - pio2_1t;
                        y1 = (z - y0) - pio2_1t;
                    }
                    else
                    {
                        z -= pio2_2;
                        y0 = z - pio2_2t;
                        y1 = (z - y0) - pio2_2t;
                    }
                    n = 1;
                }
                else
                {
                    z = x + pio2_1;
                    if (ix != 0x3ff921fb)
                    {
                        y0 = z + pio2_1t;
                        y1 = (z - y0) + pio2_1t;
                    }
                    else
                    {
                        z += pio2_2;
                        y0 = z + pio2_2t;
                        y1 = (z - y0) + pio2_2t;
                    }
                    n = -1;
                }
                return true;
            }
            if (ix > 0x413921fb)
            {
                n = 0;
                y0 = y1 = 0;
                return false;
            }
            double t = Math.Abs(x);
            n = (int)(t * invpio2 + 0.5);
            double fn = n, r = t - fn * pio2_1, w = fn * pio2_1t;
            y0 = r - w;
            int j = ix >> 20, i = j - ((HighWord(y0) >> 20) & 0x7ff);
            if (i > 16)
            {
                // 2nd iteration, good to 118 bits
                double tt = r;
                w = fn * pio2_2;
                r = tt - w;
                w = fn * pio2_2t - ((tt - r) - w);
                y0 = r - w;
                i = j - ((HighWord(y0) >> 20) & 0x7ff);
                if (i > 49)
                {
                    // 3rd iteration, 151 bits
                    tt = r;
                    w = fn * pio2_3;
                    r = tt - w;
                    w = fn * pio2_3t - ((tt - r) - w);
                    y0 = r - w;
                }
            }
            y1 = (r - y0) - w;
            if (hx < 0)
            {
                y0 = -y0;
                y1 = -y1;
                n = -n;
            }
            return true;
        }

        /// <summary>Math.sin as V8 computes it. Past 2^19 * pi/2 it falls back to the runtime's sine, which no effect reaches.</summary>
        public static double Sin(double x)
        {
            int ix = HighWord(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelSin(x, 0, false);
            if (ix >= 0x7ff00000) return x - x;
            if (!RemPio2(x, out int n, out double y0, out double y1)) return Math.Sin(x);
            switch (n & 3)
            {
                case 0: return KernelSin(y0, y1, true);
                case 1: return KernelCos(y0, y1);
                case 2: return -KernelSin(y0, y1, true);
                default: return -KernelCos(y0, y1);
            }
        }

        /// <summary>Math.cos as V8 computes it. Past 2^19 * pi/2 it falls back to the runtime's cosine, which no effect reaches.</summary>
        public static double Cos(double x)
        {
            int ix = HighWord(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelCos(x, 0);
            if (ix >= 0x7ff00000) return x - x;
            if (!RemPio2(x, out int n, out double y0, out double y1)) return Math.Cos(x);
            switch (n & 3)
            {
                case 0: return KernelCos(y0, y1);
                case 1: return -KernelSin(y0, y1, true);
                case 2: return -KernelCos(y0, y1);
                default: return KernelSin(y0, y1, true);
            }
        }

        const double pio2_hi = 1.57079632679489655800e+00, pio2_lo = 6.12323399573676603587e-17, pio4_hi = 7.85398163397448278999e-01,
            pi_ = 3.14159265358979311600e+00;
        const double pS0 = 1.66666666666666657415e-01, pS1 = -3.25565818622400915405e-01, pS2 = 2.01212532134862925881e-01,
            pS3 = -4.00555345006794114027e-02, pS4 = 7.91534994289814532176e-04, pS5 = 3.47933107596021167570e-05;
        const double qS1 = -2.40339491173441421878e+00, qS2 = 2.02094576023350569471e+00, qS3 = -6.88283971605453293030e-01,
            qS4 = 7.70381505559019352791e-02;

        static double ClearLow(double x) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(x) & unchecked((long)0xffffffff00000000UL));

        /// <summary>Math.asin as V8 computes it.</summary>
        public static double Asin(double x)
        {
            double t = 0, w, p, q, c, r, s;
            int hx = HighWord(x), ix = hx & 0x7fffffff;
            if (ix >= 0x3ff00000)
            {
                if (((ix - 0x3ff00000) | (int)LowWord(x)) == 0) return x * pio2_hi + x * pio2_lo;
                return (x - x) / (x - x);
            }
            if (ix < 0x3fe00000)
            {
                if (ix < 0x3e400000)
                {
                    if (1.0e300 + x > 1) return x;
                }
                else t = x * x;
                p = t * (pS0 + t * (pS1 + t * (pS2 + t * (pS3 + t * (pS4 + t * pS5)))));
                q = 1 + t * (qS1 + t * (qS2 + t * (qS3 + t * qS4)));
                w = p / q;
                return x + x * w;
            }
            w = 1 - Math.Abs(x);
            t = w * 0.5;
            p = t * (pS0 + t * (pS1 + t * (pS2 + t * (pS3 + t * (pS4 + t * pS5)))));
            q = 1 + t * (qS1 + t * (qS2 + t * (qS3 + t * qS4)));
            s = Math.Sqrt(t);
            if (ix >= 0x3FEF3333)
            {
                w = p / q;
                t = pio2_hi - (2.0 * (s + s * w) - pio2_lo);
            }
            else
            {
                w = ClearLow(s);
                c = (t - w * w) / (s + w);
                r = p / q;
                p = 2.0 * s * r - (pio2_lo - 2.0 * c);
                q = pio4_hi - 2.0 * w;
                t = pio4_hi - (p - q);
            }
            return hx > 0 ? t : -t;
        }

        /// <summary>Math.acos as V8 computes it.</summary>
        public static double Acos(double x)
        {
            double z, p, q, r, w, s, c, df;
            int hx = HighWord(x), ix = hx & 0x7fffffff;
            if (ix >= 0x3ff00000)
            {
                if (((ix - 0x3ff00000) | (int)LowWord(x)) == 0) return hx > 0 ? 0.0 : pi_ + 2.0 * pio2_lo;
                return (x - x) / (x - x);
            }
            if (ix < 0x3fe00000)
            {
                if (ix <= 0x3c600000) return pio2_hi + pio2_lo;
                z = x * x;
                p = z * (pS0 + z * (pS1 + z * (pS2 + z * (pS3 + z * (pS4 + z * pS5)))));
                q = 1 + z * (qS1 + z * (qS2 + z * (qS3 + z * qS4)));
                r = p / q;
                return pio2_hi - (x - (pio2_lo - x * r));
            }
            if (hx < 0)
            {
                z = (1 + x) * 0.5;
                p = z * (pS0 + z * (pS1 + z * (pS2 + z * (pS3 + z * (pS4 + z * pS5)))));
                q = 1 + z * (qS1 + z * (qS2 + z * (qS3 + z * qS4)));
                s = Math.Sqrt(z);
                r = p / q;
                w = r * s - pio2_lo;
                return pi_ - 2.0 * (s + w);
            }
            z = (1 - x) * 0.5;
            s = Math.Sqrt(z);
            df = ClearLow(s);
            c = (z - df * df) / (s + df);
            p = z * (pS0 + z * (pS1 + z * (pS2 + z * (pS3 + z * (pS4 + z * pS5)))));
            q = 1 + z * (qS1 + z * (qS2 + z * (qS3 + z * qS4)));
            r = p / q;
            w = r * s + c;
            return 2.0 * (df + w);
        }
    }
}
