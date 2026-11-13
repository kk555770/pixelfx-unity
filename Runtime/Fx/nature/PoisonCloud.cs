using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class PoisonCloud : Effect
    {
        const double D = 4;

        public static readonly Module Module = new Module("poison-cloud", "pixelfx/fx/nature/poison-cloud", 256, 100, 128, 86, D, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0e0816", "#24123a", "#3e2056", "#5a3a62", "#6a6a3a", "#96aa30", "#cce040", "#f4ffa0" },
                ["spread"] = 1,
                ["seed"] = 3,
            },
            (o, e) => new PoisonCloud(o))
        {
            Title = "Poison cloud",
            Desc = "A miasma hugging the floor: ragged sheets of gas lit yellow-green from within, fading to violet, creep out in tongues that curl over at their tips; plumes twist up as ribbons.",
            Tags = new[] { "nature", "poison", "gas", "cloud", "miasma", "toxic", "area", "ground", "loop" },
            Bg = "ground",
        };

        sealed class Sheet
        {
            public double h, e, a, flow;
            public int dim, n;
            public bool back;
        }

        // sheets of gas, back to front: how tall, how wide, how thick, how much dimmer, how fast their edges flow
        static readonly Sheet[] SHEETS =
        {
            new Sheet { h = 36, e = 0.84, a = 0.46, dim = 2, flow = 4, n = 2, back = true },
            new Sheet { h = 26, e = 0.96, a = 0.5, dim = 1, flow = 8, n = 2, back = true },
            new Sheet { h = 16, e = 1.04, a = 0.56, dim = 0, flow = 8, n = 3, back = false },
        };

        struct Tongue
        {
            public int s;
            public double q, x, h;
        }

        // tongues creeping out along the floor from the middle: where each one's front is, how tall, how far along
        static List<Tongue> Tongues(Sheet sh, int si, double E, double ph)
        {
            var o = new List<Tongue>();
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < sh.n; i++)
                {
                    double q = (ph + (double)i / sh.n + si * 0.17 + (s > 0 ? 0.5 / sh.n : 0)) % 1;
                    o.Add(new Tongue { s = s, q = q, x = s * E * (0.3 + 0.82 * q), h = sh.h * (0.75 - 0.45 * q) });
                }
            return o;
        }

        // a sheet's height above the floor at x: flat through the middle, falling away at its ends, its top torn by
        // noise that flows outward, raised behind each tongue's front
        static double Height(Sheet sh, int si, double E, double x, double ph, List<Tongue> T, double seed)
        {
            double ax = Math.Abs(x), env = 1 - Smooth(0.5, 1, ax / (E * sh.e)), r = TileNoise(ax * 0.07 - ph * sh.flow, si * 3.7, sh.flow, 0, seed + si);
            double h = sh.h * env * (0.55 + 0.45 * r);
            foreach (Tongue g in T)
            {
                double behind = g.s * (g.x - x);
                if (behind < -1.5 || behind > 30) continue;
                h = Math.Max(h, g.h * Math.Sqrt(Math.Max(0, 1 - behind / 30)) * Smooth(-1.5, 1.5, behind) * (1 - Smooth(0.75, 1, g.q)));
            }
            return h;
        }

        // one sheet: brightest down at the floor where it's lit from within, darkening to violet toward its torn top,
        // streaked in thin layers, a pale rim along its edge
        static void DrawSheet(Painter p, uint[] C, Sheet sh, int si, double E, double t, double seed)
        {
            double ph = t / D;
            List<Tongue> T = Tongues(sh, si, E, ph);
            int d = sh.dim;
            double X = Math.Ceiling(E * 1.25);
            for (double x = -X; x <= X; x++)
            {
                double H = Height(sh, si, E, x + 0.5, ph, T, seed);
                if (H < 0.6) continue;
                double top = -Round(H);
                for (double y = top; y <= 1; y++)
                {
                    double v = Clamp(-y / H), streak = 0.7 + 0.3 * TileNoise(x * 0.05 - ph * sh.flow * Sign(x != 0 ? x : 1), y * 0.55, sh.flow, 0, seed + 9 + si);
                    double lv = v - (streak - 0.7) * 0.6;
                    uint c = y == top ? C[Math.Max(1, 4 - d)] : lv < 0.16 ? C[6 - d] : lv < 0.42 ? C[5 - d] : lv < 0.68 ? C[4 - d] : C[Math.Max(1, 3 - d)];
                    p.Blend(x, y, c, sh.a * (y == top ? 0.65 : (0.6 + 0.4 * (1 - v)) * streak));
                }
            }
            // the tongues' leading edges roll over in curls as they creep forward
            foreach (Tongue g in T)
            {
                double k = Px.Sin(Math.PI * g.q);
                if (k < 0.15 || g.h < 4) continue;
                double r = 1.8 + g.h * 0.22, cx = g.x - g.s * r * 0.6, cy = -g.h * 0.72, spin = -g.s * g.q * TAU * 2.2;
                // a roll of gas: a thick soft band winding in on itself, lighter along its outer edge
                for (double a = 0; a < 4.6; a += 0.1)
                {
                    double rr = r * (1 - a * 0.12), px = cx + g.s * Px.Cos(spin + a) * rr, py = cy + Px.Sin(spin + a) * rr, w = 1.6 * (1 - a / 6);
                    for (double o = -w; o <= w; o += 0.8)
                    {
                        double qx = px + g.s * Px.Cos(spin + a) * o, qy = py + Px.Sin(spin + a) * o;
                        p.Blend(qx, qy, o > w * 0.4 ? C[5 - d] : C[4 - d], sh.a * 0.75 * k * (1 - a / 5.5));
                    }
                }
            }
        }

        // plumes: columns that lift off the sheets and twist into ribbons, thinning as they climb
        static void Plumes(Painter p, uint[] C, double E, double t, double seed)
        {
            Cycle(t, D, 5, seed, g => g.Range(2.4, 3.2), (age, L, R, _) =>
            {
                double u = age / L, x0 = R.Range(-0.55, 0.55) * E, rise = R.Range(13, 19), len = R.Range(34, 48), ph = R.Range(0, TAU), amp = R.Range(3, 5);
                double head = 14 + rise * age, tail = Math.Max(10, head - len), fade = Smooth(0, 0.15, u) * (1 - Smooth(0.5, 1, u));
                for (double h = tail; h <= head; h += 0.7)
                {
                    double s = (h - tail) / Math.Max(1, head - tail), tw = Px.Cos(h * 0.22 + age * 2.4 + ph), w = 0.6 + 2.2 * Math.Abs(tw) * (1 - s * 0.5);
                    double x = x0 + amp * Px.Sin(h * 0.12 + age + ph) * (0.4 + s), a = fade * Px.Sin(Math.PI * Math.Min(1, s * 1.2)) * 0.32;
                    for (double o = -w; o <= w; o += 1) p.Blend(x + o, -h, tw > 0 ? (Math.Abs(o) < w * 0.5 ? C[6] : C[5]) : Math.Abs(o) < w * 0.5 ? C[3] : C[2], a);
                }
            });
        }

        // specks drifting out through the gas with its flow, twinkling
        static void Specks(Painter p, uint[] C, double E, double t, double seed)
        {
            Cycle(t, D, 30, seed, g => g.Range(1.5, 2.6), (age, L, R, _) =>
            {
                double u = age / L;
                int s = R.Sign();
                double x = s * (R.Range(0.05, 0.7) * E + age * R.Range(6, 14)), y = -R.Range(2, 20) - Px.Sin(age * R.Range(1.5, 3) + R.Next() * TAU) * 2;
                double a = Math.Min(1, u * 5) * Math.Min(1, (1 - u) * 3) * (0.6 + 0.4 * Px.Sin(age * R.Range(6, 10) + R.Next() * TAU));
                if (a < 0.1) return;
                p.Dot(x, y, a > 0.7 ? C[7] : C[6], a * 1.3);
                p.Glow(x, y, 4, C[6], 0.3 * a);
            });
        }

        readonly uint[] C;
        readonly double E, seed;

        PoisonCloud(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            E = 96 * o.Num("spread");
            seed = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            double pulse = 0.85 + 0.15 * Px.Sin((TAU * t * 2) / D);
            // lit from within, low down: a soft line of light along the floor under it all
            for (double x = -E; x <= E; x += 22) p.Glow(x, -3, 30, C[5], 0.14 * pulse * (1 - Math.Abs(x) / (E * 1.3)));
            // pockets of light wandering inside it, low down
            for (int i = 0; i < 6; i++)
            {
                Rng R = NewRng(seed, i, 7);
                double x = Px.Sin((TAU * t) / D + R.Range(0, TAU)) * E * R.Range(0.2, 0.7) + R.Range(-0.3, 0.3) * E, k = 0.6 + 0.4 * Px.Sin((TAU * t * 3) / D + i);
                double y = -R.Range(4, 10), r = R.Range(14, 22);
                p.Glow(x, y, r, C[6], 0.2 * k);
            }
            for (int i = 0; i < SHEETS.Length; i++)
                if (SHEETS[i].back)
                    DrawSheet(p, C, SHEETS[i], i, E, t, seed);
        }

        public override void Draw(Painter p, double t)
        {
            Plumes(p, C, E, t, seed * 7 + 2);
            for (int i = 0; i < SHEETS.Length; i++)
                if (!SHEETS[i].back)
                    DrawSheet(p, C, SHEETS[i], i, E, t, seed);
            Specks(p, C, E, t, seed * 11 + 5);
            p.Glow(0, -6, E * 0.9, C[5], 0.1);
        }
    }
}
