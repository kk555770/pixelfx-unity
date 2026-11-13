using System;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class Halo : Effect
    {
        static readonly Pose PRAY = new Pose { ["turn"] = 1.1, ["fx"] = -1, ["fy"] = -26, ["bx"] = 2, ["by"] = -26, ["armsFront"] = true, ["hy"] = 1, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, PRAY),
                new PoseKey(0.5, new Pose(PRAY) { ["crouch"] = 1 }),
                new PoseKey(1, new Pose(PRAY) { ["crouch"] = 1, ["hy"] = 2 }),
                new PoseKey(1.5, new Pose(PRAY) { ["crouch"] = 1 }),
                new PoseKey(2, PRAY),
            },
        };

        const double T = 2;

        public static readonly Module Module = new Module("halo", "pixelfx/fx/holy/halo", 56, 80, 28, 70, T, true, 30,
            new Options { ["palette"] = GOLD, ["seed"] = 5 },
            (o, e) => new Halo(o, e), act: Act, actor: 2)
        {
            Title = "Halo",
            Desc = "A ring of gold light floats over the head with a glint running round it; fine dust sifts down from it and the head catches its light.",
            Tags = new[] { "holy", "halo", "saint", "buff", "glow", "character", "loop" },
            Bg = "ground",
        };

        const double HY = -46, RX = 9.5, RY = 2.6;
        // how far apart two angles are (0..π)
        static double Apart(double a, double b) => Math.Abs(((a - b + Math.PI) % TAU + TAU) % TAU - Math.PI);

        readonly Options o;

        Halo(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = (t / T) * TAU;
            Actor A = o.Actor;
            double y0 = HY + (Px.Sin(ph) > 0.55 ? -1 : 0), gl = Math.PI / 2 + ph;
            // the hair under it catches the light
            if (A != null)
            {
                // a warm rim on the top edge of each column of the head, and light falling off below it
                Mask m = A.Mask;
                for (int i = 0; i < m.W; i++)
                    for (int j = 0; j < Math.Min(m.H, 9); j++)
                    {
                        uint c = A.Pixels[j * m.W + i];
                        // skip empty space and the dark outline, light the first pixel of hair under it
                        if (c == 0 || Luma(c) < 0.15) continue;
                        int x = A.X + i, y = A.Y + j;
                        p.Px(x, y, Mix(c, C[6], 0.45));
                        int k2 = (j + 1) * m.W + i;
                        uint c2 = k2 < A.Pixels.Length ? A.Pixels[k2] : 0;
                        if (c2 != 0 && Luma(c2) >= 0.15) p.Px(x, y + 1, Mix(c2, C[5], 0.18));
                        for (int d = 0; d <= 4; d++) p.Light(x, y + d, C[4], 0.3 * (1 - d / 5.0));
                        break;
                    }
            }
            // the ring: a thick bright near side, a thin far side, a slow shimmer running round, the glint
            for (int j = 0, n = 92; j < n; j++)
            {
                double g = ((double)j / n) * TAU, x = Px.Cos(g) * RX, y = y0 + Px.Sin(g) * RY;
                bool near = Px.Sin(g) >= 0;
                bool shimmer = Px.Sin(g * 3 - ph * 2) > 0.7, hot = Apart(g, gl) < 0.3;
                if (near)
                {
                    p.Px(x, y, hot ? WHITE : shimmer ? WHITE : C[6]);
                    p.Px(x, y + 1, hot ? C[6] : C[4]);
                }
                else p.Dot(x, y, hot ? WHITE : shimmer ? C[6] : C[5], 0.9);
            }
            p.Glow(0, y0, 20, C[4], 0.4);
            p.Glow(0, y0 + 1, 9, WHITE, 0.16);
            double gx = Px.Cos(gl) * RX, gy = y0 + Px.Sin(gl) * RY;
            if (Px.Sin(gl) > -0.2) Sim.Sparkle(p, gx, gy + 0.5, 1.5 + 2.5 * Math.Max(0, Px.Sin(gl)), WHITE, C[6], 0.45);
            else p.Glow(gx, gy, 5, C[6], 0.3);
            // light falls from it onto the head
            p.GlowLine(0, y0 + 2, 0, y0 + 9, 9, C[4], 0.16);
            // little stars wink on and off about the ring
            Cycle(t, T, 6, o.Seed + 3, g => g.Range(0.3, 0.5), (age, life, R, i) =>
            {
                double g = R.Range(0, TAU), d = R.Range(1.1, 1.5), k = Px.Sin((age / life) * Math.PI);
                Sim.Sparkle(p, Px.Cos(g) * RX * d, y0 + Px.Sin(g) * RY * d - R.Range(1, 4), 1 + 2 * k, WHITE, C[6], 0.4 * k);
            });
            // dust sifting down off the ring
            Cycle(t, T, 16, o.Seed, g => g.Range(0.8, 1.5), (age, life, R, i) =>
            {
                double g = R.Range(0, TAU), u = age / life, x = Px.Cos(g) * RX * R.Range(0.8, 1) + Px.Sin(u * 5 + g) * 1.2, y = y0 + Px.Sin(g) * RY + 1 + u * R.Range(7, 14);
                double tw = 0.6 + 0.4 * Px.Sin(age * 18 + g * 7), k = Px.Sin(u * Math.PI) * tw;
                p.Dot(x, y, tw > 0.85 ? WHITE : C[6], Math.Min(1, k * 1.5));
                p.Light(x, y, C[5], 0.5 * k);
            });
        }
    }
}
