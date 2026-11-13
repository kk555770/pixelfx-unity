// The rain recipe (depth layers of two-tone streaks, far drops that end in the air, near ones that splash and
// throw droplets, mist low on the ground) is adapted from Sprite Fusion's open-source rain
// (https://www.spritefusion.com/pixel-art-vfx/rain), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Rain : Effect
    {
        public static readonly Module Module = new Module("rain", "pixelfx/fx/weather/rain", 320, 180, 160, 170, 8, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0e1322", "#1a2440", "#283a5e", "#3a5482", "#5674a8", "#84a6d6", "#c0dcf8", "#f2f8ff" },
                ["density"] = 1,
                ["seed"] = 4,
            },
            (o, e) => new Rain(o))
        {
            Title = "Rain",
            Desc = "Slanted rain at four depths: far drops fade into the dark, near ones glint and splash, drops ring the puddles and a low mist drifts over the wet floor.",
            Tags = new[] { "weather", "rain", "storm", "water", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 8, SLANT = -0.2;
        static readonly double UX = SLANT / Hypot(SLANT, 1), UY = 1 / Hypot(SLANT, 1);

        sealed class Depth
        {
            public int n;
            public int[] m, col;
            public double len;
            public double[] stop, z, size;
            public bool near;
        }

        // the depths, far to near: how many, falls a loop (speed), streak length, colors (tail, body, head), where
        // they end (in the air, or on the floor at depth z), how big their splash
        static readonly Depth[] LAYERS =
        {
            new Depth { n = 200, m = new[] { 10, 13 }, len = 2, col = new[] { 2, 2, 3 }, stop = new double[] { -80, -6 } },
            new Depth { n = 150, m = new[] { 14, 17 }, len = 4, col = new[] { 2, 3, 4 }, stop = new double[] { -45, -2 } },
            new Depth { n = 96, m = new[] { 19, 23 }, len = 7, col = new[] { 3, 4, 5 }, z = new double[] { 0, 1.4 }, size = new double[] { 0.15, 0.4 } },
            new Depth { n = 42, m = new[] { 25, 29 }, len = 13, col = new[] { 3, 5, 7 }, z = new double[] { 2, 6.5 }, size = new double[] { 0.6, 1 }, near = true },
        };

        // puddles on the wet floor: long thin strips that hold the sky, at depth y, from x0 to x1
        sealed class Pool
        {
            public double x0, x1, y;
        }

        static Pool[] Pools(double S)
        {
            var r = new Pool[5];
            for (int i = 0; i < 5; i++)
            {
                double x = -150 + i * 66 + Rand(S, i, 61) * 30, w = 16 + Rand(S, i, 63) * 26;
                r[i] = new Pool { x0 = x, x1 = x + w, y = 3 + Math.Floor(Rand(S, i, 62) * 3) };
            }
            return r;
        }

        static Pool PoolAt(Pool[] P, double x, double y)
        {
            foreach (Pool p in P)
                if (x >= p.x0 + 2 && x <= p.x1 - 2 && Math.Abs(y - p.y) < 1.5) return p;
            return null;
        }

        readonly Options o;

        Rain(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            t = Round(t * 3e4) / 3e4;
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            Pool[] P = Pools(S);
            // an overcast haze, thicker toward the ground, that hides the stars
            uint haze = Mix(C[0], C[1], 0.6);
            Pass(p, Module, -170, -0.5, (x, y, w) =>
            {
                w.c = haze;
                w.a = 0.3 + 0.26 * Smooth(-170, 0, y);
                w.b = true;
            });
            DrawFloor(q, t, S, C, P);
            for (int L = 0; L < 3; L++) DrawLayer(q, t, S, C, P, L, o.Num("density"));
            Mist(p, t, S, C);
        }

        public override void Draw(Painter p, double t)
        {
            t = Round(t * 3e4) / 3e4;
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            Pool[] P = Pools(S);
            DrawLayer(q, t, S, C, P, 3, o.Num("density"));
        }

        // one depth of rain: each slot falls a whole number of times a loop, somewhere new each time
        static void DrawLayer(Clip q, double t, double S, uint[] C, Pool[] P, int L, double density)
        {
            Depth Ly = LAYERS[L];
            double sd = S * 11 + L, len = Ly.len;
            uint c0 = C[Ly.col[0]], c1 = C[Ly.col[1]], c2 = C[Ly.col[2]];
            int n = RoundInt(Ly.n * density);
            Field G = MakeField(S + 21, 256, 128, new[] { 4, 8, 16 });
            double sweep = (256 * t) / D;
            Trips(t, D, n, sd, Ly.m[0], Ly.m[1], (u, k, m, i) =>
            {
                double j = Wrap(k, m);
                // gusts: sheets of heavier and lighter rain sweep across with the wind
                if (L < 2 && Rand(sd, i, j, 7) > 0.35 + 1.3 * (Sample(G, (-166 + Rand(sd, i, j, 2) * 332) * 0.8 + sweep, 9 + L * 40) - 0.32)) return;
                double end = Ly.z != null ? Ly.z[0] + Rand(sd, i, j, 1) * (Ly.z[1] - Ly.z[0]) : Ly.stop[0] + Rand(sd, i, j, 1) * (Ly.stop[1] - Ly.stop[0]);
                double y0 = -172 - len, y = y0 + (end - y0) * u, x = -166 + Rand(sd, i, j, 2) * 332 - (end - y) * SLANT;
                // far drops thin out before they end; near ones are whole
                double a = Ly.z != null ? 1 : Math.Min(1, (1 - u) * 5);
                if (len <= 2)
                {
                    q.Dot(x, y, c2, a);
                    q.Dot(x - UX, y - UY, c1, a * 0.8);
                }
                else
                {
                    q.Line(x - UX * len, y - UY * len, x - UX * len * 0.45, y - UY * len * 0.45, c0, v => a * (0.35 + 0.65 * v));
                    q.Line(x - UX * len * 0.45, y - UY * len * 0.45, x - UX, y - UY, c1, a);
                    q.Dot(x, y, c2, a);
                }
                // near drops catch the light
                if (Ly.near) q.Glow(x, y - 2, 4, C[5], 0.12);
                if (Ly.z == null) return;
                // the drops before this one landed: a crown on the ground, or rings sliding apart along a puddle
                double dur = D / m;
                for (int back = 1; back < 4; back++)
                {
                    double age = u * dur + (back - 1) * dur;
                    if (age > 0.7) break;
                    double jp = Wrap(k - back, m), z = Ly.z[0] + Rand(sd, i, jp, 1) * (Ly.z[1] - Ly.z[0]), xp = -166 + Rand(sd, i, jp, 2) * 332;
                    if (Rand(sd, i, jp, 5) > (Ly.near ? 1 : 0.7)) continue;
                    double size = Ly.size[0] + ((z - Ly.z[0]) / (Ly.z[1] - Ly.z[0])) * (Ly.size[1] - Ly.size[0]);
                    Pool pool = Ly.near ? PoolAt(P, xp, z) : null;
                    if (pool != null) Rings(q, xp, pool, age, C);
                    else if (Crown(q, xp, z, age, size, C[6], C[4], WHITE, Rand(sd, i, jp, 4)) && Ly.near && age < 0.1) q.Glow(xp, z - 1, 5, C[5], 0.3 * (1 - age / 0.1));
                }
            });
        }

        static readonly int[] SIDES = { -1, 1 };

        // a drop in a puddle: a bright point, then rings seen edge-on, so two short dashes sliding apart, twice
        static void Rings(Clip q, double x, Pool pool, double age, uint[] C)
        {
            double X = Round(x), y = pool.y;
            if (age < 0.05) q.Px(X, y, C[7]);
            for (int r = 0; r < 2; r++)
            {
                double a = age - r * 0.16, u = a / 0.5;
                if (u <= 0 || u >= 1) continue;
                double w = Round(1 + 7 * Math.Sqrt(u));
                uint c = u < 0.4 && r == 0 ? C[6] : C[5];
                foreach (int s in SIDES)
                    for (int d = 0; d < (u < 0.6 ? 2 : 1); d++)
                    {
                        double px = X + s * (w - d);
                        if (px > pool.x0 && px < pool.x1) q.Px(px, y, c);
                    }
            }
        }

        // the wet floor: a clean wet edge (a lit lip over a dark line), a dark sheen, puddle strips holding the sky
        static void DrawFloor(Clip q, double t, double S, uint[] C, Pool[] P)
        {
            Pass(q.P, Module, 0, 9.5, (x, y, w) =>
            {
                if (y < 1)
                {
                    w.c = C[4];
                    w.l = C[5];
                    w.k = 0.08;
                    return;
                }
                if (y < 2)
                {
                    w.c = C[1];
                    return;
                }
                w.c = C[0];
                w.a = 0.55;
                w.b = true;
                foreach (Pool p in P)
                    if (x >= p.x0 && x < p.x1 && Math.Floor(y) == p.y)
                    {
                        bool edge = x < p.x0 + 2 || x >= p.x1 - 2;
                        w.c = edge ? C[3] : C[4];
                        w.a = 1;
                        w.b = false;
                        w.l = C[5];
                        w.k = 0.06;
                    }
            });
            // a glint that slides slowly along each puddle
            for (int i = 0; i < P.Length; i++)
            {
                Pool p = P[i];
                double gx = Round(p.x0 + 3 + (p.x1 - p.x0 - 6) * (0.5 + 0.5 * Px.Sin((Math.PI * 2 * t) / D + i * 1.9)));
                q.Px(gx, p.y, C[6]);
                q.Glow(gx, p.y, 4, C[5], 0.2);
            }
        }

        // mist kicked up by the rain, drifting low along the ground: soft bands with lit crests
        static void Mist(Painter p, double t, double S, uint[] C)
        {
            Field M = MakeField(S + 9, 256, 128, new[] { 4, 8, 16, 32 });
            double drift = (256 * t) / D;
            uint crest = Mix(C[4], C[5], 0.5);
            double Dens(double x, double y)
            {
                double prof = Smooth(-22, -4, y) * (1 - Smooth(7, 10, y));
                return prof * (0.62 + (Sample(M, x * 0.45 + drift, y * 4 + 30) - 0.5) * 2);
            }
            Pass(p, Module, -20, 9, (x, y, w) =>
            {
                double d = Dens(x, y);
                if (d < 0.28) return;
                double th = Bayer(x, y) * 0.06;
                w.b = true;
                if (d > 0.46 + th && Dens(x, y - 1) < 0.43)
                {
                    w.c = crest;
                    w.a = 0.5;
                }
                else
                {
                    w.c = d > 0.58 + th ? C[4] : C[3];
                    w.a = Clamp((d - 0.28) * 1.6) * 0.5;
                }
                w.l = C[4];
                w.k = (d - 0.3) * 0.1;
            });
        }
    }
}
