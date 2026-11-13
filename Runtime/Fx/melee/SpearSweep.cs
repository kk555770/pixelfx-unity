// The crescent, trace and glint recipe is adapted from Sprite Fusion's open-source slash, and the rolling dust and
// grit from his explosion (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class SpearSweep : Effect
    {
        // upright in one hand; two hands take it low across the belt, he winds right round, and it whips through the front
        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "spear", ["sa"] = -1.42, ["fx"] = 11, ["fy"] = -21, ["bx"] = 7, ["by"] = -16 };
        static readonly Pose COCK = new Pose(REST_) { ["two"] = true, ["yaw"] = 2.5, ["crouch"] = 6, ["lean"] = 2, ["fx"] = 15, ["fy"] = -17, ["sa"] = 0.05, ["ffx"] = 9, ["fbx"] = -8 };
        static readonly Pose OUT_ = new Pose(REST_) { ["two"] = true, ["x"] = 2, ["crouch"] = 6, ["lean"] = 6, ["fx"] = 20, ["fy"] = -15, ["sa"] = 0.04, ["ffx"] = 12, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 62 },
            Poses = new[]
            {
                new PoseKey(-0.56, REST_),
                new PoseKey(-0.34, new Pose(REST_) { ["two"] = true, ["crouch"] = 4, ["fx"] = 14, ["fy"] = -22, ["sa"] = -0.7 }),
                new PoseKey(-0.14, COCK),
                new PoseKey(-0.02, new Pose(COCK) { ["crouch"] = 7, ["yaw"] = 2.7 }),
                new PoseKey(0.045, new Pose(OUT_) { ["x"] = 1, ["yaw"] = 1.3, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.09, new Pose(OUT_) { ["ghost"] = 1 }, "snap"),
                new PoseKey(0.14, OUT_, "snap"),
                new PoseKey(0.62, new Pose(OUT_) { ["crouch"] = 5, ["lean"] = 5 }),
                new PoseKey(1.12, new Pose(REST_) { ["two"] = true, ["crouch"] = 4, ["sa"] = -0.7, ["fx"] = 14, ["fy"] = -22 }),
                new PoseKey(1.5, REST_),
            },
            Hits = new[] { new HitCue { T = 0.1, Stop = 0.12, Shake = 0.18, Amp = 2, Push = 2.6, Straw = 18, Y = -12 } },
            Dust = new[] { new DustCue { T = 0.09, X = 14, N = 6, Dir = 1 }, new DustCue { T = 0.09, X = -10, N = 5, Dir = -1 } },
        };

        public static readonly Module Module = new Module("spear-sweep", "pixelfx/fx/melee/spear-sweep", 216, 84, 104, 70, 1.5, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0f2a2a", "#18504a", "#22806e", "#3fb894", "#8fe6c0", "#dcfff0", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["stone"] = "#4a3f69",
                ["seed"] = 10,
            },
            (o, e) => new SpearSweep(o, e), act: Act, actor: 2)
        {
            Title = "Spear sweep",
            Desc = "A wide low sweep at shin height: a long flat crescent swings from behind the legs round the front, and the wind of it kicks a wake of dust and grit along the floor.",
            Tags = new[] { "melee", "sweep", "spear", "polearm", "low", "aoe", "ground", "dust", "combat", "one-shot" },
            Bg = "ground",
        };

        readonly uint[] C;
        readonly Rng Rg;
        readonly Stone st;
        readonly Particles ps;
        readonly Lights lights;
        readonly Swing A;
        readonly Travel end;
        double t, lastU;
        bool done;

        SpearSweep(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            Rg = new Rng(o.Seed);
            st = new Stone(o.Color("stone"));
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            // from behind the left leg, round the front, out to the right; the far part is behind the fighter
            A = new Swing { cx = 2, cy = -12, R = 50, sy = 0.2, k = 0.19, rot = -0.04, a0 = Math.PI + 0.55, dir = -1, span = Math.PI + 0.35, W = 19, t0 = 0, sweep = 0.13, hold = 0.05, trail = 0.3, ramp = C };
            end = SlashEnd(A);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the wind under the blade kicks dust up off the floor as it passes, rolling on the way it goes
            double U = A.span * Ease.OutExpo(Clamp(t / A.sweep));
            uint[] grit = { st.light, st.body, st.shade };
            for (double u = lastU; u < U; u += 0.09)
            {
                Travel tr = TravelAt(A, u);
                bool near = Px.Sin(tr.a) >= 0;
                double fy = near ? -1 : -3, sp = Rg.Range(30, 90);
                ps.Spawn(P.Smoke, tr.x, fy - Rg.Range(0, 2), tr.dx * sp, -Rg.Range(4, 16), Rg.Range(0.7, 1.15), st.dust, Rg.Range(3, 5.5), Rg.Range(0, 0.05));
                if (Rg.Chance(0.6)) ps.Spawn(P.Grit, tr.x, fy, tr.dx * Rg.Range(40, 120) + Rg.Range(-20, 20), -Rg.Range(40, 130), Rg.Range(0.6, 1.0), Rg.Pick(grit), Rg.Chance(0.3) ? 2 : 1);
                if (Rg.Chance(0.35)) ps.Spawn(P.Mote, tr.x, tr.y, tr.dx * Rg.Range(40, 100), tr.dy * 40 - Rg.Range(5, 20), Rg.Range(0.2, 0.4), Rg.Chance(0.5) ? C[4] : C[5], 0.6, 0, near ? PF.None : PF.Back);
            }
            lastU = U;
            if (!done && t >= A.sweep)
            {
                done = true;
                lights.Add(end.x, end.y - 4, 50, C[3], 0.5, 0.35);
                for (int i = 0; i < 8; i++)
                {
                    double a = Atan2(end.dy, end.dx) + Rg.Range(-0.5, 0.3), sp = Rg.Range(120, 260);
                    ps.Spawn(P.Spark, end.x, end.y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, Rg.Range(0.12, 0.3));
                }
                // the wake piles up and rolls on past the tip
                for (int i = 0; i < 6; i++) ps.Spawn(P.Smoke, end.x + Rg.Range(-6, 4), -Rg.Range(1, 4), Rg.Range(30, 90), -Rg.Range(4, 14), Rg.Range(0.9, 1.25), st.dust, Rg.Range(4.5, 7), Rg.Range(0, 0.08), PF.Big);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Slash(p, A, t, -1);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            Slash(p, A, t, 1);
            double g = t - A.sweep * 0.6;
            if (g > 0 && g < 0.24) Sim.Sparkle(p, end.x, end.y, 9 * Ease.OutBack(Math.Min(1, g / 0.05)) * (1 - g / 0.24), C[5], C[3], 0.55);
            // light rides the edge
            if (t < A.sweep + 0.12)
            {
                var (x, y) = OnSwing(A, A.R - 5, A.span * Ease.OutExpo(Clamp(t / A.sweep)));
                p.Glow(x, y, 36, C[3], 0.45 * (1 - t / (A.sweep + 0.12)));
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
