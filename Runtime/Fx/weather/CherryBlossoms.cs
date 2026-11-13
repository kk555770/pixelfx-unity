// Things that flutter down (a fall timed to land as their time in the air runs out, a flip by squashing across,
// a rest on the ground and a fade) after Sprite Fusion's open-source falling leaves
// (https://www.spritefusion.com/pixel-art-vfx/falling-leaves), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class CherryBlossoms : Effect
    {
        public static readonly Module Module = new Module("cherry-blossoms", "pixelfx/fx/weather/cherry-blossoms", 320, 180, 160, 170, 10, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1832", "#8e3460", "#d26a94", "#f2a2c0", "#ffd2e2", "#fff4f8" },
                ["seed"] = 12,
            },
            (o, e) => new CherryBlossoms(o))
        {
            Title = "Cherry blossoms",
            Desc = "Pink petals flutter down on a spring breeze, tumbling in little spirals and flashing as they catch the light, with a few whole blossoms spinning among them.",
            Tags = new[] { "weather", "cherry blossom", "sakura", "petals", "spring", "wind", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 10, FLOOR = 1;
        static double Gust(double t) => 22 * Px.Sin((TAU * t) / D + 0.4) + 7 * Px.Sin((TAU * 2 * t) / D - 1);

        readonly Options o;

        CherryBlossoms(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 6);
            // a soft plum dusk, so the sky's stars don't read as petals
            uint top = Hex("#0e0a14"), low = Mix(Hex("#1a0e1a"), C[0], 0.4);
            Rows(p, Module, -170, 9, (y, R) =>
            {
                double k = (y + 170) / 180;
                Row.Fill(R.c, Mix(top, low, k * k));
                Row.Fill(R.a, 0.3 + 0.12 * k);
                Row.Fill(R.b, 1);
            });
            Scatter(q, S, C);
            Petals(q, t, S, C, 0);
        }

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 6);
            Petals(q, t, S, C, 1);
            Blossoms(q, t, S, C);
        }

        // petals: each tumbles in a little spiral as it falls and drifts on the breeze, flipping fast; turned full face
        // to the light it flashes
        static void Petals(Clip q, double t, double S, uint[] C, int near)
        {
            int n = near != 0 ? 54 : 90;
            double sd = S * 7 + near;
            Trips(t, D, n, sd, 1, 2, (u, k, m, i) =>
            {
                double j = Wrap(k, m);
                double R(double c) => Rand(sd, i, j, c);
                double dur = D / m, tf = dur * 0.84, a = Math.Min(u * dur, tf), rest = u * dur - tf;
                double born = t - u * dur, x0 = -200 + R(1) * 330, y0 = -184, vy = (FLOOR + (near != 0 ? 2 : 0) - y0) / tf, vx = 9 + R(2) * 10;
                double rho = (near != 0 ? 3 : 2) + R(3) * 3, w = 2.2 + R(4) * 2.8, ph = R(5) * TAU, gk = near != 0 ? 1 : 0.6;
                (double, double) At(double aa) => (x0 + vx * aa + (Gust(born + aa) - Gust(born)) * gk + rho * Px.Cos(w * aa + ph), y0 + vy * aa + rho * 0.6 * Px.Sin(w * aa + ph));
                double len = (near != 0 ? 3.6 : 2.6) + R(6) * 1.1;
                if (rest > 0)
                {
                    var (lx, ly) = At(tf);
                    // a landed petal settles and shrinks away (no dither, so the ground stays clean)
                    double fade = 1 - rest / (dur - tf);
                    if (fade > 0.15) Petal(q.Shade, lx, ly, len * (0.35 + 0.65 * fade), (R(7) - 0.5) * 1.2, 0.5, C, 1);
                    return;
                }
                var (x, y) = At(a);
                double flip = Px.Cos(R(8) * TAU + a * (3 + R(9) * 5)), ang = R(10) * TAU + a * (R(11) - 0.5) * 3;
                Petal(q.Shade, x, y, len, ang, flip, C, 1);
                if (flip > 0.9)
                {
                    double g = (flip - 0.9) / 0.1;
                    q.Px(x, y - 1, WHITE);
                    q.Glow(x, y, near != 0 ? 7 : 5, C[4], (near != 0 ? 0.32 : 0.22) * g);
                }
            });
        }

        // a petal: a little notched teardrop, squashed across as it flips; its pale face or its deeper back
        static void Petal(Shader shade, double x, double y, double len, double ang, double flip, uint[] C, double a)
        {
            double fs = 0.3 + 0.7 * Math.Abs(flip), c = Px.Cos(ang), s = Px.Sin(ang), e = len + 1;
            bool back = flip < -0.2;
            // deeper at the base, palest at the tip, lit on one side; the back is a step deeper all over
            uint[] face = back ? new[] { C[1], C[2], C[3] } : new[] { C[2], C[3], C[4] };
            uint tip = back ? C[3] : C[5];
            shade(x - e, y - e, x + e, y + e, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, u = (dx * c + dy * s) / len, v = (-dx * s + dy * c) / (len * fs);
                if (u < -1 || u > 1) return 0;
                double hw = 0.55 * Math.Sqrt(1 - u * u) * (1 + 0.35 * u);
                if (Math.Abs(v) > hw || (u > 0.7 && Math.Abs(v) < 0.12)) return 0;
                o.c = u > 0.45 && v < 0.1 ? tip : u < -0.45 ? face[0] : v < 0 ? face[2] : face[1];
                o.a = a;
                return 0;
            });
        }

        // a whole blossom: five petals round a gold heart, spinning slowly as it sinks
        static void Blossoms(Clip q, double t, double S, uint[] C)
        {
            Trips(t, D, 7, S * 31, 1, 1, (u, k, m, i) =>
            {
                double R(double c) => Rand(S * 31, i, c);
                double y = -190 + (FLOOR + 2 + 190) * Math.Min(1, u / 0.9), born = t - u * D;
                if (u > 0.9) return;
                double x = -190 + R(1) * 330 + 14 * u * D * 0.1 + (Gust(t) - Gust(born)) + 6 * Px.Sin(u * TAU * 2 + R(2) * 6), rot = R(3) * TAU + u * D * (R(4) - 0.5) * 2;
                double r = 3.6 + R(5) * 1.2;
                for (int pi = 0; pi < 5; pi++)
                {
                    double an = rot + (pi * TAU) / 5;
                    Petal(q.Shade, x + Px.Cos(an) * r * 0.75, y + Px.Sin(an) * r * 0.75, r * 0.7, an, 1, C, 1);
                }
                q.Px(x, y, Hex("#ffe08a"));
                q.Glow(x, y, 8, C[4], 0.2);
            });
        }

        // petals already fallen, scattered along the floor; they never move, so they're shaded once and replayed
        static readonly Dictionary<string, List<double>> SCATTER = new Dictionary<string, List<double>>();

        static void Scatter(Clip q, double S, uint[] C)
        {
            string key = S.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + string.Join(",", C);
            if (!SCATTER.TryGetValue(key, out List<double> px))
            {
                px = new List<double>();
                Shader rec = Recorder(px, false);
                for (int i = 0; i < 46; i++)
                    Petal(rec, -164 + Rand(S, i, 71) * 328, FLOOR + Math.Floor(Rand(S, i, 72) * 3) * 2, 2.6 + Rand(S, i, 73), (Rand(S, i, 74) - 0.5) * 1.2, 0.5, C, 1);
                SCATTER[key] = px;
            }
            for (int i = 0; i < px.Count; i += 4) q.Dot(px[i], px[i + 1], (uint)px[i + 2], px[i + 3]);
        }
    }
}
