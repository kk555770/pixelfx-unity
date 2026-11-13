// The hop timing, the link shapes that re-strike every other frame, the glowing nodes and the crawling arcs
// follow Sprite Fusion's open-source lightning (https://www.spritefusion.com/pixel-art-vfx/lightning),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class ChainLightning : Effect
    {
        static readonly Pose AIM = new Pose(Poses.READY) { ["x"] = -6, ["crouch"] = 4, ["lean"] = 4, ["fx"] = 16, ["fy"] = -16, ["bx"] = -6, ["by"] = -23, ["ffx"] = 11, ["fbx"] = -9 };
        static readonly Pose BACK = new Pose(Poses.READY) { ["x"] = -6, ["crouch"] = 4, ["lean"] = -3, ["fx"] = -5, ["fy"] = -32, ["bx"] = 8, ["by"] = -24, ["armsFront"] = true, ["ffx"] = 9, ["fbx"] = -7 };

        static HitCue Zap(int at, double t, double stop = 0.03) => new HitCue { T = t, At = at, Stop = stop, Push = 1.2, Straw = 6, Skin = "shock", Shiver = 0.7, Y = -22 };

        static HitCue ZapShake()
        {
            HitCue h = Zap(2, 0.23, 0.07);
            h.Shake = 0.14;
            h.Amp = 1;
            return h;
        }

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 58, 104, 150 },
            Poses = new[]
            {
                new PoseKey(-0.46, new Pose(Poses.READY) { ["x"] = -6 }),
                new PoseKey(-0.22, BACK),
                new PoseKey(0, new Pose(BACK) { ["crouch"] = 5 }),
                new PoseKey(0.04, AIM, "snap"),
                new PoseKey(0.7, new Pose(AIM) { ["crouch"] = 3 }),
                new PoseKey(1.35, new Pose(AIM) { ["crouch"] = 3, ["fy"] = -18 }),
                new PoseKey(1.8, new Pose(Poses.READY) { ["x"] = -6 }),
            },
            Hits = new[]
            {
                Zap(0, 0.05),
                Zap(1, 0.14),
                ZapShake(),
                new HitCue { T = 1.35, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
        };

        public static readonly Module Module = new Module("chain-lightning", "pixelfx/fx/lightning/chain-lightning", 248, 104, 40, 82, 1.8, false, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["seed"] = 3 },
            (o, e) => new ChainLightning(o, e), act: Act, actor: 2)
        {
            Title = "Chain lightning",
            Desc = "A bolt leaps from the caster's hand through three targets in turn; each flashes, sparks and grounds itself to the floor while the arcs between them flicker, then the chain snaps.",
            Tags = new[] { "lightning", "electric", "chain", "spell", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        static readonly double[] HAND = { 10, -16 };
        static readonly double[][] HOPS = { new double[] { 58, -22 }, new double[] { 104, -30 }, new double[] { 150, -19 } };
        const double WIND = 0.12, LEAP = 0.05, GAP = 0.075, HOLD = 0.78, BREAK = 0.16;
        static readonly uint ASH = Hex("#5b5566");
        static readonly double[] HUM = { 1, 0.55, 1 };

        static double On(int i) => WIND + i * GAP + LEAP; // when link i lands on its target
        static double Off(int i) => HOLD + i * 0.05; // when it starts to give out

        // a link's strength: a hard first flash, a restless hum, then it stutters out
        static double Level(int i, double t)
        {
            double a = t - On(i);
            if (a < 0) return 0;
            double hi = Math.Floor(a * 30);
            double hum = a < 0.1 ? (hi >= 0 && hi < HUM.Length ? HUM[(int)hi] : 1) : 0.78 + 0.22 * Px.Sin(t * 61 + i) * Px.Sin(t * 23 + i * 2);
            double b = t - Off(i);
            return b < 0 ? hum : b < BREAK ? hum * (1 - b / BREAK) * (Math.Floor(b * 60) % 3 == 1 ? 0.5 : 1) : 0;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps, hot;
        readonly Lights lights;
        readonly double[][] pts;
        readonly bool[] hit, popped;
        readonly double[] ground;
        readonly Options o;
        double t;

        ChainLightning(Options o, Emitter e)
        {
            this.o = o;
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            hot = new Particles(R, ground: x => 0, emitter: e);
            lights = new Lights(e);
            pts = new[] { HAND, HOPS[0], HOPS[1], HOPS[2] };
            hit = new bool[HOPS.Length];
            popped = new bool[HOPS.Length];
            // where each target grounds itself: a spot on the floor below it
            ground = new double[HOPS.Length];
            for (int i = 0; i < HOPS.Length; i++) ground[i] = HOPS[i][0] + R.Range(-5, 5);
        }

        Bolt Shape(int i, double f, int salt = 0)
        {
            Rng r = NewRng(o.Seed, 31 + salt, i, f);
            double x0 = pts[i][0], y0 = pts[i][1], x1 = pts[i + 1][0], y1 = pts[i + 1][1], L = Hypot(x1 - x0, y1 - y0);
            return Bowed(r, x0, y0, x1, y1, -L * r.Range(0.05, 0.15), 0.2, 0.08);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the charge gathers at the hand
            if (t < WIND && R.Chance(70 * dt))
            {
                double a = R.Range(0, TAU), d = R.Range(10, 24), T = R.Range(0.06, 0.1), v = (d * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, HAND[0] + Px.Cos(a) * d, HAND[1] + Px.Sin(a) * d, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            for (int i = 0; i < HOPS.Length; i++)
            {
                double x = HOPS[i][0], y = HOPS[i][1];
                if (!hit[i] && t >= On(i))
                {
                    hit[i] = true;
                    double px = pts[i][0], py = pts[i][1], dir = Atan2(y - py, x - px);
                    lights.Add(x, y, 56, C[4], 0.85, 0.22);
                    lights.Add(x, y, 16, WHITE, 0.7, 0.08);
                    if (i == 0) lights.Add(HAND[0], HAND[1], 40, C[4], 0.6, 0.15);
                    for (int j = 0; j < 18; j++)
                    {
                        double a = R.Chance(0.6) ? dir + R.Range(-1.1, 1.1) : R.Range(0, TAU), sp = R.Range(70, 230);
                        ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.15, 0.4));
                    }
                    for (int j = 0; j < 5; j++) hot.Spawn(P.Spark, x + R.Range(-2, 2), y + 2, R.Range(-50, 50), R.Range(-60, 20), R.Range(0.5, 0.9));
                    ps.Spawn(P.Star, x + R.Range(-4, 4), y - R.Range(3, 7), 0, -6, R.Range(0.25, 0.4), C[6], R.Range(1.5, 2.5));
                    for (int j = 0; j < 2; j++) ps.Spawn(P.Smoke, x + R.Range(-4, 4), y + R.Range(-2, 3), R.Range(-6, 6), -R.Range(8, 16), R.Range(0.7, 1), Mix(ASH, C[2], 0.35), R.Range(2, 3.5), R.Range(0.1, 0.25));
                }
                // while the chain holds: sparks shed off the links, spit from the floor where each target grounds
                if (hit[i] && t < Off(i))
                {
                    if (R.Chance(9 * dt))
                    {
                        double u = R.Next(), x0 = pts[i][0], y0 = pts[i][1];
                        ps.Spawn(P.Spark, Lerp(x0, x, u), Lerp(y0, y, u) - 2, R.Range(-60, 60), R.Range(-90, 20), R.Range(0.1, 0.25));
                    }
                    if (R.Chance(7 * dt)) ps.Spawn(P.Spark, ground[i], -1, R.Range(-70, 70), -R.Range(40, 140), R.Range(0.12, 0.3));
                    if (R.Chance(2 * dt)) hot.Spawn(P.Spark, x + R.Range(-3, 3), y + 3, R.Range(-30, 30), R.Range(-30, 10), R.Range(0.5, 0.8));
                }
                // when it gives out, the target pops once more and smokes
                if (!popped[i] && t >= Off(i) + BREAK)
                {
                    popped[i] = true;
                    for (int j = 0; j < 7; j++)
                    {
                        double a = R.Range(0, TAU), sp = R.Range(40, 140);
                        ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.12, 0.3));
                    }
                    ps.Spawn(P.Smoke, x, y, R.Range(-4, 4), -R.Range(10, 16), R.Range(0.55, 0.7), Mix(ASH, C[2], 0.35), R.Range(2.5, 3.5));
                    lights.Add(x, y, 30, C[4], 0.5, 0.12);
                }
            }
            ps.Update(dt);
            hot.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // the floor scorches where the targets ground themselves
            for (int i = 0; i < HOPS.Length; i++)
            {
                double a = t - On(i);
                if (a < 0) continue;
                double T0 = Math.Min(1, a / 0.2) * Px.Pow(Clamp(1 - (t - Off(i)) / 0.7), 1.3) * 0.9;
                Scorch(p, ground[i], 0, 7, T0, 1 - Smooth(1.35, 1.75, t));
            }
            ps.Draw(p, Layer.Back);
            hot.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            int f = (int)Math.Floor(t * 30 + 1e-4), f2 = f >> 1;
            // the hand: a node that swells while the charge gathers and holds the chain
            double kh = t < WIND ? t / WIND : Level(0, t);
            if (kh > 0.02)
            {
                double r = (1.2 + 1.6 * kh + Px.Sin(t * 53) * 0.4) * Math.Min(1, kh * 1.5);
                p.Circle(HAND[0], HAND[1], r + 1.2, C[4]);
                p.Circle(HAND[0], HAND[1], r, C[6]);
                p.Circle(HAND[0], HAND[1], r * 0.5, WHITE);
                p.Glow(HAND[0], HAND[1], 18, C[4], 0.6 * kh);
                if (t > WIND * 0.5)
                {
                    Rng r2 = NewRng(o.Seed, 5, f);
                    double a = r2.Range(0, TAU), d = r2.Range(4, 8);
                    Crackle(p, Zigzag(r2, HAND[0], HAND[1], HAND[0] + Px.Cos(a) * d, HAND[1] + Px.Sin(a) * d, 0.4, 0.3), 0.6 * kh, C, 0.3);
                }
            }
            for (int i = 0; i < HOPS.Length; i++)
            {
                double x = HOPS[i][0], y = HOPS[i][1], t0 = WIND + i * GAP;
                // the leap: the bolt races out from the last node with a hot head
                if (t >= t0 && t < On(i))
                {
                    double u = Ease.OutQuad((t - t0) / LEAP);
                    Bolt b = Shape(i, -1);
                    Stroke(p, new Bolt(b.paths[0]), 1, C, 0.75, new StrokeOpt { upTo = u });
                    double[][] tp = b.paths[0].pts;
                    double[] h = tp[(int)Math.Min(tp.Length - 1, Math.Floor(u * (tp.Length - 1)))];
                    p.Circle(h[0], h[1], 2, WHITE);
                    p.Glow(h[0], h[1], 16, C[5], 0.7);
                }
                double k = Level(i, t);
                if (k > 0)
                {
                    Stroke(p, Shape(i, f2), k, C, 0.75);
                }
                else if (t >= Off(i) + BREAK && t < Off(i) + BREAK + 0.35)
                {
                    // the spent channel: a dotted trace that cools and goes
                    double g = 1 - (t - Off(i) - BREAK) / 0.35;
                    Bolt b = Shape(i, Math.Floor((Off(i) + BREAK) * 15));
                    Trace(p, b.paths[0].pts, g > 0.5 ? C[4] : C[2], g * 0.9);
                }
                // the target: a flash as the bolt lands, then a node with arcs crawling over it and down to the floor
                double a = t - On(i);
                if (a >= 0 && a < 0.09)
                {
                    double q = a / 0.09;
                    p.Circle(x, y, 6 * (1 - q) + 1.5, C[5]);
                    p.Circle(x, y, 5 * (1 - q), WHITE);
                    Sim.Sparkle(p, x, y, Round(10 * (1 - q)), C[6], C[4], 0.6);
                }
                if (k > 0.05)
                {
                    double r = (2.6 + Px.Sin(t * 47 + i) * 0.7) * k;
                    p.Circle(x, y, r + 1.5, C[2]);
                    p.Circle(x, y, r + 0.7, C[5]);
                    p.Circle(x, y, r * 0.6, WHITE);
                    p.Glow(x, y, 26, C[4], 0.6 * k);
                    p.Glow(x, y, 8, C[6], 0.5 * k);
                    // arcs crawl over the target as if over a body caught in the current
                    Rng r2 = NewRng(o.Seed, 11, i, f);
                    for (int j = 0; j < (t - On(i) < 0.12 ? 2 : 1); j++)
                    {
                        double a0 = r2.Range(0, TAU), a1 = a0 + r2.Sign() * r2.Range(0.5, 1), d0 = r2.Range(4, 6), d1 = r2.Range(5, 8);
                        Crackle(p, Zigzag(r2, x + Px.Cos(a0) * d0, y + Px.Sin(a0) * d0, x + Px.Cos(a1) * d1, y + Px.Sin(a1) * d1, 0.35, 0), r2.Range(0.45, 0.7) * k, C, 0.25);
                    }
                    if (r2.Next() < 0.3 * k)
                    {
                        double gx = ground[i] + r2.Range(-3, 3);
                        Crackle(p, Zigzag(r2, x + r2.Range(-2, 2), y + 3, gx, -0.5, 0.3, 0.35), 0.75 * k, C, 0.35);
                        p.Glow(gx, -1, 12, C[5], 0.5 * k);
                        p.Px(gx, -1, WHITE);
                    }
                }
            }
            ps.Draw(p, Layer.Front);
            hot.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
