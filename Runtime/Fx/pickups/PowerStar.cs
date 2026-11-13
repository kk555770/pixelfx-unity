// The rays and the white-hearted glints follow Sprite Fusion's open-source sparkle
// (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class PowerStar : Effect
    {
        const double DURATION = 1.6, HOVER = 25, RO = 11.5, RI = 5.2;
        const int RAYS = 10;
        static readonly uint INK = Hex("#1a0e04");
        static readonly double[] BEATS = { 0.25, 0.75 };
        static readonly double LA = Atan2(-0.8, -0.6);

        public static readonly Module Module = new Module("power-star", "pixelfx/fx/pickups/power-star", 88, 84, 44, 76, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1604", "#5e3208", "#a05c0c", "#e09414", "#ffc626", "#ffe866", "#fff8c4", "#ffffff" },
                ["seed"] = 9,
            },
            (o, e) => new PowerStar(o, e))
        {
            Title = "Power star",
            Desc = "A fat bevelled star rocks side to side and swells on each beat, rays of light turning slowly behind it, sparks spinning off its points.",
            Tags = new[] { "pickup", "star", "power-up", "invincible", "bonus", "loop" },
            Bg = "ground",
        };

        readonly ScratchLayer L = new ScratchLayer(44, 44);
        readonly Options o;

        PowerStar(Options o, Emitter e) => this.o = o;

        // a beat: it swells fast and settles slowly
        static double Swell(double ph)
        {
            double s = 0;
            foreach (double b in BEATS)
            {
                double u = ((ph - b) % 1 + 1) % 1;
                s += Exp(-u * 14) * (1 - u * 2.5 > 0 ? 1 : 0);
            }
            return s;
        }

        // the star: five arms, each with a ridge down its middle, the two halves catching the light differently
        void Star(Painter p, uint[] C, double cx, double cy, double rot, double sc)
        {
            L.Begin(Math.Floor(cx) - 21, Math.Floor(cy) - 21);
            L.Fill(Math.Floor(cx) - 21, Math.Floor(cy) - 21, Math.Floor(cx) + 20, Math.Floor(cy) + 20, (px, py) =>
            {
                double dx = (px - cx) / sc, dy = (py - cy) / sc, r = Hypot(dx, dy);
                if (r > RO + 0.5) return 0;
                double a = Atan2(dy, dx) - rot + Math.PI / 2;
                a = (a % TAU + TAU) % TAU;
                double k = Math.Floor(a / (Math.PI / 5)), half = k % 2, tip = (Math.Floor(k / 2) + half) * (2 * Math.PI / 5);
                // inside the edge from this arm's tip to the inner corner beside it
                double tA = tip - Math.PI / 2 + rot, iA = tA + (half != 0 ? -1 : 1) * (Math.PI / 5);
                double tx = Px.Cos(tA) * RO, ty = Px.Sin(tA) * RO, ix = Px.Cos(iA) * RI, iy = Px.Sin(iA) * RI;
                double ex = ix - tx, ey = iy - ty, side = (dx - tx) * ey - (dy - ty) * ex, cside = -tx * ey + ty * ex;
                if (side * cside < 0) return 0;
                // the facet slopes down from the ridge towards the inner corner: light it by that direction
                double n = tA + (half != 0 ? -1 : 1) * (Math.PI / 2), l = Px.Cos(n - LA);
                bool ridge = Math.Abs(Px.Sin(Atan2(dy, dx) - tA)) * r < 0.6;
                if (r < 1.2) return WHITE;
                double toLight = Px.Cos(tA - LA);
                if (ridge) return toLight > 0.3 ? WHITE : toLight > -0.4 ? C[6] : C[5];
                return C[l > 0.6 ? 6 : l > 0.15 ? 5 : l > -0.35 ? 4 : l > -0.75 ? 3 : 2];
            });
            L.Outline(INK).Draw(p);
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION, y = -HOVER - 1.5 * Px.Sin(ph * TAU * 2), sw = Swell(ph);
            BlobShadow(p, 0, 9 + 2 * sw, 0.7);
            // rays turning behind it: long and short by turns, one ray's width a loop, swelling with the beat
            double spin = ph * (TAU / RAYS * 2);
            for (int i = 0; i < RAYS; i++)
            {
                double a = spin + (double)i * TAU / RAYS, len = (i % 2 != 0 ? 22 : 34) * (1 + 0.18 * sw), ca = Px.Cos(a), sa = Px.Sin(a), w = i % 2 != 0 ? 0.06 : 0.09;
                p.Shade(-len - 2, y - len - 2, len + 2, y + len + 2, (x, yy, o) =>
                {
                    double rx = x, ry = yy - y, d = rx * ca + ry * sa;
                    if (d < 8 || d > len) return 0;
                    double q = Math.Abs(rx * sa - ry * ca), hw = 0.5 + d * w;
                    if (q > hw + 1.5) return 0;
                    o.l = q < hw * 0.5 ? C[6] : C[5];
                    o.k = (0.32 + 0.22 * sw) * (q < hw ? 1 : 1 - (q - hw) / 1.5) * Px.Pow(1 - d / len, 1.2) * Math.Min(1, (d - 8) / 4);
                    return 0;
                });
                // a fine bright line down each long ray
                if (i % 2 == 0)
                {
                    // (clipped at the floor)
                    double d0 = RO + 3, d1 = Math.Min(len * 0.88, sa > 0.05 ? (-2 - y) / sa : double.PositiveInfinity);
                    if (d1 > d0) p.Line(ca * d0, y + sa * d0, ca * d1, y + sa * d1, u => u < 0.35 ? C[6] : C[5], u => (u < 0.5 ? 1 : 1.6 - u * 1.4) * (0.75 + 0.35 * sw));
                }
            }
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION, y = -HOVER - 1.5 * Px.Sin(ph * TAU * 2), sw = Swell(ph), seed = o.Seed;
            double rot = 0.22 * Px.Sin(ph * TAU), sc = 1 + 0.13 * sw;
            p.GlowRing(0, y, RO * sc + 3, 13, C[4], 0.32 + 0.25 * sw);
            p.Glow(0, y, 6, C[6], 0.12 + 0.2 * sw);
            Star(p, C, 0, y, rot, sc);
            // on each beat a spark spins off every point
            foreach (double b in BEATS)
            {
                double age = ((ph - b) % 1 + 1) % 1 * DURATION;
                if (age > 0.45) continue;
                for (int i = 0; i < 5; i++)
                {
                    double a = rot + (double)i * TAU / 5 - Math.PI / 2, sp = 70 + Rand(i, b * 10, 3) * 30, tan = 35;
                    double vx = Px.Cos(a) * sp - Px.Sin(a) * tan, vy = Px.Sin(a) * sp + Px.Cos(a) * tan;
                    var (x, yy) = Fly(Px.Cos(a) * RO * sc, y + Px.Sin(a) * RO * sc, vx, vy, age, 60, 4);
                    double k = 1 - age / 0.45;
                    Glint(p, x, yy, Round(2 * k), k > 0.5 ? WHITE : C[6], 0.45 * k);
                }
            }
            // specks of light winking round it, and small stars popping one after another
            double f = Math.Floor(ph * 24);
            for (int i = 0; i < 18; i++)
            {
                if (Rand(f + i * 3, i, seed) > 0.32) continue;
                double a = Rand(i, 1, seed) * TAU, d = 13 + Rand(i, 2, seed) * 20, sy = y + Px.Sin(a) * d * 0.85;
                if (sy < -2) p.Px(Px.Cos(a) * d, sy, Rand(f, i, 5) < 0.5 ? WHITE : C[6]);
            }
            for (int i = 0; i < 4; i++)
            {
                double life = 0.3, age = ((t - (i + 0.3) / 4 * DURATION) % DURATION + DURATION) % DURATION;
                if (age > life) continue;
                double a = Rand(i, 7, seed) * TAU, d = 15 + Rand(i, 8, seed) * 12, k = Px.Sin(age / life * Math.PI);
                Glint(p, Px.Cos(a) * d, y + Px.Sin(a) * d * 0.85, Round(2.5 * k), i % 2 != 0 ? C[6] : WHITE, 0.4 * k);
            }
            // a glint on the top arm now and then
            double g = (ph + 0.1) % 0.5 / 0.5;
            if (g < 0.14) Glint(p, -2 + rot * 4, y - 6, 3 * Px.Sin(g / 0.14 * Math.PI), WHITE, 0.5);
        }
    }
}
