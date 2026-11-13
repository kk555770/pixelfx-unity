// The crack-and-burst recipe (cracks walking out from the point of impact, a white frame, the ice flung apart
// as shards with ice dust and glints) is adapted from Sprite Fusion's open-source ice
// (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class IceShatter : Effect
    {
        const double DURATION = 2.8;

        public static readonly Module Module = new Module("ice-shatter", "pixelfx/fx/ice/ice-shatter", 224, 112, 68, 90, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["seed"] = 6,
            },
            (o, e) => new IceShatter(o, e))
        {
            Title = "Ice shatter",
            Desc = "A block of ice takes a hit from the left: it jolts, cracks race over its faces, it flashes white and bursts into chunks that tumble, bounce and skid off in a roll of ice dust.",
            Tags = new[] { "ice", "shatter", "break", "destroy", "impact", "directional", "one-shot" },
            Bg = "ground",
        };

        const double HIT = 0.26, CRACK2 = 0.4, SHIVER = 0.54, FLASH = 0.62, BURST = 0.68;
        // the block, seen from the front and a little above: a front face, a top, a side in shadow
        static readonly double[][] FRONT = { new double[] { -16, 0 }, new double[] { -16, -28 }, new double[] { 12, -28 }, new double[] { 12, 0 } };
        static readonly double[][] TOP = { new double[] { -16, -28 }, new double[] { -8, -34 }, new double[] { 20, -34 }, new double[] { 12, -28 } };
        static readonly double[][] SIDE = { new double[] { 12, 0 }, new double[] { 12, -28 }, new double[] { 20, -34 }, new double[] { 20, -6 } };
        static readonly double[][] SIL = { new double[] { -16, 0 }, new double[] { -16, -28 }, new double[] { -8, -34 }, new double[] { 20, -34 }, new double[] { 20, -6 }, new double[] { 12, 0 } };
        static readonly double[] HIT_AT = { -16, -15 };
        static readonly double[][] PLANE = { new double[] { -16, -21 }, new double[] { -5, -28 }, new double[] { 3, -13 }, new double[] { -16, -9 } }; // a fracture plane inside, lighter
        static readonly double[][] SHADE = { new double[] { 5, -28 }, new double[] { 12, -28 }, new double[] { 12, -10 } }; // the far corner in shadow
        static readonly double[][] RIM = { new[] { -17, 1.0 }, new[] { -17, -28.5 }, new[] { -8.3, -35.2 }, new[] { 21, -35.2 }, new[] { 21, -5.6 }, new[] { 12.5, 1 } };

        sealed class Scuff
        {
            public double x, t;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Panes panes;
        readonly List<Scuff> scuffs = new List<Scuff>();
        readonly List<double[]> veins = new List<double[]>();
        readonly List<int[]> bubbles = new List<int[]>();
        readonly List<double[][]> cracks, cracks2;
        double t;
        bool struck, burst, wave2, white;

        IceShatter(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            panes = new Panes(floor,
                onLand: (q, x, y, v) =>
                {
                    // a chunk hitting the floor: a puff of ice dust, a few splinters, sometimes a glint
                    if (v < 100) return;
                    ps.Spawn(P.Vapour, x, y - 1, q.vx * 0.3 + R.Range(-15, 15), -R.Range(2, 10), R.Range(0.4, 0.7), C[6], R.Range(1.6, 2.8));
                    Splinter(x, y, q.vx, 2);
                    if (R.Chance(0.3)) ps.Spawn(P.Star, x, y - 3, 0, -5, R.Range(0.2, 0.4), WHITE, R.Range(0.8, 1.6));
                },
                onCrumble: q =>
                {
                    Splinter(q.x, 0, q.vx, 3);
                    ps.Spawn(P.Vapour, q.x, -1, R.Range(-10, 14), -R.Range(2, 6), R.Range(0.4, 0.7), C[6], R.Range(1.2, 2));
                },
                onSkid: (q, x, y) =>
                {
                    // sliding chunks scuff frost onto the floor
                    if (R.Chance(0.5)) scuffs.Add(new Scuff { x = Round(x + R.Range(-1, 1)), t = 0 });
                },
                emitter: e);

            // the ice's insides: veins of frost and trapped bubbles in the front face
            for (int k = 0; k < 3; k++)
            {
                double x = R.Range(-12, 8), y = R.Range(-24, -6), a = R.Range(0, TAU);
                for (int i = 0; i < 9; i++)
                {
                    a += R.Range(-0.6, 0.6);
                    x += Px.Cos(a) * 1.5;
                    y += Px.Sin(a) * 1.2;
                    if (x < -14 || x > 10 || y < -26 || y > -2) break;
                    veins.Add(new[] { Math.Floor(x), Math.Floor(y) });
                }
            }
            for (int k = 0; k < 5; k++)
            {
                int bx = R.Int(-12, 8), by = R.Int(-23, -4);
                bubbles.Add(new[] { bx, by, R.Chance(0.4) ? 1 : 0 });
            }
            cracks = Fractures(R, Inside, HIT_AT[0] + 1, HIT_AT[1], 4, len: 8, a0: -0.9, spread: 1.9);
            cracks2 = Fractures(R, Inside, HIT_AT[0] + 1, HIT_AT[1], 6, len: 18, a0: -1.3, spread: 2.7, fork: 0.16);
        }

        static bool Inside(double x, double y) => InPoly(SIL, x, y);

        void Splinter(double x, double y, double vx, int n)
        {
            for (int i = 0; i < n; i++)
                bits.Add(new Bits.Bit { kind = B.Shard, x = x + R.Range(-2, 2), y = y - 1, vx = vx * 0.6 + R.Range(-35, 45), vy = -R.Range(30, 100), g = 0.8, drag = 0.6, bounce = 0.35, life = R.Range(0.4, 0.8), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1.2, 2.4), c = C[R.Int(4, 7)] });
        }

        int Jolt() => t >= HIT && t < HIT + 0.05 ? 2 : t >= HIT + 0.05 && t < HIT + 0.12 ? 1 : t >= SHIVER && t < FLASH ? (Math.Floor(t * 40) % 2 != 0 ? 1 : 0) : 0;

        static double[][] Shift(double[][] pts, double jx) => pts.Select(q => new[] { q[0] + jx, q[1] }).ToArray();

        // the block: shaded faces, frost veins and bubbles inside, lit edges, a dark rim
        void DrawBlock(Painter p)
        {
            if (t >= BURST) return;
            int jx = Jolt();
            bool wh = t >= FLASH;
            double sweep = -40 + 70 * Seg(t, 0.04, 0.3);
            // rim
            p.Poly(Shift(RIM, jx), C[0]);
            if (wh)
            {
                p.Poly(Shift(SIL, jx), WHITE);
                return;
            }
            // the front: lit at the top, deep at the foot, the far edges showing through, a fracture plane catching
            // light, reflections streaked across the upper left
            p.Poly(Shift(FRONT, jx), (x, y) =>
            {
                double lx = x - jx, d = lx + y * 0.8;
                if (Math.Abs(d - sweep) < 1.3 && y < -6) return WHITE;
                if (y < -9 && (Math.Abs(d + 21) < 1.1 || Math.Abs(d + 16.5) < 0.6)) return C[6];
                if (((Math.Abs(lx + 7.5) < 0.5 && y < -6) || (Math.Abs(y + 5.5) < 0.5 && lx > -8)) && (Math.Floor(lx) + Math.Floor(y)) % 2 == 0) return C[5];
                if (InPoly(PLANE, lx, y)) return y < -25.5 ? C[6] : C[5];
                if (InPoly(SHADE, lx, y)) return C[3];
                return y < -25.5 ? C[5] : y < -8 ? C[4] : y < -2.5 ? C[3] : C[2];
            });
            p.Poly(Shift(TOP, jx), (x, y) => y > -29.5 ? C[7] : y < -32.5 ? C[5] : Rand(Math.Floor(x), Math.Floor(y), 3) < 0.08 ? C[7] : C[6]);
            p.Poly(Shift(SIDE, jx), (x, y) =>
            {
                double lx = x - jx;
                return lx < 13.5 ? C[3] : Math.Abs(lx - 16.5) < 0.6 && y < -8 ? C[3] : y > (lx - 12) * -0.75 - 3 ? C[1] : C[2];
            });
            foreach (double[] v in veins) p.Px(v[0] + jx, v[1], C[5]);
            foreach (int[] b in bubbles)
            {
                p.Px(b[0] + jx, b[1], C[6]);
                if (b[2] != 0)
                {
                    p.Px(b[0] + jx + 1, b[1], C[5]);
                    p.Px(b[0] + jx, b[1] + 1, C[5]);
                }
            }
            void E(double ax, double ay, double bx, double by, uint c) => p.Line(ax + jx, ay, bx + jx, by, c);
            E(-16, -28, 12, -28, WHITE);
            E(-16, -28, -8, -34, C[6]);
            E(-8, -34, 20, -34, C[5]);
            E(12, -28, 20, -34, C[7]);
            E(-16, -1, -16, -28, C[5]);
            E(12, -1, 12, -28, C[6]);
            E(20, -6, 20, -34, C[3]);
            E(13, -1, 20, -6, C[1]);
            if (t >= HIT) DrawFractures(p, cracks, (t - HIT) * 170, C, ox: jx);
            if (t >= CRACK2) DrawFractures(p, cracks2, (t - CRACK2) * 200, C, ox: jx);
        }

        // the floor: a frost patch where the block stands, frost scuffed along by sliding chunks
        void DrawFloor(Painter p)
        {
            double outA = 1 - Smooth(BURST + 0.6, BURST + 1.5, t);
            if (outA > 0)
                for (int x = -22; x <= 26; x++)
                {
                    double d = Math.Abs(x - 2) / 24.0, h = Rand(x, 41), a = outA * Clamp((1.1 - d) / 0.25);
                    if (a <= 0) continue;
                    p.Dot(x, 0, h < 0.35 ? WHITE : C[6], a);
                    if (h < 0.4 && (x < -16 || x > 20)) p.Dot(x, -1, C[5], a);
                    for (int j = 1; j <= 1 + Round(Rand(x, 7) * 3 * (1 - d)); j++) p.Dot(x, j, j < 2 ? C[5] : C[4], a * (1 - j / 5.0));
                }
            foreach (Scuff s in scuffs)
            {
                double a = 1 - s.t / 0.9;
                if (a > 0)
                {
                    p.Dot(s.x, 0, s.t < 0.15 ? WHITE : C[6], a);
                    p.Dot(s.x, 1, C[4], a * 0.6);
                }
            }
        }

        List<double[][]> Chop(double[][] pts, double max, int depth)
        {
            var c = Centroid(pts);
            if (c.area < max || depth > 4) return new List<double[][]> { pts };
            var o = new List<double[][]>();
            foreach (double[][] q in SplitPoly(pts, c.x + R.Range(-1, 1), c.y + R.Range(-1, 1), R.Range(0, Math.PI))) o.AddRange(Chop(q, max, depth + 1));
            return o;
        }

        void Explode()
        {
            // chunks of each face, cut at random, flung away from the hit, mostly right and up
            foreach (var (face, tone, max) in new[] { (FRONT, 0.6, 46.0), (TOP, 1.0, 40.0), (SIDE, 0.3, 46.0) })
                foreach (double[][] part in Chop(face, max, 0))
                {
                    var m = Centroid(part);
                    double dx = m.x - HIT_AT[0], dy = m.y - HIT_AT[1], d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    double sp = R.Range(50, 150) * (0.7 + 0.3 * (1 - d / 40));
                    panes.Add(new Pane
                    {
                        pts = part.Select(q => new[] { q[0] - m.x, q[1] - m.y }).ToArray(),
                        x = m.x,
                        y = m.y,
                        vx = (dx / d) * sp * 0.9 + R.Range(40, 110),
                        vy = (dy / d) * sp * 0.4 - R.Range(60, 200),
                        a = 0,
                        va = R.Range(-10, 10),
                        f = R.Range(-0.9, 0.9),
                        vf = R.Range(-7, 7),
                        tone = tone,
                        lit = tone,
                        g = 0.9,
                        drag = 0.5,
                        bounce = 0.38,
                        solid = true,
                        brittle = 3,
                        crumble = R.Range(0.25, 0.6),
                        life = R.Range(1.5, 1.9),
                    });
                }
            for (int i = 0; i < 46; i++)
            {
                double x = R.Range(-15, 19), y = -R.Range(1, 33), dx = x - HIT_AT[0], dy = y - HIT_AT[1], d = Hypot(dx, dy);
                if (d == 0 || double.IsNaN(d)) d = 1;
                double sp = R.Range(60, 200);
                if (!Inside(x, y)) continue;
                bits.Add(new Bits.Bit { kind = R.Chance(0.15) ? B.Chunk : B.Shard, x = x, y = y, vx = (dx / d) * sp * 0.8 + R.Range(20, 90), vy = (dy / d) * sp * 0.4 - R.Range(60, 240), g = 0.85, drag = 0.6, bounce = 0.32, life = R.Range(0.8, 1.4), a = R.Range(0, TAU), va = R.Range(-22, 22), s = R.Range(1.2, 3.4), c = C[R.Int(4, 7)], ramp = new[] { C[1], C[3], C[4], C[6], C[7] } });
            }
            // ice dust rolls out along the floor, vapour hangs where it stood, a flurry, glints, a flash of light
            for (int i = 0; i < 9; i++) ps.Spawn(P.Vapour, R.Range(-6, 18), -R.Range(1, 6), R.Range(10, 130), -R.Range(2, 12), R.Range(0.6, 1.1), C[6], R.Range(2.2, 3.8));
            for (int i = 0; i < 5; i++) ps.Spawn(P.Dust, R.Range(-6, 16), -R.Range(1, 4), R.Range(30, 110), -R.Range(2, 8), R.Range(0.5, 0.9), C[5], R.Range(1.6, 2.8));
            for (int i = 0; i < 4; i++) ps.Spawn(P.Vapour, R.Range(-8, 18), -R.Range(8, 28), R.Range(-10, 50), -R.Range(8, 24), R.Range(0.6, 1), C[6], R.Range(2.6, 4));
            for (int i = 0; i < 16; i++)
            {
                double a = R.Range(-Math.PI * 0.9, -0.1), sp = R.Range(40, 140);
                ps.Spawn(P.Flake, R.Range(-8, 16), -R.Range(6, 28), Px.Cos(a) * sp + 20, Px.Sin(a) * sp, R.Range(1, 1.6), R.Chance(0.6) ? WHITE : C[6], R.Range(0.5, 1.6));
            }
            for (int i = 0; i < 7; i++) ps.Spawn(P.Star, R.Range(-14, 40), -R.Range(4, 40), R.Range(-8, 16), -R.Range(4, 16), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.4));
            lights.Add(2, -17, 64, WHITE, 0.85, 0.14);
            lights.Add(16, -18, 86, C[4], 0.5, 0.55);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the hit: a flash on the left face, chips and sparks thrown back, the block jolts
            if (!struck && t >= HIT)
            {
                struck = true;
                lights.Add(HIT_AT[0], HIT_AT[1], 24, WHITE, 0.6, 0.08);
                lights.Add(HIT_AT[0] + 6, HIT_AT[1], 40, C[5], 0.4, 0.3);
                for (int i = 0; i < 12; i++)
                {
                    double a = Math.PI + R.Range(-0.9, 0.9), sp = R.Range(50, 120);
                    ps.Spawn(P.Spark, HIT_AT[0] - 1, HIT_AT[1], Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.15, 0.4));
                }
                for (int i = 0; i < 6; i++)
                {
                    double a = Math.PI + R.Range(-1, 0.8), sp = R.Range(40, 100);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = HIT_AT[0] - 1, y = HIT_AT[1] + R.Range(-2, 2), vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - 60, g = 0.85, drag = 0.6, bounce = 0.3, life = R.Range(0.5, 0.9), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1.2, 2.4), c = C[R.Int(5, 7)] });
                }
            }
            if (!wave2 && t >= CRACK2)
            {
                wave2 = true;
                lights.Add(0, -16, 36, C[6], 0.35, 0.15);
                for (int i = 0; i < 3; i++)
                    bits.Add(new Bits.Bit { kind = B.Shard, x = R.Range(-14, 18), y = -R.Range(4, 30), vx = R.Range(-30, 30), vy = -R.Range(10, 50), g = 0.85, drag = 0.6, bounce = 0.3, life = R.Range(0.4, 0.7), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1, 2), c = C[R.Int(5, 7)] });
            }
            if (!white && t >= FLASH)
            {
                white = true;
                lights.Add(2, -17, 44, WHITE, 0.7, 0.08);
            }
            if (!burst && t >= BURST)
            {
                burst = true;
                Explode();
            }
            // glints wink on the settling debris
            if (burst && t < BURST + 1 && R.Chance(0.08) && bits.List.Count > 0)
            {
                Bits.Bit b = R.Pick(bits.List.ToArray());
                ps.Spawn(P.Star, b.x, b.y - 1, 0, -3, R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.5));
            }
            for (int i = scuffs.Count - 1; i >= 0; i--)
                if ((scuffs[i].t += dt) > 0.9)
                    scuffs.RemoveAt(i);
            ps.Update(dt);
            bits.Update(dt);
            panes.Update(dt);
            lights.Update(dt);
            EndBy(t, DURATION, ps, bits, panes);
        }

        public override void DrawBack(Painter p)
        {
            DrawFloor(p);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            DrawBlock(p);
            // the hit: a star and a short flare on the left face
            double h = t - HIT;
            if (h >= 0 && h < 0.2)
            {
                Sim.Sparkle(p, HIT_AT[0] - 1, HIT_AT[1], 9 * Ease.OutBack(Math.Min(1, h / 0.04)) * (1 - h / 0.2), C[6], C[4], 0.7);
                if (h < 0.08) Sim.Flare(p, HIT_AT[0] - 1, HIT_AT[1], 14 * (1 - h / 0.08), 1 - h / 0.08, C);
            }
            // cold light in the ice, brighter as it cracks; a pool on the floor
            double k = 1 - Smooth(BURST, BURST + 0.4, t), crk = Smooth(HIT, FLASH, t);
            if (k > 0)
            {
                p.Glow(2, -17, 34, C[4], (0.16 + 0.22 * crk) * k);
                p.Glow(2, -2, 30, C[4], 0.14 * k);
            }
            if (t >= FLASH && t < BURST + 0.12) Sim.Flare(p, 2, -17, 26 * (1 - Clamp((t - FLASH) / 0.18)), 1 - Clamp((t - FLASH) / 0.18), C);
            panes.Draw(p, C);
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
