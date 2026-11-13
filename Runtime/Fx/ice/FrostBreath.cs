// Soft puffs drawn as a body, a denser core and a lit cap, and streaks with a hot head and a cool tail, follow
// the recipes in Sprite Fusion's open-source pixel art VFX (https://www.spritefusion.com/pixel-art-vfx),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class FrostBreath : Effect
    {
        const double D = 1.2;

        public static readonly Module Module = new Module("frost-breath", "pixelfx/fx/ice/frost-breath", 184, 104, 12, 50, D, true, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["spread"] = 0.21,
                ["power"] = 1,
                ["seed"] = 11,
            },
            (o, e) => new FrostBreath(o))
        {
            Title = "Frost breath",
            Desc = "A cone of icy wind blowing right: frost puffs billow out, swell and sink, wind streaks race through, ice crystals tumble and glint off a white-hot mouth.",
            Tags = new[] { "ice", "breath", "frost", "cone", "wind", "directional", "loop" },
            Bg = "dark",
        };

        const double SINK = 9; // cold air is heavy: it sags a little as it slows

        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        readonly Options o;

        FrostBreath(Options o) => this.o = o;

        static Func<double, double, double> Soft(double q) => (d, g) => q * (1 - d * d);

        // a puff of frost: a soft body, a denser core sitting low, a lit cap while it's young, a cold haze of light
        static void Puff(Painter p, uint[] C, double x, double y, double r, double a, double k, bool front)
        {
            uint body = front ? C[5] : C[4], cap = front ? C[7] : C[6];
            p.Disc(x, y, r, body, Soft(a * 0.6));
            p.Disc(x + r * 0.06, y + r * 0.12, r * 0.68, Mix(body, C[3], 0.3), Soft(a * 0.5));
            if (k < 0.55) p.Disc(x - r * 0.25, y - r * 0.3, r * 0.5, cap, Soft(a * 0.7 * (1 - k / 0.55)));
            p.Glow(x, y, r * 1.7, C[4], 0.1 * a);
        }

        // the body of the gust: a cone of turbulent, translucent cold air streaming right, brightest at its heart.
        // The noise scrolls exactly one tile per loop, so it never seams.
        static void Gust(Painter p, uint[] C, double t, Options o)
        {
            double ph = t / D, sp = Px.Tan(o.Num("spread")) * 1.08, S = o.Seed;
            p.Each(2, -46, 170, 46, (x, y, sx, sy) =>
            {
                double u = x / 170, hw = 2.5 + x * sp, yc = 7 * u * u, v = (y - yc) / hw;
                if (v * v >= 1 || u >= 1) return;
                double n = TileFbm(x * 0.055 - ph * 10, (y - yc) * 0.15 + 7, 10, 0, S, 2), edge = (1 - v * v) * (u < 0.08 ? u / 0.08 : 1) * Px.Pow(1 - u, 0.8);
                // denser and brighter down its heart near the mouth
                double d = Clamp(0.5 + n * 1.3 + (1 - Math.Abs(v)) * 0.35 * Px.Pow(1 - u, 1.5)) * edge;
                if (d < 0.08) return;
                p.BlendPx(sx, sy, d > 0.72 ? C[7] : d > 0.52 ? C[6] : d > 0.32 ? C[5] : C[4], Math.Min(0.78, d * 0.95) * Clamp((d - 0.08) / 0.3));
                if (d > 0.3) p.Add(sx, sy, d > 0.6 ? C[6] : C[5], 0.2 * d);
            });
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double S = o.Seed, sp = o.Num("spread"), power = o.Num("power");
            Gust(p, C, t, o);
            // the big slow billows behind
            Cycle(t, D, 12, S, g => g.Range(1, 1.25), (age, life, r, i) =>
            {
                double th = r.Range(-sp, sp), v = r.Range(170, 230) * power;
                var (x, y) = Fly(3, r.Range(-1, 1), v * Px.Cos(th), v * Px.Sin(th), age, SINK, 1.4);
                double k = age / life, rad = 2 + 12 * Px.Pow(k, 0.75);
                Puff(p, C, x, Math.Min(y, 40 - rad), rad, 0.75 * Px.Pow(1 - k, 1.2) * Math.Min(1, age / 0.08), k, false);
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double S = o.Seed, sp = o.Num("spread"), power = o.Num("power"), ph = t / D;
            // light down the cone
            p.GlowLine(4, 0, 120, 6, 24, C[4], 0.26);
            p.GlowLine(2, 0, 50, 1, 9, C[6], 0.3);
            p.Glow(60, 4, 44, C[3], 0.16);
            // brighter puffs in front, smaller and quicker
            Cycle(t, D, 10, S + 1, g => g.Range(0.7, 0.95), (age, life, r, i) =>
            {
                double th = r.Range(-sp * 0.8, sp * 0.8), v = r.Range(190, 260) * power;
                var (x, y) = Fly(3, r.Range(-1, 1), v * Px.Cos(th), v * Px.Sin(th), age, SINK, 1.6);
                double k = age / life, rad = 1.5 + 8 * Px.Pow(k, 0.7);
                Puff(p, C, x, Math.Min(y, 40 - rad), rad, 0.85 * Px.Pow(1 - k, 1.3) * Math.Min(1, age / 0.06), k, true);
            });
            // snow carried along, swaying
            Cycle(t, D, 18, S + 2, g => g.Range(0.8, 1.1), (age, life, r, i) =>
            {
                double th = r.Range(-sp * 1.1, sp * 1.1), v = r.Range(120, 190);
                var (x, y) = Fly(4, 0, v * Px.Cos(th), v * Px.Sin(th), age, SINK * 1.4, 1.5);
                double k = age / life, sw = Px.Sin(age * 9 + r.Range(0, TAU)) * 1.5;
                if (k < 0.85 || (age * 30) % 2 < 1) p.Px(x, y + sw, r.Chance(0.5) ? WHITE : C[6]);
            });
            // wind streaks: a white head, a pale body, a cool tail
            Cycle(t, D, 26, S + 3, g => g.Range(0.22, 0.36), (age, life, r, i) =>
            {
                double th = r.Range(-sp * 0.9, sp * 0.9), v = r.Range(280, 380);
                var (x, y) = Fly(6, r.Range(-2, 2), v * Px.Cos(th), v * Px.Sin(th), age, 0, 0.9);
                var (vx, vy) = FlyV(v * Px.Cos(th), v * Px.Sin(th), age, 0, 0.9);
                double s = Hypot(vx, vy), L = 4 + s * 0.03, ux = vx / s, uy = vy / s, k = age / life;
                p.Line(x - ux * L, y - uy * L, x, y, u => u > 0.8 ? WHITE : u > 0.4 ? C[6] : C[4], u => (u > 0.35 ? 1 : u / 0.35) * (k < 0.75 ? 1 : (1 - k) / 0.25));
                p.Glow(x, y, 3, C[5], 0.3 * (1 - k));
            });
            // ice crystals tumbling through: a short shard with a white edge, glinting as it turns
            Cycle(t, D, 9, S + 4, g => g.Range(0.65, 0.9), (age, life, r, i) =>
            {
                double th = r.Range(-sp, sp), v = r.Range(150, 230);
                var (x, y) = Fly(5, 0, v * Px.Cos(th), v * Px.Sin(th), age, SINK, 1.1);
                double k = age / life, a = r.Range(0, TAU) + age * r.Range(9, 15) * r.Sign(), L = r.Range(1.5, 2.6), ca = Px.Cos(a), sa = Px.Sin(a);
                if (k > 0.85 && (age * 30) % 2 < 1) return;
                p.Line(x - ca * L, y - sa * L, x + ca * L, y + sa * L, C[5]);
                p.Px(x + ca * L, y + sa * L, WHITE);
                double gl = Px.Cos(a * 2);
                if (gl > 0.92)
                {
                    foreach (int[] q in Cross) p.Px(x + q[0], y + q[1], C[6]);
                    p.Px(x, y, WHITE);
                    p.Glow(x, y, 6, C[5], 0.5);
                }
                else p.Glow(x, y, 3, C[5], 0.2);
            });
            // the mouth: a white-hot core, a flickering jet of cold
            for (int i = 0; i < 3; i++)
            {
                double L = 9 + 6 * LoopNoise(ph, 1.2, S + i * 7), dy = (i - 1) * 1.4;
                p.Line(2, dy * 0.5, 2 + L, dy, u => u < 0.5 ? WHITE : C[6], u => u < 0.6 ? 1 : 1 - (u - 0.6) / 0.4);
            }
            p.Ellipse(2.5, 0, 4.6, 3, C[6]);
            p.Ellipse(2, 0, 3, 1.8, WHITE);
            double fl = 0.85 + 0.15 * Px.Sin(ph * TAU * 3);
            p.Glow(2, 0, 24, C[5], 0.7 * fl);
            p.Glow(2, 0, 9, WHITE, 0.5 * fl);
        }
    }
}
