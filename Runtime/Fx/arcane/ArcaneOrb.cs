// The orb (a glassy bubble that thickens towards its rim, a hot core, a double rim, a highlight arc and spot,
// motes orbiting in front and behind) is adapted from Sprite Fusion's open-source energy orb
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class ArcaneOrb : Effect
    {
        const double D = 2.4, RAD = 17, Y0 = -52;

        public static readonly Module Module = new Module("arcane-orb", "pixelfx/fx/arcane/arcane-orb", 136, 112, 68, 104, D, true, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 6 },
            (o, e) => new ArcaneOrb(o))
        {
            Title = "Arcane orb",
            Desc = "A floating orb of smoky violet glass with a swirl turning round a white-hot heart, two tilted rings of light orbiting behind and in front.",
            Tags = new[] { "arcane", "orb", "sphere", "magic", "floating", "loop" },
            Bg = "ground",
        };

        // two rings, each a circle tilted in depth (sy) and rolled in the picture (rot), beads running round it
        sealed class RingDef
        {
            public double r, sy, rot, turn, c;
            public int beads;
        }

        static readonly RingDef[] RINGS =
        {
            new RingDef { r = 31, sy = 0.24, rot = 0.36, beads = 3, turn = 2, c = 0 },
            new RingDef { r = 27, sy = 0.3, rot = -0.55, beads = 2, turn = -3, c = 1 },
        };

        struct State
        {
            public double ph, cx, cy;
        }

        static State Setup(double t)
        {
            double ph = t / D;
            return new State { ph = ph, cx = 0, cy = Y0 + Px.Sin(ph * TAU) * 2 };
        }

        // one ring's points on the near (z > 0) or far side; beads brighter, with light
        static void Ring(Painter p, uint[] C, State s, RingDef R, bool near)
        {
            double ph = s.ph, cx = s.cx, cy = s.cy, rot = R.rot + 0.12 * Px.Sin(ph * TAU + R.c), cr = Px.Cos(rot), sr = Px.Sin(rot);
            int n = (int)Math.Ceiling(R.r * TAU * 1.5);
            (double, double) Pos(double g)
            {
                double lx = Px.Cos(g) * R.r, ly = Px.Sin(g) * R.r * R.sy;
                return (cx + lx * cr - ly * sr, cy + lx * sr + ly * cr);
            }
            for (int j = 0; j < n; j++)
            {
                double g = ((double)j / n) * TAU, z = Px.Sin(g);
                if (z > 0 != near) continue;
                var (x, y) = Pos(g);
                p.Dot(x, y, near ? C[5] : C[4], near ? 0.7 : 0.4);
            }
            for (int b = 0; b < R.beads; b++)
            {
                double g = ph * TAU * R.turn + (b * TAU) / R.beads;
                for (int k = 0; k < 7; k++)
                {
                    // a bead and its trail along the ring
                    double gg = g - Sign(R.turn) * k * 0.07, z = Px.Sin(gg);
                    if (z > 0 != near) continue;
                    var (x, y) = Pos(gg);
                    p.Dot(x, y, k == 0 ? WHITE : k < 3 ? C[6] : C[5], k < 4 ? 1 : 1 - (k - 3) / 4.0);
                    if (k == 0) p.Glow(x, y, near ? 7 : 5, C[6], near ? 0.5 : 0.25);
                }
            }
        }

        // the orb itself: smoky glass, the swirl, the heart, the rim, the highlights
        static void Orb(Painter p, uint[] C, State s)
        {
            double ph = s.ph, cx = s.cx, cy = s.cy;
            // the glass: translucent, thicker towards the rim
            p.Disc(cx, cy, RAD, (d, g) => d > 0.82 ? C[2] : C[1], (d, g) => 0.32 + 0.5 * d * d);
            // the swirl: two bright arms with a lilac edge, and two faint ones between, turning twice a loop
            foreach (double[] arm in new[] { new double[] { 0, 1 }, new[] { Math.PI, 1 }, new[] { Math.PI / 2, 0 }, new[] { (3 * Math.PI) / 2, 0 } })
            {
                double s0 = arm[0];
                bool main = arm[1] != 0;
                for (double u = 0.08; u <= 0.85; u += 0.012)
                {
                    double a = ph * TAU * 2 + s0 + u * 3.4, r = u * RAD * 0.92, x = cx + Px.Cos(a) * r, y = cy + Px.Sin(a) * r, fade = u < 0.6 ? 1 : 1 - (u - 0.6) / 0.25;
                    if (!main)
                    {
                        p.Dot(x, y, C[4], fade * 0.8);
                        continue;
                    }
                    // the arm's lit edge trails just behind it
                    double b = a - 0.16, xb = cx + Px.Cos(b) * r, yb = cy + Px.Sin(b) * r;
                    p.Dot(xb, yb, u < 0.4 ? C[6] : C[5], fade);
                    p.Dot(x, y, u < 0.3 ? WHITE : u < 0.55 ? C[6] : C[5], fade);
                }
            }
            // the heart
            double pulse = 0.85 + 0.15 * Px.Sin(ph * TAU * 3);
            p.Circle(cx, cy, 3.6 * pulse + 1, C[5]);
            p.Circle(cx, cy, 2.8 * pulse, C[6]);
            p.Circle(cx, cy, 1.7 * pulse, WHITE);
            // the rim: a bright line just inside a dark one, a highlight arc and spot top left, a glint bottom right
            p.Ring(cx, cy, RAD + 0.4, 1, C[5]);
            p.Ring(cx, cy, RAD + 1.3, 1, C[2], 0.8);
            for (double a = 3.5; a < 4.7; a += 0.03) p.Dot(cx + Px.Cos(a) * (RAD - 2.4), cy + Px.Sin(a) * (RAD - 2.4), a > 3.85 && a < 4.35 ? WHITE : C[6]);
            p.Circle(cx - RAD * 0.42, cy - RAD * 0.5, 1.3, WHITE);
            p.Dot(cx + RAD * 0.52, cy + RAD * 0.55, C[6]);
            p.Glow(cx, cy, 20, C[6], 0.26 * pulse);
            p.GlowRing(cx, cy, RAD, 6, C[5], 0.25);
        }

        // motes orbiting on tilted paths, and sparks drifting off the orb
        static void Motes(Painter p, uint[] C, State s, double S, bool near)
        {
            double ph = s.ph, cx = s.cx, cy = s.cy;
            for (int i = 0; i < 8; i++)
            {
                double a = ((i * 2.4 + S) % TAU) + ph * TAU * (i % 2 != 0 ? 1 : -1) * (1 + (i % 3)), rr = RAD + 5 + ((i * 0.37) % 1) * 12, tilt = (((i * 0.71) % 1) - 0.5) * 1.4;
                double x0 = Px.Cos(a) * rr, z = Px.Sin(a);
                if (z > 0 != near) continue;
                double x = cx + x0 * Px.Cos(tilt), y = cy + x0 * Px.Sin(tilt) + z * rr * 0.28, dir = i % 2 != 0 ? 1 : -1;
                p.Dot(x, y, near ? WHITE : C[5]);
                p.Dot(x - dir, y, near ? C[6] : C[4], near ? 1 : 0.6);
                if (near)
                {
                    p.Dot(x - 2 * dir, y, C[4], 0.6);
                    p.Glow(x, y, 5, C[6], 0.35);
                }
            }
            if (!near) return;
            Cycle(ph * D, D, 10, S * 3 + 1, g => g.Range(0.6, 1), (age, life, r, _) =>
            {
                double a = r.Range(0, TAU), q = age / life, d = RAD + 1 + q * r.Range(8, 18);
                double x = cx + Px.Cos(a) * d, y = cy + Px.Sin(a) * d - q * 10;
                Mote(p, x, y, Px.Cos(a) * 25, Px.Sin(a) * 25 - 12, r.Chance(0.5) ? C[6] : C[5], 0.6, 1 - q, 0.3);
            });
        }

        readonly Options o;

        ArcaneOrb(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            State s = Setup(t);
            double lift = (s.cy - Y0) / 2;
            // the light it throws on the floor, stronger as it bobs down
            p.Glow(0, -1, 36, C[4], 0.26 - 0.04 * lift);
            for (int x = -20; x <= 20; x++)
            {
                double u = Math.Abs(x + 0.5) / 20;
                p.Dot(x, 0, u < 0.3 ? C[6] : C[5], (1.1 - u) * (0.9 - 0.15 * lift));
            }
            p.Glow(s.cx, s.cy, 60, C[4], 0.36);
            foreach (RingDef R in RINGS) Ring(p, C, s, R, false);
            Motes(p, C, s, o.Seed, false);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            State s = Setup(t);
            Orb(p, C, s);
            foreach (RingDef R in RINGS) Ring(p, C, s, R, true);
            Motes(p, C, s, o.Seed, true);
            Sim.Sparkle(p, s.cx - RAD * 0.42, s.cy - RAD * 0.5, 1.5 + Math.Max(0, Px.Sin(s.ph * TAU * 2)) * 2, C[6], C[5], 0.3);
        }
    }
}
