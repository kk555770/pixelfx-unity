using System;

namespace Pixelfx
{
    public static class Px
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

        public static double Bump(double t, double t0, double t1) => t <= t0 || t >= t1 ? 0 : Math.Sin(Math.PI * Seg(t, t0, t1));
        public static double Wrap(double x, double m) => ((x % m) + m) % m;

        public static int Floor(double x) => (int)Math.Floor(x);
        public static int Ceil(double x) => (int)Math.Ceiling(x);

        public static double Round(double x)
        {
            double r = Math.Floor(x);
            return x - r >= 0.5 ? r + 1 : r;
        }

        public static int RoundInt(double x) => (int)Round(x);

        // V8's Math.hypot: scaled by the largest, summed with Kahan compensation.
        public static double Hypot(double x, double y)
        {
            double ax = Math.Abs(x), ay = Math.Abs(y), max = Math.Max(ax, ay);
            if (double.IsInfinity(max)) return double.PositiveInfinity;
            if (max == 0 || double.IsNaN(max)) return max;
            double sum = 0, compensation = 0;
            foreach (double v in new[] { ax, ay })
            {
                double n = v / max, summand = n * n - compensation, preliminary = sum + summand;
                compensation = preliminary - sum - summand;
                sum = preliminary;
            }
            return Math.Sqrt(sum) * max;
        }

        public static int Sign(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

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

        public static double Rand(params int[] ns) => Hash(ns) / 4294967296.0;
        public static Rng NewRng(params int[] ns) => new Rng(Hash(ns));

        public static class Ease
        {
            public static double Linear(double t) => t;
            public static double InQuad(double t) => t * t;
            public static double OutQuad(double t) => 1 - (1 - t) * (1 - t);
            public static double InOutQuad(double t) => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
            public static double InCubic(double t) => t * t * t;
            public static double OutCubic(double t) => 1 - Math.Pow(1 - t, 3);
            public static double InOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            public static double InQuart(double t) => t * t * t * t;
            public static double OutQuart(double t) => 1 - Math.Pow(1 - t, 4);
            public static double InExpo(double t) => t <= 0 ? 0 : Math.Pow(2, 10 * t - 10);
            public static double OutExpo(double t) => t >= 1 ? 1 : 1 - Math.Pow(2, -10 * t);
            public static double InSine(double t) => 1 - Math.Cos(t * Math.PI / 2);
            public static double OutSine(double t) => Math.Sin(t * Math.PI / 2);
            public static double InOutSine(double t) => -(Math.Cos(Math.PI * t) - 1) / 2;
            public static double InBack(double t, double s = 1.70158) => t <= 0 ? 0 : t >= 1 ? 1 : (s + 1) * t * t * t - s * t * t;
            public static double OutBack(double t, double s = 1.70158) => t <= 0 ? 0 : t >= 1 ? 1 : 1 + (s + 1) * Math.Pow(t - 1, 3) + s * Math.Pow(t - 1, 2);
            public static double OutElastic(double t) => t <= 0 ? 0 : t >= 1 ? 1 : Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1;

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
