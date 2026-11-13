// The stream's build (a white core banded out to darker edges, surges running along it, light down its length, a
// flare where it leaves the source) follows Sprite Fusion's open-source energy beam
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class WaterJet : Effect
    {
        public static readonly Module Module = new Module("water-jet", "pixelfx/fx/water/water-jet", 188, 80, 14, 32, 1, true, 30,
            new Options { ["palette"] = WATER, ["length"] = 118, ["seed"] = 3 },
            (o, e) => new WaterJet(o, e))
        {
            Title = "Water jet",
            Desc = "A pressurised stream blasts right: a tight glassy rod with surges running down it that swells, roughens, bends under its weight and breaks into chunks, spray and mist.",
            Tags = new[] { "water", "jet", "stream", "hose", "spray", "directional", "loop" },
            Bg = "dark",
        };

        const double D = 1, G = 520, SPEED = 260;

        // the stream's centre line drops as it goes, and how thick it is there
        static double Drop(double x, double L)
        {
            double q = x / L;
            return q * q * 9;
        }

        static double Thick(double x, double L) => 2.6 + 5.4 * Px.Pow(x / L, 1.1);

        readonly Options o;

        WaterJet(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, L = o.Num("length"), S = o.Seed, T = ph * TAU;
            uint[] lvls = { WHITE, C[6], C[5], C[4] };
            // the stream, column by column
            for (double X = 0; X <= L + 3; X++)
            {
                double x = X + 0.5, u = x / L;
                if (u > 1) break;
                double yc = Drop(x, L), rough = Smooth(0.25, 1, u), sd = ((((x - ph * SPEED * 2) % 130) + 130) % 130) - 65;
                double w0 = Thick(x, L) + Exp(-(sd * sd) / 90) * 1.4 * (1 - u * 0.5);
                double up = w0 + TileNoise(x * 0.18 - ph * 30, 9, 30, 0, S + 1) * 2.2 * rough, dn = w0 + TileNoise(x * 0.18 - ph * 30, 3, 30, 0, S + 1) * 2.2 * rough;
                for (double Y = Math.Floor(yc - up); Y <= Math.Ceiling(yc + dn); Y++)
                {
                    double y = Y + 0.5, q = (y - yc) / Math.Max(0.8, y < yc ? up : dn), aq = Math.Abs(q);
                    if (aq > 1) continue;
                    if (u > 0.55 && TileNoise(x * 0.12 - ph * 25, (y - yc) * 0.25, 25, 0, S + 2) < -0.9 + 1.6 * Smooth(0.55, 1, u)) continue;
                    double streak = TileNoise(x * 0.06 - ph * 40, Math.Floor(y - yc + 20) * 1.7, 40, 0, S);
                    int lvl = aq < 0.2 ? 0 : aq < 0.48 ? 1 : aq < 0.78 ? 2 : 3;
                    if (streak > 0.35) lvl = Math.Max(0, lvl - 1);
                    if (q > 0.4) lvl = Math.Min(3, lvl + 1);
                    p.Px(X, Y, lvls[lvl]);
                    if (lvl == 0) p.Light(x, y, C[5], 0.12);
                }
            }
            // the nozzle flare: a white burst where it leaves, ringed with light
            double fl = 0.85 + 0.15 * Px.Sin(T * 6);
            p.Circle(0, 0, 4.4 * fl, C[5]);
            p.Circle(0, 0, 3.4 * fl, C[6]);
            p.Circle(0, 0, 2.2 * fl, WHITE);
            for (double a = 0; a < TAU; a += 0.35) p.Dot(Px.Cos(a + T * 2) * 5.2 * fl, Px.Sin(a + T * 2) * 5.2 * fl, C[5], 0.6);
            p.Glow(0, 0, 24, C[5], 0.6 * fl);
            p.Glow(0, 0, 8, WHITE, 0.4 * fl);
            // where it pinches apart: beads strung out along the end of the stream, racing on
            for (int k = 0; k < 6; k++)
            {
                double x = L * 0.82 + ((k * 13 + ph * SPEED) % 78), e = (x - L) / 60;
                double y = Drop(Math.Min(x, L), L) + (x > L ? e * e * 10 : 0) + Px.Sin(k * 2.1) * 1.5;
                if (x < L + 34) Bead(p, x, y, SPEED, 40, k % 3 == 0 ? 3 : 2, C);
            }
            // spray: a widening cone of drops shed from the stream and torn off its end, arcing down
            Cycle(t, D, 70, S + 3, g => g.Range(0.2, 0.42), (age, life, r, _) =>
            {
                double f = Math.Sqrt(r.Next()), x0 = L * (0.3 + 0.7 * f), y0 = Drop(x0, L) + r.Range(-1, 1) * Thick(x0, L);
                double vx = r.Range(150, 270) * (0.55 + 0.45 * f), vy = r.Range(-1, 1) * 110 * f + r.Range(-10, 20);
                double x = x0 + vx * age, y = y0 + vy * age + 0.5 * G * age * age, k = age / life;
                if (x > L + 50 || y > 40 || y < -28 || (k > 0.75 && r.Next() < (k - 0.75) * 4)) return;
                if (r.Next() < 0.45) p.Px(x, y, k < 0.5 ? WHITE : C[6]);
                else Bead(p, x, y, vx, vy + G * age, r.Next() < 0.2 ? 2 : 1, C);
            });
            // mist blooming off the end
            Cycle(t, D, 12, S + 4, g => g.Range(0.7, 1), (age, life, r, _) =>
            {
                double k = age / life, x = L * r.Range(0.7, 1) + age * r.Range(20, 45), y = Drop(Math.Min(x, L), L) + r.Range(-5, 5) + age * r.Range(4, 14);
                double rad = r.Range(3, 5) + k * r.Range(5, 9), a = r.Range(0.1, 0.18) * Math.Min(1, k / 0.15) * Px.Pow(1 - k, 1.5);
                p.Disc(x, y, rad, C[6], (d, g) => a * (1 - d * d));
            });
            // light: down the core, brightest near the nozzle
            for (double x = 0; x < L; x += 10) p.Glow(x, Drop(x, L), 14 + x * 0.1, C[4], 0.26 * (1 - (x / L) * 0.5));
            p.Glow(L * 0.9, Drop(L * 0.9, L), 26, C[5], 0.22);
        }
    }
}
