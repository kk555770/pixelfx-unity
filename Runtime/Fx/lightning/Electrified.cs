// Arcs leaping between points of the silhouette's edge, re-struck every other frame, grounding to the floor, and
// the periodic jolt that throws sparks follow Sprite Fusion's open-source lightning crackle
// (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class Electrified : Effect
    {
        static readonly Pose TENSE = new Pose { ["turn"] = 0.9, ["crouch"] = 3, ["fx"] = -8, ["fy"] = -17, ["bx"] = 9, ["by"] = -17, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose JOLT_A = new Pose { ["turn"] = 1.1, ["crouch"] = 0, ["y"] = -1, ["fx"] = -13, ["fy"] = -36, ["bx"] = 13, ["by"] = -17, ["ffx"] = -4, ["fbx"] = 5 };
        static readonly Pose JOLT_B = new Pose { ["turn"] = 0.7, ["crouch"] = 1, ["y"] = -1, ["fx"] = -13, ["fy"] = -16, ["bx"] = 13, ["by"] = -36, ["ffx"] = -4, ["fbx"] = 5 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, TENSE),
                new PoseKey(0.1, JOLT_A, "snap"),
                new PoseKey(0.17, JOLT_B, "snap"),
                new PoseKey(0.3, TENSE, "snap"),
                new PoseKey(0.733, TENSE),
                new PoseKey(0.733, JOLT_B, "snap"),
                new PoseKey(0.8, JOLT_A, "snap"),
                new PoseKey(0.93, TENSE, "snap"),
                new PoseKey(1.367, TENSE),
                new PoseKey(1.367, JOLT_A, "snap"),
                new PoseKey(1.45, JOLT_B, "snap"),
                new PoseKey(1.58, TENSE, "snap"),
                new PoseKey(2, TENSE),
            },
        };

        public static readonly Module Module = new Module("electrified", "pixelfx/fx/lightning/electrified", 108, 92, 54, 76, 2, true, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["seed"] = 5 },
            (o, e) => new Electrified(o, e), act: Act, actor: 2)
        {
            Title = "Electrified",
            Desc = "Current runs through a character: arcs crawl over and leap across the body, sparks spit off, the body flares to an electric silhouette on each jolt, and the feet ground out.",
            Tags = new[] { "lightning", "electric", "status", "shock", "stun", "actor", "loop" },
            Bg = "ground",
        };

        const int FPS = 30, N = 60;
        const double P = 2;
        static readonly double[][] JOLTS = { new double[] { 3, 1 }, new[] { 22, 0.7 }, new[] { 41, 0.9 } };
        static readonly double[] LIFT = { 1.1, 0.6, -0.2 };

        // which jolt frame f is in: its age in frames (a) and strength (k), or a = -1
        static (int a, double k) Jolt(int f)
        {
            foreach (double[] j in JOLTS)
                if (f >= j[0] && f < j[0] + 3) return (f - (int)j[0], j[1]);
            return (-1, 0);
        }

        readonly Options o;

        Electrified(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor A = o.Actor;
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            var j = Jolt(f);
            bool jo = j.a >= 0;
            Rng r = NewRng(o.Seed, 4, f);
            // a cold aura: a broken outline that flickers round the body, light pooling at the feet
            p.Outline(A.Mask, A.X, A.Y, jo ? C[4] : r.Next() < 0.5 ? C[2] : C[1]);
            p.Glow(0, -18, 30, C[3], jo ? 0.55 * j.k : 0.16 + 0.1 * r.Next());
            p.Glow(0, -2, 22, C[4], 0.18);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), S = Ramp(o.Ramp("sparks"));
            Actor A = o.Actor;
            double[][] E = EdgeOf(A, true);
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            var j = Jolt(f);
            bool jo = j.a >= 0;
            // the jolt: the body flares to an electric silhouette, then fades back through its own shading
            if (jo)
            {
                double lift = LIFT[j.a] * j.k;
                uint[] ramp = { C[1], C[2], C[3], C[4], C[5], C[6], C[7] };
                for (int y = 0; y < A.H; y++)
                    for (int x = 0; x < A.W; x++)
                    {
                        uint c = A.Pixels[y * A.W + x];
                        if (c != 0) p.Px(A.X + x, A.Y + y, ramp[(int)Clamp(Round(0.4 + Luma(c) * 6 + lift), 0, 6)]);
                    }
                p.Glow(0, -19, 26, C[5], 0.5 * j.k * (1 - j.a / 3.0));
            }
            // arcs: two or three at a time, re-struck every other frame, leaping between nearby points of the edge
            Rng r = NewRng(o.Seed, 7, f >> 1);
            int n = 2 + (jo ? 2 : r.Int(0, 1));
            for (int i = 0; i < n; i++)
            {
                double[] a = r.Pick(E);
                double ang = r.Range(0, TAU), d = r.Range(6, 14);
                double[] b = { a[0] + Px.Cos(ang) * d, a[1] + Px.Sin(ang) * d };
                for (int k2 = 0; k2 < 10; k2++)
                {
                    double[] c = r.Pick(E);
                    if (Hypot(c[0] - b[0], c[1] - b[1]) < 5 && Hypot(c[0] - a[0], c[1] - a[1]) > 4) b = c;
                }
                double k = ((f & 1) != 0 ? 0.75 : 0.95) * r.Range(0.6, 1);
                Crackle(p, Zigzag(NewRng(o.Seed, 8, f, i), a[0], a[1], b[0], b[1], 0.42, 0.35), k, C, 0.4);
                // the edge catches the light where an arc touches it
                foreach (double[] e in E)
                    if (Hypot(e[0] - a[0], e[1] - a[1]) < 3.5 || Hypot(e[0] - b[0], e[1] - b[1]) < 3) p.Px(e[0] - 0.5, e[1] - 0.5, C[5]);
            }
            // the feet ground out to the floor now and then
            if (r.Next() < (jo ? 0.9 : 0.35))
            {
                int s = r.Sign();
                double x0 = s * r.Range(3, 8);
                Crackle(p, Zigzag(NewRng(o.Seed, 9, f), x0, -1.5, x0 + s * r.Range(5, 12), -0.5, 0.45, 0.3), 0.7, C, 0.35);
                p.Glow(x0, -1, 10, C[5], 0.35);
            }
            // sparks: a spray off the edge with every jolt, and a few spat out between them
            foreach (double[] jt in JOLTS)
            {
                double f0 = jt[0], k = jt[1], age = (((t - f0 / FPS) % P) + P) % P;
                for (int i = 0; i < 9; i++)
                {
                    Rng q = NewRng(o.Seed, 21, f0, i);
                    double life = q.Range(0.18, 0.4);
                    if (age >= life) continue;
                    double[] e0 = E[(int)Math.Floor(q.Next() * E.Length)];
                    double x0 = e0[0], y0 = e0[1], ang = Atan2(y0 + 19, x0) + q.Range(-0.7, 0.7), sp = q.Range(60, 150) * k;
                    double[] s = Flight(x0, y0, Px.Cos(ang) * sp, Px.Sin(ang) * sp - 30, age, floor: 0);
                    Spark(p, s[0], s[1], s[2], s[3], 1 - age / life, S);
                }
            }
            Cycle(t, P, 8, o.Seed + 3, g => g.Range(0.15, 0.3), (age, life, q, _) =>
            {
                double[] e0 = E[(int)Math.Floor(q.Next() * E.Length)];
                double x0 = e0[0], y0 = e0[1], ang = Atan2(y0 + 19, x0) + q.Range(-0.6, 0.6), sp = q.Range(50, 110);
                double[] s = Flight(x0, y0, Px.Cos(ang) * sp, Px.Sin(ang) * sp - 20, age, floor: 0);
                Spark(p, s[0], s[1], s[2], s[3], 1 - age / life, S);
            });
        }
    }
}
