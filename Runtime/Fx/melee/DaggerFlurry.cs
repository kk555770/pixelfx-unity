// The crescent, trace, glint and spark recipe is adapted from Sprite Fusion's open-source slash
// (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class DaggerFlurry : Effect
    {
        static readonly Pose BASE = new Pose(Poses.READY) { ["weapon"] = "dagger", ["offWeapon"] = "dagger", ["sa"] = -0.5, ["sb"] = -0.9, ["crouch"] = 4, ["lean"] = 3, ["ffx"] = 11, ["fbx"] = -9, ["fx"] = 12, ["fy"] = -24, ["bx"] = 9, ["by"] = -24 };

        // a stab with one hand while the other comes back to the chest; the shoulders turn into each one
        static Pose Near(double fx, double fy, double sa, Pose o = null) =>
            new Pose(new Pose(BASE) { ["fx"] = fx, ["fy"] = fy, ["sa"] = sa, ["bx"] = 4, ["by"] = -24, ["sb"] = -1 }, o ?? new Pose());

        static Pose Far(double bx, double by, double sb, Pose o = null) =>
            new Pose(new Pose(BASE) { ["bx"] = bx, ["by"] = by, ["sb"] = sb, ["fx"] = 1, ["fy"] = -23, ["sa"] = -1 }, o ?? new Pose());

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 60 },
            Poses = new[]
            {
                new PoseKey(-0.4, new Pose(BASE) { ["x"] = -4 }),
                new PoseKey(-0.14, new Pose(BASE) { ["x"] = -6, ["lean"] = -2, ["crouch"] = 5, ["fx"] = 2, ["fy"] = -22, ["sa"] = -1.2, ["bx"] = 2, ["by"] = -26, ["sb"] = -1.5 }),
                new PoseKey(-0.03, new Pose(BASE) { ["x"] = -6, ["lean"] = -3, ["crouch"] = 6, ["fx"] = 1, ["fy"] = -21, ["sa"] = -1.2, ["bx"] = 1, ["by"] = -25, ["sb"] = -1.5 }),
                new PoseKey(0, Near(20, -31, -0.5, new Pose { ["x"] = 2, ["ghost"] = 1 }), "snap"),
                new PoseKey(0.055, Far(21, -23, 0.5, new Pose { ["x"] = 3 }), "snap"),
                new PoseKey(0.11, Near(18, -36, -1, new Pose { ["x"] = 4 }), "snap"),
                new PoseKey(0.165, Far(22, -31, -0.6, new Pose { ["x"] = 5 }), "snap"),
                new PoseKey(0.22, Near(20, -22, 0.2, new Pose { ["x"] = 5 }), "snap"),
                new PoseKey(0.275, Far(21, -27, 0.05, new Pose { ["x"] = 6 }), "snap"),
                new PoseKey(0.33, new Pose(BASE) { ["x"] = 4, ["lean"] = -3, ["crouch"] = 5, ["fx"] = -1, ["fy"] = -33, ["sa"] = -2.5, ["bx"] = 2, ["by"] = -23, ["sb"] = -1.2 }, "snap"),
                new PoseKey(0.37, new Pose(BASE) { ["x"] = 7, ["lean"] = 2, ["crouch"] = 4, ["fx"] = 16, ["fy"] = -34, ["sa"] = -1, ["bx"] = -7, ["by"] = -24, ["sb"] = -2.4, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.43, new Pose(BASE) { ["x"] = 9, ["lean"] = 6, ["crouch"] = 6, ["fx"] = 22, ["fy"] = -23, ["sa"] = 0.15, ["bx"] = -9, ["by"] = -24, ["sb"] = -2.6, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true }, "snap"),
                new PoseKey(0.68, new Pose(BASE) { ["x"] = 9, ["lean"] = 5, ["crouch"] = 5, ["fx"] = 21, ["fy"] = -22, ["sa"] = 0.2, ["bx"] = -8, ["by"] = -24, ["sb"] = -2.6 }),
                new PoseKey(1.05, new Pose(BASE) { ["x"] = -4 }),
            },
            Hits = new[]
            {
                new HitCue { T = 0.05, Stop = 0.015, Push = 0.5, Straw = 3, Flash = 0.02 },
                new HitCue { T = 0.105, Stop = 0.015, Push = -0.3, Straw = 3, Flash = 0.02 },
                new HitCue { T = 0.16, Stop = 0.015, Push = 0.5, Straw = 3, Flash = 0.02 },
                new HitCue { T = 0.215, Stop = 0.015, Push = -0.3, Straw = 3, Flash = 0.02 },
                new HitCue { T = 0.27, Stop = 0.015, Push = 0.5, Straw = 3, Flash = 0.02 },
                new HitCue { T = 0.325, Stop = 0.015, Push = -0.3, Straw = 3, Flash = 0.02 },
                new HitCue { T = 0.42, Stop = 0.12, Shake = 0.18, Amp = 2, Push = 2.6, Straw = 16 },
            },
            Dust = new[] { new DustCue { T = 0.43, X = 20, N = 7, Dir = 1 }, new DustCue { T = 0, X = -6, N = 3, Dir = -1 } },
        };

        public static readonly Module Module = new Module("dagger-flurry", "pixelfx/fx/melee/dagger-flurry", 148, 104, 36, 86, 0.95, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1b1630", "#3a2f63", "#6150a3", "#9480d8", "#c7b8f5", "#efe9ff", "#ffffff" },
                ["sparks"] = new[] { "#ffffff", "#efe9ff", "#c7b8f5", "#9480d8" },
                ["seed"] = 12,
            },
            (o, e) => new DaggerFlurry(o, e), act: Act, actor: 2)
        {
            Title = "Dagger flurry",
            Desc = "A flurry of small quick crescents at every angle in front of the fighter, each with a flick of sparks, finished by one big cut that glints and bursts.",
            Tags = new[] { "melee", "slash", "dagger", "combo", "flurry", "fast", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        const double EVERY = 0.055;
        // small cuts: where, how they're turned, which way they go
        static readonly double[][] CUTS =
        {
            new double[] { 21, -27, -0.5, 1 }, new double[] { 33, -16, 0.9, -1 }, new double[] { 26, -38, 2.4, 1 },
            new double[] { 37, -30, -1.8, -1 }, new double[] { 19, -17, 0.2, 1 }, new double[] { 30, -24, 1.4, -1 },
        };

        sealed class SliverSpec
        {
            public double a, r, v, arc;
        }

        readonly uint[] C;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly Swing big;
        readonly Swing[] all;
        readonly Travel[] ends;
        readonly bool[] done;
        readonly SliverSpec[] slivers;
        double t;

        DaggerFlurry(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            var small = new Swing[CUTS.Length];
            for (int i = 0; i < CUTS.Length; i++)
            {
                double[] c = CUTS[i];
                small[i] = new Swing { cx = c[0], cy = c[1], R = Rg.Range(12, 15), sy = Rg.Range(0.55, 0.75), rot = c[2], a0 = Rg.Range(-2.8, -2.2), dir = c[3], span = Rg.Range(2.3, 2.8), W = Rg.Range(5.2, 6.4), t0 = i * EVERY, sweep = 0.035, hold = 0.03, trail = 0.13, ramp = C };
            }
            // the finisher: one wide flat cut right across the front, left to right
            big = new Swing { cx = 24, cy = -26, R = 30, sy = 0.36, k = 0.24, rot = 0.12, a0 = 3.0, dir = -1, span = 2.85, W = 11, t0 = small.Length * EVERY + 0.04, sweep = 0.055, hold = 0.06, trail = 0.24, ramp = C };
            all = new Swing[small.Length + 1];
            Array.Copy(small, all, small.Length);
            all[small.Length] = big;
            ends = Array.ConvertAll(all, SlashEnd);
            done = new bool[all.Length];
            double[] sa = { -2.6, -1.5, -0.4, 0.7 };
            slivers = new SliverSpec[sa.Length];
            for (int i = 0; i < sa.Length; i++) slivers[i] = new SliverSpec { a = sa[i] + Rg.Range(-0.2, 0.2), r = Rg.Range(4, 7), v = Rg.Range(70, 110), arc = Rg.Range(0.4, 0.7) };
        }

        public override void Update(double dt)
        {
            t += dt;
            for (int i = 0; i < all.Length; i++)
            {
                Swing s = all[i];
                if (done[i] || t < s.t0 + s.sweep) continue;
                done[i] = true;
                Travel e = ends[i];
                bool last = s == big;
                lights.Add(e.x, e.y, last ? 56 : 26, C[3], last ? 0.6 : 0.4, last ? 0.4 : 0.14);
                if (last) lights.Add(e.x, e.y, 16, WHITE, 0.7, 0.08);
                for (int k = 0; k < (last ? 12 : 3); k++)
                {
                    double a = Atan2(e.dy, e.dx) + Rg.Range(-0.5, 0.5), sp = Rg.Range(100, last ? 340 : 220);
                    ps.Spawn(P.Spark, e.x, e.y, Px.Cos(a) * sp, Px.Sin(a) * sp, Rg.Range(0.1, last ? 0.35 : 0.22));
                }
                Shavings(ps, Rg, s, last ? 14 : 4, new[] { C[4], C[5] });
                if (last)
                    for (int k = 0; k < 5; k++)
                        ps.Spawn(P.Star, s.cx + Rg.Range(-22, 22), s.cy + Rg.Range(-14, 14), 0, -4, Rg.Range(0.2, 0.45), Rg.Chance(0.5) ? C[5] : WHITE, Rg.Range(1, 2.2), Rg.Range(0.05, 0.25));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            foreach (Swing s in all) Slash(p, s, t);
            for (int i = 0; i < all.Length; i++)
            {
                Swing s = all[i];
                Travel e = ends[i];
                double g = t - s.t0 - s.sweep * 0.6;
                bool last = s == big;
                double life = last ? 0.24 : 0.12;
                if (g > 0 && g < life) Sim.Sparkle(p, e.x, e.y, (last ? 10 : 4) * Ease.OutBack(Math.Min(1, g / 0.04)) * (1 - g / life), C[5], C[3], last ? 0.6 : 0.35);
                // light rides each edge while it's moving
                double u = t - s.t0;
                if (u > 0 && u < s.sweep + 0.1)
                {
                    Swing hs = s.With();
                    hs.span = s.span * Ease.OutExpo(Clamp(u / s.sweep));
                    Travel h = SlashEnd(hs);
                    p.Glow(h.x, h.y, last ? 40 : 22, C[3], (last ? 0.5 : 0.35) * (1 - u / (s.sweep + 0.1)));
                }
            }
            // the finisher throws slivers off its end
            double q = t - big.t0 - big.sweep;
            if (q > 0.02 && q < 0.14)
            {
                double k = (q - 0.02) / 0.12;
                Travel e = ends[ends.Length - 1];
                foreach (SliverSpec s in slivers) Sliver(p, e.x, e.y, s.a + Atan2(e.dy, e.dx), s.r + s.v * (q - 0.02), s.arc * (1 - 0.4 * k), k < 0.4 ? C[6] : k < 0.7 ? C[5] : C[4], 1 - k);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
