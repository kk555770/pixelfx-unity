using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class NecroSkulls : Effect
    {
        public static readonly Module Module = new Module("necro-skulls", "pixelfx/fx/shadow/necro-skulls", 136, 116, 68, 108, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#040e10", "#0a2228", "#0f3d44", "#16615f", "#23907f", "#45c4a0", "#9af0c8", "#e8fff4" },
                ["bone"] = new[] { "#0c1a1c", "#2c4a4a", "#5c847c", "#a6cdb8", "#e4fff0" },
                ["seed"] = 6,
            },
            (o, e) => new NecroSkulls(o))
        {
            Title = "Necro skulls",
            Desc = "Spectral skulls circle a cold green flame on a tilted orbit, passing behind and before it, eyes lit, trailing curls of ghost smoke.",
            Tags = new[] { "shadow", "necromancy", "skull", "orbit", "ghost", "spectral", "magic", "loop" },
            Bg = "ground",
        };

        // the flame's base and height; the orbit (centre, radii, tilt); six skulls, one turn a loop
        const double CY = -52, FB = -32, FH = 34, RX = 42, RY = 11, TILT = -0.12, D = 3;
        const int N = 5;

        static readonly string[] SKULL =
        {
            "...kkkkk...",
            ".kkhhhllkk.",
            "khhhhlllmmk",
            "khhllllllmk",
            "kleeeleeemk",
            "kleeeleeedk",
            "kmleelleedk",
            ".kmllnllmk.",
            ".kmmlnlmdk.",
            "..kmmmmmk..",
            "..ktgtgtk..",
            "...kkkkk...",
        };

        struct Place
        {
            public double x, y, z, a;
        }

        // a skull's place at t: round the tilted orbit, bobbing; z > 0 is in front of the flame
        static Place SkullAt(int i, double t)
        {
            double a = (TAU * t) / D + (i * TAU) / N, ex = Px.Cos(a) * RX, ey = Px.Sin(a) * RY;
            double bob = Px.Sin((TAU * 2 * t) / D + i * 1.7) * 2;
            return new Place { x = ex * Px.Cos(TILT) - ey * Px.Sin(TILT), y = CY + ex * Px.Sin(TILT) + ey * Px.Cos(TILT) + bob, z = Px.Sin(a), a = a };
        }

        // ghost smoke curling off behind each skull along its orbit: puffs that swell, rise and thin with age
        static void Tail(Painter p, uint[] C, int i, double t)
        {
            for (int k = 8; k >= 1; k--)
            {
                double tt = t - k * 0.05, age = k / 8.0;
                Place s = SkullAt(i, tt);
                double r = 2 + age * 3.6, a = (1 - age) * 0.5 * (0.6 + 0.4 * (s.z + 1) / 2);
                p.Disc(s.x, s.y - age * 7 + 2, r, age < 0.4 ? C[5] : age < 0.7 ? C[4] : C[3], a);
            }
        }

        static void DrawSkull(Painter p, uint[] C, uint[] K, Place s, double t, int i)
        {
            bool front = s.z >= 0;
            double depth = 0.55 + 0.45 * (s.z + 1) / 2;
            var PAL = front
                ? new Dictionary<char, uint> { ['k'] = K[0], ['d'] = K[1], ['m'] = K[2], ['l'] = K[3], ['h'] = K[4], ['e'] = C[0], ['n'] = C[0], ['t'] = K[3], ['g'] = K[0] }
                : new Dictionary<char, uint> { ['k'] = K[0], ['d'] = K[0], ['m'] = K[1], ['l'] = K[2], ['h'] = K[3], ['e'] = C[0], ['n'] = C[0], ['t'] = K[2], ['g'] = K[0] };
            double x0 = Round(s.x) - 5, y0 = Round(s.y) - 6;
            p.Sprite(SKULL, PAL, x0, y0);
            // eyes: cold points of light in the dark, flaring as the skull swings toward us
            double k = (0.5 + 0.5 * Px.Sin((TAU * 3 * t) / D + i * 2.1)) * 0.5 + 0.5;
            foreach (int ex in new[] { 3, 7 })
            {
                p.Px(x0 + ex + 0.5, y0 + 5.5, front ? (k > 0.75 ? WHITE : C[7]) : C[6]);
                if (front) p.Px(x0 + ex + 0.5, y0 + 4.5, C[5]);
                p.Glow(x0 + ex + 1, y0 + 6, 6, C[5], 0.45 * depth * k);
            }
            p.Glow(s.x, s.y, 11, C[4], 0.18 * depth);
        }

        // the cold flame: a body that swells off its base and tapers into licking tongues, white only low in its heart,
        // cooling outward and upward through the ghost greens, scrolling up (the noise wraps once a loop)
        static void Flame(Painter p, uint[] C, double t)
        {
            double W = 10, scroll = t / D;
            p.Shade(-W - 4, FB - FH - 4, W + 4, FB + 2, (x, y, o) =>
            {
                double u = (FB - y) / FH;
                if (u < -0.04 || u >= 1) return 0;
                double n1 = TileNoise(x * 0.3, y * 0.2 + scroll * 8, 0, 8, 3), n2 = TileNoise(x * 0.6, y * 0.45 + scroll * 16, 0, 16, 5);
                double prof = u < 0.22 ? 0.35 + 0.65 * Math.Sqrt(Math.Max(0, u) / 0.22) : Px.Pow(1 - (u - 0.22) / 0.78, 0.85);
                double w = W * prof * (1 + 0.3 * n1) + n2 * 2.2 * u, sway = n1 * 3 * u * u, d = Math.Abs(x - sway) / Math.Max(0.6, w);
                if (d > 1) return 0;
                double heat = Px.Pow(1 - d, 0.7) * Px.Pow(Math.Max(0, 1 - u), 0.55) + n2 * 0.12;
                if ((((int)Math.Floor(x) + (int)Math.Floor(y)) & 1) == 0) o.k = 0.16 * heat;
                return heat > 0.82 ? WHITE : heat > 0.66 ? C[7] : heat > 0.46 ? C[6] : heat > 0.27 ? C[5] : heat > 0.12 ? C[4] : C[3];
            });
            p.Glow(0, FB - FH * 0.35, 40, C[5], 0.45);
            p.Glow(0, FB - 5, 12, C[7], 0.25);
        }

        readonly uint[] C, K;
        readonly double S;

        NecroSkulls(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            K = Fit(o.Ramp("bone"), 5);
            S = o.Seed;
        }

        static (int i, Place s)[] Order(double t, bool front) =>
            Enumerable.Range(0, N).Select(i => (i, s: SkullAt(i, t))).Where(e => (e.s.z >= 0) == front).OrderBy(e => e.s.z).ToArray();

        public override void DrawBack(Painter p, double t)
        {
            p.Glow(0, -1, 40, C[4], 0.2);
            // the far half of the orbit: tails and skulls behind the flame, back to front
            var order = Order(t, false);
            foreach (var e in order) Tail(p, C, e.i, t);
            foreach (var e in order) DrawSkull(p, C, K, e.s, t, e.i);
        }

        public override void Draw(Painter p, double t)
        {
            // motes and cold sparks rising off the flame
            Cycle(t, D, 18, S, r => r.Range(0.8, 1.4), (age, L, r, i) =>
            {
                double x0 = r.Range(-6, 6), u = age / L, x = x0 + Px.Sin(u * 5 + r.Range(0, 6)) * 3 * u, y = FB - 6 - u * r.Range(36, 56);
                p.Dot(x, y, u < 0.5 ? C[7] : C[6], u < 0.7 ? 1 : (1 - u) * 3.3);
                p.Glow(x, y, 3, C[6], 0.3 * (1 - u));
            });
            Flame(p, C, t);
            // the near half of the orbit, back to front
            var order = Order(t, true);
            foreach (var e in order) Tail(p, C, e.i, t);
            foreach (var e in order) DrawSkull(p, C, K, e.s, t, e.i);
            Halo(p, 0, CY, 0, 46, C[4], 0.12);
        }
    }
}
