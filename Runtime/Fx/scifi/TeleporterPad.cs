// The rising band and the column of light it rides in follow Sprite Fusion's open-source heal
// (https://www.spritefusion.com/pixel-art-vfx/heal), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class TeleporterPad : Effect
    {
        public static readonly Module Module = new Module("teleporter-pad", "pixelfx/fx/scifi/teleporter-pad", 96, 104, 48, 92, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#120a26", "#26145a", "#3c2296", "#5a3ad2", "#7f66f0", "#b4a2ff", "#e6deff", "#ffffff" },
                ["metal"] = new[] { "#0e0d14", "#1e1c27", "#312e3e", "#4b4759", "#6f6a82", "#9d98b0" },
                ["seed"] = 26,
            },
            (o, e) => new TeleporterPad(o, e))
        {
            Title = "Teleporter pad",
            Desc = "A steel pad glows on the floor, its rings rippling outward; a soft column of light rises off it and rings of light lift away, sparkles drifting up through them.",
            Tags = new[] { "scifi", "teleporter", "pad", "portal", "beam", "spawn", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, RX = 31, RY = 7, PY = -3, IN = 0.78, HIGH = -84;

        readonly uint[] C, H;
        readonly double seed;
        readonly ScifiLib.Ghosts g;

        TeleporterPad(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            H = Fit(o.Ramp("metal"), 6);
            seed = o.Seed;
            var rn = new Rng(o.Seed);
            g = new ScifiLib.Ghosts(D, null, null, 0, new double[] { -46, -90, 46, 10 });
            // motes and sparkles drifting up through the column
            for (int i = 0; i < 40; i++)
            {
                double a = rn.Range(0, TAU), r = Math.Sqrt(rn.Next()) * RX * IN * 0.9;
                bool star = i % 3 == 0;
                g.Add(rn.Range(0, D), star ? P.Star : P.Mote, Px.Cos(a) * r, PY + Px.Sin(a) * r * (RY / RX), rn.Range(-4, 4), -rn.Range(30, 75), rn.Range(0.7, 1.3), rn.Pick(new[] { WHITE, C[6], C[5] }), star ? rn.Range(1.8, 3.4) : rn.Range(0.6, 1.2));
            }
        }

        // the pad: a steel rim with a lit far edge, a recessed disc of light whose rings ripple outward, a hot middle
        static void Pad(Painter p, double t, uint[] C, uint[] H)
        {
            double ph = t / D;
            p.Shade(-RX - 1, PY - RY - 1, RX + 1, 2, (x, y, o) =>
            {
                double u = x / RX, v = (y - PY) / RY, d = Math.Sqrt(u * u + v * v);
                // the drum's side, below the top face
                if (d > 1)
                {
                    if (Math.Abs(x) <= RX && y > PY && y <= 1 && Math.Sqrt(u * u + Px.Pow((y - 3 - PY) / RY, 2)) <= 1) o.c = y == 1 || Math.Abs(x) > RX - 1 ? H[0] : x < -RX * 0.4 ? H[3] : x > RX * 0.4 ? H[1] : H[2];
                    return 0;
                }
                if (d > 0.97) return o.c = H[0];
                if (d > IN)
                {
                    // the rim: lit along its far edge, shadowed on the near one, a seam round it
                    o.c = Math.Abs(d - (IN + 0.1)) < 0.03 ? H[1] : d < IN + 0.03 ? C[3] : v < -0.3 ? H[5] : v < 0.2 ? H[4] : H[3];
                    return 0;
                }
                // the light well: rings rolling outward, brighter toward the middle
                double r = d / IN, heat = Px.Pow(1 - r, 1.5);
                bool ring = ((((r * 3 - ph * 3) % 1) + 1) % 1) < 0.16;
                o.c = r > 0.94 ? C[3] : ring ? (heat > 0.3 ? WHITE : C[6]) : heat > 0.55 ? C[6] : heat > 0.25 ? C[5] : r > 0.8 ? C[3] : C[4];
                return 0;
            });
            // emitters round the rim, chasing
            for (int i = 0; i < 10; i++)
            {
                double a = (i / 10.0) * TAU + 0.15, x = Px.Cos(a) * RX * 0.885, y = PY + Px.Sin(a) * RY * 0.885, k = 0.5 + 0.5 * Px.Cos(TAU * (2 * ph - i / 10.0));
                p.Px(x, y, k > 0.7 ? WHITE : k > 0.4 ? C[5] : C[2]);
                if (k > 0.5) p.Glow(x, y, 4, C[4], 0.35 * k);
            }
        }

        // a ring of light lifting off the pad: crisp lines stepping down the ramp as it climbs, a glint running round it;
        // its far half or its near half
        static void Lift(Painter p, double t, uint[] C, bool near)
        {
            for (int i = 0; i < 4; i++)
            {
                double k = ScifiLib.Since(t, (i / 4.0) * D, D) / 1.25;
                if (k >= 1) continue;
                double y = Lerp(PY, HIGH + 12, Px.Pow(k, 0.85)), rx = RX * IN * (1 + 0.08 * k), ry = rx * (RY / RX);
                uint c = k < 0.12 ? WHITE : k < 0.4 ? C[6] : k < 0.65 ? C[5] : k < 0.85 ? C[4] : C[3];
                double glint = ((i * 0.37 + k * 1.5) % 1) * TAU;
                for (int j = 0, n = (int)Math.Ceiling(rx * 7); j < n; j++)
                {
                    double g = ((double)j / n) * TAU, sg = Px.Sin(g);
                    if (sg >= 0 != near) continue;
                    double x = Px.Cos(g) * rx, yy = y + sg * ry;
                    bool hot = Math.Abs(((g - glint + Math.PI * 3) % TAU) - Math.PI) < 0.25;
                    if (k > 0.85 && (j & 1) != 0) continue;
                    p.Px(x, yy, hot && k < 0.8 ? WHITE : near ? c : k < 0.4 ? C[5] : C[3]);
                    if (k < 0.3) p.Px(x, yy + 1, near ? C[5] : C[3]);
                }
                if (near) p.Glow(0, y + ry * 0.5, rx + 8, C[4], 0.22 * (1 - k));
            }
        }

        public override void DrawBack(Painter p, double t)
        {
            Pad(p, t, C, H);
            // the column: a soft tube of light standing on the well, fading as it rises, light drawn up it
            double W = RX * IN;
            for (double y = HIGH; y < PY; y++)
            {
                double v = (y - PY) / (HIGH - PY), fade = Px.Pow(1 - v, 1.6);
                for (double x = Math.Floor(-W); x <= Math.Ceiling(W); x++)
                {
                    double q = Math.Abs(x + 0.5) / W;
                    if (q > 1) continue;
                    p.Blend(x + 0.5, y + 0.5, q > 0.9 ? C[6] : C[5], fade * (0.07 + 0.26 * Px.Pow(q, 4) + 0.12 * Px.Pow(1 - v, 4)));
                }
            }
            Cycle(t, D, 30, seed + 3, r => r.Range(0.6, 1.1), (age, life, r, i) =>
            {
                double k = age / life, x = r.Range(-0.9, 0.9) * W, y = Lerp(PY - 2, HIGH + 10, k), L = 3 + 7 * k;
                p.Line(x, y, x, y + L, u => u < 0.3 ? C[6] : C[4], (1 - k) * 1.2);
            });
            Lift(p, t, C, false);
            for (double y = PY - 6; y > HIGH + 20; y -= 12) p.Glow(0, y, W + 8, C[4], 0.16 * (1 - (y - PY) / (HIGH - PY)));
        }

        public override void Draw(Painter p, double t)
        {
            double ph = t / D;
            Lift(p, t, C, true);
            g.At(t).Draw(p);
            // the light: a pool on the floor, a hot heart to the well, pulsing
            double k = 0.85 + 0.15 * Px.Sin(TAU * 2 * ph);
            foreach (double x in new[] { -RX * 0.6, 0, RX * 0.6 }) p.Glow(x, PY, 20, C[4], 0.25 * k);
            p.Glow(0, PY, 10, WHITE, 0.3 * k);
        }
    }
}
