// The firefly (a wandering point that glows up and fades on its own rhythm, close ones drawn with a dark body,
// beating wings and a lantern that throws a big soft light, grass blades swaying underneath) is adapted from
// Sprite Fusion's open-source fireflies (https://www.spritefusion.com/pixel-art-vfx/fireflies), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Fireflies : Effect
    {
        public static readonly Module Module = new Module("fireflies", "pixelfx/fx/weather/fireflies", 320, 180, 160, 170, 12, true, 30,
            new Options
            {
                ["palette"] = new[] { "#2a3a10", "#5a7a1a", "#9ad02a", "#d6ff5a", "#f4ffb8", "#ffffff" },
                ["grass"] = new[] { "#070f0b", "#0c1a12", "#13281a", "#1d3a24", "#2b5232", "#3f6e40" },
                ["seed"] = 2,
            },
            (o, e) => new Fireflies(o))
        {
            Title = "Fireflies",
            Desc = "Fireflies wander over a meadow at night, each glowing up and fading on its own rhythm, trailing light and lighting the grass tips beneath them.",
            Tags = new[] { "weather", "fireflies", "night", "glow", "meadow", "nature", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 12;
        static double Cyc(double t, double n, double ph = 0) => (TAU * n * t) / D + ph;
        static readonly double[] DEPTH_K = { 0.5, 0.8, 1 };
        static readonly int[][] PLUS = { new[] { 0, -2 }, new[] { 0, 2 }, new[] { -2, 0 }, new[] { 2, 0 } };

        // a firefly's path and glow, both whole cycles a loop. depth 0 far .. 2 near
        sealed class Fly
        {
            public Func<double, (double x, double y)> pos;
            public int depth;
            public double glow;
        }

        static double Pulse(double v) => v < 0.06 ? v / 0.06 : v < 0.14 ? 1 : v < 0.4 ? 1 - (v - 0.14) / 0.26 : 0;

        static Fly MakeFly(double S, int i, double t)
        {
            double R(double n) => Rand(S, i, n);
            int depth = i < 14 ? 0 : i < 26 ? 1 : 2;
            double k = DEPTH_K[depth];
            double cx = -150 + R(1) * 300, cy = -16 - R(2) * (depth == 2 ? 70 : 130);
            double ax = (14 + R(3) * 26) * k, ay = (8 + R(4) * 14) * k, n1 = 1 + Math.Floor(R(5) * 2), n2 = 1 + Math.Floor(R(6) * 3);
            (double, double) Pos(double tt) => (
                cx + ax * Px.Sin(Cyc(tt, n1, R(7) * TAU)) + 10 * k * LoopNoise(tt / D, 1.5, i * 3),
                cy + ay * Px.Sin(Cyc(tt, n2, R(8) * TAU)) + 6 * k * LoopNoise(tt / D, 1.5, i * 3 + 1));
            // its rhythm: a quick rise, a hold, a slow fade, dark; some flash twice
            double per = D / (3 + Math.Floor(R(9) * 4)), u = (((t / per + R(10)) % 1) + 1) % 1;
            bool two = R(11) < 0.35;
            double glow = Math.Max(Pulse(u), two ? Pulse(u - 0.22) * 0.8 : 0);
            return new Fly { pos = Pos, depth = depth, glow = glow };
        }

        static Fly[] Flies(double S, double t)
        {
            var f = new Fly[32];
            for (int i = 0; i < 32; i++) f[i] = MakeFly(S, i, t);
            return f;
        }

        readonly Options o;

        Fireflies(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 6), G = Fit(o.Ramp("grass"), 6);
            // a cool night haze, so the sky's stars don't read as fireflies
            Rows(p, Module, -170, 9, (y, R) =>
            {
                double k = (y + 170) / 180;
                Row.Fill(R.c, Mix(G[0], G[1], k));
                Row.Fill(R.a, 0.48 + 0.14 * k);
                Row.Fill(R.b, 1);
            });
            Fly[] flies = Flies(S, t);
            Grass(q, t, S, G, C, flies, false);
            foreach (Fly f in flies)
                if (f.depth < 2) Firefly(q, t, f, C);
        }

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 6), G = Fit(o.Ramp("grass"), 6);
            Fly[] flies = Flies(S, t);
            Grass(q, t, S, G, C, flies, true);
            foreach (Fly f in flies)
                if (f.depth == 2) Firefly(q, t, f, C);
        }

        static void Firefly(Clip q, double t, Fly f, uint[] C)
        {
            var (x, y) = f.pos(t);
            double g = f.glow, X = Round(x), Y = Round(y);
            if (f.depth == 0)
            {
                if (g < 0.08) return;
                q.Dot(X, Y, g > 0.6 ? C[4] : C[3], Math.Min(1, g * 1.6));
                q.Glow(x, y, 6, C[2], 0.45 * g);
                return;
            }
            // a short trail of light where it has just been, while it's lit
            if (g > 0.3)
                for (int j = 1; j <= 4; j++)
                {
                    var (tx, ty) = f.pos(t - j * 0.05);
                    q.Dot(tx, ty, C[2], (g - 0.3) * (1 - j / 5.0) * 1.3);
                }
            if (f.depth == 1)
            {
                q.Px(X, Y, g > 0.5 ? C[5] : g > 0.15 ? C[3] : C[1]);
                if (g > 0.4) q.Px(X, Y + 1, C[2]);
                q.Glow(x, y, 9, C[2], 0.5 * g);
                q.Glow(x, y, 3, C[4], 0.5 * g);
                return;
            }
            // a close one: a dark body, wings a blur above it, a lantern at its tail; a glint at the peak
            double nx = f.pos(t + 0.05).x, dir = nx >= x ? 1 : -1, beat = Math.Floor(t * 30) % 2;
            q.Px(X + dir, Y, C[0]);
            q.Px(X + dir * 2, Y, C[0]);
            q.Dot(X, Y - 1 - beat, Mix(C[1], WHITE, 0.4), 0.7);
            q.Dot(X + dir, Y - 1 - beat, Mix(C[1], WHITE, 0.4), 0.7);
            q.Px(X, Y, g > 0.3 ? C[5] : C[1]);
            q.Px(X - dir, Y, g > 0.5 ? C[4] : g > 0.15 ? C[3] : C[1]);
            if (g > 0.6) q.Px(X - dir, Y + 1, C[3]);
            if (g > 0.7)
                foreach (int[] v in PLUS)
                    q.Dot(X - dir + v[0], Y + v[1], C[3], (g - 0.7) * 3);
            q.Glow(x, y, 28, C[2], 0.45 * g);
            q.Glow(x, y, 10, C[4], 0.5 * g);
            q.Glow(x, y, 4, C[5], 0.5 * g);
        }

        // the meadow: blades in two rows (the front row drawn over the low fireflies), swaying as the breeze rolls
        // across, a few seed heads; blade tips near a lit firefly catch its light
        static void Grass(Clip q, double t, double S, uint[] G, uint[] C, Fly[] flies, bool front)
        {
            var lit = new List<double[]>();
            foreach (Fly f in flies)
                if (f.glow > 0.25 && f.depth > 0)
                {
                    var (fx, fy) = f.pos(t);
                    lit.Add(new[] { fx, fy, f.glow });
                }
            int n = front ? 150 : 230;
            double sd = S * 3 + (front ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                double R(double k) => Rand(sd, i, k);
                double x = -162 + (i + R(1)) * (324.0 / n), h = (front ? 4 : 5) + R(2) * (front ? 8 : 11) + (R(3) < 0.1 ? 6 : 0);
                double bse = (front ? 9 : 2) - R(4) * 3, sway = (1.2 + R(5)) * Px.Sin(Cyc(t, 2, -x * 0.03) + R(6)) + 0.6 * Px.Sin(Cyc(t, 5, x * 0.07));
                int tone = front ? 1 + (int)Math.Floor(R(7) * 3) : 1 + (int)Math.Floor(R(7) * 2);
                double tipLight = 0;
                foreach (double[] l in lit)
                {
                    double d = Hypot(l[0] - (x + sway), l[1] - (bse - h));
                    if (d < 28) tipLight = Math.Max(tipLight, l[2] * (1 - d / 28));
                }
                for (int r = 0; r < h; r++)
                {
                    double u = r / h;
                    uint c = u > 0.8 && tipLight > 0.2 ? (tipLight > 0.55 && u > 0.9 ? C[3] : C[2]) : u > 0.7 ? G[Math.Min(5, tone + 1)] : G[tone];
                    q.Px(x + sway * u * u, bse - r, c);
                }
                if (R(8) < 0.07)
                {
                    double sx = x + sway, sy = bse - h;
                    q.Px(sx, sy - 1, tipLight > 0.2 ? C[3] : G[5]);
                    q.Px(sx - 1, sy, G[4]);
                    q.Px(sx + 1, sy, G[4]);
                }
            }
        }
    }
}
