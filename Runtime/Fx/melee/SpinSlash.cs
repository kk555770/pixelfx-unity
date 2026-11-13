// The crescent shading, cooling ramp, dotted trace, glint and spark recipe is adapted from Sprite Fusion's
// open-source slash (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class SpinSlash : Effect
    {
        static readonly Pose STANCE = new Pose(Poses.READY) { ["weapon"] = "sword", ["fx"] = 12, ["fy"] = -21, ["sa"] = -0.85 };
        // he really turns: forward, away from us, facing back, towards us, and round to the front
        static readonly Pose WOUND = new Pose(STANCE) { ["crouch"] = 6, ["lean"] = -2, ["yaw"] = 1.1, ["fx"] = 13, ["fy"] = -20, ["sa"] = 0.3, ["bx"] = 6, ["by"] = -24, ["ffx"] = 8, ["fbx"] = -8 };
        static readonly Pose WIDE = new Pose(STANCE) { ["crouch"] = 5, ["lean"] = 3, ["fx"] = 21, ["fy"] = -21, ["sa"] = 0.05, ["bx"] = -9, ["by"] = -23, ["ffx"] = 11, ["fbx"] = -11 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 60 },
            Poses = new[]
            {
                new PoseKey(-0.46, STANCE),
                new PoseKey(-0.2, WOUND),
                new PoseKey(-0.02, new Pose(WOUND) { ["crouch"] = 7, ["yaw"] = 1.3 }),
                new PoseKey(0, new Pose(WIDE) { ["sa"] = -0.1, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.05, new Pose(WIDE) { ["yaw"] = -1.57, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.09, new Pose(WIDE) { ["yaw"] = -3.14, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.13, new Pose(WIDE) { ["yaw"] = -4.71, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.17, WIDE, "snap"),
                new PoseKey(0.56, new Pose(WIDE) { ["crouch"] = 4, ["lean"] = 2 }),
                new PoseKey(0.92, STANCE),
            },
            Hits = new[] { new HitCue { T = 0.17, Stop = 0.12, Shake = 0.16, Amp = 2, Push = 2.4, Straw = 16 } },
            Dust = new[] { new DustCue { T = 0, X = 0, N = 6 }, new DustCue { T = 0.17, X = 12, N = 6, Dir = 1 } },
        };

        public static readonly Module Module = new Module("spin-slash", "pixelfx/fx/melee/spin-slash", 208, 104, 104, 84, 0.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#16224a", "#1f4a86", "#2a7fc0", "#48b8ec", "#9fe2ff", "#e2f7ff", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["radius"] = 46,
                ["seed"] = 3,
            },
            (o, e) => new SpinSlash(o, e), act: Act, actor: 2)
        {
            Title = "Spin slash",
            Desc = "The blade whirls once round the waist on a tilted plane, behind the body and back in front; the closed ring flares white, dissolves into a dotted trace and throws sparks.",
            Tags = new[] { "melee", "slash", "spin", "sword", "aoe", "combat", "one-shot" },
            Bg = "ground",
        };

        const double SW = 0.17; // the edge goes once round
        const double FLARE = 0.3; // the closed ring flares and dissolves
        const double W = 16, WE = 5;

        static double Turn(double t) => 1 - Px.Pow(1 - Clamp(t / SW), 1.6); // fraction of the turn the edge has made
        static double PassedAt(double f) => SW * (1 - Px.Pow(1 - Clamp(f), 1 / 1.6)); // when the edge passed fraction f

        readonly uint[] C, DIM;
        readonly int n;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly Swing A, E;
        double t, lastU;
        bool closed;

        SpinSlash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            n = C.Length;
            Rg = new Rng(o.Seed);
            double R0 = o.Num("radius");
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            // the spin starts front-right, goes up the right side, behind the body, down the left and back across the front
            A = new Swing { cx = 0, cy = -17, R = R0, sy = 0.27, rot = -0.1, a0 = 0.5, dir = -1, k = 0.15 };
            // a thinner echo just inside, a beat behind
            E = A.With();
            E.R = R0 * 0.8;
            E.k = 0.1;
            // the far half sits deeper in the ramp
            DIM = new[] { C[0], C[0], C[1], C[2], C[3], C[4], C[5] };
        }

        // the smear: thickest just behind the edge, thinning and cooling where the edge passed longer ago
        void Body(Painter p, Swing s, double w0, double lag, double half, bool rim)
        {
            double U = TAU * Turn(t - lag);
            uint[] ramp = half < 0 ? DIM : C;
            if (U < 0.02 || t - lag > SW + 0.02) return;
            Smear(p, s, 0, U, w0, (u, r, o, _) =>
            {
                double age = t - lag - PassedAt(u / TAU), lead = U - u;
                double w = w0 * Px.Pow(Clamp(lead / 0.85), 0.5) * (0.3 + 0.7 * Exp(-age / 0.075)) * Px.Pow(Clamp(u / 1.3), 0.7);
                if (w < 0.45) return;
                double q = (s.R - r) / w;
                if (q < 0 || q > 1) return;
                // white rim while fresh, then darker steps inward, with age and towards the tail
                double ci = rim && q < 0.26 && age < 0.06 ? 0 : Round(q * (n - 1) * 0.7 + age * 13 + (rim ? 0.4 : 1.4));
                o.c = ramp[n - 1 - (int)Clamp(ci, 0, n - 1)];
                o.a = Math.Min(1, u / 1.1 + 0.2);
            }, half);
        }

        void Ring(Painter p, double half)
        {
            // the swept smear and its echo
            Body(p, E, WE, 0.025, half, false);
            Body(p, A, W, 0, half, true);
            if (t < SW + 0.04) GlowAlong(p, A, A.R - 5, 0, TAU * Turn(t), 14, C[3], u => (half < 0 ? 0.2 : 0.3) * Exp(-(t - PassedAt(u / TAU)) / 0.08), half);
            if (t < SW) return;
            // closed: the ring flares white, swells a little, thins, cools and breaks into dots
            double f = (t - SW) / FLARE;
            uint[] ramp = half < 0 ? DIM : C;
            if (f < 1)
            {
                double R = A.R + 6 * Ease.OutCubic(f), th = f < 0.13 ? 9 : Math.Max(1, 6 * Px.Pow(1 - f, 1.5) + 0.5);
                Swing S = A.With();
                S.R = R;
                int step = f < 0.13 ? 6 : f < 0.3 ? 5 : f < 0.55 ? 4 : 3;
                Smear(p, S, 0, TAU, th, (u, r, o, _) =>
                {
                    double q = (R - r) / th;
                    if (q < 0 || q > 1) return;
                    o.c = f < 0.13 ? (q < 0.6 ? WHITE : ramp[5]) : q < 0.45 ? ramp[step] : ramp[step - 1];
                    o.a = f < 0.4 ? 1 : 1.3 - f;
                }, half);
                GlowAlong(p, S, R - 2, 0, TAU, 14, C[4], 0.32 * (1 - f) * (1 - f), half);
            }
            // a dotted trace of the blade's path lingers where the ring was, fading
            double k = 1 - Clamp((t - SW - 0.06) / 0.4);
            if (k > 0) Trace(p, A, A.R - 0.5, 0, TAU, ramp[4], u => Clamp(k * 1.5 - Rand(Math.Floor(u * 30), 5) * 0.8) * 0.95, half);
        }

        public override void Update(double dt)
        {
            t += dt;
            double U = TAU * Turn(t);
            // shavings peel off the edge as it goes round, flung along its path
            if (t < SW)
            {
                for (double u = lastU; u < U; u += 0.3)
                {
                    Travel tr = TravelAt(A, u);
                    double ox = tr.x - A.cx, oy = tr.y - A.cy, l = Hypot(ox, oy);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    double sp = Rg.Range(40, 120);
                    ps.Spawn(P.Mote, tr.x, tr.y, tr.dx * sp + (ox / l) * sp * 0.35, tr.dy * sp * 0.6 - Rg.Range(5, 30), Rg.Range(0.22, 0.45), C[4 + Rg.Int(0, 1)], 0.55, 0, Px.Sin(tr.a) < 0 ? PF.Back : PF.None);
                }
                lastU = U;
            }
            if (!closed && t >= SW)
            {
                closed = true;
                lights.Add(A.cx, A.cy + 4, 90, C[3], 0.4, 0.4);
                // sparks off the edge where it closes, and all round the ring along its turn
                Travel end = TravelAt(A, TAU);
                for (int i = 0; i < 9; i++)
                {
                    double a = Atan2(end.dy, end.dx) + Rg.Range(-0.45, 0.35), sp = Rg.Range(160, 360);
                    ps.Spawn(P.Spark, end.x, end.y, Px.Cos(a) * sp, Px.Sin(a) * sp - Rg.Range(20, 70), Rg.Range(0.14, 0.36));
                }
                for (int i = 0; i < 16; i++)
                {
                    double u = Rg.Range(0, TAU);
                    Travel tr = TravelAt(A, u);
                    double sp = Rg.Range(110, 260);
                    ps.Spawn(P.Spark, tr.x, tr.y, tr.dx * sp, tr.dy * sp - Rg.Range(20, 80), Rg.Range(0.12, 0.32), 0, 1, 0, Px.Sin(tr.a) < 0 ? PF.Back : PF.None);
                }
                // and twinkles left hanging on the ring
                for (int i = 0; i < 6; i++)
                {
                    double u = Rg.Range(0, TAU);
                    var (x, y) = OnSwing(A, A.R + Rg.Range(-1, 3), u);
                    ps.Spawn(P.Star, x, y, 0, -Rg.Range(2, 8), Rg.Range(0.25, 0.5), Rg.Chance(0.5) ? C[5] : WHITE, Rg.Range(1.2, 2.6), Rg.Range(0.04, 0.16), Px.Sin(A.a0 + A.dir * u) < 0 ? PF.Back : PF.None);
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Ring(p, -1);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            Ring(p, 1);
            // the blade catches the light as it sets off, and glints again where the ring closes
            if (t < 0.06)
            {
                var (x, y) = OnSwing(A, A.R - 3, 0);
                Sim.Sparkle(p, x, y, 7 * (1 - t / 0.06), C[5], C[3], 0.5);
            }
            double g = t - SW * 0.85;
            var (tx, ty) = OnSwing(A, A.R - 3, TAU);
            if (g > 0 && g < 0.24) Sim.Sparkle(p, tx, ty, 10 * Ease.OutBack(Math.Min(1, g / 0.05)) * (1 - g / 0.24), C[5], C[3], 0.6);
            // light rides the edge
            if (t < SW + 0.05)
            {
                double U = TAU * Turn(t);
                var (x, y) = OnSwing(A, A.R - 5, U);
                bool far = Px.Sin(A.a0 + A.dir * U) < 0;
                p.Glow(x, y, far ? 26 : 38, C[3], (far ? 0.3 : 0.5) * (1 - Smooth(SW - 0.04, SW + 0.05, t)));
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
