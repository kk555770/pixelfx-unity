// The dithered tone ramp and the brighter, bluer approaching side are adapted from Sprite Fusion's open-source
// black-hole (https://www.spritefusion.com/pixel-art-vfx/black-hole), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class BlackHole : Effect
    {
        public static readonly Module Module = new Module("black-hole", "pixelfx/fx/shadow/black-hole", 184, 132, 92, 124, 4, true, 30,
            new Options { ["palette"] = VOID, ["rock"] = new[] { "#120d1c", "#2e2440", "#544468", "#857399" }, ["seed"] = 4 },
            (o, e) => new BlackHole(o))
        {
            Title = "Black hole",
            Desc = "A tilted disk of banded rings turns round a black hole, its far side bent up over the top and under the bottom; debris spirals in, stretches and is swallowed.",
            Tags = new[] { "shadow", "black-hole", "void", "space", "gravity", "dark", "magic", "loop" },
            Bg = "ground",
        };

        // the hole's centre, its shadow, how hard it bends light; the disk's tilt (screen angle, and how open it is)
        // light bent over the top left the far side's upper face, so the arch sees the disk more face-on (SI_ARCH)
        const double Y = -70, RS = 13.5, E = 17.5, SI = 0.25, SI_ARCH = 0.46, TILT = -0.14, D = 4;
        static readonly double CT = Px.Cos(TILT), ST = Px.Sin(TILT);

        // rings with gaps between them, inner ones hotter and turning faster (whole turns of pattern a loop)
        static readonly double[][] BANDS =
        {
            new[] { 18, 24, 5, 1.35, 0.3 },
            new[] { 26, 34, 3, 0.92, 1.9 },
            new[] { 35.5, 46, 2, 0.64, 3.7 },
            new[] { 47.5, 60, 1, 0.42, 5.2 },
        };
        const double ROUT = 60;
        static readonly double[] BAYER = Array.ConvertAll(new[] { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }, v => (v + 0.5) / 16);
        static readonly uint ICE = Hex("#e4f7ff");

        // brightness of the disk at a point of its plane (X across, Z away from us), banded, streaked, turning
        static bool Disk(double X, double Z, double t, out double I, out double dop)
        {
            double rho = Math.Sqrt(X * X + Z * Z);
            foreach (double[] b in BANDS)
            {
                double r0 = b[0], r1 = b[1], n = b[2], hot = b[3], k = b[4];
                if (rho < r0 || rho > r1) continue;
                double w = (rho - r0) / (r1 - r0), ph = Atan2(Z, X) - (TAU * n * t) / D;
                double streak = 0.62 + 0.22 * Px.Cos(7 * ph + rho * 0.8 + k) + 0.16 * Px.Cos(13 * ph - rho * 1.6 + k * 2);
                double lane = 0.72 + 0.28 * Px.Cos(rho * 2.3 + k);
                double edge = Smooth(0, 0.18, w) * (1 - Smooth(0.78, 1, w));
                I = hot * streak * lane * (0.35 + 0.65 * edge);
                dop = -X / rho;
                return true;
            }
            I = dop = 0;
            return false;
        }

        // the hole and its disk, ray by ray: the near half straight in front, the far half seen through the bend
        // (above the hole, and as a thin second image below it), the shadow, and the photon ring at its edge
        static void Hole(Painter p, uint[] C, double t, double pulse)
        {
            uint[] RAMP = { C[1], C[2], C[3], C[4], C[5], C[6], C[7], WHITE };
            int n = RAMP.Length;
            double W = ROUT + 2, H = 42;
            p.Shade(-W, Y - H, W, Y + H, (x, y, o) =>
            {
                double dx = x, dy = y - Y, u = dx * CT + dy * ST, v = -dx * ST + dy * CT, r = Math.Sqrt(u * u + v * v);
                bool s = false;
                double sI = 0, sdop = 0;
                if (v > 0) s = Disk(u, -v / SI, t, out sI, out sdop);
                if (!s && r > RS)
                {
                    double k = 1 - (E * E) / (r * r), bu = u * k, bv = v * k, si = k > 0 ? SI_ARCH : SI;
                    if (-bv / si > 0) s = Disk(bu, -bv / si, t, out sI, out sdop);
                }
                if (!s || sI < 0.06)
                {
                    if (r < RS - 0.4) return C[0];
                    if (r < RS + 0.7)
                    {
                        double a = Atan2(v, u), f0 = 0.5 + 0.5 * Px.Cos(3 * a + (TAU * 2 * t) / D) + pulse * 0.4;
                        o.k = 0.3;
                        return f0 > 0.55 ? WHITE : C[7];
                    }
                    return 0;
                }
                // tone: dithered between ramp steps; the side turning toward us brighter and icier
                double I = sI * 1.3 * (1 + 0.55 * sdop) * (1 + 0.25 * pulse), f = Clamp(I) * (n - 1);
                int i = (int)Math.Floor(f);
                if (f - i > BAYER[(((int)Math.Floor(y) & 3) << 2) | ((int)Math.Floor(x) & 3)]) i = Math.Min(n - 1, i + 1);
                if (i == 0 && I < 0.12) return 0;
                uint c = RAMP[i];
                if (sdop > 0.35 && i >= n - 3) c = Mix(c, ICE, Math.Min(1, (sdop - 0.35) * 2));
                if (sdop < -0.4 && i >= 2 && i < n - 2) c = Mix(c, C[3], 0.35);
                if (i >= n - 4 && (((int)Math.Floor(x) + (int)Math.Floor(y)) & 1) == 0) o.k = 0.3 * Math.Min(1, I - 0.35);
                return c;
            });
        }

        readonly uint[] C, K;
        readonly double S;

        BlackHole(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            K = Fit(o.Ramp("rock"), 4);
            S = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            p.Glow(0, -1, 44, C[4], 0.12);
            p.Glow(0, Y, 80, C[4], 0.32);
            p.Glow(0, Y, 46, Mix(C[6], C[5], 0.5), 0.45);
        }

        public override void Draw(Painter p, double t)
        {
            // a gulp as debris is swallowed, twice a loop
            double pulse = Px.Pow(Math.Max(0, Px.Cos((TAU * 2 * t) / D)), 12);
            // pebbles torn off the floor, rising to join the disk
            Cycle(t, D, 10, S + 3, r => r.Range(1.6, 2.6), (age, L, r, i) =>
            {
                double x0 = r.Range(-60, 60), u = Px.Pow(age / L, 1.5), tx = Sign(x0 != 0 ? x0 : 1) * r.Range(40, 56), ty = Y + r.Range(4, 9);
                double x = x0 + (tx - x0) * u, y = -1 + (ty + 1) * u - Px.Sin(Math.PI * u) * 10;
                p.Px(x, y, u > 0.6 ? K[3] : K[2]);
                if (age < 0.25) p.Disc(x0, -1.5, 2 + age * 8, K[2], 0.4 * (1 - age / 0.25));
            });
            Hole(p, C, t, pulse);
            // motes streaking in from all round, turning with the disk as they fall
            Cycle(t, D, 40, S + 7, r => r.Range(0.5, 0.9), (age, L, r, i) =>
            {
                double a0 = r.Range(0, TAU), d0 = r.Range(48, 76), q = Px.Pow(age / L, 1.6), d = RS + 2 + (d0 - RS - 2) * (1 - q), a = a0 + 0.9 * q;
                double x = Px.Cos(a) * d * 1.15, y = Y + Px.Sin(a) * d * 0.75, q2 = Px.Pow(Math.Max(0, age - 0.09) / L, 1.6), d2 = RS + 2 + (d0 - RS - 2) * (1 - q2), a2 = a0 + 0.9 * q2;
                if (d < RS + 3 || y > -3 || y < -120 || Y + Px.Sin(a2) * d2 * 0.75 < -120 || Math.Abs(x) > 90) return;
                uint col = r.Pick(new[] { WHITE, C[7], C[6], C[5] });
                p.Line(Px.Cos(a2) * d2 * 1.15, Y + Px.Sin(a2) * d2 * 0.75, x, y, uu => uu > 0.6 ? col : C[4], Math.Min(1, age * 6));
                p.Glow(x, y, 3, col, 0.3);
            });
            // debris spiralling in: lumps of rock lit on the side toward the hole, stretched into hot streaks as they
            // fall, gone in a flash at the photon ring; behind the hole they're hidden by it
            Cycle(t, D, 22, S, r => r.Range(1.4, 2.4), (age, L, r, i) =>
            {
                double ph0 = r.Range(0, TAU), rho0 = r.Range(58, 80), hgt = r.Range(-8, 8), size = r.Range(1.6, 2.8);
                (double x, double y, double Z, double rho) At(double aa)
                {
                    double q = Clamp(aa / L), rho = RS + 1 + (rho0 - RS - 1) * (1 - Px.Pow(q, 1.5)), ph = ph0 + 2.4 * Px.Pow(q, 1.6) * (1 + 1.5 * q);
                    double X = Px.Cos(ph) * rho, Z = Px.Sin(ph) * rho, sx = X, sy = -Z * SI + hgt * (1 - q);
                    return (sx * CT - sy * ST, Y + sx * ST + sy * CT, Z, rho);
                }
                var a = At(age);
                var b = At(age - 0.04);
                bool hidden = a.Z > 0 && Hypot(a.x, a.y - Y) < RS + 1;
                if (hidden) return;
                if (a.rho < RS + 3)
                {
                    p.Px(a.x, a.y, WHITE);
                    p.Glow(a.x, a.y, 6, C[6], 0.6);
                    return;
                }
                double heat = 1 - Smooth(16, 34, a.rho), len = 1 + heat * 7;
                if (heat > 0.05)
                {
                    double dx = a.x - b.x, dy = a.y - b.y, l = Hypot(dx, dy);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    p.Line(a.x - (dx / l) * len, a.y - (dy / l) * len, a.x, a.y, u => u > 0.7 ? WHITE : u > 0.35 ? C[6] : C[4]);
                    p.Glow(a.x, a.y, 4, C[6], 0.4 * heat);
                }
                else
                {
                    double h1 = Hypot(a.x, a.y - Y);
                    if (h1 == 0 || double.IsNaN(h1)) h1 = 1;
                    double lx = -a.x / h1, ly = -(a.y - Y) / h1;
                    // a lump of rock: a fading wake along its orbit, a dark body, its face toward the hole lit violet
                    var c = At(age - 0.09);
                    var d = At(age - 0.15);
                    p.Px(b.x, b.y, C[3]);
                    p.Dot(c.x, c.y, C[3], 0.7);
                    p.Dot(d.x, d.y, C[2], 0.5);
                    bool s2 = size > 2.2;
                    p.Px(a.x, a.y, K[2]);
                    p.Px(a.x + 1, a.y, K[1]);
                    p.Px(a.x, a.y + 1, K[1]);
                    if (s2)
                    {
                        p.Px(a.x + 1, a.y + 1, K[0]);
                        p.Px(a.x - 1, a.y, K[1]);
                        p.Px(a.x, a.y - 1, K[2]);
                    }
                    p.Px(a.x + Round(lx * 1.4), a.y + Round(ly * 1.4), s2 ? C[6] : C[5]);
                    p.Glow(a.x, a.y, 4, C[5], 0.15);
                }
            });
            Halo(p, 0, Y, RS - 0.4, RS + 4, C[6], 0.35 + 0.3 * pulse);
            Unlight(p, -RS, Y - RS, RS, Y + RS, (x, y) =>
            {
                double dx = x, dy = y - Y, v = -dx * ST + dy * CT, u = dx * CT + dy * ST;
                return u * u + v * v < Px.Pow(RS - 0.6, 2) && !(v > 0 && Disk(u, -v / SI, t, out _, out _));
            });
        }
    }
}
