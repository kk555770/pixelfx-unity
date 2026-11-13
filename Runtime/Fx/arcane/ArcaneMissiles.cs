// The bolt and its pop (a layered head with spinning rays, a flash disc, a cross, a ring, plasma sparks and stars)
// are adapted from Sprite Fusion's open-source energy bolt (https://www.spritefusion.com/pixel-art-vfx/energy),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class ArcaneMissiles : Effect
    {
        const double HX = 15, HY = -24, TX = 152, TY = -22;
        const int TRAIL = 18;

        static readonly Pose OPEN = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = 4, ["fx"] = 18, ["fy"] = -24, ["bx"] = -6, ["by"] = -22, ["ffx"] = 11, ["fbx"] = -9 };
        static readonly Pose KICK = new Pose(OPEN) { ["fx"] = 15, ["fy"] = -26, ["lean"] = 2 };
        static readonly Pose GATHER = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = -2, ["fx"] = 6, ["fy"] = -24, ["bx"] = 8, ["by"] = -24, ["ffx"] = 9, ["fbx"] = -7 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 152 },
            Poses = new[]
            {
                new PoseKey(-0.52, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(-0.24, GATHER),
                new PoseKey(0, new Pose(GATHER) { ["crouch"] = 5 }),
                new PoseKey(0.05, KICK, "snap"),
                new PoseKey(0.1, OPEN, "snap"),
                new PoseKey(0.15, KICK, "snap"),
                new PoseKey(0.2, OPEN, "snap"),
                new PoseKey(0.25, KICK, "snap"),
                new PoseKey(0.29, OPEN, "snap"),
                new PoseKey(0.33, KICK, "snap"),
                new PoseKey(0.38, OPEN, "snap"),
                new PoseKey(0.43, KICK, "snap"),
                new PoseKey(0.48, OPEN, "snap"),
                new PoseKey(1.2, new Pose(OPEN) { ["crouch"] = 3 }),
                new PoseKey(1.7, new Pose(Poses.READY) { ["x"] = -3 }),
            },
            Hits = new[]
            {
                new HitCue { T = 0.77, Stop = 0.02, Push = 0.7, Straw = 4, Y = -24, Flash = 0.03 },
                new HitCue { T = 0.85, Stop = 0.02, Push = 0.7, Straw = 4, Y = -24, Flash = 0.03 },
                new HitCue { T = 0.95, Stop = 0.02, Push = 0.7, Straw = 4, Y = -24, Flash = 0.03 },
                new HitCue { T = 1.08, Stop = 0.02, Push = 0.7, Straw = 4, Y = -24, Flash = 0.03 },
                new HitCue { T = 1.2, Stop = 0.07, Shake = 0.14, Amp = 1, Push = 1.4, Straw = 9, Y = -24 },
            },
        };

        public static readonly Module Module = new Module("arcane-missiles", "pixelfx/fx/arcane/arcane-missiles", 240, 116, 26, 88, 1.75, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 3 },
            (o, e) => new ArcaneMissiles(o, e), act: Act, actor: 2)
        {
            Title = "Arcane missiles",
            Desc = "A volley of violet bolts fans out of the hand, curls round on twisting ribbons and pops on the target one after another.",
            Tags = new[] { "arcane", "missile", "projectile", "homing", "volley", "magic", "directional", "one-shot" },
            Bg = "ground",
        };

        sealed class Bolt
        {
            public double t0, x, y, vx, vy, hit = -1, tx, ty, tw, acc;
            public bool on;
            public readonly List<double[]> trail = new List<double[]>();
        }

        sealed class Pop
        {
            public double t, x, y;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Bolt[] bolts;
        readonly List<Pop> pops = new List<Pop>();
        double t;

        ArcaneMissiles(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            // each bolt leaves the hand on its own heading, then homes in harder and faster until it lands
            double[] fan = { -1.05, 0.7, -0.5, 1.1, -1.4 };
            bolts = new Bolt[fan.Length];
            for (int i = 0; i < fan.Length; i++)
            {
                double a = fan[i];
                a += R.Range(-0.12, 0.12);
                double sp = R.Range(130, 165);
                var b = new Bolt { t0 = 0.05 + i * 0.1, x = HX, y = HY, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp };
                b.tx = TX + R.Range(-3, 3);
                b.ty = TY + R.Range(-6, 6);
                b.tw = R.Range(0, TAU);
                bolts[i] = b;
            }
        }

        void Ribbon(Painter p, Bolt b)
        {
            List<double[]> pts = b.trail;
            int n = pts.Count;
            double fade = b.hit < 0 ? 1 : 1 - Clamp((t - b.hit) / 0.16);
            if (n < 2 || fade <= 0) return;
            for (int i = 0; i < n - 1; i++)
            {
                double x0 = pts[i][0], y0 = pts[i][1], x1 = pts[i + 1][0], y1 = pts[i + 1][1], L = Hypot(x1 - x0, y1 - y0);
                if (L == 0 || double.IsNaN(L)) L = 1;
                double nx = -(y1 - y0) / L, ny = (x1 - x0) / L;
                for (double d = 0; d < L; d += 0.5)
                {
                    double u = (i + d / L) / (n - 1), x = x0 + ((x1 - x0) * d) / L, y = y0 + ((y1 - y0) * d) / L, a = (1 - u) * 1.7 * fade;
                    // the body: a capsule that tapers down the tail, white along its middle near the head, cooling outward
                    double w = 2.6 * Px.Pow(1 - u, 0.8) + 0.3;
                    for (double q = -w; q <= w; q += 0.5)
                    {
                        double v = Math.Abs(q) / w;
                        p.Dot(x + nx * q, y + ny * q, v < 0.35 ? (u < 0.25 ? WHITE : C[6]) : v < 0.7 ? (u < 0.4 ? C[6] : C[5]) : u < 0.5 ? C[5] : C[4], a * (v < 0.7 ? 1 : 0.75));
                    }
                    // two strands twisting round it: bright where they come round the front, dim behind
                    foreach (double s in new[] { 0, Math.PI })
                    {
                        double ph = u * 10 - t * 26 + b.tw + s, off = Px.Sin(ph) * (w + 1.4);
                        if (Px.Cos(ph) > -0.2) p.Dot(x + nx * off, y + ny * off, Px.Cos(ph) > 0.4 ? (u < 0.35 ? WHITE : C[6]) : C[4], a);
                    }
                }
                if (i % 3 == 0) p.Glow(x0, y0, 9 * (1 - (double)i / n) + 3, C[5], 0.3 * (1 - (double)i / n) * fade);
            }
        }

        void Head(Painter p, Bolt b)
        {
            double x = b.x, y = b.y;
            for (int i = 0; i < 4; i++)
            {
                double a = t * 24 + (i * TAU) / 4 + b.tw, l = 5.5 + 1.5 * Px.Sin(t * 50 + i);
                p.Line(x + Px.Cos(a) * 3.2, y + Px.Sin(a) * 3.2, x + Px.Cos(a) * l, y + Px.Sin(a) * l, C[6]);
            }
            p.Circle(x, y, 3.8, C[4]);
            p.Circle(x, y, 3, C[5]);
            p.Circle(x, y, 2.1, C[6]);
            p.Circle(x, y, 1.2, WHITE);
            p.Glow(x, y, 24, C[5], 0.6);
            p.Glow(x, y, 7, WHITE, 0.4);
        }

        void DrawPop(Painter p, double q, double x, double y)
        {
            if (q < 0.16)
            {
                double r = 9 * (1 - q / 0.16) + 1.5;
                p.Circle(x, y, r + 1.5, C[5]);
                p.Circle(x, y, r, WHITE);
            }
            if (q < 0.4)
            {
                double l = 15 * (1 - q / 0.4) + 3;
                for (double d = -l; d <= l; d++)
                {
                    double a = Math.Abs(d) / l;
                    uint c = a < 0.3 ? WHITE : a < 0.65 ? C[6] : C[5];
                    p.Dot(x + d, y, c, 1.15 - a);
                    p.Dot(x, y + d * 0.7, c, 1.15 - a);
                }
            }
            double rr = 3 + 18 * Ease.OutCubic(q);
            p.Ring(x, y, rr + 0.5, Math.Max(1, 3 * (1 - q)), q < 0.3 ? WHITE : C[6], 1.2 - q);
        }

        // the lock: four brackets close on the target and pulse as each bolt lands
        void Lock(Painter p)
        {
            double k = Smooth(0, 0.22, t) * (1 - Smooth(1.15, 1.45, t));
            if (k <= 0) return;
            double kick = 0;
            foreach (Pop q in pops)
            {
                double z = (t - q.t) / 0.06;
                kick = Math.Max(kick, Exp(-(z * z)));
            }
            double r = 9 + 10 * (1 - Ease.OutCubic(Smooth(0, 0.22, t))) + 3 * kick, spin = t * 1.5;
            for (int i = 0; i < 4; i++)
            {
                double a0 = spin + (i * TAU) / 4 - 0.38;
                for (double a = a0; a <= a0 + 0.76; a += 0.06) p.Dot(TX + Px.Cos(a) * r, TY + Px.Sin(a) * r, kick > 0.4 ? WHITE : C[5], k);
            }
            p.Glow(TX, TY, 12, C[4], 0.2 * k);
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Bolt b in bolts)
            {
                if (t < b.t0 || b.hit >= 0) continue;
                if (!b.on)
                {
                    b.on = true;
                    lights.Add(HX, HY, 28, C[5], 0.7, 0.14);
                    for (int i = 0; i < 4; i++)
                        ps.Spawn(P.Mote, HX, HY, Px.Cos(Atan2(b.vy, b.vx) + R.Range(-0.6, 0.6)) * R.Range(60, 140), Px.Sin(Atan2(b.vy, b.vx) + R.Range(-0.6, 0.6)) * R.Range(60, 140), R.Range(0.12, 0.25), C[6], 0.6);
                }
                double age = t - b.t0, dx = b.tx - b.x, dy = b.ty - b.y, d = Hypot(dx, dy);
                if (d == 0 || double.IsNaN(d)) d = 1;
                double sp = 150 + 560 * age * age, steer = Math.Min(1, (1.4 + 15 * age) * dt);
                b.vx += ((dx / d) * sp - b.vx) * steer;
                b.vy += ((dy / d) * sp - b.vy) * steer;
                b.x += b.vx * dt;
                b.y += b.vy * dt;
                b.trail.Insert(0, new[] { b.x, b.y });
                if (b.trail.Count > TRAIL) b.trail.RemoveAt(b.trail.Count - 1);
                if ((b.acc += dt) > 0.012)
                {
                    b.acc = 0;
                    if (R.Chance(0.6)) ps.Spawn(P.Mote, b.x, b.y, -b.vx * 0.08 + R.Range(-25, 25), -b.vy * 0.08 + R.Range(-25, 25), R.Range(0.2, 0.4), R.Chance(0.5) ? C[6] : C[5], 0.55);
                }
                if (d < 3 || age > 1.3)
                {
                    b.hit = t;
                    pops.Add(new Pop { t = t, x = b.x, y = b.y });
                    lights.Add(b.x, b.y, 70, C[5], 0.9, 0.3);
                    lights.Add(b.x, b.y, 24, WHITE, 0.85, 0.09);
                    for (int i = 0; i < 16; i++)
                    {
                        double a = R.Range(0, TAU), v = R.Range(80, 230);
                        ps.Spawn(P.Spark, b.x, b.y, Px.Cos(a) * v, Px.Sin(a) * v - 40, R.Range(0.15, 0.45));
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        double a = R.Range(-Math.PI, 0.3), v = R.Range(40, 140);
                        ps.Spawn(i % 2 != 0 ? P.Star : P.Mote, b.x, b.y, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.25, 0.48), R.Pick(new[] { WHITE, C[6], C[5] }), R.Range(0.8, 1.6));
                    }
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the floor catches the light of each pop
            foreach (Pop q in pops)
            {
                double k = 1 - Clamp((t - q.t) / 0.5);
                if (k > 0) p.Glow(q.x, -1, 22, C[5], 0.25 * k * k);
            }
        }

        public override void Draw(Painter p)
        {
            Lock(p);
            foreach (Bolt b in bolts)
                if (b.on) Ribbon(p, b);
            foreach (Bolt b in bolts)
                if (b.on && b.hit < 0) Head(p, b);
            foreach (Pop q in pops)
            {
                double k = (t - q.t) / 0.35;
                if (k < 1) DrawPop(p, k, q.x, q.y);
            }
            if (t < 0.55) Sim.Sparkle(p, HX, HY, 3 + 2 * Px.Sin(t * 40), C[6], C[5], 0.45 * (1 - t / 0.55));
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
