// The crescent, trace, glint and spark recipe is adapted from Sprite Fusion's open-source slash
// (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class DoubleSlash : Effect
    {
        static readonly Pose STANCE = new Pose(Poses.READY) { ["weapon"] = "sword", ["fx"] = 12, ["fy"] = -21, ["sa"] = -0.85 };
        static readonly Pose HIGH = new Pose(STANCE) { ["x"] = -7, ["crouch"] = 4, ["lean"] = -4, ["fx"] = -7, ["fy"] = -35, ["sa"] = -2.65, ["bx"] = 9, ["by"] = -25, ["armsFront"] = true, ["ffx"] = 9, ["fbx"] = -8 };
        static readonly Pose LOW = new Pose(STANCE) { ["x"] = 3, ["crouch"] = 6, ["lean"] = 6, ["fx"] = 22, ["fy"] = -20, ["sa"] = 0.62, ["bx"] = -8, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 62 },
            Poses = new[]
            {
                new PoseKey(-0.42, new Pose(STANCE) { ["x"] = -7 }),
                new PoseKey(-0.18, HIGH),
                new PoseKey(-0.04, new Pose(HIGH) { ["crouch"] = 5 }),
                new PoseKey(0, new Pose(STANCE) { ["x"] = 0, ["crouch"] = 2, ["lean"] = 3, ["fx"] = 16, ["fy"] = -37, ["sa"] = -1.45, ["bx"] = -7, ["by"] = -24, ["ffx"] = 11, ["fbx"] = -9, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.06, LOW, "snap"),
                new PoseKey(0.14, new Pose(LOW) { ["x"] = 4, ["crouch"] = 6, ["lean"] = 5, ["fx"] = 20, ["fy"] = -17, ["sa"] = 0.85, ["ghost"] = 1 }),
                new PoseKey(0.17, new Pose(LOW) { ["x"] = 5, ["crouch"] = 3, ["lean"] = 5, ["fx"] = 22, ["fy"] = -28, ["sa"] = 0.1, ["by"] = -18, ["toeB"] = true, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.21, new Pose(LOW) { ["x"] = 6, ["crouch"] = 1, ["lean"] = 6, ["fx"] = 19, ["fy"] = -37, ["sa"] = -0.95, ["by"] = -18, ["ffx"] = 13, ["fby"] = -6 }, "snap"),
                new PoseKey(0.5, new Pose(LOW) { ["x"] = 6, ["crouch"] = 3, ["lean"] = 3, ["fx"] = 18, ["fy"] = -36, ["sa"] = -1.05, ["by"] = -19, ["ffx"] = 12 }),
                new PoseKey(0.92, new Pose(STANCE) { ["x"] = -7 }),
            },
            Hits = new[]
            {
                new HitCue { T = 0.06, Stop = 0.05, Shake = 0.08, Amp = 1, Push = 1.4, Straw = 8 },
                new HitCue { T = 0.16, Stop = 0.14, Shake = 0.2, Amp = 2, Push = 2.8, Straw = 18, Y = -28 },
            },
            Dust = new[] { new DustCue { T = 0.06, X = 16, N = 5, Dir = 1 }, new DustCue { T = 0.17, X = -8, N = 4, Dir = -1 } },
        };

        public static readonly Module Module = new Module("double-slash", "pixelfx/fx/melee/double-slash", 164, 108, 44, 92, 0.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0a22", "#5e1238", "#a01f4e", "#e0386a", "#ff86a6", "#ffd2dd", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["seed"] = 6,
            },
            (o, e) => new DoubleSlash(o, e), act: Act, actor: 2)
        {
            Title = "Double slash",
            Desc = "A down cut, then an up cut that crosses it: the moment the X closes both strokes flash white, the crossing bursts with light and sparks, and a dotted X lingers.",
            Tags = new[] { "melee", "slash", "combo", "sword", "cross", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        const double UP = 0.14; // the second cut starts
        const double R0 = 62;
        const double CLOSE = UP + 0.012;
        const double X = 34, Y = -23;

        static Swing Cut(double x, double y, double th, double dir, double span) => Through(x, y, th, dir, R0, span, 0.55);

        sealed class SliverSpec
        {
            public double a, r, v, arc;
        }

        readonly uint[] C;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly Swing down, up;
        readonly Travel[] ends;
        readonly SliverSpec[] slivers;
        double t;
        bool cut1, cut2, closed;

        DoubleSlash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            down = Cut(X, Y, 0.72, 1, 0.84);
            down.W = 10; down.t0 = 0; down.sweep = 0.06; down.hold = 0.07; down.trail = 0.24; down.ramp = C; down.lens = true;
            up = Cut(X, Y, -0.66, -1, 0.84);
            up.W = 11; up.t0 = UP; up.sweep = 0.06; up.hold = 0.06; up.trail = 0.26; up.ramp = C; up.lens = true;
            ends = new[] { SlashEnd(down), SlashEnd(up) };
            double[] sa = { -2.4, -1.1, 0.2, 1.6 };
            slivers = new SliverSpec[sa.Length];
            for (int i = 0; i < sa.Length; i++) slivers[i] = new SliverSpec { a = sa[i] + Rg.Range(-0.2, 0.2), r = Rg.Range(5, 8), v = Rg.Range(70, 120), arc = Rg.Range(0.4, 0.7) };
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!cut1 && t >= down.sweep)
            {
                cut1 = true;
                Shavings(ps, Rg, down, 10, new[] { C[4], C[5] });
                for (int i = 0; i < 6; i++)
                {
                    double a = Atan2(ends[0].dy, ends[0].dx) + Rg.Range(-0.5, 0.5), sp = Rg.Range(120, 300);
                    ps.Spawn(P.Spark, ends[0].x, ends[0].y, Px.Cos(a) * sp, Px.Sin(a) * sp, Rg.Range(0.12, 0.3));
                }
            }
            if (!closed && t >= CLOSE)
            {
                closed = true;
                lights.Add(X, Y, 56, C[3], 0.45, 0.4);
                lights.Add(X, Y, 18, WHITE, 0.6, 0.08);
                for (int i = 0; i < 18; i++)
                {
                    double a = Rg.Range(-Math.PI, Math.PI) * 0.8, sp = Rg.Range(140, 360);
                    ps.Spawn(P.Spark, X, Y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, Rg.Range(0.14, 0.4));
                }
                for (int i = 0; i < 5; i++) ps.Spawn(P.Star, X + Rg.Range(-14, 14), Y + Rg.Range(-12, 12), 0, -4, Rg.Range(0.2, 0.4), Rg.Chance(0.5) ? C[5] : WHITE, Rg.Range(1, 2.2), Rg.Range(0.05, 0.2));
            }
            if (!cut2 && t >= UP + up.sweep)
            {
                cut2 = true;
                Shavings(ps, Rg, up, 12, new[] { C[4], C[5] });
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            // both strokes flash white for two frames when the X closes
            double flash = t >= CLOSE && t < CLOSE + 0.07 ? 1 : 0;
            down.hot = up.hot = flash;
            Slash(p, down, t);
            Slash(p, up, t);
            // glints at the ends of the strokes
            for (int i = 0; i < 2; i++)
            {
                Swing s = i == 0 ? down : up;
                Travel e = ends[i];
                double g = t - s.t0 - s.sweep * 0.6;
                if (g > 0 && g < 0.2) Sim.Sparkle(p, e.x, e.y, 7 * Ease.OutBack(Math.Min(1, g / 0.05)) * (1 - g / 0.2), C[5], C[3], 0.5);
            }
            // the crossing: a flare, slivers flying off it, light
            double q = t - CLOSE;
            if (q >= 0 && q < 0.26)
            {
                double k = q / 0.26;
                Sim.Flare(p, X, Y, 22 * Px.Pow(1 - k, 1.4), 1 - k, C);
                p.Glow(X, Y, 34, C[4], 0.35 * (1 - k));
            }
            if (q >= 0.03 && q < 0.15)
            {
                double k = (q - 0.03) / 0.12;
                foreach (SliverSpec s in slivers) Sliver(p, X, Y, s.a, s.r + s.v * (q - 0.03), s.arc * (1 - 0.4 * k), k < 0.4 ? C[6] : k < 0.7 ? C[5] : C[4], 1 - k);
            }
            // light rides each edge as it goes
            foreach (Swing s in new[] { down, up })
            {
                double u = t - s.t0;
                if (u <= 0 || u >= 0.25) continue;
                Swing hs = s.With();
                hs.span = s.span * Ease.OutExpo(Clamp(u / s.sweep));
                Travel e = SlashEnd(hs);
                p.Glow(e.x, e.y, 40, C[3], 0.5 * (1 - u / 0.25));
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
