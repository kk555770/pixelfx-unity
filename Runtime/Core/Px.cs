using System;
using System.Collections.Generic;

namespace Pixelfx
{
    public static partial class Px
    {
        public const double PI = Math.PI;
        public const double TAU = Math.PI * 2;
        public const uint WHITE = 0xffffffff;
        public const uint BLACK = 0xff000000;

        public static double Clamp(double x, double a = 0, double b = 1) => x < a ? a : x > b ? b : x;
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double Seg(double t, double t0, double t1) => Clamp((t - t0) / (t1 - t0));

        public static double Smooth(double e0, double e1, double x)
        {
            double t = Seg(x, e0, e1);
            return t * t * (3 - 2 * t);
        }

        public static double Bump(double t, double t0, double t1) => t <= t0 || t >= t1 ? 0 : Px.Sin(Math.PI * Seg(t, t0, t1));
        public static double Wrap(double x, double m) => ((x % m) + m) % m;
        public static double InvLerp(double a, double b, double x) => b == a ? 0 : (x - a) / (b - a);
        public static double Remap(double x, double a0, double a1, double b0, double b1) => Lerp(b0, b1, InvLerp(a0, a1, x));
        public static double Fract(double x) => x - Math.Floor(x);
        public static double Pingpong(double x) => 1 - Math.Abs(Wrap(x, 2) - 1);
        public static double Dist(double x0, double y0, double x1, double y1) => Hypot(x1 - x0, y1 - y0);
        public static double AngleTo(double x0, double y0, double x1, double y1) => Atan2(y1 - y0, x1 - x0);

        public static int Floor(double x) => (int)Math.Floor(x);
        public static int Ceil(double x) => (int)Math.Ceiling(x);

        public static double Round(double x)
        {
            double r = Math.Floor(x);
            r = x - r >= 0.5 ? r + 1 : r;
            return r == 0 && x < 0 ? -0.0 : r; // Math.round(-0.3) is -0
        }

        public static int RoundInt(double x) => (int)Round(x);

        /// <summary>Math.hypot(x, y, z) as V8 computes it: scaled by the largest, the squares summed with Kahan's compensation.</summary>
        public static double Hypot(double x, double y, double z)
        {
            double ax = Math.Abs(x), ay = Math.Abs(y), az = Math.Abs(z);
            bool nan = double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z);
            double max = 0;
            if (!double.IsNaN(ax) && ax > max) max = ax;
            if (!double.IsNaN(ay) && ay > max) max = ay;
            if (!double.IsNaN(az) && az > max) max = az;
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (nan) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            foreach (double v in new[] { ax, ay, az })
            {
                double n = v / max, summand = n * n - compensation, preliminary = sum + summand;
                compensation = preliminary - sum - summand;
                sum = preliminary;
            }
            return Math.Sqrt(sum) * max;
        }

        // V8's Math.hypot: scaled by the largest, summed with Kahan compensation.
        public static double Hypot(double x, double y)
        {
            double ax = Math.Abs(x), ay = Math.Abs(y);
            if (double.IsInfinity(ax) || double.IsInfinity(ay)) return double.PositiveInfinity;
            if (double.IsNaN(ax) || double.IsNaN(ay)) return double.NaN;
            double max = ax > ay ? ax : ay;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            double n = ax / max, summand = n * n - compensation, preliminary = sum + summand;
            compensation = preliminary - sum - summand;
            sum = preliminary;
            n = ay / max;
            summand = n * n - compensation;
            preliminary = sum + summand;
            sum = preliminary;
            return Math.Sqrt(sum) * max;
        }

        public static int Sign(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

        // ---- V8's Math.atan2 and Math.exp (fdlibm, as ported in V8's src/base/ieee754.cc). The C runtime's
        // Math.Atan2 and Math.Exp differ from them in the last bit for about 1 input in 6 and 1 in 10.

        static readonly double Two1023 = BitConverter.Int64BitsToDouble(0x7fe0000000000000);
        static int HighWord(double x) => (int)(BitConverter.DoubleToInt64Bits(x) >> 32);
        static uint LowWord(double x) => (uint)BitConverter.DoubleToInt64Bits(x);

        static readonly double[] AtanHi = { 4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00 };
        static readonly double[] AtanLo = { 2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17 };
        static readonly double[] AT =
        {
            3.33333333333329318027e-01, -1.99999999998764832476e-01, 1.42857142725034663711e-01, -1.11111104054623557880e-01,
            9.09088713343650656196e-02, -7.69187620504482999495e-02, 6.66107313738753120669e-02, -5.83357013379057348645e-02,
            4.97687799461593236017e-02, -3.65315727442169155270e-02, 1.62858201153657823623e-02,
        };

        /// <summary>Math.atan as V8 computes it.</summary>
        public static double Atan(double x)
        {
            int hx = HighWord(x), ix = hx & 0x7fffffff, id;
            if (ix >= 0x44100000)
            {
                // |x| >= 2^66
                uint low = LowWord(x);
                if (ix > 0x7ff00000 || (ix == 0x7ff00000 && low != 0)) return x + x;
                return hx > 0 ? AtanHi[3] + AtanLo[3] : -AtanHi[3] - AtanLo[3];
            }
            if (ix < 0x3fdc0000)
            {
                // |x| < 0.4375
                if (ix < 0x3e400000) return x; // |x| < 2^-27
                id = -1;
            }
            else
            {
                x = Math.Abs(x);
                if (ix < 0x3ff30000)
                {
                    if (ix < 0x3fe60000)
                    {
                        id = 0;
                        x = (2.0 * x - 1.0) / (2.0 + x);
                    }
                    else
                    {
                        id = 1;
                        x = (x - 1.0) / (x + 1.0);
                    }
                }
                else if (ix < 0x40038000)
                {
                    id = 2;
                    x = (x - 1.5) / (1.0 + 1.5 * x);
                }
                else
                {
                    id = 3;
                    x = -1.0 / x;
                }
            }
            double z = x * x, w = z * z;
            double s1 = z * (AT[0] + w * (AT[2] + w * (AT[4] + w * (AT[6] + w * (AT[8] + w * AT[10])))));
            double s2 = w * (AT[1] + w * (AT[3] + w * (AT[5] + w * (AT[7] + w * AT[9]))));
            if (id < 0) return x - x * (s1 + s2);
            z = AtanHi[id] - ((x * (s1 + s2) - AtanLo[id]) - x);
            return hx < 0 ? -z : z;
        }

        /// <summary>Math.atan2 as V8 computes it.</summary>
        public static double Atan2(double y, double x)
        {
            const double tiny = 1.0e-300, piO4 = 7.8539816339744827900E-01, piO2 = 1.5707963267948965580E+00, pi = 3.1415926535897931160E+00, piLo = 1.2246467991473531772E-16;
            int hx = HighWord(x), ix = hx & 0x7fffffff, hy = HighWord(y), iy = hy & 0x7fffffff;
            uint lx = LowWord(x), ly = LowWord(y);
            if ((ix | (lx != 0 ? 1 : 0)) > 0x7ff00000 || (iy | (ly != 0 ? 1 : 0)) > 0x7ff00000) return x + y; // NaN
            if (hx == 0x3ff00000 && lx == 0) return Atan(y); // x = 1.0
            int m = ((hy >> 31) & 1) | ((hx >> 30) & 2); // 2 * sign(x) + sign(y)
            if ((iy | (int)ly) == 0)
            {
                switch (m)
                {
                    case 0:
                    case 1: return y;
                    case 2: return pi + tiny;
                    default: return -pi - tiny;
                }
            }
            if ((ix | (int)lx) == 0) return hy < 0 ? -piO2 - tiny : piO2 + tiny;
            if (ix == 0x7ff00000)
            {
                if (iy == 0x7ff00000)
                {
                    switch (m)
                    {
                        case 0: return piO4 + tiny;
                        case 1: return -piO4 - tiny;
                        case 2: return 3.0 * piO4 + tiny;
                        default: return -3.0 * piO4 - tiny;
                    }
                }
                switch (m)
                {
                    case 0: return 0.0;
                    case 1: return -0.0;
                    case 2: return pi + tiny;
                    default: return -pi - tiny;
                }
            }
            if (iy == 0x7ff00000) return hy < 0 ? -piO2 - tiny : piO2 + tiny;
            int k = (iy - ix) >> 20;
            double z;
            if (k > 60)
            {
                z = piO2 + 0.5 * piLo;
                m &= 1;
            }
            else if (hx < 0 && k < -60) z = 0.0;
            else z = Atan(Math.Abs(y / x));
            switch (m)
            {
                case 0: return z;
                case 1: return -z;
                case 2: return pi - (z - piLo);
                default: return (z - piLo) - pi;
            }
        }

        /// <summary>Math.exp as V8 computes it.</summary>
        public static double Exp(double x)
        {
            const double oThreshold = 7.09782712893383973096e+02, uThreshold = -7.45133219101941108420e+02, invln2 = 1.44269504088896338700e+00;
            const double p1 = 1.66666666666666019037e-01, p2 = -2.77777777770155933842e-03, p3 = 6.61375632143793436117e-05, p4 = -1.65339022054652515390e-06, p5 = 4.13813679705723846039e-08;
            const double e = 2.718281828459045, twom1000 = 9.33263618503218878990e-302;
            double hi = 0, lo = 0;
            int k = 0;
            int hx = HighWord(x), xsb = (hx >> 31) & 1;
            hx &= 0x7fffffff;
            if (hx >= 0x40862e42)
            {
                // |x| >= 709.78...
                if (hx >= 0x7ff00000)
                {
                    if (((hx & 0xfffff) | (int)LowWord(x)) != 0) return x + x; // NaN
                    return xsb == 0 ? x : 0.0; // exp(+-inf) = {inf, 0}
                }
                if (x > oThreshold) return double.PositiveInfinity;
                if (x < uThreshold) return 0.0;
            }
            if (hx > 0x3fd62e42)
            {
                // |x| > 0.5 ln2
                if (hx < 0x3ff0a2b2)
                {
                    // and |x| < 1.5 ln2
                    if (x == 1.0) return e;
                    hi = x - (xsb == 0 ? 6.93147180369123816490e-01 : -6.93147180369123816490e-01);
                    lo = xsb == 0 ? 1.90821492927058770002e-10 : -1.90821492927058770002e-10;
                    k = 1 - xsb - xsb;
                }
                else
                {
                    k = (int)(invln2 * x + (xsb == 0 ? 0.5 : -0.5));
                    double t = k;
                    hi = x - t * 6.93147180369123816490e-01;
                    lo = t * 1.90821492927058770002e-10;
                }
                x = hi - lo;
            }
            else if (hx < 0x3e300000) return 1.0 + x; // |x| < 2^-28
            double tt = x * x;
            double twopk = BitConverter.Int64BitsToDouble((long)(k >= -1021 ? 0x3ff00000 + (k << 20) : 0x3ff00000 + ((k + 1000) << 20)) << 32);
            double c = x - tt * (p1 + tt * (p2 + tt * (p3 + tt * (p4 + tt * p5))));
            if (k == 0) return 1.0 - ((x * c) / (c - 2.0) - x);
            double y = 1.0 - ((lo - (x * c) / (2.0 - c)) - hi);
            if (k >= -1021) return k == 1024 ? y * 2.0 * Two1023 : y * twopk;
            return y * twopk * twom1000;
        }

        public static int ToInt32(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
            return unchecked((int)(uint)(long)Math.Truncate(x % 4294967296.0));
        }

        public static uint Hash(params int[] ns)
        {
            uint h = 0x9e3779b9;
            foreach (int n in ns)
            {
                h = unchecked((h ^ (uint)n) * 0x85ebca6b);
                h ^= h >> 13;
                h = unchecked(h * 0xc2b2ae35);
                h ^= h >> 16;
            }
            return h;
        }

        // JS hash() takes any numbers and truncates each with n | 0.
        public static uint Hash(params double[] ns)
        {
            uint h = 0x9e3779b9;
            foreach (double n in ns)
            {
                h = unchecked((h ^ (uint)ToInt32(n)) * 0x85ebca6b);
                h ^= h >> 13;
                h = unchecked(h * 0xc2b2ae35);
                h ^= h >> 16;
            }
            return h;
        }

        public static double Rand(params int[] ns) => Hash(ns) / 4294967296.0;
        public static double Rand(params double[] ns) => Hash(ns) / 4294967296.0;
        public static Rng NewRng(params int[] ns) => new Rng(Hash(ns));
        public static Rng NewRng(params double[] ns) => new Rng(Hash(ns));

        // JS truthiness of a number: 0 and NaN are false.
        static bool Truthy(double v) => v != 0 && !double.IsNaN(v);

        /// <summary>Smooth 1D value noise in [-1, 1].</summary>
        public static double Noise(double x, double seed = 0)
        {
            double i = Math.Floor(x), f = x - i, u = f * f * (3 - 2 * f);
            return Lerp(Rand(i, seed), Rand(i + 1, seed), u) * 2 - 1;
        }

        /// <summary>Smooth 2D value noise in [-1, 1].</summary>
        public static double Noise2(double x, double y, double seed = 0)
        {
            double ix = Math.Floor(x), iy = Math.Floor(y), fx = x - ix, fy = y - iy;
            double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            double a = Rand(ix, iy, seed), b = Rand(ix + 1, iy, seed), c = Rand(ix, iy + 1, seed), d = Rand(ix + 1, iy + 1, seed);
            return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uy) * 2 - 1;
        }

        /// <summary>Fractal 2D noise in about [-1, 1].</summary>
        public static double Fbm(double x, double y, double seed = 0, double octaves = 3)
        {
            double s = 0, a = 0.5, n = 0;
            for (int o = 0; o < octaves; o++)
            {
                s += Noise2(x, y, seed + o * 101) * a;
                n += a;
                x *= 2.03;
                y *= 2.03;
                a *= 0.5;
            }
            return s / n;
        }

        /// <summary>2D value noise whose lattice repeats every px cells in x and py cells in y (0: no repeat).</summary>
        public static double TileNoise(double x, double y, double px = 0, double py = 0, double seed = 0)
        {
            double ix = Math.Floor(x), iy = Math.Floor(y), fx = x - ix, fy = y - iy;
            double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            double x0 = Truthy(px) ? Wrap(ix, px) : ix, x1 = Truthy(px) ? Wrap(ix + 1, px) : ix + 1;
            double y0 = Truthy(py) ? Wrap(iy, py) : iy, y1 = Truthy(py) ? Wrap(iy + 1, py) : iy + 1;
            double a = Rand(x0, y0, seed), b = Rand(x1, y0, seed), c = Rand(x0, y1, seed), d = Rand(x1, y1, seed);
            return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uy) * 2 - 1;
        }

        /// <summary>Fractal TileNoise: every octave keeps the repeat, so scrolled fields still loop.</summary>
        public static double TileFbm(double x, double y, double px = 0, double py = 0, double seed = 0, double octaves = 3)
        {
            double s = 0, a = 0.5, n = 0, k = 1;
            for (int o = 0; o < octaves; o++)
            {
                s += TileNoise(x * k, y * k, px * k, py * k, seed + o * 101) * a;
                n += a;
                a *= 0.5;
                k *= 2;
            }
            return s / n;
        }

        /// <summary>Noise that loops: the same value at phase 0 and phase 1.</summary>
        public static double LoopNoise(double phase, double radius = 1, double seed = 0)
        {
            double a = phase * TAU;
            return Noise2(Px.Cos(a) * radius + 50, Px.Sin(a) * radius + 50, seed);
        }

        // ---- particles.js: particles without a simulation, closed-form in their age.

        /// <summary>Where a particle is after t seconds, launched from (x, y) at (vx, vy) px/s, under gravity g and linear drag.</summary>
        public static (double x, double y) Fly(double x, double y, double vx, double vy, double t, double g = 0, double drag = 0)
        {
            if (drag <= 0) return (x + vx * t, y + vy * t + 0.5 * g * t * t);
            double f = (1 - Exp(-drag * t)) / drag, vt = g / drag;
            return (x + vx * f, y + vt * t + (vy - vt) * f);
        }

        /// <summary>Its velocity at t.</summary>
        public static (double x, double y) FlyV(double vx, double vy, double t, double g = 0, double drag = 0)
        {
            if (drag <= 0) return (vx, vy + g * t);
            double e = Exp(-drag * t), vt = g / drag;
            return (vx * e, vt + (vy - vt) * e);
        }

        /// <summary>n one-shot particles: fn(r, i) with a repeatable rng per particle.</summary>
        public static void Burst(double n, double seed, Action<Rng, int> fn)
        {
            for (int i = 0; i < n; i++) fn(NewRng(seed, i), i);
        }

        /// <summary>Particles that repeat every period seconds: fn(age, life, r, i) for every live copy.</summary>
        public static void Cycle(double t, double period, double count, double seed, double life, Action<double, double, Rng, int> fn) =>
            Cycle(t, period, count, seed, null, life, fn);

        public static void Cycle(double t, double period, double count, double seed, Func<Rng, double> life, Action<double, double, Rng, int> fn) =>
            Cycle(t, period, count, seed, life, 0, fn);

        static void Cycle(double t, double period, double count, double seed, Func<Rng, double> lifeFn, double life, Action<double, double, Rng, int> fn)
        {
            for (int i = 0; i < count; i++)
            {
                Rng g = NewRng(seed, i, 911);
                double born = g.Next() * period;
                double l = lifeFn != null ? lifeFn(g) : life;
                double age = (((t - born) % period) + period) % period;
                for (; age < l; age += period) fn(age, l, NewRng(seed, i), i);
            }
        }

        /// <summary>Particles born at a steady rate between t0 and t1: fn(age, life, r, i) for every particle alive at t.</summary>
        public static void Stream(double t, double t0, double t1, double rate, double seed, double life, Action<double, double, Rng, int> fn) =>
            Stream(t, t0, t1, rate, seed, null, life, fn);

        public static void Stream(double t, double t0, double t1, double rate, double seed, Func<Rng, double> life, Action<double, double, Rng, int> fn) =>
            Stream(t, t0, t1, rate, seed, life, 0, fn);

        static void Stream(double t, double t0, double t1, double rate, double seed, Func<Rng, double> lifeFn, double life, Action<double, double, Rng, int> fn)
        {
            double n = Math.Floor((Math.Min(t, t1) - t0) * rate);
            for (int i = 0; i <= n; i++)
            {
                double born = t0 + i / rate;
                Rng g = NewRng(seed, i, 911);
                double l = lifeFn != null ? lifeFn(g) : life;
                double age = t - born;
                if (age >= 0 && age < l) fn(age, l, NewRng(seed, i), i);
            }
        }

        public static class Ease
        {
            public static double Linear(double t) => t;
            public static double InQuad(double t) => t * t;
            public static double OutQuad(double t) => 1 - (1 - t) * (1 - t);
            public static double InOutQuad(double t) => t < 0.5 ? 2 * t * t : 1 - Px.Pow(-2 * t + 2, 2) / 2;
            public static double InCubic(double t) => t * t * t;
            public static double OutCubic(double t) => 1 - Px.Pow(1 - t, 3);
            public static double InOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Px.Pow(-2 * t + 2, 3) / 2;
            public static double InQuart(double t) => t * t * t * t;
            public static double OutQuart(double t) => 1 - Px.Pow(1 - t, 4);
            public static double InExpo(double t) => t <= 0 ? 0 : Px.Pow(2, 10 * t - 10);
            public static double OutExpo(double t) => t >= 1 ? 1 : 1 - Px.Pow(2, -10 * t);
            public static double InSine(double t) => 1 - Px.Cos(t * Math.PI / 2);
            public static double OutSine(double t) => Px.Sin(t * Math.PI / 2);
            public static double InOutSine(double t) => -(Px.Cos(Math.PI * t) - 1) / 2;
            public static double InBack(double t, double s = 1.70158) => t <= 0 ? 0 : t >= 1 ? 1 : (s + 1) * t * t * t - s * t * t;
            public static double OutBack(double t, double s = 1.70158) => t <= 0 ? 0 : t >= 1 ? 1 : 1 + (s + 1) * Px.Pow(t - 1, 3) + s * Px.Pow(t - 1, 2);
            public static double OutElastic(double t) => t <= 0 ? 0 : t >= 1 ? 1 : Px.Pow(2, -10 * t) * Px.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1;

            public static double OutBounce(double t)
            {
                const double n = 7.5625, d = 2.75;
                if (t < 1 / d) return n * t * t;
                if (t < 2 / d) return n * (t -= 1.5 / d) * t + 0.75;
                if (t < 2.5 / d) return n * (t -= 2.25 / d) * t + 0.9375;
                return n * (t -= 2.625 / d) * t + 0.984375;
            }
        }

        public static uint Rgb(double r, double g, double b) =>
            0xff000000u | ((uint)(ToInt32(b) & 255) << 16) | ((uint)(ToInt32(g) & 255) << 8) | (uint)(ToInt32(r) & 255);

        public static uint Red(uint c) => c & 255;
        public static uint Green(uint c) => (c >> 8) & 255;
        public static uint Blue(uint c) => (c >> 16) & 255;

        public static uint Hex(string h)
        {
            string s = h.Replace("#", "");
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            int v = Convert.ToInt32(s.Substring(0, 6), 16);
            return Rgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
        }

        public static string ToHex(uint c) => $"#{Red(c):x2}{Green(c):x2}{Blue(c):x2}";

        public static uint Mix(uint a, uint b, double t)
        {
            t = Clamp(t);
            return Rgb(Red(a) + ((double)Red(b) - Red(a)) * t, Green(a) + ((double)Green(b) - Green(a)) * t, Blue(a) + ((double)Blue(b) - Blue(a)) * t);
        }

        public static uint Scale(uint c, double k) => Rgb(Math.Min(255, Red(c) * k), Math.Min(255, Green(c) * k), Math.Min(255, Blue(c) * k));
        public static double Luma(uint c) => (0.299 * Red(c) + 0.587 * Green(c) + 0.114 * Blue(c)) / 255;

        public static uint[] Ramp(params string[] list) => Array.ConvertAll(list, Hex);

        public static uint[] Fit(string[] r, int n) => Fit(Ramp(r), n);

        public static uint[] Fit(uint[] r, int n)
        {
            if (r.Length == n) return (uint[])r.Clone();
            var o = new uint[n];
            for (int i = 0; i < n; i++) o[i] = r[RoundInt(i * (r.Length - 1) / (double)Math.Max(1, n - 1))];
            return o;
        }

        public static uint At(uint[] r, double t) => r[(int)Clamp(Math.Floor(t * r.Length), 0, r.Length - 1)];

        public static uint Blend(uint[] r, double t)
        {
            double x = Clamp(t) * (r.Length - 1);
            int i = Math.Min(r.Length - 2, (int)Math.Floor(x));
            return Mix(r[i], r[i + 1], x - i);
        }

        // Hue-shifted ramps, dark to bright: shadows lean cool or deep, highlights lean warm or white.
        public static readonly IReadOnlyDictionary<string, string[]> PAL = new Dictionary<string, string[]>
        {
            ["fire"] = new[] { "#1f0b10", "#4a1118", "#8c1d1c", "#cc3a1a", "#f2701f", "#ffae35", "#ffe27a", "#fff8d6" },
            ["ember"] = new[] { "#1a0a0e", "#3d1012", "#701a14", "#a8301a", "#e0561e", "#ff8a2a", "#ffc46a" },
            ["lava"] = new[] { "#14060a", "#380a10", "#6e1210", "#ac2410", "#e8501a", "#ff8c2a", "#ffd25a" },
            ["smoke"] = new[] { "#0e0c12", "#1d1a22", "#2e2a35", "#433e4b", "#5c5664", "#7b7484", "#a39cab" },
            ["dust"] = new[] { "#1c1610", "#33281c", "#4f3f2c", "#6e5a40", "#927a58", "#b89f78", "#dcc8a2" },
            ["ice"] = new[] { "#0c1430", "#162c5c", "#1f4f94", "#2f7fcc", "#58b4ec", "#9bdcff", "#d8f4ff", "#ffffff" },
            ["water"] = new[] { "#081830", "#0e2e5a", "#14508c", "#1c76bc", "#36a2e0", "#78ccf4", "#c4ecff", "#ffffff" },
            ["electric"] = new[] { "#0a0f2e", "#172a78", "#2550d0", "#3f8cff", "#7cc8ff", "#c8f0ff", "#ffffff" },
            ["volt"] = new[] { "#24160a", "#5e3a0c", "#a8680f", "#e8a218", "#ffd23c", "#fff08a", "#ffffff" },
            ["poison"] = new[] { "#0d1a0c", "#173a14", "#265e18", "#3f8a1e", "#6cb82a", "#a8e04a", "#e0ff8a" },
            ["holy"] = new[] { "#2a1c08", "#5a3e10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6", "#ffffff" },
            ["heal"] = new[] { "#08201a", "#0f3f2c", "#17663e", "#24924e", "#3ec466", "#7ee88a", "#c8ffc4", "#ffffff" },
            ["arcane"] = new[] { "#140a26", "#2b1252", "#481c88", "#6b2cc0", "#9a48ec", "#c684ff", "#ecc8ff", "#ffffff" },
            ["shadow"] = new[] { "#050308", "#0f0a18", "#1c1230", "#2c1a4a", "#432468", "#5e3290", "#8048b8", "#a970e0" },
            ["blood"] = new[] { "#14040a", "#3a0a14", "#6a101c", "#a01824", "#d42a30", "#ff5a50", "#ff9a8a" },
            ["earth"] = new[] { "#140e0a", "#2a1e14", "#47321e", "#69492a", "#8e6638", "#b88a52", "#dcb47c" },
            ["stone"] = new[] { "#0e0e12", "#1c1c24", "#2c2c38", "#42424f", "#5c5c6b", "#7d7d8c", "#a4a4b2", "#d0d0da" },
            ["gold"] = new[] { "#1e1206", "#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff" },
            ["leaf"] = new[] { "#0c1a0e", "#163418", "#225224", "#357530", "#4f9a3a", "#7cc048", "#b4e070", "#e4ffb0" },
            ["wood"] = new[] { "#160c06", "#2e1a0c", "#4a2c14", "#6a421e", "#8e5c2c", "#b47c40", "#d8a464" },
            ["metal"] = new[] { "#0c0e14", "#1c2230", "#2e3648", "#4a5468", "#6e7a90", "#9aa6ba", "#d0d8e6", "#ffffff" },
            ["bone"] = new[] { "#1a1814", "#3a352c", "#5e5648", "#8a806c", "#b8ad96", "#e4dcc8", "#fffaf0" },
            ["neon"] = new[] { "#1a0516", "#3d0a33", "#701560", "#b0228f", "#f53cd1", "#ff86e6", "#ffd2f6", "#ffffff" },
            ["cyber"] = new[] { "#041418", "#08303a", "#0c5260", "#108088", "#18b4b0", "#4ce8d8", "#b4fff0", "#ffffff" },
            ["matrix"] = new[] { "#020a04", "#05200c", "#0a3c16", "#106624", "#1a9a34", "#3cd650", "#a4ffa8", "#e8ffe8" },
            ["candy"] = new[] { "#ff4a5a", "#ff9a2a", "#ffe03c", "#5ad84a", "#3cc8ff", "#7a6cff", "#f53cd1" },
            ["night"] = new[] { "#07060d", "#0c0a18", "#131026", "#1b1734", "#241f44", "#2f2956" },
        };

        /// <summary>The same ramps as color numbers.</summary>
        public static readonly IReadOnlyDictionary<string, uint[]> RAMP = RampsOf(PAL);

        static IReadOnlyDictionary<string, uint[]> RampsOf(IReadOnlyDictionary<string, string[]> pal)
        {
            var o = new Dictionary<string, uint[]>();
            foreach (KeyValuePair<string, string[]> e in pal) o[e.Key] = Ramp(e.Value);
            return o;
        }

        public const uint INK = 0xff100a0b; // hex("#0b0a10")
        public const uint PATCH = 0xffd13cf5; // hex("#f53cd1")
    }

    public sealed class Rng
    {
        uint s;

        public Rng(double seed = 1)
        {
            s = unchecked((uint)Px.ToInt32(seed));
            if (s == 0) s = 1;
        }

        public Rng(uint seed) => s = seed == 0 ? 1 : seed;

        public double Next()
        {
            unchecked
            {
                uint t = s += 0x6d2b79f5;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public double Range(double a, double b) => a + (b - a) * Next();
        public int Int(int a, int b) => (int)Math.Floor(Range(a, b + 1));
        public bool Chance(double p) => Next() < p;
        public int Sign() => Next() < 0.5 ? -1 : 1;
        public T Pick<T>(T[] list) => list[(int)Math.Floor(Next() * list.Length)];
        public double Gauss() => (Next() + Next() + Next() + Next() - 2) * 1.732;
    }
}
