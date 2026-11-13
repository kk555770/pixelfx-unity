// The crescent recipe is adapted from Sprite Fusion's open-source slash, and the star from his impact
// (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class Uppercut : Effect
    {
        static readonly Pose FISTS = new Pose(Poses.READY) { ["fx"] = 12, ["fy"] = -25, ["bx"] = 9, ["by"] = -27, ["crouch"] = 3, ["lean"] = 1 };
        static readonly Pose COIL = new Pose(FISTS) { ["crouch"] = 7, ["lean"] = 3, ["fx"] = 8, ["fy"] = -11, ["bx"] = 8, ["by"] = -27, ["ffx"] = 10, ["fbx"] = -9 };
        static readonly Pose TOP = new Pose(FISTS) { ["x"] = 1, ["y"] = -11, ["crouch"] = -1, ["lean"] = 2, ["fx"] = 12, ["fy"] = -42, ["bx"] = -6, ["by"] = -21, ["ffx"] = 8, ["ffy"] = -8, ["fbx"] = -3, ["fby"] = -2, ["toeB"] = true, ["ghost"] = 1 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 44 },
            Poses = new[]
            {
                new PoseKey(-0.42, FISTS),
                new PoseKey(-0.16, COIL),
                new PoseKey(0, new Pose(COIL) { ["crouch"] = 8 }),
                new PoseKey(0.03, new Pose(FISTS) { ["y"] = -4, ["crouch"] = 1, ["lean"] = 4, ["fx"] = 18, ["fy"] = -27, ["bx"] = -5, ["by"] = -23, ["ffx"] = 9, ["fbx"] = -7, ["toeB"] = true, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.076, TOP, "snap"),
                new PoseKey(0.3, new Pose(TOP) { ["y"] = -13, ["ghost"] = null }),
                new PoseKey(0.56, new Pose(TOP) { ["y"] = 0, ["crouch"] = 2, ["fx"] = 13, ["fy"] = -31, ["ffx"] = 7, ["ffy"] = -6, ["fbx"] = -5, ["fby"] = -5, ["ghost"] = null }, "in"),
                new PoseKey(0.6, new Pose(FISTS) { ["y"] = 0, ["crouch"] = 7, ["lean"] = 3, ["fx"] = 12, ["fy"] = -14, ["bx"] = -6, ["by"] = -16, ["ffx"] = 10, ["fbx"] = -9 }, "snap"),
                new PoseKey(0.98, FISTS),
            },
            Hits = new[] { new HitCue { T = 0.076, Stop = 0.13, Shake = 0.18, Amp = 2, Push = -2.6, Straw = 18, Y = -34 } },
            Dust = new[] { new DustCue { T = 0.03, X = 8, N = 6 }, new DustCue { T = 0.6, X = 0, N = 6 } },
        };

        public static readonly Module Module = new Module("uppercut", "pixelfx/fx/melee/uppercut", 132, 128, 48, 110, 0.85, false, 30,
            new Options
            {
                ["palette"] = new[] { "#3a1408", "#7a2a10", "#c4501a", "#f08a2a", "#ffc94a", "#fff2a8", "#ffffff" },
                ["outline"] = "#2a0a08",
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["seed"] = 11,
            },
            (o, e) => new Uppercut(o, e), act: Act, actor: 2)
        {
            Title = "Uppercut",
            Desc = "A crescent rises from the floor past the face, and at the top a comic impact star bursts: bold outline, hot core, burst lines, twinkles and sparks.",
            Tags = new[] { "melee", "uppercut", "punch", "rising", "launcher", "comic", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        static readonly uint DUST = Hex("#6a6080");

        sealed class BurstLine
        {
            public double a, r0, L;
        }

        readonly uint[] C;
        readonly uint INK_;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly Swing A;
        readonly double SX, SY, HIT;
        readonly Spike[] spikes;
        readonly BurstLine[] bursts;
        double t;
        bool hit, kicked;

        Uppercut(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            INK_ = o.Color("outline");
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            // up from the floor in front, bowing forward past the face, to the top
            A = new Swing { cx = 8, cy = -25, R = 27, sy = 1, rot = 0, a0 = 1.08, dir = -1, span = 2.5, W = 13, t0 = 0.02, sweep = 0.07, hold = 0.05, trail = 0.22, ramp = C };
            Travel top = SlashEnd(A);
            SX = top.x + 3;
            SY = top.y - 5;
            HIT = A.t0 + A.sweep * 0.8;
            spikes = new Spike[10];
            for (int i = 0; i < 10; i++)
            {
                double a = (i / 10.0) * TAU + Rg.Range(-0.12, 0.12) - Math.PI / 2, len = (i % 2 != 0 ? 7 : 13) + Rg.Range(0, 4), w = i % 2 != 0 ? 2.2 : 3;
                spikes[i] = new Spike(a, len, w);
            }
            bursts = new BurstLine[8];
            for (int i = 0; i < 8; i++) bursts[i] = new BurstLine { a = (i / 8.0) * TAU + 0.2 + Rg.Range(-0.15, 0.15), r0 = Rg.Range(15, 18), L = Rg.Range(5, 9) };
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!kicked)
            {
                kicked = true;
                // the fist comes up off the floor
                for (int i = 0; i < 4; i++) ps.Spawn(P.Dust, 18 + Rg.Range(-4, 4), -1, Rg.Range(-30, 30), -Rg.Range(5, 20), Rg.Range(0.3, 0.55), DUST, Rg.Range(1.5, 2.6));
            }
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(SX, SY, 56, C[4], 0.6, 0.4);
                lights.Add(SX, SY, 18, WHITE, 0.7, 0.08);
                for (int i = 0; i < 16; i++)
                {
                    double a = -Math.PI / 2 + Rg.Range(-1.6, 1.6), sp = Rg.Range(120, 320);
                    ps.Spawn(P.Spark, SX, SY, Px.Cos(a) * sp, Px.Sin(a) * sp, Rg.Range(0.15, 0.45));
                }
                for (int i = 0; i < 6; i++)
                {
                    double a = Rg.Range(0, TAU), d = Rg.Range(14, 24);
                    ps.Spawn(P.Star, SX + Px.Cos(a) * d, SY + Px.Sin(a) * d, Px.Cos(a) * 20, Px.Sin(a) * 20 - 10, Rg.Range(0.25, 0.5), Rg.Chance(0.5) ? C[5] : WHITE, Rg.Range(1.4, 2.6), Rg.Range(0.04, 0.18));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            Slash(p, A, t);
            // speed lines rising beside the crescent while it goes up
            double u = t - A.t0;
            if (u > 0 && u < 0.16)
            {
                double[][] specs = { new[] { A.cx + A.R + 7, -16, 18 }, new[] { A.cx + A.R + 11, -30, 12 }, new[] { A.cx - 4, -40, 10 } };
                foreach (double[] spec in specs)
                {
                    double dx = spec[0], y0 = spec[1], L = spec[2];
                    double k = u / 0.16, y = y0 - 30 * k;
                    for (double d = 0; d < L; d += 1) p.Dot(dx, y + d, d < 3 ? WHITE : d < L * 0.5 ? C[5] : C[3], (1 - d / L) * (1 - k));
                }
            }
            if (u > 0 && u < A.sweep + 0.06)
            {
                var (x, y) = OnSwing(A, A.R - 5, A.span * Ease.OutExpo(Clamp(u / A.sweep)));
                p.Glow(x, y, 34, C[3], 0.45 * (1 - u / (A.sweep + 0.06)));
            }
            // the comic star: pops past full size, holds, then shrinks away turning, with burst lines round it
            double q = t - HIT;
            if (q >= 0 && q < 0.36)
            {
                double pop = Ease.OutBack(Clamp(q / 0.05), 2.6), outK = 1 - Smooth(0.16, 0.36, q), sc = pop * (0.35 + 0.65 * outK), turn = q * 1.2;
                var sp = new Spike[spikes.Length];
                for (int i = 0; i < spikes.Length; i++) sp[i] = new Spike(spikes[i].a + turn, spikes[i].len, spikes[i].w);
                if (sc > 0.08)
                {
                    Star(p, SX, SY, sp, sc * 1.12, 1, INK_, INK_, INK_, INK_);
                    Star(p, SX, SY, sp, sc, 1, q < 0.05 ? WHITE : C[6], C[4], C[3], C[2]);
                }
                p.Glow(SX, SY, 30, C[4], 0.45 * outK);
            }
            if (q >= 0.02 && q < 0.24)
            {
                // a ring bursts out round the star, white to orange, breaking into dots
                double k = (q - 0.02) / 0.22, r = 7 + 17 * Ease.OutCubic(k), th = Math.Max(1, 2.2 * (1 - k));
                uint col = k < 0.25 ? WHITE : k < 0.5 ? C[5] : k < 0.75 ? C[4] : C[3];
                for (double a = 0; a < TAU; a += 0.6 / r)
                    for (double o = 0; o < th; o += 0.5) p.Dot(SX + Px.Cos(a) * (r - o), SY + Px.Sin(a) * (r - o), col, 1.15 - k);
            }
            if (q >= 0.02 && q < 0.2)
            {
                double k = (q - 0.02) / 0.18;
                foreach (BurstLine b in bursts)
                {
                    double r0 = b.r0 + 10 * Ease.OutCubic(k), ca = Px.Cos(b.a), sa = Px.Sin(b.a);
                    p.Line(SX + ca * r0, SY + sa * r0, SX + ca * (r0 + b.L * (1 - k)), SY + sa * (r0 + b.L * (1 - k)), k < 0.4 ? WHITE : C[5], 1.1 - k);
                }
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
