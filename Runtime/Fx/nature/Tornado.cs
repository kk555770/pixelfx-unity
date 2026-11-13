using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Tornado : Effect
    {
        const double DURATION = 2;

        public static readonly Module Module = new Module("tornado", "pixelfx/fx/nature/tornado", 196, 168, 98, 152, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#141221", "#231f33", "#36314a", "#4d4762", "#68617d", "#8a839e", "#b3adc4", "#e2def0" },
                ["dust"] = new[] { "#2a2018", "#4e3e2e", "#76624a", "#a08a6a", "#cbb894" },
                ["leaves"] = new[] { "#4a200c", "#a8541c", "#e8962e", "#ffd27a", "#123818", "#2f7a2a", "#6cb83a", "#c8f070" },
                ["height"] = 112,
                ["seed"] = 4,
            },
            (o, e) => new Tornado(o))
        {
            Title = "Tornado",
            Desc = "A snaking funnel of spinning wind bands under a flared, churning top that flickers with lightning; leaves and debris whirl round it front and back, dust boils at its foot.",
            Tags = new[] { "nature", "wind", "tornado", "storm", "hazard", "ground", "loop" },
            Bg = "ground",
        };

        const double SY = 0.28;

        // the funnel at time t: its axis snakes (a wave running up it), its radius flares at the top
        sealed class Geo
        {
            public double FH, ph;
            public double Ax(double u) => 8.5 * Px.Sin(ph - 2.6 * u) * (0.2 + 0.8 * u) + 3 * Px.Sin(2 * ph + 1.3 - 4.5 * u) * u;
            public double Rad(double u) => 3 + 20 * Px.Pow(u, 1.5) + 17 * Px.Pow(Smooth(0.78, 1, u), 2);
        }

        static Geo GeoAt(double height, double t) => new Geo { FH = height, ph = (TAU * t) / DURATION };

        // the body: a translucent column, thicker through its middle, lit from the left, with streaks of dust that
        // shear past each other as rows spin at different speeds
        static void Body(Painter p, uint[] C, uint[] Dst, Geo g, double t, double seed)
        {
            double FH = g.FH;
            for (double y = -Math.Ceiling(FH) + 2; y <= 0; y++)
            {
                double u = -y / FH, cx = g.Ax(u), r = g.Rad(u), K = 2 + (Rand(y, seed) < 0.35 ? 1 : 0), rot = (TAU * K * t) / DURATION;
                double fade = Math.Min(1, u * 8 + 0.25) * (1 - Smooth(0.9, 1.02, u));
                for (double x = Math.Floor(cx - r); x <= Math.Ceiling(cx + r); x++)
                {
                    double c = (x + 0.5 - cx) / r;
                    if (Math.Abs(c) > 1) continue;
                    double n = TileNoise(((Px.Acos(c) + rot) * 6) / TAU, y * 0.34, 6, 0, seed), v = -c * 0.55 + 0.15 + n * 0.55;
                    uint col = v > 0.6 ? C[6] : v > 0.28 ? C[5] : v > -0.05 ? C[4] : v > -0.4 ? C[3] : C[2];
                    if (u < 0.3) col = Mix(col, Dst[v > 0.3 ? 3 : v > -0.2 ? 2 : 1], 0.7 * (1 - u / 0.3));
                    p.Blend(x, y, col, (0.14 + 0.36 * Math.Sqrt(1 - c * c)) * fade);
                }
            }
        }

        // wind bands: 1 px streaks wrapped round the funnel, bright at the head, fading along the tail; the far half
        // goes behind, dimmer
        static void Bands(Painter p, uint[] C, Geo g, double t, double seed, bool back)
        {
            double FH = g.FH;
            for (int j = 0; j < 58; j++)
            {
                Rng R = NewRng(seed, j);
                double u = (j + R.Range(0.1, 0.9)) / 58;
                int K = R.Int(2, 4);
                double span = R.Range(0.7, 2.1), lvl = R.Next();
                double cx = g.Ax(u), r = g.Rad(u) * R.Range(1.0, 1.1), y0 = -u * FH, head = R.Range(0, TAU) + (TAU * K * t) / DURATION;
                if (u > 0.97) continue;
                for (double s = 0; s <= span; s += 0.5 / Math.Max(2, r))
                {
                    double th = head - s, sn = Px.Sin(th);
                    if ((sn > 0) == back) continue;
                    double k = s / span, x = cx + r * Px.Cos(th), y = y0 + r * SY * sn;
                    bool lit = Px.Cos(th) < 0.2;
                    int i = lvl < 0.18 ? 2 : k < 0.15 && lvl > 0.55 ? 7 : k < 0.45 ? (lit ? 6 : 5) : lit ? 5 : 4;
                    if (back) i = Math.Max(1, i - 2);
                    if (k < 0.5) p.Dot(x, y, C[i]);
                    else p.Blend(x, y, C[i], 2 * (1 - k));
                }
                if (!back && lvl > 0.6 && Px.Sin(head) > 0)
                {
                    double hx = cx + r * Px.Cos(head), hy = y0 + r * SY * Px.Sin(head);
                    p.Dot(hx, hy, WHITE);
                    p.Glow(hx, hy, 5, C[6], 0.3);
                }
            }
        }

        // a soft flattened puff: rx wide, ry tall, blended
        static void Blob(Painter p, double cx, double cy, double rx, double ry, uint c, double a)
        {
            for (double y = Math.Floor(cy - ry); y <= Math.Ceiling(cy + ry); y++)
            for (double x = Math.Floor(cx - rx); x <= Math.Ceiling(cx + rx); x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                if (dx * dx + dy * dy <= 1) p.Blend(x, y, c, a);
            }
        }

        // the flared top: a lid of cloud sheared into flat bands that ride round with the funnel, each thick in the
        // middle and drawn out to a thin tail behind, lit along its upper rim, dark beneath; a dark mass at its core
        static void Crown(Painter p, uint[] C, Geo g, double t, double seed, bool back, double flash)
        {
            double FH = g.FH, cx = g.Ax(1), r = g.Rad(1), y0 = -FH - 3;
            uint L(uint c) => Mix(c, C[7], flash * 0.6);
            if (back)
            {
                Blob(p, cx, y0 + 1, r * 0.85, 6, C[1], 0.6);
                Blob(p, cx - 4, y0 - 3, r * 0.6, 3.5, L(C[3]), 0.45);
            }
            for (int j = 0; j < 16; j++)
            {
                Rng R = NewRng(seed, j);
                double rr = r * R.Range(0.4, 1.15);
                int K = R.Int(1, 2);
                double span = R.Range(1.4, 2.8), head = R.Range(0, TAU) + (TAU * K * t) / DURATION;
                double th0 = R.Range(3.2, 6), lift = R.Range(-4, 3) - (1 - rr / (r * 1.15)) * 5;
                for (double s2 = 0; s2 <= span; s2 += 0.6 / rr)
                {
                    double th = head - s2, sn = Px.Sin(th);
                    if ((sn > 0) == back) continue;
                    double k = s2 / span, th2 = th0 * Px.Sin(Math.PI * Px.Pow(1 - k, 0.7)), x = cx + rr * Px.Cos(th), y = y0 + lift + rr * SY * sn;
                    double T = Math.Max(1, Round(th2));
                    int d = back ? 1 : 0;
                    bool lit = Px.Cos(th) < 0.3;
                    for (int i = 0; i < T; i++)
                    {
                        uint c = i == 0 ? L(C[(lit ? 6 : 5) - d]) : i == T - 1 && T > 2 ? C[1] : L(C[(i == 1 ? 4 : 3) - d]);
                        p.Blend(x, y - T / 2 + i, c, (0.55 + 0.35 * (1 - k)) * (back ? 0.75 : 1));
                    }
                }
            }
        }

        // leaves, twigs and pebbles spiralling up round it and flung off the top
        static void Debris(Painter p, uint[] C, uint[] L, Geo g, double t, double seed, bool back, uint[] S)
        {
            double FH = g.FH;
            Cycle(t, DURATION, 24, seed, q => q.Range(1.3, 1.95), (age, life, R, i) =>
            {
                // the first stretch skids in across the floor, then it spirals up and is flung off the top
                double k = age / life, sk = R.Range(0.15, 0.3), q = Clamp((k - sk) / (1 - sk)), u = Lerp(0.03, 0.98, Px.Pow(q, 0.85));
                double th = R.Range(0, TAU) + R.Range(2, 3.2) * TAU * k, sn = Px.Sin(th);
                if ((sn > 0) == back) return;
                double fling = Smooth(0.78, 1, q), inward = 1 - Smooth(0, sk, k), far = R.Range(26, 44);
                double rr = Lerp(Math.Min(68, g.Rad(u) * (1.12 + fling * 1.1) + R.Range(2, 7)), far, inward), x = g.Ax(u) + rr * Px.Cos(th), y = Math.Min(-1, -u * FH + rr * SY * sn - fling * 8);
                double a = Math.Min(1, k * 8) * (1 - Smooth(0.85, 1, k)), kind = R.Next(), spin = R.Range(-14, 14) * age;
                int dk = back ? 1 : 0;
                if (kind < 0.62)
                {
                    int o = R.Next() < 0.5 ? 0 : 4;
                    uint[] pal = { L[o], L[o + 1], L[o + 2 - dk], L[o + 3 - dk] };
                    NatureLib.DrawLeaf(p, x, y, R.Range(6, 9), R.Range(1.8, 2.6), spin, age * R.Range(5, 10), pal, a);
                }
                else if (kind < 0.8)
                {
                    double ca = Px.Cos(spin) * 2.5, sa = Px.Sin(spin) * 2.5;
                    p.Line(x - ca, y - sa, x + ca, y + sa, back ? L[0] : L[1], a);
                    p.Dot(x + ca, y + sa, L[2], a);
                }
                else NatureLib.DrawRock(p, x, y, R.Range(1.6, 2.6), spin, NatureLib.StoneShape(R, 5), S, a);
            });
        }

        // at its foot: dust boiling round and out, grit hopping, scuffs swept round the floor
        static void Skirt(Painter p, uint[] D, Geo g, double t, double seed, bool back)
        {
            double cx0 = g.Ax(0);
            Cycle(t, DURATION, 40, seed, q => q.Range(0.7, 1.3), (age, life, R, _) =>
            {
                double k = age / life, th = R.Range(0, TAU) + R.Range(5, 9) * age, sn = Px.Sin(th);
                if ((sn > 0) == back) return;
                double rr = 4 + k * R.Range(14, 28), x = cx0 + rr * Px.Cos(th), y = -1.5 - k * R.Range(4, 16) + rr * 0.3 * sn, s = R.Range(3, 6) * (0.6 + 0.8 * k);
                double a = Math.Min(1, k * 6) * (1 - k) * 0.6;
                p.Disc(x, y + s * 0.2, s, D[back ? 1 : 2], a);
                p.Disc(x - s * 0.2, y - s * 0.3, s * 0.55, D[back ? 2 : 3], a * 0.8);
            });
            if (back) return;
            Cycle(t, DURATION, 16, seed + 7, q => q.Range(0.35, 0.6), (age, life, R, _) =>
            {
                double th = R.Range(0, TAU), v = R.Range(25, 55), x = cx0 + Px.Cos(th) * (4 + v * age), y = Px.Sin(th) * 2 - R.Range(40, 80) * age + 260 * age * age;
                if (y < 0.5) p.Dot(x, Math.Min(0, y), R.Next() < 0.5 ? D[3] : D[4], 1 - age / life);
            });
            for (int j = 0; j < 7; j++)
            {
                Rng R = NewRng(seed + 3, j);
                double rr = R.Range(12, 28), th0 = R.Range(0, TAU) + (TAU * 3 * t) / DURATION, span = R.Range(0.5, 1.1);
                for (double s = 0; s < span; s += 0.6 / rr)
                {
                    double th = th0 - s;
                    p.Dot(cx0 + rr * Px.Cos(th), 0.5 + rr * 0.18 * Px.Sin(th), D[s < span * 0.4 ? 3 : 2], 1 - s / span);
                }
            }
        }

        // once a loop the crown lights up from inside
        static double FlashAt(double t)
        {
            double q = t / DURATION;
            return Math.Max(0, 1 - Math.Abs(q - 0.62) / 0.025) + 0.6 * Math.Max(0, 1 - Math.Abs(q - 0.69) / 0.015);
        }

        readonly uint[] C, D, Lv;
        readonly double height, seed;

        Tornado(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            D = Fit(o.Ramp("dust"), 5);
            Lv = Fit(o.Ramp("leaves"), 8);
            height = o.Num("height");
            seed = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            Geo g = GeoAt(height, t);
            double f = FlashAt(t);
            Skirt(p, D, g, t, seed * 5 + 1, true);
            Crown(p, C, g, t, seed * 7 + 2, true, f);
            Bands(p, C, g, t, seed * 3 + 5, true);
            Debris(p, C, Lv, g, t, seed * 11 + 3, true, C);
            Body(p, C, D, g, t, seed);
        }

        public override void Draw(Painter p, double t)
        {
            Geo g = GeoAt(height, t);
            double f = FlashAt(t), cx = g.Ax(1);
            Bands(p, C, g, t, seed * 3 + 5, false);
            Crown(p, C, g, t, seed * 7 + 2, false, f);
            Skirt(p, D, g, t, seed * 5 + 1, false);
            Debris(p, C, Lv, g, t, seed * 11 + 3, false, C);
            // light: a soft pale column, a cold pool at its foot, the crown's lightning
            p.GlowLine(g.Ax(0.1), -g.FH * 0.1, cx, -g.FH * 0.9, 26, C[5], 0.08);
            p.Glow(g.Ax(0), -2, 30, D[3], 0.12);
            if (f > 0.05)
            {
                Rng R = NewRng(seed, 99, Math.Floor(t / DURATION * 7));
                double[][] path = Shapes.Bolt(R, cx - 18, -g.FH - 6, cx + 16, -g.FH + 2, 0.3, 3);
                p.Polyline(path, f > 0.6 ? WHITE : C[7], Math.Min(1, f * 1.5));
                p.Glow(cx, -g.FH - 2, 70, C[7], 0.45 * f);
            }
        }
    }
}
