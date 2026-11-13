// The snow recipe (depth layers falling at their own speeds with a sway, a gusty wind, flakes that settle on a
// drift for a while) is adapted from Sprite Fusion's open-source snow (https://www.spritefusion.com/pixel-art-vfx/snow),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Snow : Effect
    {
        public static readonly Module Module = new Module("snow", "pixelfx/fx/weather/snow", 320, 180, 160, 170, 12, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0c1020", "#161d36", "#24304f", "#3a4a72", "#5f74a3", "#93abd6", "#cfe0fa", "#ffffff" },
                ["seed"] = 6,
            },
            (o, e) => new Snow(o))
        {
            Title = "Snow",
            Desc = "Snow at three depths in a gusty breeze: far specks, drifting flakes, big near crystals that spin and glow; they settle on a sparkling drift and melt away.",
            Tags = new[] { "weather", "snow", "winter", "cold", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 12;
        // the drift's top edge, and the gusts: a sway of the whole snowfall, periodic so the loop closes
        static double Bank(double x) => -3 + 1.6 * Px.Sin(x * 0.045 + 1.3) + 1.1 * Px.Sin(x * 0.12 + 0.4) + 0.6 * Px.Sin(x * 0.31 + 2);
        static double Gust(double t) => 10 * Px.Sin((TAU * t) / D + 0.6) + 4 * Px.Sin((TAU * 2 * t) / D + 2.1);

        sealed class Depth
        {
            public int n;
            public int[] m;
            public double air, wind, gust;
            public double[] sway;
        }

        // far to near: how many, falls a loop, share of a fall spent in the air (the rest lying on the drift), sway, wind
        static readonly Depth[] LAYERS =
        {
            new Depth { n = 230, m = new[] { 1, 2 }, air = 1, sway = new double[] { 1, 3 }, wind = 3, gust = 0.4 },
            new Depth { n = 140, m = new[] { 2, 3 }, air = 0.8, sway = new double[] { 3, 7 }, wind = 6, gust = 0.7 },
            new Depth { n = 24, m = new[] { 2, 3 }, air = 0.74, sway = new double[] { 8, 15 }, wind = 9, gust = 1 },
        };

        static readonly int[][] CROSS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        static readonly int[][] TOP3 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, -1 } };

        readonly Options o;

        Snow(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            // a cold haze, thicker low down, so the sky's stars don't read as snow
            uint haze = Mix(C[1], C[2], 0.4);
            Pass(p, Module, -170, 9, (x, y, w) =>
            {
                w.c = haze;
                w.a = 0.26 + 0.22 * Smooth(-170, 0, y);
                w.b = true;
            });
            DrawLayer(q, t, S, C, 0);
            Drift(q, t, S, C);
            DrawLayer(q, t, S, C, 1);
        }

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            DrawLayer(q, t, S, C, 2);
        }

        static void DrawLayer(Clip q, double t, double S, uint[] C, int L)
        {
            Depth Ly = LAYERS[L];
            double sd = S * 13 + L;
            Trips(t, D, Ly.n, sd, Ly.m[0], Ly.m[1], (u, k, m, i) =>
            {
                double j = Wrap(k, m), dur = D / m;
                double R(double n) => Rand(sd, i, j, n);
                double tf = dur * Ly.air, age = Math.Min(u * dur, tf), sw = Ly.sway[0] + R(3) * (Ly.sway[1] - Ly.sway[0]), f = 0.25 + R(4) * 0.35, ph = R(5) * TAU;
                double XAt(double a) => -175 + R(2) * 350 + Ly.wind * a + (Gust(t - u * dur + a) - Gust(t - u * dur)) * Ly.gust + sw * Px.Sin(TAU * f * a + ph);
                double x = XAt(age), xl = XAt(tf), y0 = -176, y1 = L != 0 ? Bank(xl) - 0.5 : -110 + R(1) * 106;
                double y = y0 + (y1 - y0) * (age / tf), rest = u * dur - tf;
                if (L == 0)
                {
                    // far: a speck, thinning out before it's gone
                    double a = Math.Min(1, (1 - u) * 6);
                    q.Dot(x, y, R(6) < 0.5 ? C[4] : C[5], a);
                    return;
                }
                double fade = rest > 0 ? 1 - rest / (dur - tf) : 1;
                if (L == 1)
                {
                    // a drifting flake: a point with a smaller one beside it, a plus when it's big
                    bool big = R(6) < 0.3;
                    if (rest > 0)
                    {
                        q.Dot(xl, y1, C[7], fade * 0.9);
                        return;
                    }
                    q.Px(x, y, big ? WHITE : C[7]);
                    if (big)
                        foreach (int[] v in CROSS)
                            q.Px(x + v[0], y + v[1], C[5]);
                    else
                    {
                        q.Px(x + 1, y, C[6]);
                        q.Px(x, y + 1, C[6]);
                        q.Px(x + 1, y + 1, C[5]);
                    }
                    return;
                }
                // a near crystal: six arms with side branches, spinning, a white heart, a cold glow; it lies down on the drift
                int r = R(6) < 0.4 ? 3 : R(6) < 0.8 ? 4 : 5;
                double spin = 1.5 + R(7) * 2.5;
                if (rest > 0)
                {
                    q.Dot(xl - 1, y1, C[6], fade);
                    q.Dot(xl, y1, WHITE, fade);
                    q.Dot(xl + 1, y1, C[6], fade);
                    q.Glow(xl, y1, 5, C[5], 0.25 * fade);
                    return;
                }
                Crystal(q, x, y, r, Math.Floor(age * spin + R(8) * 2) % 2 != 0, C);
            });
        }

        // a near crystal, kept crisp: long arms on one set of four directions, short ones on the other, nubs near the
        // tips; turning means trading which set is long. Plates are a ring round a bright heart.
        static readonly int[][] AXIS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } }, DIAG = { new[] { 1, 1 }, new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 } };

        static void Crystal(Clip q, double x, double y, int r, bool turn, uint[] C)
        {
            double X = Round(x), Y = Round(y);
            int[][] lng = turn ? DIAG : AXIS, sht = turn ? AXIS : DIAG;
            int R = turn ? r - 1 : r;
            // long arms with a white-blue tip; nubs only where they can't touch the next arm, so the gaps stay open
            foreach (int[] v in lng)
            {
                int dx = v[0], dy = v[1];
                for (int d = 1; d <= R; d++) q.Px(X + dx * d, Y + dy * d, d == R ? WHITE : C[6]);
                int[] nubs = turn ? (R >= 3 ? new[] { R - 1 } : new int[0]) : R >= 5 ? new[] { R - 1, R - 2 } : R >= 4 ? new[] { R - 1 } : new int[0];
                foreach (int n in nubs)
                {
                    int ox = turn ? dx : dy, oy = turn ? -dy : -dx;
                    q.Px(X + dx * n + (turn ? 0 : ox), Y + dy * n + (turn ? oy : oy), C[4]);
                    q.Px(X + dx * n + (turn ? -dx : -ox) + (turn ? dx : 0), Y + dy * n + (turn ? dy * 0 : -oy), C[4]);
                }
            }
            foreach (int[] v in sht)
                for (int d = 1; d <= (turn ? 1 : Math.Max(1, R / 2)); d++)
                    q.Px(X + v[0] * d, Y + v[1] * d, d == 1 ? C[6] : C[5]);
            q.Px(X, Y, WHITE);
            // a faint halo, wide enough not to fill the gaps between the arms
            q.Glow(X + 0.5, Y + 0.5, r * 2.5 + 5, C[5], 0.13);
            q.Light(X, Y, C[6], 0.4);
        }

        // the drift on the floor: a lumpy bank with a white crest, blue shade below, and glints that twinkle
        static void Drift(Clip q, double t, double S, uint[] C)
        {
            Pass(q.P, Module, -7, 9, (x, y, w) =>
            {
                double top = Bank(x), d = y - top;
                if (d < 0) return;
                w.c = d < 1 ? WHITE : d < 2 ? C[7] : d < 3.5 ? C[6] : d < 6 ? C[5] : C[4];
                if (d < 1)
                {
                    w.l = C[6];
                    w.k = 0.12;
                }
            });
            for (int i = 0; i < 18; i++)
            {
                double x = -156 + Rand(S, i, 81) * 312, c = 1 + Math.Floor(Rand(S, i, 82) * 3), tw = Px.Sin((TAU * c * t) / D + Rand(S, i, 83) * TAU);
                if (tw < 0.55) continue;
                double y = Math.Ceiling(Bank(x)) + Math.Floor(Rand(S, i, 84) * 3), k = (tw - 0.55) / 0.45;
                q.Px(x, y, WHITE);
                if (k > 0.5)
                    foreach (int[] v in TOP3)
                        q.Px(x + v[0], y + v[1], C[6]);
                q.Glow(x + 0.5, y + 0.5, 6, C[6], 0.35 * k);
            }
        }
    }
}
