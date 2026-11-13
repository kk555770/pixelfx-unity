// The crescent and trace recipe is adapted from Sprite Fusion's open-source slash, and the vapour from his
// ice and poison (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class ScytheReap : Effect
    {
        // the butt rests on the floor in one hand; both hands take it out to the right, and he turns with it round the front
        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "scythe", ["x"] = 2, ["sa"] = -1.5, ["fx"] = 11, ["fy"] = -22, ["bx"] = 7, ["by"] = -16 };
        static readonly Pose OUT_ = new Pose(REST_) { ["two"] = true, ["crouch"] = 4, ["lean"] = 4, ["fx"] = 17, ["fy"] = -27, ["sa"] = -0.45, ["ffx"] = 11, ["fbx"] = -8 };
        static readonly Pose END = new Pose(REST_) { ["two"] = true, ["x"] = 0, ["yaw"] = 3.14, ["crouch"] = 6, ["lean"] = 4, ["fx"] = 15, ["fy"] = -24, ["sa"] = 0.3, ["ffx"] = 11, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 66 },
            Poses = new[]
            {
                new PoseKey(-0.56, REST_),
                new PoseKey(-0.26, OUT_),
                new PoseKey(0.08, new Pose(OUT_) { ["crouch"] = 5 }),
                new PoseKey(0.2, new Pose(OUT_) { ["yaw"] = 0.8, ["crouch"] = 5, ["lean"] = 3, ["fx"] = 16, ["fy"] = -23, ["sa"] = 0.1, ["ghost"] = 1 }),
                new PoseKey(0.3, new Pose(OUT_) { ["x"] = 1, ["yaw"] = 1.9, ["crouch"] = 6, ["lean"] = 3, ["fx"] = 15, ["fy"] = -23, ["sa"] = 0.2, ["ghost"] = 1 }),
                new PoseKey(0.42, END),
                new PoseKey(0.92, new Pose(END) { ["crouch"] = 5 }),
                new PoseKey(1.3, new Pose(REST_) { ["x"] = 1, ["yaw"] = 1.4, ["crouch"] = 4 }),
                new PoseKey(1.7, REST_),
            },
            Hits = new[] { new HitCue { T = 0.14, Stop = 0.12, Shake = 0.16, Amp = 2, Push = 2.4, Straw = 20, Skin = "shadow" } },
            Dust = new[] { new DustCue { T = 0.3, X = 2, N = 6 } },
        };

        public static readonly Module Module = new Module("scythe-reap", "pixelfx/fx/melee/scythe-reap", 184, 116, 84, 98, 2.1, false, 30,
            new Options
            {
                ["palette"] = new[] { "#07040c", "#140a22", "#24113a", "#3b1d58", "#5a3a82", "#7fd6c0", "#d8fff2", "#ffffff" },
                ["seed"] = 13,
            },
            (o, e) => new ScytheReap(o, e), act: Act, actor: 2)
        {
            Title = "Scythe reap",
            Desc = "A slow, huge dark crescent reaps round the front with a pale ghostly edge; it hangs, then tears away from the tail into curling wisps and cold vapour.",
            Tags = new[] { "melee", "scythe", "reap", "dark", "ghost", "death", "slash", "combat", "one-shot" },
            Bg = "ground",
        };

        const double WIND = 0.08, SWEEP = 0.34, HANG = 0.14, TEAR = 0.55;
        const double T0 = WIND, T1 = WIND + SWEEP, T2 = T1 + HANG, T3 = T2 + TEAR;
        const double W = 21;

        static double Head(double t) => Ease.InOutSine(Clamp((t - T0) / SWEEP));

        sealed class Wisp
        {
            public double x, y, vx, vy, life, max, seed;
            public List<double[]> pts = new List<double[]>();
            public bool far;
        }

        readonly uint[] C;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly Swing A;
        readonly List<Wisp> wisps = new List<Wisp>();
        double t, lastTear;

        ScytheReap(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, haze: C[5], emitter: e);
            lights = new Lights(e);
            // a wide reap from out on the right, round the front of the fighter, to the left
            A = new Swing { cx = 8, cy = -33, R = 52, sy = 0.6, k = 0.1, rot = 0.08, a0 = -0.5, dir = 1, span = 3.5 };
        }

        // how far the tear has eaten in from the tail (0..1 of the span), ragged by a per-pixel threshold
        double Tear() => Smooth(T2, T3, t);

        void AddWisp(double x, double y, bool far) =>
            wisps.Add(new Wisp { x = x, y = y, vx = Rg.Range(-12, 12), vy = -Rg.Range(8, 26), life = Rg.Range(0.5, 0.9), max = 0, seed = Rg.Range(0, 100), far = far });

        public override void Update(double dt)
        {
            t += dt;
            // the cold gathers on the blade before it moves
            if (t < WIND && Rg.Chance(0.5))
            {
                var (x, y) = OnSwing(A, A.R - 4, 0);
                ps.Spawn(P.Vapour, x + Rg.Range(-14, 14), y + Rg.Range(-8, 8), Rg.Range(-10, 10), Rg.Range(-6, 6), Rg.Range(0.3, 0.5), C[5], Rg.Range(1.5, 2.5));
            }
            // the edge sheds a little vapour as it goes
            if (t > T0 && t < T1 && Rg.Chance(0.5))
            {
                Travel tr = TravelAt(A, A.span * Head(t) * Rg.Range(0.75, 1));
                ps.Spawn(P.Vapour, tr.x, tr.y, tr.dx * 20, -Rg.Range(4, 14), Rg.Range(0.5, 0.9), C[6], Rg.Range(2, 3.5), 0, Px.Sin(tr.a) < 0 ? PF.Back : PF.None);
            }
            // tearing away: wisps peel off where the crescent is coming apart
            double k = Tear();
            for (double u = lastTear; u < k * 1.15; u += 0.022)
            {
                double uu = Math.Min(1, u) * A.span;
                Swing s = A.With();
                s.R = A.R - Rg.Range(0, 8);
                Travel tr = TravelAt(s, uu);
                bool far = Px.Sin(tr.a) < 0;
                AddWisp(tr.x, tr.y, far);
                if (Rg.Chance(0.5)) ps.Spawn(P.Vapour, tr.x, tr.y, Rg.Range(-10, 10), -Rg.Range(6, 18), Rg.Range(0.55, 0.85), Rg.Chance(0.5) ? C[5] : C[4], Rg.Range(2.5, 4.5), 0, far ? PF.Back : PF.None);
                if (Rg.Chance(0.2)) ps.Spawn(P.Star, tr.x + Rg.Range(-3, 3), tr.y + Rg.Range(-3, 3), 0, -6, Rg.Range(0.25, 0.5), Rg.Chance(0.5) ? C[6] : WHITE, Rg.Range(0.6, 1.6), 0, far ? PF.Back : PF.None);
            }
            lastTear = Math.Max(lastTear, k * 1.15);
            // wisps rise, sway and curl, leaving a fading thread behind them
            for (int i = wisps.Count - 1; i >= 0; i--)
            {
                Wisp w = wisps[i];
                w.max += dt;
                if (w.max > w.life)
                {
                    wisps.RemoveAt(i);
                    continue;
                }
                w.vy -= 30 * dt;
                w.vx += Px.Sin(w.max * 9 + w.seed) * 120 * dt;
                w.vx -= w.vx * 2 * dt;
                w.vy -= w.vy * 1.2 * dt;
                w.x += w.vx * dt;
                w.y += w.vy * dt;
                w.pts.Add(new[] { w.x, w.y });
                if (w.pts.Count > 14) w.pts.RemoveAt(0);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Blade(p, -1);
            ps.Draw(p, Layer.Back);
            Threads(p, true);
        }

        public override void Draw(Painter p, double tt)
        {
            Blade(p, 1);
            // the blade's point glints cold as it sets off and where it stops
            if (t < WIND + 0.05)
            {
                var (x, y) = OnSwing(A, A.R - 3, 0);
                Sim.Sparkle(p, x, y, 6 * Math.Min(1, t / 0.04) * (1 - Smooth(WIND - 0.02, WIND + 0.05, t)), C[6], C[5], 0.45);
            }
            double g = t - T1 + 0.06;
            if (g > 0 && g < 0.3)
            {
                var (x, y) = OnSwing(A, A.R - 3, A.span);
                Sim.Sparkle(p, x, y, 9 * Ease.OutBack(Math.Min(1, g / 0.06)) * (1 - g / 0.3), C[6], C[5], 0.55);
            }
            ps.Draw(p, Layer.Front);
            Threads(p, false);
            lights.Draw(p);
        }

        void Blade(Painter p, double half)
        {
            double U = A.span * Head(t), k = Tear();
            if (U < 0.02 || k >= 1) return;
            uint[] ramp = half < 0 ? new[] { C[0], C[0], C[1], C[2], C[3], C[4], C[5], C[6] } : C;
            Smear(p, A, 0, U, W, (u, r, o, a) =>
            {
                double v = u / U, w = W * Px.Pow(Px.Sin(Math.PI * Px.Pow(v, 1.7)), 0.55);
                if (w < 0.5) return;
                double q = (A.R - r) / w;
                if (q < 0 || q > 1) return;
                // torn away from the tail: a ragged front of per-pixel thresholds
                double nn = Rand(Math.Floor(u * 40), Math.Floor(r), 7);
                if (k > 0 && (u / A.span) < k * 1.2 - nn * 0.2) return;
                // a pale ghostly rim, a dark body that deepens inward, a faint lit seam
                int ci = q < 0.08 ? 7 : q < 0.16 ? 6 : q < 0.25 ? 5 : q < 0.4 ? 4 : q < 0.62 ? 3 : 2;
                o.c = ramp[t > T1 && q < 0.2 ? ci - 1 : ci];
                o.a = Math.Min(1, v / 0.25);
            }, half);
            GlowAlong(p, A, A.R - 2, Math.Max(0, k * 1.2 * A.span - 0.3), U, 14, C[5], u => 0.3 * (u / U), half, 0.25);
        }

        void Threads(Painter p, bool back)
        {
            foreach (Wisp w in wisps)
            {
                if (w.far != back) continue;
                double life = 1 - w.max / w.life;
                for (int i = 1; i < w.pts.Count; i++)
                {
                    double f = (double)i / w.pts.Count;
                    p.Line(w.pts[i - 1][0], w.pts[i - 1][1], w.pts[i][0], w.pts[i][1], f > 0.75 ? C[6] : f > 0.4 ? C[5] : C[3], life * (0.3 + 0.7 * f));
                }
                if (life > 0.5) p.Glow(w.x, w.y, 5, C[5], 0.25 * life);
            }
        }
    }
}
