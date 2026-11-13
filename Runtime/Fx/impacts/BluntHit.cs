// The clods and the dust they kick up are adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class BluntHit : Effect
    {
        public static readonly Module Module = new Module("blunt-hit", "pixelfx/fx/impacts/blunt-hit", 128, 88, 64, 72, 0.9, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1d1620", "#4a3a3c", "#7d6a66", "#b09c90", "#dccab4", "#f6ead2", "#ffffff" },
                ["warm"] = "#ffc98a",
                ["dust"] = "#b7a8a6",
                ["clod"] = "#6e5a5a",
                ["height"] = 22,
                ["seed"] = 9,
            },
            (o, e) => new BluntHit(o, e))
        {
            Title = "Blunt hit",
            Desc = "A heavy, soft thud: a white-hot lumpy blob cools to cream in a warm pool of light, inked wedges jab out and snap back, a squashed ring of dust puffs swells and drifts, clods drop.",
            Tags = new[] { "impact", "hit", "blunt", "punch", "club", "thump", "dust", "comic", "one-shot" },
            Bg = "ground",
        };

        // a lumpy outline: radius at angle a for a blob of radius r
        static double Lump(double r, double a, double ph) => r * (1 + 0.15 * Px.Sin(5 * a + ph) + 0.08 * Px.Sin(9 * a + ph * 2.3));

        sealed class Wedge
        {
            public double a, len, w, r0, t0, @out, back;
        }

        sealed class Puff
        {
            public double x, y, vx, vy, r, life, max, t0, lift;
        }

        sealed class Clod
        {
            public double[] pts;
            public double pvy;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double X, Y, PH;
        readonly uint WARM, DUST;
        readonly ImpactsLib.Stone st;
        readonly Emitter emitter;
        readonly Lights lights;
        readonly Bits bits;
        readonly Dictionary<Bits.Bit, Clod> clods = new Dictionary<Bits.Bit, Clod>();
        readonly Wedge[] wedges;
        readonly List<Puff> puffs = new List<Puff>();
        readonly List<Puff> kicks = new List<Puff>();
        double t;

        BluntHit(Options o, Emitter e)
        {
            emitter = e;
            C = Fit(o.Ramp("palette"), 7);
            R = new Rng(o.Seed);
            X = 0;
            Y = -o.Num("height");
            PH = R.Range(0, TAU);
            Func<double, double> floor = x => 0;
            WARM = o.Color("warm");
            DUST = o.Color("dust");
            st = new ImpactsLib.Stone(o.Color("clod"));
            lights = new Lights(e);
            bits = new Bits(floor, e);
            // a warm pool of light, a white-hot heart, and a glow on the floor below
            lights.Add(X, Y, 48, WARM, 0.6, 0.4);
            lights.Add(X, Y, 16, WHITE, 0.6, 0.07);
            lights.Add(X, -2, 36, WARM, 0.25, 0.4);
            // inked wedges: each jabs out at its own length and pace, then snaps back
            wedges = new Wedge[9];
            for (int i = 0; i < 9; i++)
                wedges[i] = new Wedge
                {
                    a = (double)i / 9 * TAU + R.Range(-0.2, 0.2), len = R.Range(6, 15), w = R.Range(3.2, 5), r0 = R.Range(13, 16), t0 = R.Range(0.015, 0.04),
                    @out = R.Range(0.04, 0.06), back = R.Range(0.09, 0.14),
                };
            // the squash ring: soft puffs flung out from the blob's rim, wider than tall, each its own size, pace and life
            for (int i = 0; i < 30; i++)
            {
                double a = (double)i / 30 * TAU + R.Range(-0.12, 0.12), side = Math.Abs(Px.Cos(a)), v = R.Range(40, 140) * (0.6 + 0.5 * side);
                puffs.Add(new Puff
                {
                    x = X + Px.Cos(a) * 9, y = Y + Px.Sin(a) * 6, vx = Px.Cos(a) * v, vy = Px.Sin(a) * v * 0.5, r = R.Range(2.4, 6.2) * (0.75 + 0.35 * side),
                    life = R.Range(0.35, 0.85), max = 0, t0 = R.Range(0.03, 0.1), lift = R.Range(4, 16),
                });
            }
            // and a few that billow up off the top and hang longer
            for (int i = 0; i < 5; i++)
                puffs.Add(new Puff
                {
                    x = X + R.Range(-6, 6), y = Y - R.Range(3, 6), vx = R.Range(-30, 30), vy = -R.Range(20, 45), r = R.Range(3.5, 5.5), life = R.Range(0.6, 0.85), max = 0,
                    t0 = R.Range(0.05, 0.1), lift = R.Range(14, 24),
                });
            foreach (Puff q in puffs) q.max = q.life;
            // heavier clods knocked loose: they arc out, drop to the floor, bounce once and settle
            for (int i = 0; i < 5; i++)
            {
                int s = R.Sign();
                double a = -Math.PI / 2 + s * R.Range(0.5, 1.3), v = R.Range(70, 140);
                var b = new Bits.Bit
                {
                    kind = B.Chunk, x = X + s * R.Range(4, 8), y = Y + R.Range(-3, 3), vx = Px.Cos(a) * v, vy = Px.Sin(a) * v, g = 1, drag = 0.6, bounce = 0.28,
                    life = R.Range(0.65, 0.85), a = R.Range(0, TAU), va = s * R.Range(8, 16), s = R.Chance(0.4) ? 2 : 1,
                };
                clods[b] = new Clod { pts = ImpactsLib.RockShape(R) };
                bits.Add(b);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Puff q in puffs)
            {
                if (t < q.t0) continue;
                q.life -= dt;
                q.vx -= q.vx * 4.5 * dt;
                q.vy -= q.vy * 4.5 * dt + q.lift * dt; // they slow, and lift a little as they thin
                q.x += q.vx * dt;
                q.y += q.vy * dt;
            }
            foreach (Bits.Bit b in bits.List) clods[b].pvy = b.vy;
            bits.Update(dt);
            // a clod hitting the floor kicks a little dust
            foreach (Bits.Bit b in bits.List)
            {
                Clod c = clods[b];
                if (c.pvy > 60 && b.vy <= 0)
                {
                    for (int i = 0; i < 2; i++)
                        kicks.Add(new Puff { x = b.x, y = -1, vx = R.Sign() * R.Range(8, 22), vy = -R.Range(3, 10), r = R.Range(1.2, 2), life = 0.4, max = 0.4, t0 = 0 });
                    c.pvy = 0;
                }
            }
            foreach (Puff q in kicks)
            {
                q.life -= dt;
                q.vx -= q.vx * 4 * dt;
                q.vy -= q.vy * 4 * dt;
                q.x += q.vx * dt;
                q.y += q.vy * dt;
            }
            lights.Update(dt);
        }

        void DrawPuff(Painter p, Puff q)
        {
            double k = q.life / q.max;
            if (t < q.t0 || k <= 0) return;
            double r = q.r * (0.7 + (1 - k) * 0.85), a = Px.Pow(k, 1.25) * Math.Min(1, (t - q.t0) / 0.03);
            uint c = Mix(DUST, WARM, Clamp(1 - (t - 0.03) / 0.25) * 0.55);
            p.Disc(q.x, q.y, r, c, (d, g) => 0.58 * a * (1 - d * d * 0.75));
            p.Disc(q.x - r * 0.22, q.y - r * 0.32, r * 0.58, Mix(c, WHITE, 0.55), (d, g) => 0.48 * a * (1 - d * d));
        }

        public override void Draw(Painter p)
        {
            // dust: each puff swells and thins, warm-lit at first, its top catching the light
            foreach (Puff q in puffs) DrawPuff(p, q);
            foreach (Puff q in kicks) DrawPuff(p, q);
            foreach (Bits.Bit b in bits.List) ImpactsLib.Rock(p, emitter, b, clods[b].pts, st);
            // the blob: white-hot and lumpy for a frame, then cream and a little bigger, then gone into the dust
            if (t < 0.058)
            {
                int f = t < 0.025 ? 0 : 1;
                double R0 = f != 0 ? 13.5 : 11.5;
                p.Shade(X - R0 * 1.4, Y - R0 * 1.4, X + R0 * 1.4, Y + R0 * 1.4, (x, y, o) =>
                {
                    double dx = x - X, dy = (y - Y) * (f != 0 ? 1.25 : 1.1), d = Hypot(dx, dy), ro = Lump(R0, Atan2(dy, dx), PH + f * 0.6);
                    if (d > ro) return 0;
                    double q = d / ro;
                    return f == 0 ? (q < 0.55 ? WHITE : q < 0.86 ? C[5] : C[4]) : q < 0.3 ? WHITE : q < 0.72 ? C[5] : q < 0.9 ? C[4] : C[3];
                });
            }
            // inked wedges jab out from the blob and snap back
            foreach (Wedge w in wedges)
            {
                double u = t - w.t0;
                if (u < 0 || u > w.@out + w.back) continue;
                double s = u < w.@out ? Ease.OutCubic(u / w.@out) : 1 - Ease.InQuad((u - w.@out) / w.back), L = w.len * s;
                if (L < 1.5) continue;
                double ca = Px.Cos(w.a), sa = Px.Sin(w.a), r0 = w.r0 + 2 * s, hw = w.w * (0.6 + 0.4 * s) / 2;
                double tipX = X + ca * r0, tipY = Y + sa * r0, endX = X + ca * (r0 + L), endY = Y + sa * (r0 + L), nX = -sa * hw, nY = ca * hw;
                double[][] Shape(double g) => new[]
                {
                    new[] { tipX - ca * g, tipY - sa * g },
                    new[] { endX + nX * (1 + g * 0.45) + ca * g, endY + nY * (1 + g * 0.45) + sa * g },
                    new[] { endX - nX * (1 + g * 0.45) + ca * g, endY - nY * (1 + g * 0.45) + sa * g },
                };
                p.Poly(Shape(1), C[1]);
                p.Poly(Shape(0), u < w.@out + 0.02 ? WHITE : C[5]);
            }
            double pop = Exp(-t / 0.06);
            if (pop > 0.02) p.Glow(X, Y, 28, WARM, 0.5 * pop);
            lights.Draw(p);
        }
    }
}
