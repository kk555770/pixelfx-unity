// The charge recipe (motes spiralling in, pulse rings collapsing onto a layered ball with flickering rays, short
// crackling sparks) is adapted from Sprite Fusion's open-source energy charge
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class SpellCharge : Effect
    {
        const double D = 1.6, HX = 15, HY = -22, RT = 0.5;
        const int RINGS = 4;

        static readonly Pose HOLD = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = 2, ["fx"] = 18, ["fy"] = -22, ["bx"] = 11, ["by"] = -17, ["ffx"] = 10, ["fbx"] = -8 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(-0.46, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(-0.12, new Pose(HOLD) { ["crouch"] = 3 }),
                new PoseKey(0, HOLD),
                new PoseKey(0.4, new Pose(HOLD) { ["crouch"] = 5 }),
                new PoseKey(0.8, new Pose(HOLD) { ["lean"] = 3 }),
                new PoseKey(1.2, new Pose(HOLD) { ["crouch"] = 5 }),
                new PoseKey(1.6, HOLD),
            },
        };

        public static readonly Module Module = new Module("spell-charge", "pixelfx/fx/arcane/spell-charge", 120, 86, 44, 80, D, true, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 5 },
            (o, e) => new SpellCharge(o), act: Act, actor: 2)
        {
            Title = "Spell charge",
            Desc = "Energy gathers in the caster's hand: streaks and motes spiral in, rings collapse into a swelling orb that crackles with arcs, ready to fire.",
            Tags = new[] { "arcane", "charge", "spell", "orb", "cast", "magic", "loop" },
            Bg = "ground",
        };

        // each ring is born at k * D / RINGS and lands RT later; the times it lands, wrapped into the loop
        static readonly double[] landings = Landings();

        static double[] Landings()
        {
            var l = new double[RINGS];
            for (int k = 0; k < RINGS; k++) l[k] = (k * D) / RINGS + RT;
            return l;
        }

        static double SinceLast(double t)
        {
            double m = double.PositiveInfinity;
            foreach (double l in landings) m = Math.Min(m, (((t - l) % D) + D) % D);
            return m;
        }

        static readonly double[][] SHAPES = { new double[] { 1, 0 }, new[] { 0.38, 0.55 }, new double[] { 1, 0 }, new[] { 0.42, -0.7 } };

        static void Orb(Painter p, uint[] C, double x, double y, double r, double t, double ph)
        {
            // rays flickering round the ball, turning three steps a loop
            for (int i = 0; i < 8; i++)
            {
                double a = (i / 8.0) * TAU + ph * (TAU / 8) * 3, l = (1.5 + 4.5 * Math.Abs(LoopNoise(ph, 3, i * 7 + 1))) * (i % 2 != 0 ? 0.7 : 1);
                double x0 = x + Px.Cos(a) * (r + 1.2), y0 = y + Px.Sin(a) * (r + 1.2);
                int ii = i;
                p.Line(x0, y0, x0 + Px.Cos(a) * l, y0 + Px.Sin(a) * l, u => u < 0.4 ? C[6] : ii % 2 != 0 ? C[4] : C[5], u => u < 0.6 ? 1 : 1.6 - u);
            }
            // the ball: a violet rim, a bright body lit from the top left, a big white heart
            p.Circle(x, y, r + 1.3, C[4]);
            p.Circle(x, y, r + 0.3, C[5]);
            p.Circle(x - r * 0.12, y - r * 0.12, r * 0.8, C[6]);
            p.Circle(x, y, r * 0.5, WHITE);
            // filaments crackling out from the heart through the body to the shell, new ones every other frame
            Rng R = NewRng(Math.Floor(t * 15), 77);
            for (int i = 0; i < 3; i++)
            {
                double a = R.Range(0, TAU);
                double[][] pts = Arc(R, x + Px.Cos(a) * r * 0.4, y + Px.Sin(a) * r * 0.4, x + Px.Cos(a + R.Range(-0.6, 0.6)) * (r + 0.6), y + Px.Sin(a + R.Range(-0.6, 0.6)) * (r + 0.6), jag: 0.45, forks: 0)[0].pts;
                p.Polyline(pts, u => u < 0.6 ? WHITE : C[6]);
            }
            // a glint on the shell
            p.Px(x - r * 0.5, y - r * 0.62, WHITE);
        }

        static void RingAt(Painter p, uint[] C, double x, double y, double rr, double sy, double rot, double q)
        {
            int n = (int)Math.Ceiling(rr * TAU * 1.4);
            double c = Px.Cos(rot), s = Px.Sin(rot);
            uint col = q > 0.85 ? WHITE : q > 0.55 ? C[6] : C[5];
            double a = Math.Min(1, q * 3);
            for (int j = 0; j < n; j++)
            {
                double g = ((double)j / n) * TAU, lx = Px.Cos(g) * rr, ly = Px.Sin(g) * rr * sy, px = x + lx * c - ly * s, py = y + lx * s + ly * c;
                if (py > -0.5) continue; // the floor hides the bottom of a big ring
                p.Dot(px, py, col, a);
                if (j % 10 == 0) p.Glow(px, py, 5, C[5], 0.12 * a);
            }
        }

        readonly Options o;

        SpellCharge(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, S = o.Seed;
            int f = (int)Math.Floor(t * 30);
            double since = SinceLast(t), s1 = since / 0.07, s2 = since / 0.18, swell = Exp(-(s1 * s1)) + 0.5 * Exp(-(s2 * s2));
            double r = 6.1 + 0.5 * Px.Sin(ph * TAU * 4) + 1.4 * swell;
            // unstable: it shivers a pixel as each ring lands
            double jit = swell > 0.4 ? (f % 2 != 0 ? 1 : -1) : 0, x = HX + jit, y = HY;

            // streaks spiralling in from all round, faster as they near the hand
            Cycle(t, D, 110, S * 3 + 1, g => g.Range(0.2, 0.34), (age, life, R, _) =>
            {
                // they come from the left, above and the upper right; the swirl would carry the rest into the floor
                double a0 = R.Range(2.8, 5.75), d0 = R.Range(20, 52), spin = R.Range(0.6, 1.3);
                (double, double) Pos(double qq)
                {
                    double a = a0 + spin * qq * qq, d = d0 * (1 - qq * qq) + r;
                    return (HX + Px.Cos(a) * d, HY + Px.Sin(a) * d * 0.92);
                }
                double q = age / life;
                var (px, py) = Pos(q);
                var (qx, qy) = Pos(Math.Min(1, q + 0.02));
                // their light dies as they reach the orb, so it isn't washed out by them
                Streak(p, px, py, (qx - px) / (0.02 * life), (qy - py) / (0.02 * life), R.Chance(0.55) ? C[6] : C[5], Math.Min(1, q * 4) * (1 - Smooth(0.9, 1, q)), 7, 0.4 * (1 - Smooth(0.6, 0.9, q)));
            });
            // slower motes, drifting in on the same swirl
            Cycle(t, D, 56, S * 3 + 2, g => g.Range(0.4, 0.7), (age, life, R, _) =>
            {
                double a0 = R.Range(2.6, 5.2), d0 = R.Range(12, 32), spin = R.Range(1, 2.2), size = R.Range(0.6, 1);
                (double, double) Pos(double qq)
                {
                    double a = a0 + spin * qq, d = d0 * (1 - Ease.InQuad(qq)) + r;
                    return (HX + Px.Cos(a) * d, HY + Px.Sin(a) * d);
                }
                double q = age / life;
                var (px, py) = Pos(q);
                var (qx, qy) = Pos(Math.Min(1, q + 0.02));
                Mote(p, px, py, (qx - px) / (0.02 * life), (qy - py) / (0.02 * life), R.Pick(new[] { C[5], C[6], C[6], WHITE }), size, Math.Min(1, q * 3) * (q > 0.9 ? (1 - q) / 0.1 : 1), 0.3 * (1 - Smooth(0.55, 0.85, q)));
            });

            // rings collapsing onto the orb, each tilted its own way
            for (int k = 0; k < RINGS; k++)
                foreach (double off in new[] { -D, 0 })
                {
                    double q = (t - (k * D) / RINGS - off) / RT;
                    if (q < 0 || q >= 1) continue;
                    double rr = r + 1.5 + 22 * (1 - Ease.InQuad(q));
                    double[] shape = SHAPES[k];
                    RingAt(p, C, x, y, rr, shape[0], shape[1], q);
                }

            Orb(p, C, x, y, r, t, ph);

            // arcs crackling off it, new ones every other frame
            int w = (int)Math.Floor(f / 2.0);
            Rng Rw = NewRng(S, w, 3);
            int n = (Rw.Next() < 0.6 ? 1 : 0) + (swell > 0.3 ? 1 : 0);
            double fresh = f % 2 == 0 ? 0.8 : 0.4;
            for (int i = 0; i < n; i++)
            {
                double a = Rw.Range(0, TAU), L = Rw.Range(5, 10) * (1 + swell * 0.4), a1 = a + Rw.Range(-0.5, 0.5);
                double x0 = x + Px.Cos(a) * (r + 0.5), y0 = y + Px.Sin(a) * (r + 0.5), x1 = x0 + Px.Cos(a1) * L, y1 = Math.Min(-2, y0 + Px.Sin(a1) * L);
                DrawArc(p, Arc(Rw, x0, y0, x1, y1, jag: 0.35, forks: Rw.Next() < 0.5 ? 1 : 0), new ArcCols { core = WHITE, body = C[5], halo = C[3], glow = C[5] }, fresh, 0.3);
            }
            // glints winking round it
            Cycle(t, D, 7, S * 3 + 4, 0.22, (age, life, R, _) =>
            {
                double a = R.Range(0, TAU), d = R.Range(r + 5, r + 15), k = Px.Sin((age / life) * Math.PI);
                Sim.Sparkle(p, x + Px.Cos(a) * d, y + Px.Sin(a) * d * 0.9, 1 + 2.5 * k, C[6], C[4], 0.4 * k);
            });
            if (since < 0.07) Sim.Sparkle(p, x, y, 4 + 6 * (1 - since / 0.07), C[6], C[5], 0.5);

            // light: a wide pool that lights the caster, a hot heart, the floor beneath the hand
            double pulse = 0.85 + 0.15 * Px.Sin(ph * TAU * 4) + 0.2 * swell;
            p.Glow(x, y, 46, C[4], 0.26 * pulse);
            p.GlowRing(x, y, r + 3, 8, C[5], 0.3 * pulse);
            p.Glow(x, y, r, C[6], 0.12);
            p.Glow(HX, -1, 22, C[5], 0.16 * pulse);
        }
    }
}
