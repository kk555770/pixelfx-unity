// The rocks, grit, rolling dust and floor ring are adapted from Sprite Fusion's open-source explosion, and the
// dome from his ice nova (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class HammerSmash : Effect
    {
        // both hands low on the shaft, the head resting on the floor in front; it comes up, over and down
        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "hammer", ["two"] = true, ["sa"] = 0.62, ["fx"] = 11, ["fy"] = -21, ["crouch"] = 3 };
        static readonly Pose TOP = new Pose(REST_) { ["x"] = -3, ["crouch"] = 0, ["lean"] = -3, ["fx"] = 3, ["fy"] = -41, ["sa"] = -2.1, ["ffx"] = 8, ["ffy"] = -8, ["fbx"] = -5, ["toeB"] = true };
        static readonly Pose SLAM = new Pose(REST_) { ["x"] = -1, ["crouch"] = 7, ["lean"] = 6, ["fx"] = 14, ["fy"] = -19, ["sa"] = 0.62, ["ffx"] = 11, ["fbx"] = -10 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 66 },
            Poses = new[]
            {
                new PoseKey(-0.72, new Pose(REST_) { ["x"] = -3 }),
                new PoseKey(-0.44, new Pose(REST_) { ["x"] = -3, ["crouch"] = 5, ["lean"] = -1, ["fx"] = 16, ["fy"] = -28, ["sa"] = -0.9 }),
                new PoseKey(-0.2, TOP),
                new PoseKey(-0.03, new Pose(TOP) { ["y"] = -2 }),
                new PoseKey(0.06, new Pose(REST_) { ["x"] = 0, ["y"] = -3, ["crouch"] = -1, ["lean"] = 2, ["fx"] = 13, ["fy"] = -41, ["sa"] = -1.2, ["ffx"] = 11, ["ffy"] = -8, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.11, new Pose(REST_) { ["x"] = 0, ["y"] = -1, ["crouch"] = 3, ["lean"] = 5, ["fx"] = 19, ["fy"] = -30, ["sa"] = -0.15, ["ffx"] = 13, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.15, SLAM, "snap"),
                new PoseKey(0.62, new Pose(SLAM) { ["crouch"] = 6, ["lean"] = 5 }),
                new PoseKey(1.02, new Pose(REST_) { ["x"] = -2, ["crouch"] = 5, ["lean"] = 2, ["fx"] = 14, ["fy"] = -24, ["sa"] = 0.3 }),
                new PoseKey(1.4, new Pose(REST_) { ["x"] = -3 }),
            },
            Hits = new[] { new HitCue { T = 0.15, Stop = 0.2, Shake = 0.34, Amp = 3, Push = 3, Straw = 26, Y = -8 } },
            Dust = new[] { new DustCue { T = 0.15, X = 10, N = 8 }, new DustCue { T = 0.15, X = -12, N = 5, Dir = -1 } },
        };

        public static readonly Module Module = new Module("hammer-smash", "pixelfx/fx/melee/hammer-smash", 208, 112, 80, 92, 2.0, false, 30,
            new Options
            {
                ["palette"] = new[] { "#141834", "#242c5c", "#38479a", "#5a72d0", "#8fa8f0", "#c4d4ff", "#eef3ff", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["seed"] = 9,
            },
            (o, e) => new HammerSmash(o, e), act: Act, actor: 2)
        {
            Title = "Hammer smash",
            Desc = "A hammer drops in front in a wedge of motion: a white slam with rays, a dome of force, a dent with cracks, rocks thrown up that bounce and settle, and dust.",
            Tags = new[] { "melee", "smash", "hammer", "heavy", "ground", "shockwave", "debris", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        static readonly uint SOOT = Hex("#0d0a12");
        static readonly uint[] HOT = Ramp("#fff3a6", "#ffc23d", "#ff8a2a", "#c9281f");
        const double DROP = 0.15; // the hammer comes over and down, gathering speed
        const double HW = 13;

        static double Head(double t)
        {
            double x = Clamp((t + 0.03) / (DROP + 0.03));
            return 0.28 * x + 0.72 * x * x;
        }

        sealed class Ray
        {
            public double a, L;
        }

        static readonly double[] RockSizes = { 1, 1, 2, 2, 3 };

        readonly uint[] C;
        readonly int n;
        readonly Rng R;
        readonly Stone st;
        readonly Particles ps;
        readonly Bits bits, chips;
        readonly Lights lights;
        readonly Swing A;
        readonly double U1, X, Rm;
        readonly List<Crack> lines;
        readonly Ray[] rays;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t;
        bool hit;

        HammerSmash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            n = C.Length;
            R = new Rng(o.Seed);
            st = new Stone(o.Color("stone"));
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, emitter: e);
            bits = new Bits(floor, e);
            chips = new Bits(floor, e);
            lights = new Lights(e);
            // the hammer head's path: round the shoulders, from overhead down to the floor in front
            A = new Swing { cx = 5, cy = -25, R = 32, sy = 1, rot = 0, a0 = -2.25, dir = 1 };
            double u1 = 0;
            while (OnSwing(A, A.R, u1).y < -0.5 && u1 < 4) u1 += 0.005;
            U1 = u1;
            X = OnSwing(A, A.R - HW / 2, U1).x;
            lines = Cracks(R, X, 5, 24, 6, 0);
            double[] ds = { -0.95, -0.6, -0.3, -0.05, 0.22, 0.5, 0.85 };
            rays = new Ray[ds.Length];
            for (int i = 0; i < ds.Length; i++)
                rays[i] = new Ray { a = -Math.PI / 2 + ds[i] + R.Range(-0.06, 0.06), L = (i == 3 ? 34 : i == 2 || i == 4 ? 26 : i == 1 || i == 5 ? 18 : 12) * R.Range(0.9, 1.1) };
            Rm = A.R - HW / 2;
        }

        // the smear: the hammer head's path as a comet, a hard white leading face, tapering to a hair behind it,
        // cooling along the taper (edges first), with speed lines trailing along the arc
        void Wedge(Painter p)
        {
            double h = Head(t), U = U1 * h, after = Smooth(DROP, DROP + 0.07, t), lagU = Math.Max(0, U - (0.3 + 1.05 * h)), T = lagU + (U - lagU) * after * 0.8;
            if (U - T < 0.03 || after >= 1) return;
            double len = (U - T) * Rm;
            Smear(p, A, T, U, HW + 1, (u, r, o, _) =>
            {
                double lead = (U - u) * Rm, f = lead / len, w = HW * (lead < 3 ? 1 : Px.Pow(1 - (lead - 3) / Math.Max(1, len - 3), 0.85)) * (1 - after * 0.5);
                double off = (r - Rm) / (w / 2 + 0.01);
                if (w < 0.6 || Math.Abs(off) > 1) return;
                double ci = lead < 2 ? 0 : Round(1 + f * 3.4 + Math.Abs(off) * 1.4 - (off > 0.55 ? 1 : 0) + after * 3);
                if (ci > n - 3 || double.IsNaN(ci)) return;
                o.c = C[n - 1 - (int)Math.Max(0, ci)];
            });
            // speed lines trail the head along the arc, dotted out at their ends
            if (after < 0.6)
            {
                double[][] specs =
                {
                    new[] { -HW / 2 - 2, 3, 26 }, new[] { -HW / 2 - 5, 9, 16 }, new[] { HW / 2 + 2, 2, 30 }, new[] { HW / 2 + 5, 8, 18 },
                    new[] { -1, len + 2, 14 }, new[] { 3, len + 4, 9 },
                };
                foreach (double[] spec in specs)
                {
                    double dr = spec[0], l0 = spec[1], L = spec[2], r = Rm + dr;
                    for (double d = l0; d < l0 + L * (1 - after); d += 0.5)
                    {
                        double u = U - d / r, k = (d - l0) / L;
                        if (u < 0) break;
                        var (x, y) = OnSwing(A, r, u);
                        p.Dot(x, y, k < 0.25 ? C[6] : k < 0.6 ? C[5] : C[4], (1 - k) * (1 - after));
                    }
                }
            }
            var (hx, hy) = OnSwing(A, Rm, U);
            p.Glow(hx, hy, 34, C[4], 0.5 * (1 - Smooth(DROP - 0.03, DROP, t)));
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!hit && t >= DROP)
            {
                hit = true;
                lights.Add(X, -5, 16, WHITE, 0.35, 0.08);
                lights.Add(X, -10, 64, C[4], 0.42, 0.6);
                // rocks thrown up out of the dent: they tumble, land, bounce and settle
                for (int i = 0; i < 15; i++)
                {
                    double s = R.Sign();
                    bits.Add(new Bits.Bit { kind = B.Chunk, x = X + s * R.Range(0, 5), y = -2, vx = s * R.Range(20, 105), vy = -R.Range(110, 300), g = 1, drag = 0.35, bounce = 0.38, life = R.Range(1.25, 1.65), a = R.Range(0, TAU), va = R.Range(-16, 16), s = R.Pick(RockSizes) });
                }
                // hot chips struck off the stone: they bounce, settle and cool through the ramp
                for (int i = 0; i < 11; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(90, 230);
                    chips.Add(new Bits.Bit { kind = B.Shard, x = X + R.Range(-4, 4), y = -2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 0.9, drag = 0.5, bounce = 0.35, life = R.Range(0.9, 1.5), s = 1 });
                }
                uint[] grit = { st.light, st.body, st.body, st.shade };
                for (int i = 0; i < 40; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(50, 210);
                    ps.Spawn(P.Grit, X + R.Range(-4, 4), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.9, 1.5), R.Pick(grit), R.Chance(0.4) ? 2 : 1);
                }
                // force motes skid out along the floor, sparks off the stone
                for (int i = 0; i < 14; i++)
                {
                    double s = i % 2 != 0 ? -1 : 1, sp = R.Range(120, 260);
                    ps.Spawn(P.Mote, X + s * 6, -R.Range(1, 4), s * sp, -R.Range(0, 30), R.Range(0.18, 0.34), R.Chance(0.5) ? C[6] : C[5], 0.8, R.Range(0.02, 0.05));
                }
                for (int i = 0; i < 24; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(150, 380);
                    ps.Spawn(P.Spark, X + Px.Cos(a) * 6, -2 + Px.Sin(a) * 4, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.12, 0.42), 0, 1, R.Range(0.035, 0.08));
                }
                // dust rolls out both ways along the floor, and a puff lifts off the dent
                for (int i = 0; i < 20; i++)
                {
                    double s = i % 2 != 0 ? -1 : 1;
                    ps.Spawn(P.Smoke, X + s * R.Range(2, 10), -R.Range(2, 7), s * R.Range(40, 125), -R.Range(2, 14), R.Range(0.95, 1.5), st.dust, R.Range(6, 9), R.Range(0.02, 0.08), PF.Big);
                }
                for (int i = 0; i < 7; i++) ps.Spawn(P.Smoke, X + R.Range(-6, 6), -R.Range(3, 9), R.Range(-20, 20), -R.Range(30, 80), R.Range(1.0, 1.4), st.dust, R.Range(5, 7.5), R.Range(0.04, 0.14), PF.Big);
                for (int i = 0; i < 8; i++) ps.Spawn(P.Dust, X + R.Range(-6, 6), -R.Range(1, 4), R.Range(-70, 70), -R.Range(8, 30), R.Range(0.35, 0.7), st.dust, R.Range(2, 3.5));
            }
            bits.Update(dt);
            chips.Update(dt);
            // settled rocks catch the light now and then
            if (hit && t > DROP + 0.4 && t < DROP + 1.2 && R.Chance(0.08))
            {
                Bits.Bit b = PickFrom(R, bits.List);
                if (b != null && b.rest) ps.Spawn(P.Star, b.x + R.Range(-1, 1), b.y - 1.5 - b.s, 0, -2, R.Range(0.25, 0.45), R.Chance(0.5) ? HOT[0] : WHITE, R.Range(0.6, 1.6));
            }
            // rocks that land kick up a little dust
            foreach (Bits.Bit b in bits.List)
                if (b.rest && !dusted.Contains(b))
                {
                    dusted.Add(b);
                    for (int i = 0; i < 3; i++) ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 35), -R.Range(2, 14), R.Range(0.3, 0.6), st.dust, R.Range(1.3, 2.6));
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            double q = t - DROP;
            if (q >= 0)
            {
                // the dent: the floor's top knocked down into a shallow pit, its surface lit at the bottom, chipped lips
                double fade = 1 - Smooth(1.3, 1.7, q);
                for (int x = -11; x <= 11; x++)
                {
                    double u = x / 10.0, d = Math.Abs(x) > 10 ? 0 : Round(3.2 * (1 - u * u) + (Rand(x, 4) - 0.5) * 0.8);
                    for (int y = 0; y < d; y++) p.Dot(X + x, y, y == 0 ? SOOT : st.dark, fade);
                    if (d > 0) p.Dot(X + x, d, Math.Abs(x) < 7 ? st.light : st.body, fade);
                    if (Math.Abs(x) >= 8) p.Dot(X + x, -1, Math.Abs(x) >= 10 ? st.body : st.light, fade);
                    if (Math.Abs(x) == 9 && Rand(x, 7) < 0.7) p.Dot(X + x, -2, st.body, fade);
                }
                // the floor ring's far half
                double k = q / 0.5;
                if (k < 1) Sim.Band(p, X, 0.5, 6 + 50 * Ease.OutCubic(k), 0.16, Math.Max(1, 2 * (1 - k)), 1.15 - k, new[] { st.body, C[3], C[5], C[7] }, g => Px.Sin(g) < 0);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            double q = t - DROP;
            Wedge(p);
            if (q >= 0)
            {
                // cracks split out of the dent: a white flash, then dark lines that fade with the dent
                double fade = 1 - Smooth(1.3, 1.7, q);
                foreach (Crack c in lines)
                {
                    double reach = 30 * Ease.OutCubic(Clamp((q - c.at * 0.06) / 0.08)) * (c.fork ? 0.3 : 1);
                    if (reach > 0)
                        Along(c.pts, reach, (x0, y0, x1, y1, _) =>
                        {
                            if (q >= 0.12) p.Line(x0, y0 + 3, x1, y1 + 3, st.shade, fade);
                            p.Line(x0, y0 + 2, x1, y1 + 2, q < 0.05 ? WHITE : q < 0.12 ? C[5] : SOOT, fade);
                        });
                }
                // the near half of the floor ring
                double k = q / 0.5;
                if (k < 1) Sim.Band(p, X, 0.5, 6 + 50 * Ease.OutCubic(k), 0.16, Math.Max(1, 2 * (1 - k)), 1.15 - k, new[] { st.body, C[3], C[5], C[7] }, g => Px.Sin(g) >= 0);
                // the dome of force swells off the floor, thins to dots and fades
                double d = (q - 0.045) / 0.4;
                if (d >= 0 && d < 1)
                {
                    // solid steps through the shell (white rim, two blues inside); it dissolves as a whole at the end
                    double r = 12 + 30 * Ease.OutCubic(d), th = Math.Max(1, 3.6 * Px.Pow(1 - d, 0.8)), a = d < 0.6 ? 1 : (1 - d) / 0.4;
                    p.Shade(X - r - 1, -r - 1, X + r + 1, 0, (x, y, o) =>
                    {
                        double ee = r - Hypot(x - X, y);
                        if (ee < 0 || ee > th || y > -0.5) return 0;
                        double v = ee / th;
                        o.c = d < 0.1 ? (v < 0.6 ? WHITE : C[6]) : v < 0.34 ? (d < 0.45 ? WHITE : C[6]) : v < 0.67 ? C[5] : C[4];
                        o.a = a;
                        return 0;
                    });
                    for (int i = 0; i < 7; i++)
                    {
                        double g = -Math.PI * (0.08 + (i / 6.0) * 0.84);
                        p.Glow(X + Px.Cos(g) * r, Px.Sin(g) * r, 14, C[4], 0.36 * (1 - d));
                    }
                }
                // rays of light shoot up out of the slam
                if (q < 0.2)
                    foreach (Ray ry in rays)
                    {
                        double d1 = ry.L * Ease.OutCubic(Math.Min(1, q / 0.05)), d0 = ry.L * Smooth(0.04, 0.2, q), ca = Px.Cos(ry.a), sa = Px.Sin(ry.a);
                        if (d1 - d0 < 1) continue;
                        p.Line(X + ca * (2 + d0), -1 + sa * (2 + d0), X + ca * d1, -1 + sa * d1, u => u < 0.5 ? WHITE : u < 0.8 ? C[6] : C[5]);
                        p.GlowLine(X + ca * d0, -1 + sa * d0, X + ca * d1, -1 + sa * d1, 4, C[5], 0.18 * (1 - q / 0.2));
                    }
                // the white slam: a flat flash on the floor for two frames
                if (q < 0.07)
                {
                    bool hot = q < 0.035;
                    double rx = hot ? 16 : 12, ry = hot ? 3 : 2;
                    p.Ellipse(X, 0, rx + 2, ry + 1, hot ? C[6] : C[5]);
                    p.Ellipse(X, 0, rx, ry, hot ? WHITE : C[7]);
                }
                if (q < 0.2)
                {
                    double pop = Exp(-q / 0.05);
                    p.Glow(X, -5, 26, C[5], 0.3 * pop);
                }
                // the struck stone stays warm for a moment
                p.Glow(X, -1, 16, HOT[2], 0.28 * Exp(-q / 0.35));
            }
            foreach (Bits.Bit b in bits.List) Rock(p, b, st);
            foreach (Bits.Bit b in chips.List)
            {
                double k = b.life / b.max;
                uint c = k > 0.7 ? HOT[0] : k > 0.45 ? HOT[1] : k > 0.25 ? HOT[2] : HOT[3];
                p.Dot(b.x, b.y, c, Math.Min(1, k * 4));
                if (!b.rest) p.Dot(b.x - b.vx * 0.012, b.y - b.vy * 0.012, HOT[3], Math.Min(1, k * 4));
                p.Glow(b.x, b.y, 4, c, 0.35 * k);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
