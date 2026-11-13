// The arc's re-struck bowed shape, its halo-body-core inking, its flicker and the nodes where it roots follow
// Sprite Fusion's open-source lightning (https://www.spritefusion.com/pixel-art-vfx/lightning),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class SparkArc : Effect
    {
        public static readonly Module Module = new Module("spark-arc", "pixelfx/fx/lightning/spark-arc", 124, 120, 62, 108, 2, true, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["seed"] = 2 },
            (o, e) => new SparkArc(o, e))
        {
            Title = "Spark arc",
            Desc = "A climbing arc between two rods: it strikes white at their narrow foot, climbs on rising heat as it stretches and bows, sputters and snaps at the top, then strikes again.",
            Tags = new[] { "lightning", "electric", "arc", "jacobs-ladder", "machine", "prop", "loop" },
            Bg = "ground",
        };

        const int FPS = 30, N = 60;
        const double P = 2, SNAP = 1.42, H = 58;
        static readonly uint[] STEEL = Ramp("#151823", "#262c3b", "#3d4558", "#5f6a82", "#8f9bb2", "#d2daea");
        static readonly uint[] CERAMIC = Ramp("#4b4458", "#857d96", "#c9c1d6", "#efe9f6");
        static readonly uint ASH = Hex("#6a6478");

        // the rods: a V from the insulators up; where the arc roots at height h above the gap
        static double[] RodAt(double s, double h) => new[] { s * (4.5 + (h / 60) * 19.5), -9 - h };
        static double Height(double t) => 1.5 + H * Px.Pow(Clamp(t / SNAP), 1.35);

        readonly Options o;

        SparkArc(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), S = Ramp(o.Ramp("sparks"));
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            Rng r = NewRng(o.Seed, 1, f);
            bool climbing = t < SNAP;
            double h = Height(t), u = Clamp(t / SNAP), a = t - SNAP;
            // the floor and the stand: a pool of cold light while it burns, a plate, two ribbed insulators
            p.Glow(0, -14, 50, C[3], climbing ? 0.2 + 0.15 * (1 - u) : 0.06);
            p.Rect(-18, -3, 36, 3, STEEL[1]);
            p.Line(-18, -3, 17, -3, STEEL[3]);
            foreach (int s in new[] { -1, 1 })
                for (int y = -9; y < -3; y++)
                {
                    bool rib = (y & 1) == 0;
                    for (int x = 2; x <= 6; x++) p.Px(s * x - (s < 0 ? 1 : 0), y, rib ? CERAMIC[x < 4 ? 3 : x < 6 ? 2 : 1] : CERAMIC[x < 5 ? 1 : 0]);
                }
            // the rods: steel, a lit edge, glowing where the arc has passed and cooling behind it
            foreach (int s in new[] { -1, 1 })
            {
                for (double hh = 0; hh <= 60; hh += 0.5)
                {
                    // when the arc passed this point; not yet this time round, it is still cooling from the last
                    double[] xy = RodAt(s, hh);
                    double x = xy[0], y = xy[1], tp = SNAP * Px.Pow(Clamp((hh - 1.5) / H), 1 / 1.35), age = t >= tp ? t - tp : t - tp + P;
                    double hot = hh > H + 2 ? 0 : Exp(-age / 0.26);
                    uint c = Heat(hot * 1.05 - 0.12);
                    p.Px(x, y, c != 0 ? c : (s < 0 ? STEEL[4] : STEEL[3]));
                    uint c2;
                    if (c != 0 && hot > 0.35)
                    {
                        c2 = Heat(hot * 0.6);
                        if (c2 == 0) c2 = STEEL[2];
                    }
                    else c2 = s < 0 ? STEEL[2] : STEEL[1];
                    p.Px(x + s, y, c2);
                    if (hot > 0.3 && (ToInt32(Round(hh * 2)) & 3) == 0) p.Glow(x, y, 5, c != 0 ? c : C[4], 0.25 * hot);
                }
                double[] tt = RodAt(s, 60);
                p.Circle(tt[0] + s * 0.5, tt[1], 1.6, STEEL[4]);
                p.Px(tt[0] + s * 0.5 - 0.5, tt[1] - 1, STEEL[5]);
            }
            // the arc: struck white at the foot, flickering as it climbs and stretches, sputtering at the top
            if (climbing)
            {
                double[] l = RodAt(-1, h), rr = RodAt(1, h);
                double xl = l[0], yl = l[1], xr = rr[0], yr = rr[1], k0 = t < 1.0 / FPS ? 1 : 1 - 0.22 * u, sput = u > 0.88 && r.Next() < 0.25 ? 0.4 : 1;
                double k = k0 * sput * (0.82 + 0.18 * r.Next()), bow = -(1.5 + 13 * u * u) * (0.8 + 0.4 * r.Next());
                Stroke(p, Bowed(r, xl, yl, xr, yr, bow, 0.18 + 0.12 * u, 0.12 + 0.25 * u), k, C, t < 1.0 / FPS ? 0.95 : 0.8, new StrokeOpt { hero = t < 1.0 / FPS, light = 0.7 });
                p.GlowLine(xl, yl + bow * 0.5, xr, yr + bow * 0.5, 12, C[4], 0.22 * k);
                foreach (double[] pt in new[] { new[] { xl, yl }, new[] { xr, yr } })
                {
                    double x = pt[0], y = pt[1];
                    p.Circle(x, y, 1.8 + 0.6 * k, C[5]);
                    p.Px(x, y, WHITE);
                    p.Glow(x, y, 12, C[5], 0.55 * k);
                }
                if (t < 0.1) p.Glow(0, -11, 26, C[6], 0.45 * (1 - t / 0.1));
            }
            else if (a < 2.0 / FPS)
            {
                // the snap: two stubs left clinging to the rods, a flash where it broke
                double[] l = RodAt(-1, H + 1.5), rr = RodAt(1, H + 1.5);
                double xl = l[0], yl = l[1], xr = rr[0], yr = rr[1];
                foreach (double[] q in new[] { new[] { xl, yl, 1 }, new[] { xr, yr, -1 } })
                {
                    double x0 = q[0], y0 = q[1], s = q[2];
                    Crackle(p, Zigzag(r, x0, y0, x0 + s * r.Range(5, 9), y0 - r.Range(2, 6), 0.4, 0.3), 0.7, C, 0.4);
                }
                p.Glow(0, yl - 8, 30, C[4], 0.5);
            }
            else if (t > P - 0.16)
            {
                // anticipation: little sparks jump the narrow gap before it strikes again
                if (r.Next() < 0.7) Crackle(p, Zigzag(r, -4.5, -10.5 - r.Range(0, 2), 4.5, -10.5 - r.Range(0, 2), 0.5, 0), 0.4 + 0.3 * r.Next(), C, 0.35);
            }
            // ozone and smoke rise off the arc while it climbs, and from where it snapped
            for (int j = 0; j < 3; j++)
            {
                double born = ((j + 1) / 3.2) * SNAP, age = (((t - born) % P) + P) % P, life = 1;
                if (age >= life) continue;
                double y0 = -9 - Height(born) - 6;
                Puff(p, (j % 2 != 0 ? 3 : -3) + Px.Sin(age * 3 + j) * 2, y0 - age * 20, 1.8 + j * 0.3, age / life, Mix(ASH, C[5], 0.3));
            }
            // sparks: thrown from the roots as it strikes, sprayed up where it snaps, molten drips off the rods
            void Burst(double t0, int n, int salt, Func<Rng, int, double[]> from)
            {
                double age = (((t - t0) % P) + P) % P;
                for (int j = 0; j < n; j++)
                {
                    Rng q = NewRng(o.Seed, salt, j);
                    double life = q.Range(0.25, 0.6);
                    if (age >= life) continue;
                    double[] fr = from(q, j);
                    double x0 = fr[0], y0 = fr[1], a0 = fr[2], spread = fr[3], sp0 = fr[4], sp1 = fr[5], ang = a0 + q.Range(-spread, spread), sp = q.Range(sp0, sp1);
                    double[] s = Flight(x0, y0, Px.Cos(ang) * sp, Px.Sin(ang) * sp, age, floor: 0);
                    Spark(p, s[0], s[1], s[2], s[3], 1 - age / life, S);
                }
            }
            Burst(0, 12, 11, (q, j) =>
            {
                double[] rp = RodAt(j % 2 != 0 ? 1 : -1, 1.5);
                return new[] { rp[0], rp[1], -Math.PI / 2 + (j % 2 != 0 ? 0.6 : -0.6), 1, 60, 170 };
            });
            Burst(SNAP, 14, 12, (q, j) => new[] { RodAt(1, H)[0] * q.Range(-0.8, 0.8), -9 - H - 6, -Math.PI / 2, 1.3, 50, 160 });
            foreach (double[] kt in new[] { new[] { 0, 0.45 }, new[] { 1, 0.8 }, new[] { 2, 1.15 } })
            {
                double tt = kt[1];
                Burst(tt, 2, 20 + (int)kt[0], (q, j) =>
                {
                    double[] rp = RodAt(j % 2 != 0 ? 1 : -1, Height(tt));
                    return new[] { rp[0], rp[1], Math.PI / 2, 0.5, 5, 30 };
                });
            }
            if (climbing || a < 0.25) p.Glow(0, -9 - h - 3, 26, C[4], climbing ? 0.25 : 0.4 * (1 - a / 0.25));
        }
    }
}
