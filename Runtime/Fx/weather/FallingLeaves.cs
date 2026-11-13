// The falling leaf (a pendulum swing that speeds through the bottom of each arc, a tilt that follows it, a flip
// by squashing across, a rest on the ground, a litter of fallen leaves) is adapted from Sprite Fusion's
// open-source falling leaves (https://www.spritefusion.com/pixel-art-vfx/falling-leaves), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class FallingLeaves : Effect
    {
        public static readonly Module Module = new Module("falling-leaves", "pixelfx/fx/weather/falling-leaves", 320, 180, 160, 170, 10, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1d0c", "#8a3a14", "#c8621e", "#f0982e", "#ffcf72", "#fff0c4" },
                ["reds"] = new[] { "#40101a", "#7c1c22", "#b8302a", "#e85a3c", "#ff9a72", "#ffd8c4" },
                ["golds"] = new[] { "#3e300c", "#7a5c14", "#b8901e", "#e8c23a", "#fff09a", "#fffbe0" },
                ["seed"] = 8,
            },
            (o, e) => new FallingLeaves(o))
        {
            Title = "Falling leaves",
            Desc = "Autumn leaves swing down through the air, tilting and flipping to show their pale backs, ride a gust, land flat in the litter and fade.",
            Tags = new[] { "weather", "leaves", "autumn", "fall", "wind", "nature", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 10, FLOOR = 1;
        static double Gust(double t) => 16 * Px.Sin((TAU * t) / D - 1.2) + 5 * Px.Sin((TAU * 3 * t) / D);

        // ∫|cos| from the start of a swing: speeds up through the bottom of each arc, so the drop does too
        static double Hc(double th)
        {
            double k = Math.Floor((th + Math.PI / 2) / Math.PI);
            return 2 * k + 1 + Px.Sin(th - k * Math.PI);
        }

        // a leaf's outline in its own frame: u along it (-1 stem .. 1 tip), v across; 0 inside, > 1 outside
        static readonly Func<double, double, double>[] SHAPES =
        {
            (u, v) => Math.Abs(u) >= 1 ? 9 : (v * v) / (0.36 * (1 - u * u) * (1.15 - 0.3 * u)), // a beech: a long oval, pointed tip
            (u, v) =>
            {
                double r = Hypot(u, v * 1.1), a = Atan2(v, u);
                return r / (0.62 + 0.38 * Px.Pow(Math.Abs(Px.Cos(2.5 * a)), 1.5)); // a maple: five lobes
            },
            (u, v) => Math.Abs(u) >= 1 ? 9 : (v * v) / (0.16 * (1 - u * u)), // a willow: long and thin
        };

        readonly Options o;

        FallingLeaves(Options o) => this.o = o;

        static uint[][] Ramps(Options o) => new[] { Fit(o.Ramp("palette"), 6), Fit(o.Ramp("reds"), 6), Fit(o.Ramp("golds"), 6) };

        public override void DrawBack(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[][] R3 = Ramps(o);
            uint[] C = R3[0];
            // a faint dusk haze, warmer low down, that keeps the sky's stars out of the leaves' way
            uint top = Hex("#0e0a12"), low = Mix(Hex("#140c14"), C[0], 0.3);
            Rows(p, Module, -170, 9, (y, R) =>
            {
                double k = (y + 170) / 180;
                uint c = Mix(top, low, k * k);
                Row.Fill(R.c, c);
                Row.Fill(R.a, 0.32 + 0.1 * k);
                Row.Fill(R.b, 1);
            });
            Litter(q, S, R3);
            Leaves(q, t, S, R3, false);
        }

        public override void Draw(Painter p, double t) => Leaves(new Clip(p, Module), t, o.Seed, Ramps(o), true);

        static void Leaves(Clip q, double t, double S, uint[][] R3, bool front)
        {
            Trips(t, D, 62, S * 19, 2, 3, (u, k, m, i) =>
            {
                double j = Wrap(k, m);
                double R(double n) => Rand(S * 19, i, j, n);
                bool near = R(1) < 0.35;
                if (near != front) return;
                double dur = D / m, air = 0.72, tf = dur * air, a = Math.Min(u * dur, tf), rest = u * dur - tf;
                double w = 1.7 + R(2) * 1.2, ph0 = R(3) * TAU, amp = (near ? 14 : 8) + R(4) * 10, x0 = -180 + R(5) * 340, y0 = -186;
                // the drop speed that lands it on the floor just as its time in the air runs out
                double Path(double aa) => 0.45 * aa + (0.9 * (Hc(w * aa + ph0) - Hc(ph0))) / w;
                double vy = (FLOOR - y0) / Path(tf);
                double born = t - u * dur;
                double XAt(double aa) => x0 + 7 * aa + Px.Sin(w * aa + ph0) * amp + (Gust(born + aa) - Gust(born)) * (near ? 1 : 0.7);
                double size = (near ? 5.8 : 4.2) + R(6) * 1.6;
                int shape = (int)Math.Floor(R(7) * 3);
                uint[] col = R3[(int)Math.Floor(R(8) * 3)];
                if (rest > 0)
                {
                    // lying flat in the litter, fading
                    double fade = 1 - rest / (dur - tf);
                    Leaf(q.Shade, XAt(tf), FLOOR - 1, size, (R(9) - 0.5) * 0.5, 0.42, shape, col, fade);
                    return;
                }
                double x = XAt(a), y = y0 + vy * Path(a), tilt = Px.Cos(w * a + ph0) * 0.75 + (near ? 0 : 0.3);
                double flip = Px.Cos(R(10) * TAU + a * (R(11) < 0.3 ? 5 : 1.2));
                Leaf(q.Shade, x, y, size, tilt, flip, shape, col, 1);
                // a leaf turned full face to the light catches it for a moment
                if (flip > 0.86)
                {
                    double kk = (flip - 0.86) / 0.14, hx = x - Px.Cos(tilt) * size * 0.3, hy = y - Px.Sin(tilt) * size * 0.3 - 1;
                    q.Px(hx, hy, col[5]);
                    q.Glow(hx, hy, 5 + size * 0.5, col[4], 0.22 * kk);
                }
            });
        }

        // draw a leaf: squash it across by `flip` (the sign says which face shows), tilt it, outline it, a midrib down
        // the middle, the face lit on one side; the back face is paler and duller
        static void Leaf(Shader shade, double x, double y, double size, double ang, double flip, int shape, uint[] col, double a)
        {
            // like a card turning: never thinner than a third, and the back shows only past edge-on
            bool back = flip < -0.25;
            double fs = 0.35 + 0.65 * Math.Abs(flip), c = Px.Cos(ang), s = Px.Sin(ang);
            double L = size, Wd = size * 0.9, ext = L + 1.5;
            Func<double, double, double> shapeFn = SHAPES[shape];
            uint[] face = back
                ? new[] { Mix(col[1], Hex("#9c8a78"), 0.2), Mix(col[2], Hex("#c8b49c"), 0.22), Mix(col[3], Hex("#e0d0b8"), 0.22) }
                : new[] { col[2], col[3], col[4] };
            uint rim = col[0], vein = back ? Mix(col[2], Hex("#e8dcc8"), 0.5) : col[1];
            shade(x - ext, y - ext, x + ext, y + ext, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, lu = (dx * c + dy * s) / L, lv = (-dx * s + dy * c) / (Wd * fs);
                double d = shapeFn(lu, lv);
                if (lu < -1.25 || lu > 1.1) return 0;
                // the stem pokes out past the base
                if (lu < -0.92 && Math.Abs(lv * Wd * fs) < 0.5)
                {
                    o.c = rim;
                    o.a = a;
                    return 0;
                }
                if (d > 1.32) return 0;
                o.a = a;
                if (d > 1) o.c = rim;
                else if (Math.Abs(lv * Wd * fs) < 0.45 && lu > -0.9 && lu < 0.75) o.c = vein;
                else o.c = lv < -0.15 ? face[2] : lv < 0.35 ? face[1] : face[0];
                return 0;
            });
        }

        // fallen leaves lying in a band along the floor, overlapping. They never move, so they're shaded once per seed
        // and palette and the pixels replayed
        static readonly Dictionary<string, List<double>> LITTER = new Dictionary<string, List<double>>();

        static void Litter(Clip q, double S, uint[][] R3)
        {
            string key = S.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + string.Join(",", Array.ConvertAll(R3, r => string.Join(",", r)));
            if (!LITTER.TryGetValue(key, out List<double> px))
            {
                px = new List<double>();
                Shader rec = Recorder(px, true);
                for (int i = 0; i < 64; i++)
                {
                    double x = -164 + Rand(S, i, 91) * 328, y = FLOOR - 2 + Math.Floor(Rand(S, i, 92) * 4) * 2;
                    uint[] col = R3[(int)Math.Floor(Rand(S, i, 93) * 3)];
                    double size = 3.6 + Rand(S, i, 94) * 1.8, ang = (Rand(S, i, 95) - 0.5) * 0.7, flip = 0.45 + Rand(S, i, 96) * 0.25;
                    int shape = (int)Math.Floor(Rand(S, i, 97) * 3);
                    Leaf(rec, x, y, size, ang, flip, shape, Array.ConvertAll(col, c => Mix(c, col[0], 0.12)), 1);
                }
                LITTER[key] = px;
            }
            for (int i = 0; i < px.Count; i += 4) q.Dot(px[i], px[i + 1], (uint)px[i + 2], px[i + 3]);
        }
    }
}
