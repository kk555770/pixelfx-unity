// Particles and bands that pass behind and in front of a centre (dim far half drawn back, bright near half
// drawn front) follow the recipe in Sprite Fusion's open-source black hole
// (https://www.spritefusion.com/pixel-art-vfx/black-hole), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class BlizzardVortex : Effect
    {
        const double D = 2;

        public static readonly Module Module = new Module("blizzard-vortex", "pixelfx/fx/ice/blizzard-vortex", 128, 116, 64, 102, D, true, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["height"] = 80,
                ["seed"] = 17,
            },
            (o, e) => new BlizzardVortex(o))
        {
            Title = "Blizzard vortex",
            Desc = "A swirling snow tornado: wind bands spin up a leaning funnel, flakes and ice crystals orbit in front and behind, snow boils at its foot and cold light fills its throat.",
            Tags = new[] { "ice", "snow", "blizzard", "tornado", "vortex", "wind", "loop" },
            Bg = "ground",
        };

        const double TILT = 0.24; // how flat the funnel's rings look

        // the funnel: radius by height (narrow foot, wide mouth) and an axis that sways (one full sway a loop)
        static double Radius(double h, double H) => 4 + 28 * Px.Pow(Clamp(h / H), 1.35);
        static double Axis(double h, double H, double ph) => (5 * Px.Sin(TAU * ph + h * 0.05) + 1.5 * Px.Sin(TAU * ph * 2 - h * 0.11)) * (h / H) - 2 * (h / H);

        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        readonly Options o;

        BlizzardVortex(Options o) => this.o = o;

        // wind streaks: arcs of snow-laden air swirling round the funnel and climbing it, a bright head and a fading
        // tail, faster low down; the far half of each arc goes behind, the near half in front
        static void Streaks(Painter p, uint[] C, double t, Options o, bool front)
        {
            double H = o.Num("height"), ph = t / D;
            Cycle(t, D, 58, o.Seed + 9, g => g.Range(0.5, 0.9), (age, life, r, i) =>
            {
                double h0 = r.Range(2, H), w = r.Range(8, 12) * (1.45 - h0 / H), th = r.Range(0, TAU) + age * w, k = age / life;
                double fade = Math.Min(1, age / 0.08) * (k > 0.7 ? (1 - k) / 0.3 : 1), L = r.Range(0.7, 1.5), m = Math.Ceiling(L / 0.07);
                for (int j = 0; j <= m; j++)
                {
                    double a = th - j * 0.07, s = Px.Sin(a);
                    if (s >= 0 != front) continue;
                    double h = Math.Min(H + 4, h0 + age * 16 - j * 0.25), R = Radius(Math.Min(h, H), H) * (1 + Math.Min(0.3, Math.Max(0, h - H * 0.84) * 0.03)), u = j / m;
                    double x = Axis(Math.Min(h, H), H, ph) + Px.Cos(a) * R, y = -h + s * R * TILT;
                    uint c = front ? (u < 0.15 ? WHITE : u < 0.5 ? C[6] : C[5]) : u < 0.3 ? C[5] : u < 0.6 ? C[4] : C[3];
                    p.Dot(x, y, c, fade * (u < 0.6 ? 1 : 1.5 - u * 1.2) * (front ? 1 : 0.85));
                }
            });
        }

        // the body: a translucent column of snow-filled air, densest at the edges where you look through more of it
        static void Body(Painter p, uint[] C, double ph, double H)
        {
            p.Each(-44, -H - 6, 44, 2, (x, y, sx, sy) =>
            {
                double h = -y;
                if (h < 0 || h > H + 4) return;
                double R = Radius(h, H), dx = x - Axis(h, H, ph), u = dx / R;
                if (u * u >= 1) return;
                double k = h / H, rim = Px.Pow(Math.Abs(u), 3), swirl = Px.Sin(Px.Asin(u) * 3 - TAU * ph * 2 + h * 0.2) * 0.5 + 0.5;
                double a = (0.07 + 0.24 * rim + 0.12 * swirl) * Clamp((1 - Math.Abs(u)) / 0.18) * (k > 0.7 ? Math.Max(0, (1 - k) / 0.3) : 1) * (h < 4 ? h / 4 : 1);
                if (a < 0.03) return;
                p.BlendPx(sx, sy, rim > 0.5 ? C[6] : swirl > 0.6 ? C[5] : C[4], a);
                if (rim > 0.3 || swirl > 0.75) p.Add(sx, sy, C[5], 0.16 * Math.Max(rim, swirl - 0.5) * (1 - k * 0.5));
            });
        }

        // snow and ice carried round: rising on a spiral, faster low down, flung out at the top
        static void Orbiters(Painter p, uint[] C, double t, Options o, bool front)
        {
            double H = o.Num("height"), ph = t / D;
            Cycle(t, D, 76, o.Seed, g => g.Range(0.9, 1.6), (age, life, r, i) =>
            {
                double h0 = r.Range(0, H * 0.6), h = h0 + age * r.Range(14, 30), k = age / life;
                if (h > H + 8) return;
                double w = r.Range(7, 11) * (1.5 - h / H), th = r.Range(0, TAU) + age * w, outer = 1 + r.Range(0.05, 0.3) + Math.Min(0.35, Math.Max(0, h - H * 0.82) * 0.04);
                double R = Radius(Math.Min(h, H), H) * outer, s = Px.Sin(th);
                if (s >= 0 != front) return;
                double x = Axis(Math.Min(h, H), H, ph) + Px.Cos(th) * R, y = -h + s * R * TILT;
                bool big = i % 5 == 0;
                double fade = Math.Min(1, age / 0.1) * (k > 0.8 ? (1 - k) / 0.2 : 1);
                if (fade <= 0) return;
                // a streak back along its path, a bright head
                double th2 = th - 0.25, x2 = Axis(Math.Min(h, H), H, ph) + Px.Cos(th2) * R, y2 = -h + 2 + Px.Sin(th2) * R * TILT;
                if (front)
                {
                    p.Line(x2, y2, x, y, u => u > 0.6 ? C[6] : C[4], u => u * fade);
                    p.Dot(x, y, big ? WHITE : r.Chance(0.5) ? WHITE : C[6], fade);
                    if (big)
                    {
                        foreach (int[] v in Cross) p.Dot(x + v[0], y + v[1], C[6], fade);
                        p.Glow(x, y, 5, C[5], 0.35 * fade);
                    }
                }
                else
                {
                    p.Line(x2, y2, x, y, C[3], u => u * fade * 0.8);
                    p.Dot(x, y, big ? C[5] : C[4], fade);
                }
            });
        }

        // snow boiling at the foot: soft puffs spinning round the base, lit on top
        static void Foot(Painter p, uint[] C, double t, Options o, bool front)
        {
            double ph = t / D;
            Cycle(t, D, 12, o.Seed + 5, g => g.Range(0.8, 1.2), (age, life, r, i) =>
            {
                double th = r.Range(0, TAU) + age * r.Range(4, 6), R = 9 + age * r.Range(8, 16), s = Px.Sin(th), k = age / life;
                if (s >= 0 != front) return;
                double x = Axis(0, 1, ph) + Px.Cos(th) * R, y = -2 - age * r.Range(4, 10) + s * R * 0.2, rad = 2 + 4 * k, a = 0.5 * Px.Pow(1 - k, 1.2) * Math.Min(1, age / 0.1);
                p.Disc(x, y, rad, front ? C[6] : C[5], (d, g) => a * (1 - d * d));
                p.Disc(x - rad * 0.25, y - rad * 0.3, rad * 0.5, WHITE, (d, g) => a * 0.5 * (1 - d * d) * (1 - k));
            });
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            // a snow drift on the floor, swept round
            for (int x = -30; x <= 30; x++)
            {
                double d = Math.Abs(x) / 30.0, hgt = Round((1 - d * d) * (1.5 + Rand(x, 3) * 1.5));
                for (int j = 0; j < hgt; j++) p.Px(x, -j, j == hgt - 1 ? WHITE : C[5]);
                p.Dot(x, 0, C[6], 1 - d * 0.5);
                p.Dot(x, 1, C[4], 0.8 - d * 0.6);
            }
            p.Glow(0, -2, 40, C[4], 0.22);
            Foot(p, C, t, o, false);
            Streaks(p, C, t, o, false);
            Orbiters(p, C, t, o, false);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double H = o.Num("height"), ph = t / D;
            Body(p, C, ph, H);
            // cold light in its throat
            p.Glow(Axis(H * 0.45, H, ph), -H * 0.45, 40, C[4], 0.3);
            p.Glow(Axis(H * 0.15, H, ph), -H * 0.15, 18, C[5], 0.4 + 0.1 * Px.Sin(TAU * ph * 4));
            p.Glow(Axis(H * 0.05, H, ph), -3, 10, WHITE, 0.3);
            Streaks(p, C, t, o, true);
            Orbiters(p, C, t, o, true);
            Foot(p, C, t, o, true);
            // snow thrown off the mouth, spreading into a thin cloud
            Cycle(t, D, 10, o.Seed + 3, g => g.Range(0.9, 1.3), (age, life, r, i) =>
            {
                double th = r.Range(0, TAU), R = Radius(H, H) * (0.6 + age * 0.9), k = age / life, rad = 3 + 6 * k;
                double Rc = Math.Min(R, 36), x = Axis(H, H, ph) + Px.Cos(th) * Rc, y = -H - 2 - age * 4 + Px.Sin(th) * Rc * TILT, a = 0.24 * (1 - k) * Math.Min(1, age / 0.15);
                p.Disc(x, y, rad * 0.8, C[6], (d, g) => a * (1 - d * d));
                p.Glow(x, y, rad * 1.5, C[4], 0.08 * (1 - k));
            });
        }
    }
}
