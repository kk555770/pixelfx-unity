// The crescent and its cooling ramp are adapted from Sprite Fusion's open-source slash
// (https://www.spritefusion.com/pixel-art-vfx/slash), and the impact star, dust, grit and embers from his
// impact and explosion (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class HeavyCleave : Effect
    {
        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "greatsword", ["two"] = true, ["sa"] = -1, ["fx"] = 11, ["fy"] = -21 };
        static readonly Pose OVER = new Pose(REST_) { ["x"] = -4, ["crouch"] = 2, ["lean"] = -2, ["fx"] = 1, ["fy"] = -42, ["sa"] = -2.45, ["ffx"] = 8, ["fbx"] = -7 };
        static readonly Pose FLOOR = new Pose(REST_) { ["x"] = 0, ["crouch"] = 7, ["lean"] = 6, ["fx"] = 17, ["fy"] = -21, ["sa"] = 0.72, ["ffx"] = 11, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 66 },
            Poses = new[]
            {
                new PoseKey(-0.42, new Pose(REST_) { ["x"] = -4 }),
                new PoseKey(-0.3, new Pose(REST_) { ["x"] = -3, ["crouch"] = 4, ["fx"] = 13, ["fy"] = -29, ["sa"] = -1.3 }),
                new PoseKey(-0.12, new Pose(OVER) { ["crouch"] = 4 }),
                new PoseKey(0.2, new Pose(OVER) { ["crouch"] = 3 }),
                new PoseKey(0.27, new Pose(REST_) { ["x"] = -1, ["y"] = -2, ["crouch"] = 0, ["lean"] = 2, ["fx"] = 11, ["fy"] = -42, ["sa"] = -1.45, ["ffx"] = 10, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.31, new Pose(REST_) { ["x"] = 0, ["y"] = -1, ["crouch"] = 3, ["lean"] = 4, ["fx"] = 18, ["fy"] = -33, ["sa"] = -0.45, ["ffx"] = 12, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.335, FLOOR, "snap"),
                new PoseKey(0.85, new Pose(FLOOR) { ["crouch"] = 6, ["lean"] = 5 }),
                new PoseKey(1.3, new Pose(REST_) { ["x"] = -2, ["crouch"] = 4, ["lean"] = 2, ["fx"] = 15, ["fy"] = -23, ["sa"] = 0.1 }),
                new PoseKey(1.7, new Pose(REST_) { ["x"] = -4 }),
            },
            Hits = new[] { new HitCue { T = 0.335, Stop = 0.2, Shake = 0.34, Amp = 3, Push = 3.2, Straw = 28, Y = -8 } },
            Dust = new[] { new DustCue { T = 0.335, X = 18, N = 8, Dir = 1 }, new DustCue { T = 0.335, X = -12, N = 5, Dir = -1 } },
        };

        public static readonly Module Module = new Module("heavy-cleave", "pixelfx/fx/melee/heavy-cleave", 208, 116, 68, 96, 1.9, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0c10", "#5c1414", "#a3241a", "#e0481c", "#ff8a2a", "#ffc23d", "#fff0a0", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["seed"] = 5,
            },
            (o, e) => new HeavyCleave(o, e), act: Act, actor: 2)
        {
            Title = "Heavy cleave",
            Desc = "A glint over the head, then a thick ember arc accelerates over the top into the floor: a white slam, glowing cracks, dust rolling both ways, grit and rising embers.",
            Tags = new[] { "melee", "slash", "cleave", "axe", "heavy", "ground", "fire", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        const double RAISE = 0.2; // the glint over the head
        const double SWING = 0.135; // the arc accelerates over the top
        const double HIT = RAISE + SWING;
        const double HOLD = 0.05, TRAIL = 0.22;
        const double W = 16, WE = 5;

        static double Head(double t) => Ease.InQuad(Clamp((t - RAISE) / SWING)); // fraction of the arc the edge has made
        static double PassedAt(double f) => RAISE + SWING * Math.Sqrt(Clamp(f)); // when the edge passed fraction f

        sealed class SliverSpec
        {
            public double a, r, v, arc;
        }

        readonly uint[] C;
        readonly int n;
        readonly Rng R;
        readonly Stone st;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Swing A, E;
        readonly double U1, X, GX, GY;
        readonly List<Crack> lines;
        readonly Spike[] spikes;
        readonly SliverSpec[] shards;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t;
        bool hit;

        HeavyCleave(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            n = C.Length;
            R = new Rng(o.Seed);
            st = new Stone(o.Color("stone"));
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            // the arc: from behind the head, over the top, down into the floor in front
            A = new Swing { cx = 3, cy = -22, R = 37, sy = 0.88, rot = 0.32, a0 = -2.45, dir = 1 };
            double u1 = 0;
            while (OnSwing(A, A.R, u1).y < -0.5 && u1 < 4) u1 += 0.005;
            U1 = u1;
            E = A.With();
            E.R = A.R * 0.76;
            X = OnSwing(A, A.R - 2, U1).x;
            (GX, GY) = OnSwing(A, A.R - 3, 0);
            lines = Cracks(R, X, 4, 40, 7, 0.4);
            // the impact star: long spikes along the floor, shorter ones up
            double[] sa = { -Math.PI, -2.7, -2.15, -1.62, -1.05, -0.45, 0 };
            spikes = new Spike[sa.Length];
            for (int i = 0; i < sa.Length; i++)
            {
                double a = sa[i] + R.Range(-0.08, 0.08), len = (i == 0 || i == 6 ? 20 : i == 3 ? 15 : 9) + R.Range(0, 4), w = 1.6 + R.Range(0, 0.6);
                spikes[i] = new Spike(a, len, w);
            }
            double[] ha = { -2.5, -1.9, -1.2, -0.6 };
            shards = new SliverSpec[ha.Length];
            for (int i = 0; i < ha.Length; i++) shards[i] = new SliverSpec { a = ha[i] + R.Range(-0.15, 0.15), r = 8 + R.Range(0, 3), v = R.Range(80, 130), arc = R.Range(0.35, 0.6) };
        }

        void Arc(Painter p, Swing s, double w0, double lag, bool rim)
        {
            double tt = t - lag, U = U1 * Head(tt), T = U1 * Smooth(HIT + HOLD, HIT + HOLD + TRAIL, tt);
            if (U < 0.02 || T >= U - 0.02) return;
            double gone = Smooth(HIT + HOLD, HIT + HOLD + TRAIL, tt), shrink = 1 - 0.75 * gone;
            Smear(p, s, T, U, w0, (u, r, o, _) =>
            {
                double age = Math.Max(0, tt - PassedAt(u / U1)), v = (u - T) / (U - T);
                double w = w0 * shrink * Px.Pow(Px.Sin(Math.PI * Px.Pow(v, 2.2)), 0.6);
                if (w < 0.4) return;
                double q = (s.R - r) / w;
                if (q < 0 || q > 1) return;
                double ci = rim && q < 0.22 && age < 0.12 ? 0 : Round(q * (n - 1) * 0.62 + age * 7 + Math.Max(0, tt - HIT) * 10 - v * 0.8 + (rim ? 0.7 : 1.7));
                o.c = C[n - 1 - (int)Clamp(ci, 0, n - 1)];
                o.a = Math.Min(1, v / 0.35) * (1.2 - gone);
            });
            if (rim) GlowAlong(p, s, s.R - w0 * 0.4, T, U, 7 + w0 * 0.5, C[4], u => 0.26 * Exp(-Math.Max(0, tt - PassedAt(u / U1)) / 0.2) * ((u - T) / (U - T)));
        }

        public override void Update(double dt)
        {
            t += dt;
            // heat gathers to the raised blade
            if (t < RAISE - 0.04 && R.Chance(0.35))
            {
                double a = R.Range(0, TAU), D = R.Range(14, 26), T = R.Range(0.14, 0.2), v = (D * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, GX + Px.Cos(a) * D, GY + Px.Sin(a) * D, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? C[5] : C[4]);
            }
            // embers shed off the edge as it comes over
            if (t > RAISE && t < HIT && R.Chance(0.7))
            {
                double u = U1 * Head(t) * R.Range(0.6, 1);
                var (x, y) = OnSwing(A, A.R - R.Range(0, 6), u);
                ps.Spawn(P.Ember, x, y, R.Range(-20, 20), R.Range(-40, -10), R.Range(0.5, 1.1), C[4], R.Range(1, 2));
            }
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(X, -6, 26, WHITE, 0.9, 0.1);
                lights.Add(X, -12, 64, C[4], 0.5, 0.8);
                // sparks fan up off the floor, most of them forward
                for (int i = 0; i < 26; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.35), sp = R.Range(140, 360);
                    ps.Spawn(P.Spark, X, -1, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.15, 0.5));
                }
                // dust rolls out both ways along the floor
                for (int i = 0; i < 20; i++)
                {
                    double s = i % 2 != 0 ? -1 : 1;
                    ps.Spawn(P.Smoke, X + s * R.Range(0, 8), -R.Range(2, 6), s * R.Range(45, 135), -R.Range(2, 12), R.Range(1.0, 1.45), st.dust, R.Range(5, 8.5), R.Range(0, 0.07), PF.Big);
                }
                for (int i = 0; i < 4; i++) ps.Spawn(P.Smoke, X + R.Range(-4, 4), -R.Range(3, 8), R.Range(-15, 15), -R.Range(40, 90), R.Range(1.0, 1.45), st.dust, R.Range(4, 6.5), R.Range(0.02, 0.1), PF.Big);
                for (int i = 0; i < 8; i++) ps.Spawn(P.Dust, X + R.Range(-4, 4), -R.Range(1, 4), R.Range(-60, 60), -R.Range(8, 30), R.Range(0.35, 0.7), st.dust, R.Range(2, 3.5));
                // grit and a few stones thrown up, forward
                uint[] grit = { st.light, st.body, st.body, st.shade };
                for (int i = 0; i < 44; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-0.7, 1.0), sp = R.Range(60, 230);
                    ps.Spawn(P.Grit, X + R.Range(-3, 3), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.0, 1.5), R.Pick(grit), R.Chance(0.35) ? 2 : 1);
                }
                for (int i = 0; i < 6; i++)
                    bits.Add(new Bits.Bit { kind = B.Chunk, x = X + R.Range(-3, 3), y = -2, vx = R.Range(-50, 110), vy = -R.Range(120, 260), g = 1, drag = 0.4, bounce = 0.3, life = R.Range(1.15, 1.5), a = R.Range(0, TAU), va = R.Range(-14, 14), s = R.Int(1, 2) });
                // embers lift off the cut
                for (int i = 0; i < 16; i++) ps.Spawn(P.Ember, X + R.Range(-8, 8), -R.Range(1, 6), R.Range(-50, 60), -R.Range(30, 110), R.Range(0.7, 1.5), C[4], R.Range(1, 2));
            }
            // the cracks breathe embers while they're hot
            if (hit && t < HIT + 0.6 && R.Chance(0.12))
            {
                Crack c = PickFrom(R, lines);
                double[] pt = R.Pick(c.pts);
                ps.Spawn(P.Ember, pt[0], pt[1] - 1, R.Range(-12, 12), -R.Range(20, 50), R.Range(0.5, 0.9), C[4], 1);
            }
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
                if (b.rest && !dusted.Contains(b))
                {
                    dusted.Add(b);
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 30), -R.Range(2, 12), R.Range(0.3, 0.6), st.dust, R.Range(1.3, 2.4));
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            double q = t - HIT;
            if (q >= 0)
            {
                // the floor under the blade: white hot, cooling through the ramp to soot
                double fade = 1 - Smooth(1.0, 1.5, q);
                for (int y = 0; y <= 2; y++)
                    for (int x = -9; x <= 9; x++)
                    {
                        double d = Hypot((x + 0.5) / 9, (y + 0.5) / 3.5);
                        if (d > 1) continue;
                        double T = Px.Pow(Clamp(1 - q / 0.8), 1.4) * (1.2 - d) - Rand(x, y, 3) * 0.3;
                        if (T > 0.08) p.Dot(X + x, y, C[(int)Clamp(Round(T * 9), 1, 7)], fade);
                        else p.Dot(X + x, y, d < 0.5 ? st.dark : st.shade, (d < 0.9 - Rand(x, y, 9) * 0.3 ? 1 : 1 - d) * fade);
                    }
                // the shock runs out along the floor: the far half behind the fighter
                double k = q / 0.45;
                if (k < 1) Sim.Band(p, X, 0.5, 6 + 46 * Ease.OutCubic(k), 0.16, Math.Max(1, 3 * (1 - k)), 1.15 - k, new[] { st.body, st.light, C[4], C[6] }, g => Px.Sin(g) < 0);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            double q = t - HIT;
            // the glint over the head: the raised blade catching the light
            if (t < RAISE + 0.03)
            {
                double g = Ease.OutBack(Clamp(t / 0.1)) * (1 + 0.15 * Px.Sin(t * 60)) * (t > RAISE ? 1 - (t - RAISE) / 0.03 : 1);
                Sim.Sparkle(p, GX, GY, 1 + 7 * g, C[6], C[4], 0.5);
                p.Glow(GX, GY, 22, C[4], 0.3 * g);
            }
            if (q >= 0)
            {
                // cracks race out from the cut and split down into the floor: white hot, cooling down the ramp to dark
                double heat = Px.Pow(Clamp(1 - q / 1.2), 1.2), fade = 1 - Smooth(1.15, 1.5, q);
                foreach (Crack c in lines)
                {
                    double reach = 40 * Ease.OutCubic(Clamp((q - c.at * 0.1) / 0.13)) * (c.fork ? 0.3 : 1);
                    if (reach <= 0) continue;
                    Along(c.pts, reach, (x0, y0, x1, y1, d) =>
                    {
                        double h = heat * (1 - d / 46) - (c.fork ? 0.12 : 0);
                        uint col = h > 0.72 ? WHITE : h > 0.08 ? C[(int)Clamp(Round(h * 8), 1, 6)] : st.dark;
                        if (y1 > 0.5) p.Line(x0 + 1, y0, x1 + 1, y1, st.dark, fade);
                        p.Line(x0, y0, x1, y1, col, fade);
                        if (h > 0.25 && ToInt32(d) % 6 < 2) p.Glow(x1, y1, 6, C[4], 0.2 * h * fade);
                    });
                }
                if (heat > 0) p.Glow(X, -2, 34, C[4], 0.32 * heat * fade);
                // the near half of the shock ring
                double k = q / 0.45;
                if (k < 1) Sim.Band(p, X, 0.5, 6 + 46 * Ease.OutCubic(k), 0.16, Math.Max(1, 3 * (1 - k)), 1.15 - k, new[] { st.body, st.light, C[4], C[6] }, g => Px.Sin(g) >= 0);
            }
            // the arc and its echo, then a dotted trace of embers where it has been
            Arc(p, E, WE, 0.02, false);
            Arc(p, A, W, 0, true);
            double tail = U1 * Smooth(HIT + HOLD, HIT + HOLD + TRAIL, t), th = 0.8 * (1 - Clamp((t - HIT - HOLD) / (TRAIL + 0.25)));
            if (tail > 0.02 && th > 0) Trace(p, A, A.R - 0.5, 0, tail, C[3], u => th * (0.4 + 0.6 * u / U1));
            if (t > RAISE && t < HIT + 0.05)
            {
                var (x, y) = OnSwing(A, A.R - 5, U1 * Head(t));
                p.Glow(x, y, 40, C[4], 0.5);
            }
            // the white slam: an impact star flat on the floor, then slivers flying off and a hot pool
            if (q >= 0 && q < 0.035) Star(p, X, -1, spikes, 1, 1, WHITE, C[6], C[7], C[5]);
            else if (q >= 0.035 && q < 0.07) Star(p, X, -1, spikes, 1.25, 0.55, C[7], C[6], C[6], C[4]);
            if (q >= 0.05 && q < 0.16)
            {
                double k = (q - 0.05) / 0.11;
                foreach (SliverSpec s in shards) Sliver(p, X, -1, s.a, s.r + s.v * (q - 0.05), s.arc * (1 - 0.4 * k), k < 0.4 ? C[6] : k < 0.7 ? C[5] : C[4], 1 - k);
            }
            if (q >= 0 && q < 0.2)
            {
                double pop = Exp(-q / 0.05);
                p.Glow(X, -6, 30, C[5], 0.8 * pop);
                p.Glow(X, -2, 10, WHITE, 0.7 * pop);
            }
            foreach (Bits.Bit b in bits.List) Rock(p, b, st);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
