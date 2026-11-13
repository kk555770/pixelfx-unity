// The lumpy cloud (puffs that drift off their push, rise, swell from small to large and thin out, lit caps,
// motes with light) is adapted from Sprite Fusion's open-source poison cloud (https://www.spritefusion.com/pixel-art-vfx/poison),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class SporeBurst : Effect
    {
        const double DURATION = 3.2;

        public static readonly Module Module = new Module("spore-burst", "pixelfx/fx/nature/spore-burst", 212, 132, 106, 118, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#06141c", "#0c2a34", "#11484e", "#17706a", "#22a08a", "#4cd0a8", "#a8f4d0", "#f0fff6" },
                ["cap"] = new[] { "#1a0c1e", "#3a1438", "#62204e", "#8e3462", "#bc5276", "#e88a96" },
                ["stem"] = new[] { "#2a2230", "#5a4e5e", "#9a8e98", "#d8ccd0", "#f6eeee" },
                ["seed"] = 9,
            },
            (o, e) => new SporeBurst(o, e))
        {
            Title = "Spore burst",
            Desc = "A toadstool pushes up, swells and trembles as its gills glow, then squashes and puffs a lumpy cloud of glowing spores that billows up and drifts down twinkling.",
            Tags = new[] { "nature", "mushroom", "spores", "poison", "cloud", "ground", "one-shot" },
            Bg = "ground",
        };

        const double GROW = 0.32, PUFF = 0.8, SINK = 2.15;
        static readonly double[][] SPOTS = { new[] { -0.45, 0.45, 1.4 }, new[] { 0.1, 0.72, 1.8 }, new[] { 0.55, 0.38, 1.2 }, new[] { -0.12, 0.25, 1 }, new[] { 0.3, 0.12, 0.9 }, new[] { -0.7, 0.16, 0.8 } };

        // the toadstool at its current squash: a stem with a frilled ring, a domed cap lit from the upper left with
        // pale spots, glowing gills beneath it
        static void Toadstool(Painter p, uint[] K, uint[] Sm, uint[] Cs, double g, double sx, double sy, double glow, double sink)
        {
            double H = 17 * g, cw = 17 * sx * g, ch = 13 * sy * g, bas = sink, top = bas - H;
            if (g < 0.05) return;
            // stem
            for (double y = Math.Floor(top); y <= Math.Min(0, bas); y++)
            for (int x = -4; x <= 4; x++)
            {
                double w = 3 + ((y - top) / H) * 1;
                if (Math.Abs(x + 0.5) > w) continue;
                double q = (x + 0.5 + w) / (2 * w);
                p.Px(x, y, Math.Abs(x + 0.5) > w - 0.8 ? Sm[1] : q < 0.4 ? Sm[4] : q < 0.7 ? Sm[3] : Sm[2]);
            }
            double ring = top + H * 0.3;
            if (ring <= 0)
                for (int x = -5; x <= 5; x++)
                    p.Px(x, ring + (Math.Abs(x) > 3 ? 1 : 0), Math.Abs(x) > 4 ? Sm[1] : Sm[3]);
            // gills: a glowing underside
            double gy = top + 1;
            if (gy <= 0)
                for (double x = Math.Floor(-cw); x <= Math.Ceiling(cw); x++)
                {
                    double d = Math.Abs(x + 0.5) / cw;
                    if (d > 0.95) continue;
                    bool odd = (ToInt32(x) & 1) != 0;
                    p.Px(x, gy, d < 0.3 ? Cs[0] : odd ? Sm[2] : Sm[3]);
                    if (glow > 0.05) p.Px(x, gy, glow > 0.6 && odd ? K[7] : K[5]);
                }
            if (glow > 0.05) p.Glow(0, gy + 2, cw * 1.5 + 6, K[5], 0.5 * glow);
            // the cap: a dome with a curled lip
            p.Shade(-cw - 1, top - ch - 1, cw + 1, top + 1, (x, y, o) =>
            {
                double dx = x / cw, dy = (top - y) / ch, d = dx * dx + dy * dy;
                if (dy < -0.12 || d > 1.12 || y > 0) return 0;
                if (d > 1) return o.c = Cs[0];
                int i = dx < -0.35 && dy > 0.45 ? 5 : dx < 0.05 && dy > 0.3 ? 4 : dy < 0.18 ? 2 : dx > 0.55 ? 2 : 3;
                foreach (double[] sp in SPOTS)
                {
                    double sx0 = sp[0], sy0 = sp[1], r = sp[2];
                    double ex = (dx - sx0) * cw, ey = (dy - sy0) * ch;
                    if (ex * ex + ey * ey * 1.6 < r * r * g * g) i = ex < 0 && ey > 0 ? 9 : 8;
                }
                o.c = i == 9 ? Sm[4] : i == 8 ? Sm[3] : Cs[i];
                return 0;
            });
            p.Px(-cw * 0.45, top - ch * 0.8, Cs[5]);
        }

        readonly uint[] K, Cs, Sm;
        readonly Rng R;
        readonly Particles ps;
        readonly NatureLib.Gas gas = new NatureLib.Gas();
        readonly Lights lights;
        double t;
        bool puffed, grown;

        SporeBurst(Options o, Emitter e)
        {
            K = Fit(o.Ramp("palette"), 8);
            Cs = Fit(o.Ramp("cap"), 6);
            Sm = Fit(o.Ramp("stem"), 5);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { K[7], K[6], K[5], K[4] }, haze: K[5], emitter: e);
            lights = new Lights(e);
        }

        double Gr() => Ease.OutBack(Clamp(t / GROW), 1.6) * (1 - Ease.InCubic(Clamp((t - SINK) / 0.45)));
        double Charge() => Smooth(GROW, PUFF, t);

        // squash and stretch: it swells as it charges, squashes flat as it puffs, springs back
        (double sx, double sy) Shape()
        {
            if (t < PUFF)
            {
                double k = Charge(), j = Px.Sin(t * 70) * 0.04 * k;
                return (1 + 0.28 * k + j, 1 + 0.22 * k - j);
            }
            double e = t - PUFF, sp = Exp(-e * 7) * Px.Cos(e * 26);
            return (1 + 0.25 * sp, 1 - 0.35 * sp);
        }

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            if (!grown)
            {
                grown = true;
                for (int i = 0; i < 10; i++) ps.Spawn(P.Grit, R.Range(-4, 4), -1, R.Range(-50, 50), -R.Range(40, 120), R.Range(0.5, 0.8), R.Pick(new[] { Sm[1], Sm[2] }), 1);
                for (int i = 0; i < 4; i++) ps.Spawn(P.Dust, R.Range(-4, 4), -1, R.Sign() * R.Range(10, 40), -R.Range(4, 12), R.Range(0.4, 0.7), Sm[1], R.Range(2, 3));
            }
            // charging: spores gather toward the gills
            if (t > GROW && t < PUFF - 0.05 && R.Chance(0.25 * f))
            {
                double a = R.Range(Math.PI * 1.05, Math.PI * 1.95), d = R.Range(20, 38), x = Px.Cos(a) * d, y = -18 + Px.Sin(a) * d * 0.7;
                ps.Spawn(P.Streak, x, y, -x * 3, (-18 - y) * 3, R.Range(0.2, 0.3), K[6]);
            }
            if (!puffed && t >= PUFF)
            {
                puffed = true;
                lights.Add(0, -22, 30, WHITE, 0.6, 0.1);
                lights.Add(0, -28, 70, K[5], 0.55, 0.9);
                // the cloud: lumps shoved up and out from under the cap, swelling as they go
                for (int i = 0; i < 30; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(15, 95);
                    gas.Add(t, R.Range(-8, 8), -20 + R.Range(-3, 3), Px.Cos(a) * sp * 1.3, Px.Sin(a) * sp - 10, R.Range(3, 5), R.Range(8, 14), t + R.Range(1.6, 2.2), R.Range(5, 10));
                }
                for (int i = 0; i < 6; i++)
                {
                    int sd = R.Sign();
                    gas.Add(t, sd * R.Range(6, 12), -5, sd * R.Range(30, 70), -R.Range(2, 14), R.Range(2, 3), R.Range(4, 6), t + R.Range(0.8, 1.1), R.Range(4, 8));
                }
                for (int i = 0; i < 40; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(40, 160);
                    bool mote = R.Chance(0.5);
                    double k = mote ? 0.35 : 0.7;
                    ps.Spawn(mote ? P.Mote : P.Flake, R.Range(-5, 5), -20, Px.Cos(a) * sp * k, Px.Sin(a) * sp * k, R.Range(1, 2), R.Pick(new[] { K[7], K[6], K[6], K[5] }), R.Range(0.6, 1.6));
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Star, R.Range(-20, 20), -R.Range(12, 40), 0, -6, R.Range(0.3, 0.6), K[7], R.Range(1, 2.4), R.Range(0.05, 0.4));
                for (int i = 0; i < 8; i++) ps.Spawn(P.Dust, R.Sign() * R.Range(4, 10), -1, R.Sign() * R.Range(30, 90), -R.Range(2, 10), R.Range(0.5, 0.9), K[3], R.Range(2, 3.5));
            }
            // after it: spores keep drifting down out of the cloud, twinkling
            if (puffed && t < 2.6 && R.Chance(0.12 * f)) ps.Spawn(P.Flake, R.Range(-40, 40), -R.Range(30, 70), R.Range(-8, 8), R.Range(4, 14), Math.Min(R.Range(0.9, 1.5), DURATION - 0.05 - t), R.Chance(0.5) ? K[7] : K[6], R.Range(0.6, 1.5));
            if (t > SINK && t < SINK + 0.4 && R.Chance(0.15 * f)) ps.Spawn(P.Dust, R.Range(-5, 5), -1, R.Sign() * R.Range(10, 30), -R.Range(2, 8), R.Range(0.3, 0.5), Sm[1], R.Range(1.5, 2.5));
            gas.Prune(t);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // a ring of glow on the floor round it while it charges and puffs
            double k = Charge() * (1 - Smooth(PUFF + 0.2, PUFF + 1.2, t));
            if (k > 0)
                for (int x = -20; x <= 20; x++)
                    p.Dot(x, 0, Math.Abs(x) < 10 ? K[5] : K[4], k * (1 - Math.Abs(x) / 21.0));
            p.Glow(0, -2, 30, K[4], 0.25 * k);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            var (sx, sy) = Shape();
            double gl = t < PUFF ? Charge() * (0.75 + 0.25 * Px.Sin(t * (20 + 30 * Charge()))) : Math.Max(0, 1 - (t - PUFF) * 1.5);
            Toadstool(p, K, Sm, Cs, Gr(), sx, sy, gl, Ease.InCubic(Clamp((t - SINK) / 0.45)) * 20);
            // the cloud: teal lumps lit along their tops, a cool light through them
            gas.Draw(p, t, K[2], K[4], K[6], K[7], 0.62, K[6]);
            // the cloud glows from within: a soft light riding its middle
            if (gas.List.Count > 0)
            {
                double cx = 0, cy = 0, n = 0;
                foreach (NatureLib.Puff q in gas.List)
                {
                    NatureLib.PuffAt g2 = gas.At(q, t);
                    cx += g2.x * g2.a;
                    cy += g2.y * g2.a;
                    n += g2.a;
                }
                if (n > 0.2) p.Glow(cx / n, cy / n, 46, K[5], Math.Min(0.32, 0.03 * n));
            }
            double e = t - PUFF;
            if (e > 0 && e < 0.1) Sim.Sparkle(p, 0, -20, 10 * (1 - e / 0.1), K[7], K[6], 0.6);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
