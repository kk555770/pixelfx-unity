using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class FrostNova : Effect
    {
        static readonly Pose HIGH = new Pose { ["turn"] = 1.15, ["crouch"] = 0, ["fx"] = -7, ["fy"] = -44, ["bx"] = 8, ["by"] = -44, ["ffx"] = -5, ["fbx"] = 6 };
        static readonly Pose SLAM = new Pose { ["turn"] = 1.15, ["crouch"] = 8, ["lean"] = 3, ["fx"] = -9, ["fy"] = -8, ["bx"] = 11, ["by"] = -8, ["ffx"] = -8, ["fbx"] = 9 };
        static readonly Pose EASE = new Pose { ["turn"] = 1, ["crouch"] = 2, ["fx"] = -8, ["fy"] = -17, ["bx"] = 9, ["by"] = -17, ["ffx"] = -4, ["fbx"] = 5 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 76 },
            Poses = new[]
            {
                new PoseKey(-0.34, Poses.READY),
                new PoseKey(-0.04, new Pose(HIGH) { ["crouch"] = 2, ["fy"] = -36, ["by"] = -36 }),
                new PoseKey(0.36, HIGH),
                new PoseKey(0.42, SLAM, "snap"),
                new PoseKey(0.9, new Pose(SLAM) { ["crouch"] = 7 }),
                new PoseKey(1.9, new Pose(SLAM) { ["crouch"] = 6 }),
                new PoseKey(2.6, EASE),
                new PoseKey(3.4, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = 0.42, Stop = 0.1, Shake = 0.26, Amp = 2, Target = false },
                new HitCue { T = 0.66, Push = 0.9, Straw = 6, Y = -10, Skin = "ice", Shiver = 1.7 },
                new HitCue { T = 2.9, Push = 0.2, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = 0.42, X = 10, N = 5, Dir = 1, C = "#d8f2ff" }, new DustCue { T = 0.42, X = -10, N = 5, Dir = -1, C = "#d8f2ff" } },
        };

        public static readonly Module Module = new Module("frost-nova", "pixelfx/fx/ice/frost-nova", 288, 192, 144, 150, 3.4, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["reach"] = 104,
                ["seed"] = 4,
            },
            (o, e) => new FrostNova(o, e), act: Act, actor: 2)
        {
            Title = "Frost nova",
            Desc = "Frost spirals into a spinning snowflake, then a wave blooms three rings of ice crystals round the caster; snow drifts, frost creeps, and the ice melts back into the floor.",
            Tags = new[] { "ice", "nova", "aoe", "frost", "magic", "one-shot" },
            Bg = "ground",
        };

        const double CHARGE = 0.42, SPREAD = 0.62, HOLD = 1.95, MELT = 0.7, CORE_Y = -24;

        // three rings of crystals: tall ones hugging the caster, smaller ones further out
        sealed class RingDef
        {
            public double[] q, h, w;
            public int n, kids;
        }

        static readonly RingDef[] RINGS =
        {
            new RingDef { q = new[] { 0.15, 0.24 }, h = new double[] { 24, 32 }, w = new double[] { 10, 13 }, n = 2, kids = 2 },
            new RingDef { q = new[] { 0.36, 0.52 }, h = new double[] { 13, 19 }, w = new double[] { 7, 9 }, n = 3, kids = 1 },
            new RingDef { q = new[] { 0.62, 0.9 }, h = new double[] { 5, 10 }, w = new double[] { 4, 6 }, n = 4, kids = 0 },
        };

        static readonly int[] Sides = { -1, 1 };

        // frost on the floor: a white crust, little tufts, a dithered bloom under the surface
        static void Frost(Painter p, uint[] C, double x0, double x1, double reach, double a, double edge)
        {
            for (double x = Math.Floor(x0); x <= Math.Ceiling(x1); x++)
            {
                double d = Math.Abs(x + 0.5), q = d / reach, k = Clamp((a * 1.15 - q) / 0.15);
                if (k <= 0) continue;
                double h = Rand(x, 17);
                bool fresh = edge > 0 && d > edge - 3;
                double tuft = h < 0.45 ? Round((1 + Rand(x, 5) * 3) * (1 - q * 0.6)) : 0, D = (4 + Rand(x, 11) * 4) * (1 - q * 0.5);
                p.Dot(x, 0, fresh || h < 0.25 ? WHITE : C[6], k);
                for (int j = 1; j <= tuft; j++) p.Dot(x, -j, j == tuft ? WHITE : C[5], k);
                if (tuft > 1 && Rand(x, 9) < 0.5) p.Dot(x + (Rand(x, 3) < 0.5 ? -1 : 1), -tuft + 1, C[6], k);
                for (int j = 1; j <= D; j++)
                {
                    double v = j / D;
                    p.Dot(x, j, Rand(x * 31 + j, 7) < 0.06 ? WHITE : v < 0.25 ? C[6] : v < 0.55 ? C[5] : C[4], k * (1.1 - v) * (fresh ? 1.3 : 1));
                }
            }
        }

        // the snowflake the cold gathers into: six arms with side branches, spinning, pulsing
        static void Snowflake(Painter p, uint[] C, double x, double y, double L, double spin, double k)
        {
            if (L < 0.8) return;
            for (int i = 0; i < 6; i++)
            {
                double a = spin + (i * TAU) / 6, ca = Px.Cos(a), sa = Px.Sin(a);
                for (double d = 0; d <= L; d += 0.5) p.Dot(x + ca * d, y + sa * d, d < L * 0.4 ? WHITE : C[6], k + 0.2);
                if (L > 3)
                    foreach (int s in Sides)
                    {
                        double bx = x + ca * L * 0.6, by = y + sa * L * 0.6, ba = a + s * 0.9;
                        for (double d = 0; d <= L * 0.35; d += 0.5) p.Dot(bx + Px.Cos(ba) * d, by + Px.Sin(ba) * d, C[5], k);
                    }
            }
            p.Px(x, y, WHITE);
            p.Glow(x, y, 10 + L * 3, C[5], 0.35 + 0.5 * k);
        }

        sealed class Part
        {
            public double dx, h, w, lean;
        }

        sealed class Site
        {
            public double x, d, h, t0, phase;
            public bool back, up;
            public Part[] parts;
        }

        readonly uint[] C, DIM;
        readonly double REACH;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Site[] sites;
        double t;
        bool released;

        FrostNova(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            DIM = new[] { C[0], C[0], C[1], C[2], C[3], C[4], C[5], C[6] };
            REACH = o.Num("reach");
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            // lay out the crystals: both sides, three rings, alternating back and front rows
            var list = new List<Site>();
            foreach (int side in Sides)
                for (int ri = 0; ri < RINGS.Length; ri++)
                {
                    RingDef ring = RINGS[ri];
                    for (int i = 0; i < ring.n; i++)
                    {
                        double q = ring.q[0] + ((ring.q[1] - ring.q[0]) * (i + R.Range(0.2, 0.8))) / ring.n, h = R.Range(ring.h[0], ring.h[1]), w = R.Range(ring.w[0], ring.w[1]);
                        var parts = new List<Part> { new Part { dx = 0, h = h, w = w, lean = side * h * R.Range(0.1, 0.28) } };
                        for (int k = 0; k < ring.kids; k++)
                        {
                            int s = k % 2 != 0 ? -side : side;
                            double kh = h * R.Range(0.4, 0.62);
                            parts.Insert(0, new Part { dx = s * R.Range(0.3, 0.5) * w, h = kh, w = w * 0.62, lean = s * kh * R.Range(0.35, 0.6) });
                        }
                        list.Add(new Site
                        {
                            x = side * q * REACH,
                            d = q * REACH,
                            h = h,
                            back = (i + ri + (side > 0 ? 1 : 0)) % 2 == 1,
                            t0 = ReachedAt(q * REACH) + R.Range(0, 0.03),
                            up = false,
                            parts = parts.ToArray(),
                            phase = R.Next(),
                        });
                    }
                }
            sites = list.ToArray();
        }

        double Front(double tt) => tt < CHARGE ? 0 : REACH * (1 - Px.Pow(1 - Clamp((tt - CHARGE) / SPREAD), 3));
        double ReachedAt(double d) => CHARGE + SPREAD * (1 - Px.Cbrt(1 - Math.Min(1, d / REACH)));
        static double Sink(double tt) => Smooth(HOLD, HOLD + MELT, tt);

        void DrawCrystals(Painter p, bool back)
        {
            double m = Sink(t);
            foreach (Site s in sites)
            {
                if (!s.up || s.back != back || m >= 1) continue;
                double g = Ease.OutBack(Clamp((t - s.t0) / 0.1), 2.6), sw = (((t - s.t0) * 0.7 + s.phase * 3) % 1.6) / 0.5;
                bool white = t < s.t0 + 0.03;
                foreach (Part q in s.parts)
                    Sim.Crystal(p, s.x + q.dx, back ? -1 : 0, q.h * g * (1 - m), q.w * (1 - m * 0.3), q.lean * g * (1 - m), back ? DIM : C, white: white, glint: sw <= 1 ? sw : -1);
            }
        }

        void Ring(Painter p, bool back)
        {
            if (t < CHARGE) return;
            double u = Clamp((t - CHARGE) / (SPREAD + 0.15));
            if (u >= 1) return;
            Sim.Band(p, 0, 0, Front(t) + 2, 0.16, Math.Max(2, 10 * Px.Pow(1 - u, 0.7)), u < 0.75 ? 1 : (1 - u) / 0.25, C, g => Px.Sin(g) < 0 == back, t - CHARGE < 0.1);
            if (!back)
                foreach (int s in Sides) p.Glow(s * (Front(t) + 2), -2, 22, C[5], 0.55 * (1 - u));
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < CHARGE - 0.06)
            {
                // the cold gathers: streaks spiral in to the core, vapour slides in along the floor
                if (R.Chance(0.9))
                {
                    double a = R.Range(0, TAU), D = R.Range(26, 58), T = R.Range(0.2, 0.3), v = (D * 3) / (Exp(3 * T) - 1);
                    double x = Px.Cos(a) * D, y = Math.Min(CORE_Y + Px.Sin(a) * D * 0.8, -2), l = Hypot(x, y - CORE_Y);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    ps.Spawn(P.Streak, x, y, (-x / l) * v, ((CORE_Y - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
                }
                if (R.Chance(0.14))
                {
                    int s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(28, 60), -R.Range(0, 2), -s * R.Range(35, 70), -R.Range(0, 3), R.Range(0.35, 0.5), C[6], R.Range(2, 3));
                }
            }
            if (t >= CHARGE && !released)
            {
                released = true;
                lights.Add(0, CORE_Y, 84, C[4], 0.8, 0.55);
                lights.Add(0, CORE_Y, 45, WHITE, 0.7, 0.12);
                for (int i = 0; i < 22; i++)
                {
                    double a = R.Range(-Math.PI, 0.25), sp = R.Range(110, 280);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = R.Range(-3, 3), y = CORE_Y, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 0.7, drag = 0.6, bounce = 0.3, life = R.Range(0.9, 1.5), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1.5, 3.5), c = R.Pick(new[] { C[7], C[6], C[5] }) });
                }
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(-Math.PI, 0), sp = R.Range(40, 160);
                    ps.Spawn(P.Flake, 0, CORE_Y, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.2, 1.8), R.Chance(0.6) ? WHITE : C[6], R.Range(0.5, 1.6));
                }
            }
            double fr = Front(t);
            if (t > CHARGE && t < CHARGE + SPREAD)
                foreach (int s in Sides)
                {
                    ps.Spawn(P.Vapour, s * fr + R.Range(-2, 2), -R.Range(0, 5), s * R.Range(15, 75), -R.Range(2, 15), R.Range(0.6, 1), R.Chance(0.7) ? C[6] : C[5], R.Range(3, 5.5) * (1 - 0.4 * (fr / REACH)));
                    if (R.Chance(0.45)) ps.Spawn(P.Flake, s * fr, -2, s * R.Range(10, 45), -R.Range(30, 85), R.Range(1.1, 1.9), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.2));
                }
            foreach (Site s in sites)
            {
                if (!s.up && t >= s.t0)
                {
                    s.up = true;
                    // each crystal punches up with a spray of shards, a puff and a glint
                    for (int i = 0; i < 2 + Round(s.h / 8); i++)
                    {
                        double a = -Math.PI / 2 + R.Range(-1.3, 1.3), sp = R.Range(60, 200) * (0.5 + s.h / 40);
                        bits.Add(new Bits.Bit { kind = B.Shard, x = s.x + R.Range(-3, 3), y = 0, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 0.8, drag = 0.6, bounce = 0.3, life = R.Range(0.9, 1.6), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1.5, 3), c = R.Pick(new[] { C[7], C[6], C[5] }) });
                    }
                    ps.Spawn(P.Vapour, s.x, -1, R.Range(-30, 30), -R.Range(2, 8), R.Range(0.5, 0.9), C[6], R.Range(2.5, 4));
                    if (s.h > 10) ps.Spawn(P.Star, s.x + R.Range(-3, 3), -R.Range(4, s.h * 0.8), 0, -3, R.Range(0.3, 0.6), C[6], R.Range(1, 2.4));
                }
            }
            // while it holds: glints run over the crystals, snow drifts down
            if (t > CHARGE + 0.2 && t < HOLD + 0.2)
            {
                if (R.Chance(0.5))
                {
                    Site s = R.Pick(sites);
                    if (s.up) ps.Spawn(P.Star, s.x + R.Range(-3, 3), -R.Range(2, s.h * 0.9), 0, -3, R.Range(0.3, 0.6), R.Chance(0.6) ? C[6] : WHITE, R.Range(0.6, 2.2));
                }
                if (t < HOLD - 0.3 && R.Chance(0.16)) ps.Spawn(P.Flake, R.Range(-1, 1) * fr, -R.Range(40, 90), R.Range(-5, 5), R.Range(3, 10), R.Range(1.1, 1.5), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.4));
            }
            // melting: drips run off the crystals, vapour lifts off the floor
            if (t > HOLD && t < HOLD + MELT)
            {
                if (R.Chance(0.5))
                {
                    Site s = R.Pick(sites);
                    bits.Add(new Bits.Bit { kind = B.Drip, x = s.x + R.Range(-3, 3), y = -R.Range(3, s.h * 0.6 * (1 - Sink(t))), vy = 10, g = 0.6, life = 1, c = C[5] });
                }
                if (R.Chance(0.25)) ps.Spawn(P.Vapour, R.Range(-1, 1) * REACH * 0.8, -R.Range(0, 3), R.Range(-10, 10), -R.Range(6, 16), R.Range(0.6, 1), C[5], R.Range(3, 5));
            }
            ps.Update(dt);
            bits.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double outA = 1 - Smooth(HOLD + 0.2, HOLD + MELT + 0.6, t), fr = Front(t);
            if (outA > 0 && t > CHARGE) Frost(p, C, -Math.Max(12, fr + 1), Math.Max(12, fr + 1), REACH, outA, fr);
            Ring(p, true);
            DrawCrystals(p, true);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            Ring(p, false);
            DrawCrystals(p, false);
            if (t < CHARGE)
            {
                double k = t / CHARGE, beat = 1 + 0.25 * Exp(-Px.Pow(t - CHARGE * 0.55, 2) / 0.002) + 0.3 * Exp(-Px.Pow(t - CHARGE * 0.85, 2) / 0.002);
                Snowflake(p, C, 0, CORE_Y, (2 + 5.5 * k * k) * beat, t * 4, k);
            }
            else if (t < CHARGE + 0.22)
            {
                double a = (t - CHARGE) / 0.22;
                Sim.Flare(p, 0, CORE_Y, 36 * Px.Pow(1 - a, 1.5), 1 - a, C);
                p.Glow(0, CORE_Y, 46, WHITE, 0.6 * (1 - a));
            }
            // the frosted floor gives off a cold light while the ice stands
            if (t > CHARGE)
            {
                double k = (1 - Smooth(HOLD, HOLD + MELT + 0.3, t)) * (0.85 + 0.15 * Px.Sin(t * 2.4)), fr = Front(t);
                for (int i = -3; i <= 3; i++) p.Glow((i / 3.5) * fr, -4, 26, C[4], 0.16 * k);
            }
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
