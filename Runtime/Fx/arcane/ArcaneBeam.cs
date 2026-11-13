// The beam (banded across, pulses running down it, sine strands that brighten in front, a source flare with
// spinning rays, an end flare spraying rays and sparks) is adapted from Sprite Fusion's open-source energy beam
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class ArcaneBeam : Effect
    {
        const double D = 1.2, HX = 15, HY = -24, L = 168, W = 6;

        static readonly Pose AIM = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = 4, ["fx"] = 18, ["fy"] = -24, ["bx"] = 13, ["by"] = -20, ["ffx"] = 11, ["fbx"] = -9, ["toeB"] = true };
        static readonly Pose PULL = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = -3, ["fx"] = 1, ["fy"] = -20, ["bx"] = 4, ["by"] = -22, ["ffx"] = 9, ["fbx"] = -7 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 186 },
            Poses = new[]
            {
                new PoseKey(-0.62, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(-0.3, PULL),
                new PoseKey(-0.04, new Pose(PULL) { ["crouch"] = 5, ["lean"] = -4 }),
                new PoseKey(0, new Pose(AIM) { ["crouch"] = 3 }, "snap"),
                new PoseKey(0.3, AIM),
                new PoseKey(0.6, new Pose(AIM) { ["crouch"] = 5, ["lean"] = 3 }),
                new PoseKey(0.9, AIM),
                new PoseKey(1.2, new Pose(AIM) { ["crouch"] = 3 }),
            },
            Hits = new[]
            {
                new HitCue { T = 0.1, Push = 0.5, Straw = 3, Flash = 0.02, Y = -24 },
                new HitCue { T = 0.7, Push = 0.5, Straw = 3, Flash = 0.02, Y = -24 },
            },
            Dust = new[] { new DustCue { T = 0, X = -10, N = 5, Dir = -1 } },
        };

        public static readonly Module Module = new Module("arcane-beam", "pixelfx/fx/arcane/arcane-beam", 264, 100, 26, 84, D, true, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 10 },
            (o, e) => new ArcaneBeam(o), act: Act, actor: 2)
        {
            Title = "Arcane beam",
            Desc = "A beam fires right from the hand: a white core, strands twisting round it, pulses running down it, a flaring source and an end that sprays sparks.",
            Tags = new[] { "arcane", "beam", "laser", "ray", "magic", "directional", "loop" },
            Bg = "ground",
        };

        readonly Options o;

        ArcaneBeam(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, S = o.Seed, EX = HX + L;
            // the body, built across from a white core out to a dithered violet edge, wobbling and pulsing as it goes
            uint[] LV = { WHITE, C[6], C[5], C[4], C[3], C[2] };
            for (double d = 0; d <= L; d += 0.5)
            {
                double x = HX + d, w = W * (1 + 0.12 * Px.Sin(d * 0.35 - ph * TAU * 8)) * Math.Min(1, (d + 3) / 7);
                int pulse = Fract(d * 0.025 - ph * 4) < 0.07 ? 1 : 0;
                for (double q0 = -w * 1.3; q0 <= w * 1.3; q0 += 0.5)
                {
                    double q = Math.Abs(q0) / w, y = HY + q0;
                    int lv = q < 0.2 ? 0 : q < 0.4 ? 1 : q < 0.62 ? 2 : q < 0.85 ? 3 : q < 1 ? 4 : 5;
                    lv = Math.Max(0, lv - pulse);
                    if (lv == 5 && Bayer(x, y) > (1.3 - q) / 0.3) continue;
                    p.Px(x, y, LV[lv]);
                }
                // three strands twisting round it: bright on the near side, gone behind
                for (int s = 0; s < 3; s++)
                {
                    double a = d * 0.17 - ph * TAU * 6 + (s * TAU) / 3, off = Px.Sin(a) * w * 1.05;
                    if (Px.Cos(a) > -0.15) p.Dot(x, HY + off, Px.Cos(a) > 0.55 ? WHITE : C[6]);
                }
                if ((RoundInt(d * 2) & 11) == 0) p.Glow(x, HY, w * 2.2 + 4, C[5], 0.2);
            }

            // the source: a flickering flare with spinning rays at the hand
            double fr = 4.2 + Px.Sin(ph * TAU * 10) * 0.6;
            for (int i = 0; i < 4; i++)
            {
                double a = ph * TAU * 2.5 + (i * TAU) / 4, l = 7 + 3 * Px.Sin(ph * TAU * 6 + i);
                p.Line(HX, HY, HX + Px.Cos(a) * l, HY + Px.Sin(a) * l, u => u < 0.5 ? WHITE : C[6], u => u < 0.6 ? 1 : 1.6 - u);
            }
            p.Circle(HX, HY, fr + 1.5, C[4]);
            p.Circle(HX, HY, fr, C[6]);
            p.Circle(HX, HY, fr * 0.6, WHITE);
            p.Glow(HX, HY, 32, C[5], 0.8);
            // energy drawn into the hand
            Cycle(t, D, 10, S * 3 + 1, 0.18, (age, life, r, _) =>
            {
                double a = r.Range(0, TAU), dd = 16 * (1 - age / life) + 3, x = HX + Px.Cos(a) * dd, y = HY + Px.Sin(a) * dd;
                p.Dot(x, y, age / life > 0.6 ? WHITE : C[6]);
            });

            // the end: a hot flare spraying rays, rings shocking out, sparks thrown back and falling
            double ir = 5 + Px.Sin(ph * TAU * 12) * 0.8;
            for (int i = 0; i < 7; i++)
            {
                double a = Math.PI + (i - 3) * 0.36, l = 8 + 8 * Math.Abs(LoopNoise(ph, 4, S * 7 + i));
                int ii = i;
                p.Line(EX, HY, EX + Px.Cos(a) * l, HY + Px.Sin(a) * l, u => u < 0.4 ? WHITE : ii % 2 != 0 ? C[5] : C[6], u => u < 0.6 ? 1 : 1.6 - u);
            }
            for (int k = 0; k < 3; k++)
            {
                double q = Fract(ph * 3 + k / 3.0);
                p.Ring(EX, HY, 3 + 15 * Ease.OutCubic(q), Math.Max(1, 2 * (1 - q)), q < 0.25 ? WHITE : C[5], 1.1 - q);
            }
            p.Circle(EX, HY, ir + 1.5, C[4]);
            p.Circle(EX, HY, ir, C[6]);
            p.Circle(EX, HY, ir * 0.55, WHITE);
            p.Glow(EX, HY, 40, C[5], 0.9);
            p.Glow(EX, HY, 12, WHITE, 0.6);
            // a fountain of sparks thrown up and back off the end, arcing down
            Cycle(t, D, 220, S * 3 + 2, g => g.Range(0.18, 0.5), (age, life, r, _) =>
            {
                double a = Math.PI * 1.5 + r.Range(-1.7, 0.45), sp = r.Range(70, 260), vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - 30;
                var (x, y) = Fly(EX, HY, vx, vy, age, 480, 2.2);
                var (ux, uy) = FlyV(vx, vy, age, 480, 2.2);
                double k = 1 - age / life;
                if (y > -1) return;
                uint head = k > 0.6 ? WHITE : k > 0.3 ? C[6] : C[5];
                double sp2 = Hypot(ux, uy);
                if (sp2 == 0 || double.IsNaN(sp2)) sp2 = 1;
                double len = Math.Min(8, 1 + sp2 * 0.022);
                p.Line(x - (ux / sp2) * len, y - (uy / sp2) * len, x, y, u => u > 0.5 ? head : C[4]);
                p.Glow(x, y, 3, head, 0.45 * k);
            });
            Cycle(t, D, 6, S * 3 + 3, 0.3, (age, life, r, _) =>
            {
                double k = Px.Sin((age / life) * Math.PI);
                double sx = EX + r.Range(-12, 4), sy = HY + r.Range(-12, 12);
                Sim.Sparkle(p, sx, sy, 1 + 2.5 * k, C[6], C[5], 0.4 * k);
            });
            // violet vapour lifting off the end
            Cycle(t, D, 7, S * 3 + 5, g => g.Range(0.8, 1.2), (age, life, r, _) =>
            {
                double q = age / life, x = EX + r.Range(-8, 4) - q * 6, y = HY - 4 - q * r.Range(14, 24), rad = r.Range(3, 5) * (0.7 + 0.8 * q);
                p.Disc(x, y, rad, C[4], (dd, g) => 0.28 * Px.Sin(q * Math.PI) * (1 - dd * dd));
            });
            // motes shed along the beam
            Cycle(t, D, 16, S * 3 + 4, g => g.Range(0.3, 0.6), (age, life, r, _) =>
            {
                double q = age / life, x = HX + r.Range(10, L - 10) + q * 20, y = HY + r.Sign() * (W + 1 + q * r.Range(4, 10));
                Mote(p, x, y, 40, (y - HY) * 1.5, r.Chance(0.5) ? C[6] : C[5], 0.6, 1 - q, 0.3);
            });
            // the floor beneath catches the light
            p.Glow(EX, -1, 30, C[5], 0.25);
            p.Glow(HX + L / 2, -1, 60, C[4], 0.12);
        }
    }
}
