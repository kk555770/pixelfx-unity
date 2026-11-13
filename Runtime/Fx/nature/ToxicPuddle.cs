using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class ToxicPuddle : Effect
    {
        const double D = 4;

        public static readonly Module Module = new Module("toxic-puddle", "pixelfx/fx/nature/toxic-puddle", 176, 100, 88, 86, D, true, 30,
            new Options
            {
                ["palette"] = new[] { "#06070a", "#0c100c", "#141a10", "#1e2814", "#2e3c18", "#5a7a1c", "#a6d42a", "#ecff86" },
                ["sheen"] = new[] { "#2a1648", "#5a2e8e", "#1e7a86", "#6adccc" },
                ["width"] = 42,
                ["seed"] = 2,
            },
            (o, e) => new ToxicPuddle(o))
        {
            Title = "Toxic puddle",
            Desc = "Black tar lit from inside by chartreuse veins and a glowing rim, an oily sheen drifting on it; glossy bubbles dome, stretch and pop, vapour curls up in fraying ribbons.",
            Tags = new[] { "nature", "poison", "toxic", "sludge", "tar", "puddle", "hazard", "ground", "loop" },
            Bg = "ground",
        };

        static readonly uint GREY = Hex("#b4b8ae");

        // the surface height above the floor at x: a heavy lens of tar, slow swells rolling through it
        static double Surf(double x, double W, double t)
        {
            double d = Math.Min(1, Math.Abs(x) / W), ph = (TAU * t) / D;
            double lens = Math.Sqrt(1 - d * d) * 6.6 + (d > 0.8 ? 1.6 * Smooth(0.8, 0.9, d) : 0), swell = Px.Pow(Math.Max(0, Px.Sin(x * 0.09 - ph)), 6) * 1.2 + Px.Pow(Math.Max(0, Px.Sin(x * 0.06 + ph * 2 + 1)), 8) * 0.9;
            return -Round(lens + swell * (1 - d));
        }

        // the pool: an oily skin with a violet and teal sheen sliding over it, black depths threaded with veins of light,
        // a chartreuse rim glowing where the tar meets the floor
        static void Pool(Painter p, uint[] C, uint[] Sh, double W, double t, double seed)
        {
            double ph = t / D;
            for (double x = Math.Floor(-W - 1); x <= Math.Ceiling(W + 1); x++)
            {
                double d = Math.Abs(x + 0.5) / W;
                if (d > 1.03) continue;
                double top = Surf(x + 0.5, W, t), oil = 0.6 * TileNoise(x * 0.08, ph * 4, 0, 4, seed) + 0.6 * TileNoise(x * 0.11 - ph * 4, 1, 4, 0, seed + 1), hue = TileNoise(x * 0.05, ph * 2 + 7, 0, 2, seed + 3);
                for (double y = top; y <= 2; y++)
                {
                    if ((y == 2 && d > 0.75) || (y == 1 && d > 0.9)) continue;
                    uint c;
                    if (d > 0.93 && y <= 0) c = y == top ? C[7] : C[6];
                    else if (y == top)
                    {
                        // the skin catches light along its top: a lit edge, the sheen sliding over it, specular glints running along
                        bool glint = Math.Abs(((x * 0.07 - ph * 3) % 1 + 1) % 1 - 0.5) < 0.012 || Rand(x, Math.Floor(t * 10)) < 0.015;
                        c = glint ? WHITE : oil > 0.55 ? (hue > 0 ? Sh[3] : Sh[2]) : oil > 0.15 ? (hue > 0 ? Sh[1] : C[5]) : C[5];
                        if (glint) p.Light(x, y, C[7], 0.5);
                    }
                    else if (y == top + 1) c = oil > 0.4 ? Sh[0] : C[3];
                    else if (y >= 1) c = C[0];
                    else
                    {
                        // cracks of light under the crust, writhing slowly and swelling brighter and dimmer in a slow pulse
                        double pulse = 0.5 + 0.5 * Px.Sin((TAU * t) / D + x * 0.03), v = Math.Abs(TileNoise(x * 0.09, y * 0.75 + ph * 4, 0, 4, seed + 7) + 0.2 * Px.Sin((TAU * t * 2) / D + x * 0.15)), w = 0.6 + 0.6 * pulse;
                        c = v < 0.035 * w ? C[7] : v < 0.08 * w ? C[6] : v < 0.14 * w ? C[4] : C[1];
                        if (v < 0.08 * w) p.Light(x, y, C[6], 0.25 + 0.3 * pulse);
                    }
                    p.Px(x, y, c);
                }
                if (d > 0.9 && d <= 1.03) p.Px(x, top - 1, C[1]);
            }
            // the glow bleeding out onto the floor at both lips
            foreach (int s in new[] { -1, 1 })
                for (int k = 1; k <= 6; k++)
                    p.Dot(s * (W + k), 0, k < 3 ? C[6] : C[5], 1 - k / 7.0);
        }

        // fat bubbles: dome up dark and glossy, stretch thin as light shows through their tops, pop in a ripple ring
        // and a spatter of tar, and loose a ribbon of vapour
        static void Bubbles(Painter p, uint[] C, uint[] Sh, double W, double t, double seed)
        {
            Cycle(t, D, 8, seed, g => g.Range(2.4, 3.2), (age, L, R, _) =>
            {
                double swell = R.Range(1, 1.6), x = R.Range(-0.7, 0.7) * W, rmax = R.Range(4.5, 8), sy = Surf(x, W, t) + 0.5;
                if (age < swell)
                {
                    double k = age / swell, st = Smooth(0.65, 1, k), gr = Ease.OutCubic(Math.Min(1, k * 1.3)), rx = rmax * gr * (1 - 0.18 * st), ry = rmax * gr * (1 + 0.5 * st);
                    double X = Round(x), Y = Round(sy), inner = 0.35 + 0.65 * k;
                    for (double j = -Math.Ceiling(ry) - 1; j <= 0; j++)
                    for (double i = -Math.Ceiling(rx) - 1; i <= Math.Ceiling(rx) + 1; i++)
                    {
                        double dd = Hypot(i / Math.Max(0.6, rx), j / Math.Max(0.6, ry));
                        if (dd > 1.1) continue;
                        // a dark glossy skin with chartreuse light glowing up through it, strongest low in the middle
                        uint c;
                        if (dd > 0.9) c = C[0];
                        else if (dd < 0.55 * inner && j > -ry * 0.75) c = dd < 0.3 * inner ? C[6] : C[5];
                        else if (dd < 0.8 * inner) c = C[4];
                        else c = j < -ry * 0.5 ? C[2] : C[3];
                        p.Px(X + i, Y + j, c);
                    }
                    // the gloss: a curved band of sheen on its upper left, a bright highlight
                    if (rx > 1.8)
                        for (double a = 3.5; a < 4.7; a += 0.16)
                            p.Px(X + Px.Cos(a) * rx * 0.68, Y + Px.Sin(a) * ry * 0.68, a < 3.9 ? Sh[3] : a < 4.3 ? C[7] : Sh[1]);
                    if (rx > 1.2)
                    {
                        p.Px(X - Round(rx * 0.35), Y - Round(ry * 0.62), WHITE);
                        p.Px(X - Round(rx * 0.35) + 1, Y - Round(ry * 0.62), C[7]);
                    }
                    p.Glow(x, sy - ry * 0.5, 7 + rx * 1.4, C[6], (0.2 + 0.35 * st) * gr);
                    return;
                }
                double e = age - swell;
                // the pop: a ring rippling out flat across the tar, a flash of the light inside, spatter
                if (e < 0.3)
                {
                    double k = e / 0.3, rr = 2 + 14 * Ease.OutCubic(k);
                    for (double a = 0; a < TAU; a += 0.5 / rr)
                    {
                        double rx2 = x + Px.Cos(a) * rr, ry2 = sy + Px.Sin(a) * rr * 0.22;
                        if (Math.Abs(rx2) < W * 0.95) p.Dot(rx2, ry2, k < 0.4 ? WHITE : k < 0.7 ? C[7] : C[6], (1 - k) * 1.4);
                    }
                    p.GlowRing(x, sy, rr, 3, C[6], 0.35 * (1 - k));
                    if (e < 0.1) p.Glow(x, sy - 2, 18, C[6], 0.7 * (1 - e / 0.1));
                }
                for (int j = 0; j < 7; j++)
                {
                    double vx = (Rand(j, seed, Round(x * 10)) - 0.5) * 80, vy = -45 - Rand(j, seed + 3, Round(x * 10)) * 70;
                    var (dx, dy) = Fly(x, sy - 2, vx, vy, e, 420);
                    if (dy < sy && e < 0.6)
                    {
                        p.Px(dx, dy, C[1]);
                        p.Px(dx, dy - 1, (j & 1) != 0 ? Sh[3] : C[6]);
                    }
                }
                Ribbon(p, C, x, sy - 2, e, L - swell, R, 0.9);
            });
        }

        // vapour: a thin ribbon that curls up off the tar, then frays into wisps that wander apart and fade
        static void Ribbon(Painter p, uint[] C, double x0, double y0, double age, double life, Rng R, double k)
        {
            double u = age / life, len = R.Range(16, 26), rise = R.Range(9, 14), amp = R.Range(2, 4.5), w = R.Range(0.18, 0.3), ph = R.Range(0, TAU);
            double head = rise * age, tail = Math.Max(0, head - len), fray = Smooth(0.35, 0.85, u), fade = Smooth(0, 0.12, u) * (1 - Smooth(0.5, 1, u)) * k;
            if (fade < 0.02) return;
            int n = fray > 0.05 ? 3 : 1;
            for (double h = tail; h <= head; h += 0.5)
            {
                double s = (h - tail) / Math.Max(1, head - tail), cx = x0 + amp * Px.Sin(h * w + ph + age * 1.6) * (0.3 + s), y = y0 - h, a = fade * Px.Sin(Math.PI * s) * 0.45;
                // lit from below by the pool: green and bright low down, greying as it climbs and frays
                double lit = Math.Max(0, 1 - h / 22) * (1 - 0.6 * fray);
                uint VAP = Mix(GREY, C[6], 0.75 * lit);
                for (int q = 0; q < n; q++)
                {
                    // each wisp wanders off on its own slow curl as the ribbon frays
                    double off = n == 1 ? 0 : fray * s * (q - 1) * 3 + fray * 2.5 * Px.Sin(h * (0.35 + q * 0.17) + ph * (q + 1) + age * 2);
                    p.Blend(cx + off, y, VAP, a * (n == 1 ? 1 : 0.65 - 0.25 * fray) * (0.8 + 0.4 * lit));
                }
            }
        }

        readonly uint[] C, Sh;
        readonly double W, seed;

        ToxicPuddle(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            Sh = Fit(o.Ramp("sheen"), 4);
            W = o.Num("width");
            seed = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            double pulse = 0.85 + 0.15 * Px.Sin((TAU * t * 2) / D);
            // the light comes from inside the pool and its rim
            p.Glow(0, -3, W * 1.25, C[5], 0.22 * pulse);
            p.Glow(0, -10, W * 1.1, C[6], 0.1 * pulse);
            foreach (int s in new[] { -1, 1 }) p.Glow(s * W, -1, 12, C[6], 0.35 * pulse);
            Pool(p, C, Sh, W, t, seed);
        }

        public override void Draw(Painter p, double t)
        {
            Bubbles(p, C, Sh, W, t, seed * 7 + 1);
            // a steady breath of vapour ribbons off the whole pool
            Cycle(t, D, 9, seed * 13 + 4, g => g.Range(2.4, 3.4), (age, L, R, _) =>
            {
                double x = R.Range(-0.85, 0.85) * W;
                Ribbon(p, C, x, Surf(x, W, t) - 1, age, L, R, 0.75);
            });
        }
    }
}
