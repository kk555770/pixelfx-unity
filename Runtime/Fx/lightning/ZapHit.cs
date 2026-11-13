// The arc's halo-body-core inking, its flicker and the light that strobes as it dies follow Sprite Fusion's
// open-source lightning (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class ZapHit : Effect
    {
        public static readonly Module Module = new Module("zap-hit", "pixelfx/fx/lightning/zap-hit", 144, 112, 76, 56, 0.9, false, 30,
            new Options { ["palette"] = VOLT, ["seed"] = 3 },
            (o, e) => new ZapHit(o, e))
        {
            Title = "Zap hit",
            Desc = "A contact zap: an arc snaps in from the left, the hit point overloads into a knot of tiny crackling zigzags, a fern of burn marks glows outward, sparks jink away, ozone puffs.",
            Tags = new[] { "lightning", "electric", "hit", "impact", "combat", "directional", "one-shot" },
            Bg = "dark",
        };

        const double SNAP = 0.05;
        static readonly uint ASH = Hex("#6a6680");

        sealed class Frond
        {
            public double[][] pts;
            public double w;
        }

        // the burn the current leaves on what it hit: a Lichtenberg fern, main fronds radiating, side fronds alternating
        static List<Frond> Fern(Rng R)
        {
            var o = new List<Frond>();
            void Grow(double x, double y, double a, double len, double w, double d, int depth)
            {
                var pts = new List<double[]> { new[] { x, y, d } };
                int side = R.Sign();
                for (double s = 0; s < len; s += 1.4)
                {
                    a += R.Range(-0.3, 0.3);
                    x += Px.Cos(a) * 1.4;
                    y += Px.Sin(a) * 1.4;
                    d += 1.4;
                    pts.Add(new[] { x, y, d });
                    if (depth < 2 && s > 2 && R.Chance(0.24))
                    {
                        side = -side;
                        Grow(x, y, a + side * R.Range(0.55, 1.05), (len - s) * R.Range(0.3, 0.55), w * 0.6, d, depth + 1);
                    }
                }
                o.Add(new Frond { pts = pts.ToArray(), w = w });
            }
            for (int i = 0; i < 6; i++) Grow(0, 0, (i / 6.0) * TAU + R.Range(-0.35, 0.35), R.Range(18, 32), 1, 0, 0);
            return o;
        }

        sealed class Kink
        {
            public double x, y, vx, vy, life, age, next;
            public readonly List<double[]> trail = new List<double[]>();
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Frond> tree;
        readonly double Y0;
        readonly Kink[] kinked;
        readonly Options o;
        double t;
        bool hit;

        ZapHit(Options o, Emitter e)
        {
            this.o = o;
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, emitter: e);
            lights = new Lights(e);
            tree = Fern(R);
            Y0 = R.Range(-14, 6);
            // sparks that jink: they fly out and jerk to a new heading every few hundredths of a second
            kinked = new Kink[16];
            for (int i = 0; i < 16; i++)
            {
                double a = R.Range(-1.2, 1.2) + (R.Chance(0.7) ? 0 : Math.PI), sp = R.Range(130, 250);
                var k = new Kink { x = 0, y = 0, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - 40 };
                k.life = R.Range(0.2, 0.36);
                k.age = -SNAP;
                k.next = R.Range(0.02, 0.05);
                kinked[i] = k;
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!hit && t >= SNAP)
            {
                hit = true;
                lights.Add(0, 0, 60, C[4], 0.7, 0.3);
                lights.Add(0, 0, 14, WHITE, 0.5, 0.07);
                for (int i = 0; i < 3; i++) ps.Spawn(P.Smoke, R.Range(-3, 5), R.Range(-4, 2), R.Range(-5, 8), -R.Range(8, 16), R.Range(0.55, 0.75), Mix(ASH, C[4], 0.3), R.Range(2.5, 3.8), R.Range(0.08, 0.16));
            }
            foreach (Kink s in kinked)
            {
                if ((s.age += dt) < 0 || s.age >= s.life) continue;
                if ((s.next -= dt) <= 0)
                {
                    s.next = R.Range(0.025, 0.055);
                    double sp = Hypot(s.vx, s.vy), a = Atan2(s.vy, s.vx) + R.Sign() * R.Range(0.6, 1.3);
                    s.vx = Px.Cos(a) * sp;
                    s.vy = Px.Sin(a) * sp;
                    s.trail.Add(new[] { s.x, s.y });
                    if (s.trail.Count > 4) s.trail.RemoveAt(0);
                }
                s.vx -= s.vx * 3 * dt;
                s.vy -= s.vy * 3 * dt - 140 * dt;
                s.x += s.vx * dt;
                s.y += s.vy * dt;
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // the fern burns outward from the hit, glowing white, cooling through the palette, then fading
            if (t < SNAP) return;
            // it cools by stepping down the palette, and only the last embers dither away
            double cool = Smooth(0.45, 0.85, t), fade = 1 - Smooth(0.78, 0.88, t);
            foreach (Frond br in tree)
                for (int i = 1; i < br.pts.Length; i++)
                {
                    double x0 = br.pts[i - 1][0], y0 = br.pts[i - 1][1], x = br.pts[i][0], y = br.pts[i][1], d = br.pts[i][2], age = t - SNAP - d / 260;
                    if (age < 0) break;
                    double k = Exp(-age / 0.22) - cool * 0.3;
                    uint c = k > 0.75 ? WHITE : k > 0.45 ? C[6] : k > 0.25 ? C[5] : k > 0.1 ? C[4] : k > -0.05 ? C[2] : C[1];
                    p.Line(x0, y0, x, y, c, fade);
                    if (br.w >= 1 && d < 9) p.Line(x0 + 1, y0, x + 1, y, k > 0.45 ? C[5] : C[2], fade);
                    if ((i & 3) == 0 && k > 0) p.Glow(x, y, 5, C[5], 0.35 * k * fade);
                }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            int f = (int)Math.Floor(t * 30 + 1e-4);
            // the arc snaps in from the left in two frames, holds and flickers, then lets go
            if (t < 0.32)
            {
                Rng r = NewRng(o.Seed, 2, f);
                Bolt b = Zigzag(r, -66, Y0, 0, 0, 0.22, 0.35);
                double u = Clamp(t / SNAP);
                double k = t < SNAP ? 1 : t < 0.2 ? (f % 2 != 0 ? 0.7 : 1) : 0.55 * (1 - (t - 0.2) / 0.12);
                Stroke(p, u < 1 ? new Bolt(b.paths[0]) : b, k, C, t < SNAP + 0.034 ? 0.95 : 0.7, new StrokeOpt { upTo = u, hero = t >= SNAP && t < SNAP + 0.034 });
                if (u < 1)
                {
                    double[][] tp = b.paths[0].pts;
                    double[] h = tp[(int)Math.Floor(u * (tp.Length - 1))];
                    p.Circle(h[0], h[1], 2, WHITE);
                    p.Glow(h[0], h[1], 14, C[5], 0.6);
                }
            }
            // the hit point overloads: a tight knot of tiny zigzags re-struck every frame round a white heart
            if (t >= SNAP && t < 0.45)
            {
                double a = (t - SNAP) / 0.4, R0 = 8 * (1 - a * 0.6);
                Rng r = NewRng(o.Seed, 5, f);
                double m = Round(11 * (1 - a * 0.5));
                for (int i = 0; i < m; i++)
                {
                    double ang = r.Range(0, TAU), d0 = r.Range(0.5, R0 * 0.5), ang2 = ang + r.Range(-1.2, 1.2), d1 = r.Range(R0 * 0.55, R0);
                    Crackle(p, Zigzag(r, Px.Cos(ang) * d0, Px.Sin(ang) * d0, Px.Cos(ang2) * d1, Px.Sin(ang2) * d1, 0.5, 0), 0.75 * (1 - a) + 0.25, C, 0.25);
                }
                p.Circle(0, 0, 3 * (1 - a) + 1, C[5]);
                p.Circle(0, 0, 2 * (1 - a) + 0.5, WHITE);
                p.Glow(0, 0, 20, C[4], 0.55 * (1 - a));
                if (t < SNAP + 0.07) p.Glow(0, 0, 9, WHITE, 0.4);
            }
            // the sparks: a bright head, the zigzag behind it cooling
            foreach (Kink s in kinked)
            {
                if (s.age < 0 || s.age >= s.life) continue;
                double k = 1 - s.age / s.life;
                var path = new List<double[]>(s.trail) { new[] { s.x, s.y } };
                for (int i = 0; i < path.Count - 1; i++)
                    p.Line(path[i][0], path[i][1], path[i + 1][0], path[i + 1][1], i == path.Count - 2 ? (k > 0.5 ? WHITE : C[6]) : k > 0.5 ? C[5] : C[4], 0.6 + 0.4 * ((double)i / path.Count));
                p.Px(s.x, s.y, k > 0.4 ? WHITE : C[5]);
                p.Glow(s.x, s.y, 4, C[5], 0.4 * k);
            }
            // the light strobes as it dies
            if (t >= SNAP) p.Glow(0, 0, 40 * Math.Max(0, 1 - t / 0.85), C[3], 0.45 * Exp(-(t - SNAP) * 5) * (f % 2 != 0 ? 0.7 : 1));
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
