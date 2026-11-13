// The re-struck filaments, the glass shell with its specular arc and the core follow Sprite Fusion's open-source
// energy orb (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class PlasmaBall : Effect
    {
        public static readonly Module Module = new Module("plasma-ball", "pixelfx/fx/lightning/plasma-ball", 112, 104, 56, 96, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0828", "#3b0f5e", "#6a1d9c", "#a03ad6", "#d46cf2", "#f2a8ff", "#ffe2ff", "#ffffff" },
                ["tendrils"] = 9,
                ["seed"] = 6,
            },
            (o, e) => new PlasmaBall(o, e))
        {
            Title = "Plasma ball",
            Desc = "A glass globe on a dark pedestal: wandering plasma tendrils reach from the core to the glass and fan out in bright feet where they touch it, lighting the stand and floor.",
            Tags = new[] { "lightning", "electric", "plasma", "lamp", "prop", "loop" },
            Bg = "ground",
        };

        const int FPS = 30, N = 90;
        const double GX = 0, GY = -55, RG = 24;
        static readonly uint[] BASE = Ramp("#0d0b12", "#17131f", "#231d2d", "#342b43", "#4b3f60", "#6b5c87");

        sealed class Tendril
        {
            public double a, ex, ey, w1, w2;
            public bool front, rim;
        }

        // a tendril at phase ph: its end wanders over the glass, front ones end on the near face, back ones on the far
        static Tendril MakeTendril(int i, double n, double ph, double seed)
        {
            double a = ((i + 0.5) / n) * TAU + 0.9 * LoopNoise(ph, 0.9, seed + i * 7), z = LoopNoise(ph, 0.7, seed + i * 7 + 3);
            double rho = RG * (0.72 + 0.28 * Math.Sqrt(1 - Math.Min(1, z * z * 1.6)));
            return new Tendril
            {
                a = a,
                front = z > -0.15,
                ex = GX + Px.Cos(a) * rho,
                ey = GY + Px.Sin(a) * rho,
                rim = rho > RG * 0.93,
                w1 = LoopNoise(ph, 1.1, seed + i * 7 + 5),
                w2 = LoopNoise(ph, 1.3, seed + i * 7 + 6),
            };
        }

        // the pedestal: a flared stand lit from above by the globe, a collar under the glass
        static void Pedestal(Painter p, uint[] C, double glow)
        {
            p.Shade(-18, -32, 18, 0, (x, y, o) =>
            {
                double hw = y < -27 ? 7 : y < -2 ? 9 + ((y + 27) / 25) * 6 : 16;
                if (Math.Abs(x) > hw) return 0;
                double u = (x / hw + 1) / 2;
                if (y < -27) return o.c = y < -30 ? (u < 0.6 ? C[4] : BASE[5]) : u < 0.3 ? BASE[5] : u < 0.75 ? BASE[3] : BASE[1];
                if (y >= -2) return o.c = y < -1 ? (u < 0.55 ? BASE[4] : BASE[2]) : BASE[1];
                // glossy body: a lit flank, a specular stripe, the far side in shadow, plasma light on the upper rim
                int i = u < 0.1 ? 2 : u < 0.2 ? 3 : u < 0.3 ? 5 : u < 0.42 ? 4 : u < 0.6 ? 3 : u < 0.8 ? 2 : u < 0.92 ? 1 : 0;
                o.c = y < -24 && u < 0.5 ? Mix(BASE[i], C[4], 0.3 * glow) : y > -5 ? BASE[Math.Max(0, i - 1)] : BASE[i];
                return 0;
            });
            p.Px(9, -9, C[5]);
            p.Glow(9, -9, 3, C[5], 0.5);
        }

        readonly Options o;

        PlasmaBall(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            double ph = (double)f / N, n = o.Num("tendrils");
            Rng R = NewRng(o.Seed, 3, f);
            double pulse = 0.85 + 0.15 * Px.Sin(ph * TAU * 6) + 0.1 * (R.Next() - 0.5);
            var ts = new Tendril[(int)n];
            for (int i = 0; i < ts.Length; i++) ts[i] = MakeTendril(i, n, ph, o.Seed);
            // light the floor and the stand, then the stand
            p.Glow(0, -6, 52, C[3], 0.22 * pulse);
            Pedestal(p, C, pulse);
            // the glass: a faint tint over what's behind, the plasma's glow inside it
            p.Shade(GX - RG, GY - RG, GX + RG, GY + RG, (x, y, _) =>
            {
                double d = Hypot(x - GX, y - GY);
                if (d < RG - 0.5) p.Blend(x, y, d > RG - 4 ? C[1] : C[0], 0.22 + 0.4 * Px.Pow(d / RG, 4));
                return 0;
            });
            p.Glow(GX, GY, RG + 14, C[3], 0.32 * pulse);
            p.GlowRing(GX, GY, RG - 1, 5, C[3], 0.22 * pulse);
            void DrawTendril(Tendril q, int i, bool bright)
            {
                double sx = GX + Px.Cos(q.a) * 4.5, sy = GY + Px.Sin(q.a) * 4.5, dx = q.ex - sx, dy = q.ey - sy, L = Hypot(dx, dy), nx = -dy / L, ny = dx / L;
                Rng r = NewRng(o.Seed, 17, i, f);
                var pts = new List<double[]>();
                int m = (int)Math.Max(4, Round(L / 2.2));
                for (int j = 0; j <= m; j++)
                {
                    double u = (double)j / m, w = 3.2 * q.w1 * Px.Sin(Math.PI * u) + 1.6 * q.w2 * Px.Sin(TAU * u + i), jit = j != 0 && j < m ? r.Range(-0.7, 0.7) : 0;
                    pts.Add(new[] { sx + dx * u + nx * (w + jit), sy + dy * u + ny * (w + jit) });
                }
                // the foot: the filament fans into short fingers where it meets the glass
                var paths = new List<(double[][] pts, double w)> { (pts.ToArray(), 1) };
                for (int k = 0; k < 3; k++)
                {
                    double bx = pts[m - 2][0], by = pts[m - 2][1], a = q.a + (k - 1) * r.Range(0.25, 0.45), l = r.Range(2.5, 5);
                    double mx = bx + Px.Cos(a) * l * 0.5 + r.Range(-0.6, 0.6), my = by + Px.Sin(a) * l * 0.5 + r.Range(-0.6, 0.6);
                    paths.Add((new[] { new[] { bx, by }, new[] { mx, my }, new[] { bx + Px.Cos(a) * l, by + Px.Sin(a) * l } }, 0.5));
                }
                bool hot = r.Next() < 0.2;
                foreach (var q2 in paths)
                    for (int j = 0; j < q2.pts.Length - 1; j++)
                    {
                        double ax = q2.pts[j][0], ay = q2.pts[j][1], bx = q2.pts[j + 1][0], by = q2.pts[j + 1][1], u = (double)j / (q2.pts.Length - 1);
                        if (bright && q2.w >= 1) p.Line(ax + 1, ay + 1, bx + 1, by + 1, C[3]);
                        uint c = !bright ? (q2.w >= 1 ? C[3] : C[2]) : q2.w < 1 ? C[6] : hot || u > 0.75 ? C[6] : u < 0.2 ? C[6] : C[5];
                        p.Line(ax, ay, bx, by, c);
                        if (q2.w >= 1 && j % 3 == 0) p.Glow(ax, ay, bright ? 6 : 4, C[4], bright ? (hot ? 0.4 : 0.24) : 0.12);
                    }
                // where it touches, the glass flares
                if (bright)
                {
                    p.Circle(q.ex, q.ey, 1.6, C[6]);
                    p.Px(q.ex, q.ey, WHITE);
                    p.Glow(q.ex, q.ey, 9, C[5], 0.55);
                    if (q.rim)
                        for (int s = -1; s <= 1; s += 2) p.Px(GX + Px.Cos(q.a + s * 0.09) * (RG - 0.5), GY + Px.Sin(q.a + s * 0.09) * (RG - 0.5), C[6]);
                }
                else p.Glow(q.ex, q.ey, 6, C[4], 0.25);
            }
            for (int i = 0; i < ts.Length; i++) if (!ts[i].front) DrawTendril(ts[i], i, false);
            // the core: an electrode ball, pulsing
            double cr = 3.6 + 0.5 * pulse;
            p.Circle(GX, GY, cr + 1.3, C[3]);
            p.Circle(GX, GY, cr, C[5]);
            p.Circle(GX - 0.6, GY - 0.6, cr * 0.55, C[6]);
            p.Circle(GX - 0.8, GY - 0.8, cr * 0.25, WHITE);
            p.Glow(GX, GY, 16, C[5], 0.65 * pulse);
            for (int i = 0; i < ts.Length; i++) if (ts[i].front) DrawTendril(ts[i], i, true);
            // the glass shell: a thin rim, a bright specular arc upper left, a faint reflection lower right
            p.Ring(GX, GY, RG + 1.6, 1, C[1]);
            p.Ring(GX, GY, RG + 0.6, 1.1, (v, a) => a > 2.2 && a < 3.3 ? C[5] : a > 0.3 && a < 1.3 ? C[4] : C[3]);
            p.Arc(GX, GY, RG - 1.6, 1.3, 3.55, 4.55, (u, v) => u > 0.25 && u < 0.7 ? WHITE : C[6]);
            for (double a = 0.35; a < 1.15; a += 0.045) p.Dot(GX + Px.Cos(a) * (RG - 2.6), GY + Px.Sin(a) * (RG - 2.6), C[4], 0.85);
            p.Px(GX - RG * 0.42, GY - RG * 0.62, WHITE);
        }
    }
}
