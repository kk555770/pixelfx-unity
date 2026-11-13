using System;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class VoidOrb : Effect
    {
        public static readonly Module Module = new Module("void-orb", "pixelfx/fx/shadow/void-orb", 168, 160, 84, 150, 3.2, true, 30,
            new Options { ["palette"] = VOID, ["seed"] = 5 },
            (o, e) => new VoidOrb(o))
        {
            Title = "Void orb",
            Desc = "A black orb hangs in the air, a pale rim crawling round it; a dark vortex winds into it, motes and streaks are pulled in, and light bends round its edge.",
            Tags = new[] { "shadow", "void", "orb", "vortex", "dark", "magic", "loop" },
            Bg = "ground",
        };

        // the orb's centre and radius, the vortex's reach, the radius light bends to (Einstein ring), the loop
        // matter can't orbit inside GAP: there the dark is empty but for light bent round the orb
        const double Y = -62, R = 12, GAP = R + 9, E = R + 4.5, D = 3.2, ARMS = 3, K = 7, WIND = (TAU * 2) / D;
        const int VR = 48;
        // two gulps a loop: the rim flares, the vortex brightens, a ring is drawn in
        static readonly double[] GULPS = { 0.2, 0.68 };
        static double Gulp(double t)
        {
            double s = 0;
            foreach (double g in GULPS) s = s + Exp(-Px.Pow((((Fract(t / D - g + 0.5) - 0.5) * D) / 0.07), 2));
            return s;
        }

        // specks of light behind the orb on looping paths; we see them through the lens: far off, a speck; passing
        // behind, smeared into an arc hugging the rim, with a faint second image on the far side
        static readonly double[][] SOURCES =
        {
            // cx, cy, rho, n, ph, s, sy
            new[] { 0, 0, 6, 1, 0, 2.6, 0.75 },
            new[] { 1, -1, 8, -1, 2.2, 2, 0.6 },
            new[] { 18, -6, 24, 1, 0.4, 2.4, 0.7 },
            new[] { -26, 10, 30, 1, 2.6, 2, 0.55 },
            new[] { 8, 22, 20, 2, 4.1, 1.6, 0.8 },
            new[] { -12, -26, 34, 1, 5.3, 1.8, 0.6 },
            new[] { 34, 18, 16, 2, 1.2, 1.4, 0.9 },
        };

        // tables round the orb, built once: radius, angle, log radius, and lumpy turbulence for the vortex
        sealed class Tables
        {
            public int n;
            public float[] r, th, ln, nz;
        }

        static Tables TAB;
        static readonly object tabLock = new object();

        static Tables GetTables()
        {
            lock (tabLock)
            {
                if (TAB != null) return TAB;
                int n = VR * 2, N = n * n;
                var T = new Tables { n = n, r = new float[N], th = new float[N], ln = new float[N], nz = new float[N] };
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    double x = i - VR + 0.5, y = j - VR + 0.5, a = Atan2(y, x), d = Hypot(x, y), l = Px.Log(Math.Max(1, d));
                    int k = j * n + i;
                    T.r[k] = (float)d;
                    T.th[k] = (float)a;
                    T.ln[k] = (float)l;
                    T.nz[k] = (float)Fbm(Px.Cos(a) * 2.5 + l * 2.4, Px.Sin(a) * 2.5 + l * 2.4, 17, 3);
                }
                return TAB = T;
            }
        }

        // the vortex: three arms of dark dust winding inward, a dithered veil threaded with thin bright filaments that
        // stream in along them faster than the arms turn, a lit leading edge, all heating to pale violet near the rim
        static void Vortex(Painter p, uint[] C, double t, double g)
        {
            Tables T = GetTables();
            int n = T.n;
            double wf = (TAU * 12) / D;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = j * n + i;
                double r = T.r[k];
                if (r < GAP || r > VR) continue;
                double th = T.th[k], ln = T.ln[k], nz = T.nz[k];
                double env = Smooth(VR, VR * 0.42, r) * Smooth(GAP, GAP + 4, r);
                double ph = ARMS * th + K * ln + WIND * t + nz * 2.6, cs = Px.Cos(ph);
                double A = (0.5 + 0.5 * cs) * env * (1 + 0.4 * g);
                if (A < 0.16) continue;
                double x = i - VR + 0.5, y = Y + j - VR + 0.5, hot = 1 - Smooth(GAP, GAP + 16, r);
                double fil = Px.Cos(12 * th + 28 * ln + wf * t + nz * 1.6);
                // streams break into dashes that run inward with them
                double dash = Px.Cos(9 * ln * 2.2 + 2.2 * th + wf * 0.75 * t);
                if (fil > 0.86 && A > 0.3 && dash > -0.55)
                {
                    uint c = hot > 0.6 ? C[7] : (hot > 0.25 || fil > 0.97) && dash > 0 ? C[6] : C[5];
                    p.Px(x, y, c);
                    p.Light(x, y, c, 0.1 + 0.2 * hot);
                    continue;
                }
                // the arm's leading edge, where it turns into the light
                if (A > 0.36 && A < 0.46 && Px.Sin(ph) > 0)
                {
                    p.Px(x, y, hot > 0.4 ? C[6] : C[4]);
                    continue;
                }
                // the veil: translucent dark dust, denser in the arm's heart
                p.Blend(x, y, A > 0.6 ? C[3] : C[2], Math.Min(0.75, (A - 0.16) * 1.4));
            }
        }

        // what the orb's gravity does to light from behind it
        static void Lensing(Painter p, uint[] C, double t, double g)
        {
            // the Einstein ring itself, faint, brightening as it gulps
            for (double a = 0, st = 0.7 / E; a < TAU; a += st)
            {
                double f = 0.5 + 0.5 * Px.Cos(5 * a - (TAU * 2 * t) / D) + 0.5 * g;
                p.Px(Px.Cos(a) * E, Y + Px.Sin(a) * E, f > 0.85 ? C[5] : f > 0.45 ? C[4] : C[3]);
            }
            foreach (double[] s in SOURCES)
            {
                double scx = s[0], scy = s[1], rho = s[2], sn = s[3], sph = s[4], ss = s[5], ssy = s[6];
                double q = (TAU * sn * t) / D + sph, sx = scx + rho * Px.Cos(q), sy = scy + rho * Px.Sin(q) * ssy;
                double b = Math.Max(0.3, Hypot(sx, sy)), al = Atan2(sy, sx), root = Math.Sqrt(b * b + 4 * E * E);
                var images = new[] { ((b + root) / 2, al, 1.0), ((root - b) / 2, al + Math.PI, 0.55) };
                foreach (var (r, a0, w) in images)
                {
                    if (r < R + 1.5) continue;
                    double mag = (r / b) * w, hw = Math.Min(Math.PI, (ss * 1.6) / (2 * b)), step = 0.6 / r;
                    for (double a = a0 - hw; a <= a0 + hw + 1e-6; a += step)
                    {
                        double u = hw > 0 ? Math.Abs(a - a0) / hw : 0, x = Px.Cos(a) * r, y = Y + Px.Sin(a) * r;
                        p.Dot(x, y, u < 0.3 && mag > 0.8 ? WHITE : u < 0.65 ? C[7] : C[5], Math.Min(1, mag * 1.4) * (1.15 - u));
                        if (mag > 1.2 && u < 0.6) p.Dot(x + Px.Cos(a), y + Px.Sin(a), C[6], 0.8);
                    }
                    p.Glow(Px.Cos(a0) * r, Y + Px.Sin(a0) * r, 4 + Math.Min(10, hw * r), C[6], Math.Min(0.5, 0.18 * mag));
                }
            }
        }

        // the orb: void black, a dull glint and a crescent of reflected light inside, and a pale rim crawling round
        static void Orb(Painter p, uint[] C, double t, double g)
        {
            p.Shade(-R - 1, Y - R - 1, R + 1, Y + R + 1, (x, y, o) =>
            {
                double dx = x, dy = y - Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > R + 0.5) return 0;
                double a = Atan2(dy, dx);
                if (d > R - 0.6)
                {
                    double f = 0.5 * Px.Cos(3 * a - (TAU * 2 * t) / D) + 0.3 * Px.Cos(7 * a + (TAU * 3 * t) / D + 1) + 0.25 * Px.Cos(13 * a - (TAU * 5 * t) / D + 2) + g;
                    return f > 0.5 ? WHITE : f > 0.05 ? C[6] : f > -0.4 ? C[5] : C[4];
                }
                if (R - d < 1.4 && Px.Cos(a - 0.8) > 0.6) return C[2];
                if (Px.Pow(dx + R * 0.42, 2) + Px.Pow(dy + R * 0.45, 2) < 1.6) return C[3];
                return C[0];
            });
        }

        readonly uint[] C;
        readonly double S;

        VoidOrb(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            S = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            double g = Gulp(t);
            p.Glow(0, -1, 52, C[4], 0.22 + 0.1 * g);
            Vortex(p, C, t, g);
            Lensing(p, C, t, g);
        }

        public override void Draw(Painter p, double t)
        {
            double g = Gulp(t);
            // dust drawn up off the floor, curving into the orb
            Cycle(t, D, 14, S + 2, r => r.Range(1.6, 2.4), (age, L, r, i) =>
            {
                double x0 = r.Range(-44, 44), u = Px.Pow(age / L, 1.7), side = x0 != 0 ? Sign(x0) : 1, ea = Math.PI / 2 + side * r.Range(0.3, 1);
                double ex = Px.Cos(ea) * R, ey = Y + Px.Sin(ea) * R, mx = x0 * 0.2, my = -18 - r.Range(0, 12);
                double x = (1 - u) * (1 - u) * x0 + 2 * (1 - u) * u * mx + u * u * ex, y = (1 - u) * (1 - u) * -1 + 2 * (1 - u) * u * my + u * u * ey;
                if (age < 0.5) p.Disc(x0, -1.5, 2 + age * 6, C[3], 0.4 * (1 - age / 0.5));
                p.Dot(x, y, u > 0.6 ? C[7] : u > 0.25 ? C[6] : C[5], Math.Min(1, age * 4));
                if (u > 0.3) p.Glow(x, y, 3, C[5], 0.25);
            });
            // motes swirl in along the arms, faster as they fall, and spark on the rim where they're swallowed
            Cycle(t, D, 30, S, r => r.Range(1.1, 1.8), (age, L, r, i) =>
            {
                double th0 = r.Range(0, TAU);
                if (Px.Sin(th0) > 0.75) th0 = -th0;
                double r0 = r.Range(30, 64), sw = r.Range(1.4, 2.6);
                uint col = r.Pick(new[] { WHITE, C[7], C[6], C[6], C[5] });
                (double x, double y, double rr) Pos(double aa)
                {
                    double u = Clamp(aa / L), k = Px.Pow(u, 1.8), rr = R + 1 + (r0 - R - 1) * (1 - k), th = th0 - sw * (k + 0.5 * u);
                    return (Px.Cos(th) * rr, Y + Px.Sin(th) * rr, rr);
                }
                var (x, y, rr0) = Pos(age);
                var (tx, ty, _) = Pos(age - 0.05);
                double a = Math.Min(1, age * 5);
                if (rr0 < R + 3)
                {
                    p.Px(x, y, WHITE);
                    p.Glow(x, y, 5, C[6], 0.5);
                    return;
                }
                p.Line(tx, ty, (tx + x) / 2, (ty + y) / 2, C[4], a * 0.8);
                p.Line((tx + x) / 2, (ty + y) / 2, x, y, col, a);
                p.Glow(x, y, 3, col, 0.3 * a);
            });
            // streaks: long dotted lines shot in from further out, white at the head (never from under the floor)
            Cycle(t, D, 9, S + 1, 0.42, (age, L, r, i) =>
            {
                double th = r.Range(0, TAU);
                if (Px.Sin(th) > 0.45) th = -th;
                double r0 = r.Range(56, 74), u = age / L, k = (Exp(3 * u) - 1) / (Exp(3) - 1), rr = r0 - (r0 - R - 1) * k;
                double len = 2 + 10 * k, ca = Px.Cos(th), sa = Px.Sin(th);
                for (double d = 0; d <= len; d += 0.5) p.Dot(ca * (rr + d), Y + sa * (rr + d), d < 1 ? WHITE : d < len * 0.5 ? C[6] : C[4], d < len * 0.5 ? 1 : 1.2 - d / len);
                p.Glow(ca * rr, Y + sa * rr, 4, C[6], 0.4);
            });
            // a gulp draws a faint ring in after it
            foreach (double gp in GULPS)
            {
                double u = Fract(t / D - gp) / (0.4 / D);
                if (u >= 1) continue;
                double rr = R + 2 + (VR - R) * (1 - Ease.InQuad(u));
                RingFast(p, 0, Y, rr, 1, C[4], 0.35 * (1 - u));
                RingLight(p, 0, Y, rr, 4, C[5], 0.3 * (1 - u));
            }
            Orb(p, C, t, g);
            Halo(p, 0, Y, R - 0.5, R + 7, C[6], 0.6 + 0.3 * g);
            Halo(p, 0, Y, R - 0.5, R + 46, C[4], 0.38 + 0.15 * g);
            Unlight(p, -R, Y - R, R, Y + R, (x, y) => x * x + Px.Pow(y - Y, 2) < Px.Pow(R - 0.6, 2));
        }
    }
}
