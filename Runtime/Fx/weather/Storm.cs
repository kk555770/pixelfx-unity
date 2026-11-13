// The bolt (a branching path, a stepped leader, strokes that flicker frame by frame, light along the channel) and
// the rain (streaks with lit heads, splashes that throw droplets) are adapted from Sprite Fusion's open-source
// lightning (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Storm : Effect
    {
        public static readonly Module Module = new Module("storm", "pixelfx/fx/weather/storm", 320, 180, 160, 170, 10, true, 30,
            new Options
            {
                ["palette"] = new[] { "#09080f", "#121120", "#1b1a30", "#262541", "#363a5c", "#4f5d8c", "#7790c6", "#c2d6ff" },
                ["bolt"] = new[] { "#2a1a6e", "#4835c2", "#6a6cff", "#78b8ff", "#c6ebff", "#ffffff" },
                ["seed"] = 5,
            },
            (o, e) => new Storm(o, e))
        {
            Title = "Storm",
            Desc = "Driving rain at two depths under a churning cloud deck; a forked bolt strikes in a double flash that lights the sky and the rain, then sheet lightning glows in the clouds.",
            Tags = new[] { "weather", "storm", "rain", "lightning", "thunder", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 10, SLANT = 0.42;
        // the strike: a stepped leader feels its way down, the first stroke, a restrike down the same channel
        const double T_LEAD = 2.62, T1 = 3.0, T2 = 3.3, T_SHEET = 6.4, T_FAR = 8.6;
        const double BX0 = 62, BY0 = -136, BX1 = 52, BY1 = 3;
        static readonly double[] F1 = { 1, 0.62, 0.95, 0.4, 0.7, 0.22 }, F2 = { 0.92, 0.5, 0.8, 0.3, 0.12 };

        static double Env(double t, double t0, double[] E)
        {
            double i = Math.Floor((t - t0) * 30 + 1e-3);
            return i >= 0 && i < E.Length ? E[(int)i] : 0;
        }

        static double FlashAt(double t) => Math.Max(Env(t, T1, F1), Env(t, T2, F2));

        // sheet lightning: light that swells inside the clouds with no bolt to see, twice; and a far flicker
        static double SheetAt(double t)
        {
            double a = t - T_SHEET;
            if (a < 0 || a > 0.9) return 0;
            double p1 = Exp(-Px.Pow((a - 0.08) / 0.05, 2)), p2 = Exp(-Px.Pow((a - 0.3) / 0.09, 2)), tail = a > 0.3 ? Exp(-(a - 0.3) * 6) * 0.5 : 0;
            return Math.Min(1, 0.7 * p1 + p2 + tail) * (Math.Floor(a * 30) % 3 == 1 ? 0.75 : 1);
        }

        static double Blink(double a, double k) => a < 0 || a > 0.3 ? 0 : k * (Exp(-Px.Pow((a - 0.06) / 0.04, 2)) + 0.65 * Exp(-Px.Pow((a - 0.18) / 0.05, 2)));
        static double FarAt(double t) => Blink(t - T_FAR, 0.45);
        static double Far2At(double t) => Blink(t - 0.9, 0.3);

        // times are snapped so a runtime's accumulated float time (2.9999999) lands on the frame it means
        static double Snap(double t) => Round(t * 3e4) / 3e4;

        readonly Options o;
        readonly Emitter e;

        Storm(Options o, Emitter e)
        {
            this.o = o;
            this.e = e;
        }

        public override void DrawBack(Painter p, double t)
        {
            t = Snap(t);
            var q = new Clip(p, Module);
            var B = q.B;
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8), V = Fit(o.Ramp("bolt"), 6);
            double f = FlashAt(t), sh = SheetAt(t), far = FarAt(t), lead = t > T_LEAD && t < T1 ? 0.25 + 0.2 * Px.Sin(t * 70) : 0;
            double pre = t > T_LEAD - 0.5 && t < T_LEAD ? 0.12 * Math.Max(0, Px.Sin((t - T_LEAD) * 41)) : 0;
            // what lights the clouds from inside: the bolt's root, the sheet lightning, the far flicker
            var srcs = new List<double[]>();
            if (f + lead + pre > 0) srcs.Add(new[] { BX0, BY0 - 6, 150, f + lead * 0.5 + pre });
            if (sh > 0) srcs.Add(new[] { -96, -146, 120, sh });
            if (far > 0) srcs.Add(new[] { 128, -156, 80, far });
            if (Far2At(t) > 0) srcs.Add(new[] { -40, -160, 70, Far2At(t) });
            double LitAt(double x, double y)
            {
                double L = 0;
                foreach (double[] s in srcs)
                {
                    double d = Hypot(x - s[0], (y - s[1]) * 1.5) / s[2];
                    if (d < 1) L += s[3] * (1 - d) * (1 - d);
                }
                return Math.Min(1, L);
            }
            Sky(p, t, f, V);
            if (sh > 0.05)
                Pass(p, Module, -170, 0, (x, y, w) =>
                {
                    double d = Hypot(x + 96, (y + 150) * 1.3) / 170;
                    if (d < 1)
                    {
                        w.l = V[2];
                        w.k = sh * 0.3 * (1 - d) * (1 - d);
                    }
                });
            Veil(p, t, S, C, V, f);
            // far rain behind the bolt; the bolt's root disappears up into the cloud
            DrawRain(q, t, S, C, V, f, false);
            if (t > T_LEAD && t < T2 + 0.7) Bolt(q, t, S, V);
            Deck(q, B, t, S, C, V, LitAt, srcs.Count > 0);
            Crawler(q, t, S, V);
            DrawFloor(q, t, S, C, V, f);
            Strike(q, t, S, C, V);
        }

        public override void Draw(Painter p, double t)
        {
            t = Snap(t);
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8), V = Fit(o.Ramp("bolt"), 6);
            double f = FlashAt(t);
            DrawRain(q, t, S, C, V, f, true);
        }

        // the whole sky lights up with each stroke, most near the bolt
        static void Sky(Painter p, double t, double f, uint[] V)
        {
            double k = f * 0.36;
            if (k <= 0.01) return;
            uint c = Mix(V[2], V[3], 0.6);
            Pass(p, Module, -170, 9, (x, y, w) =>
            {
                double d = Math.Abs(x - (BX0 + BX1) / 2) / 230;
                w.l = c;
                w.k = k * (0.2 + 0.8 * Px.Pow(Math.Max(0, 1 - d), 2.2));
            });
        }

        sealed class CloudRow
        {
            public double bse, wob, ry, drift;
            public int n, tex;
            public double[] rx, life;
            public int[] tone;
            public bool haze;
        }

        // the cloud deck, seen from below in perspective: rows of rolls, the nearest and biggest at the top, each row
        // further off hanging a little lower, smaller and hazier. Every roll swells, drifts on the wind and dissolves on
        // its own clock (born once a loop), so the underside churns with no seam. Each row's lower edge catches a 1 px
        // rim of light and throws a line of shadow on the row behind it.
        static readonly CloudRow[] ROWS =
        {
            new CloudRow { bse = -161, wob = 3, n = 18, rx = new double[] { 16, 30 }, ry = 0.8, drift = 15, life = new double[] { 4, 7 }, tone = new[] { 1, 2, 3, 4 }, tex = 1 },
            new CloudRow { bse = -150, wob = 3, n = 22, rx = new double[] { 10, 20 }, ry = 0.85, drift = 10, life = new double[] { 3.5, 6.5 }, tone = new[] { 1, 2, 3, 5 }, tex = 1 },
            new CloudRow { bse = -140, wob = 3, n = 26, rx = new double[] { 7, 14 }, ry = 0.9, drift = 6, life = new double[] { 3, 6 }, tone = new[] { 2, 2, 3, 5 }, tex = 0, haze = true },
        };

        // the storm cell hangs lowest over the strike
        static double Sag(double x) => 9 * Exp(-Px.Pow((x - 40) / 95, 2)) - 3;

        sealed class Roll
        {
            public double x, h, rx;
        }

        // each roll pushes down out of its row, drifts on the wind and draws back up, on its own clock (born once a loop)
        static List<Roll>[] Rolls(double S, double t)
        {
            var all = new List<Roll>[ROWS.Length];
            for (int j = 0; j < ROWS.Length; j++)
            {
                CloudRow Rw = ROWS[j];
                var output = new List<Roll>();
                for (int i = 0; i < Rw.n; i++)
                {
                    double L = Rw.life[0] + Rand(S, j, i, 1) * (Rw.life[1] - Rw.life[0]), age = Wrap(t - Rand(S, j, i, 2) * D, D);
                    if (age >= L) continue;
                    double u = age / L, g = Px.Pow(Px.Sin(Math.PI * u), 0.6);
                    double rx = Rw.rx[0] + Rand(S, j, i, 3) * (Rw.rx[1] - Rw.rx[0]), ry = rx * Rw.ry, x = -200 + Rand(S, j, i, 4) * 400 + Rw.drift * (age - L / 2);
                    output.Add(new Roll { x = x, h = ry * g * (0.6 + 0.4 * Rand(S, j, i, 5)), rx = rx * (0.8 + 0.2 * g) });
                }
                all[j] = output;
            }
            return all;
        }

        // sheets of rain sweeping across on the gusts: a translucent haze, streaked along the rain, that hides the stars
        // and catches the flash where it's thickest
        static void Veil(Painter p, double t, double S, uint[] C, uint[] V, double f)
        {
            Field F = MakeField(S + 11, 256, 128, new[] { 4, 8, 16 });
            double shift = (256 * 2 * t) / D;
            uint glow = Mix(C[3], V[3], 0.5);
            Pass(p, Module, -170, -0.5, (x, y, w) =>
            {
                double n = Sample(F, (x - y * SLANT) * 1.9 - shift, y * 0.16 + 7), dens = Clamp(0.45 + (n - 0.5) * 2.6);
                w.c = f > 0.05 ? Mix(C[1], glow, Math.Min(1, f * dens * 0.7 * Math.Max(0.25, 1 - Math.Abs(x - BX1) / 220))) : C[1];
                w.a = 0.4 + 0.38 * dens;
                w.b = true;
            });
        }

        static void Deck(Clip q, Box B, double t, double S, uint[] C, uint[] V, Func<double, double, double> litAt, bool lit)
        {
            List<Roll>[] R = Rolls(S, t);
            Field F = MakeField(S * 3 + 1, 256, 128);
            double drift = (256 * t) / D, mo = Px.Pow(Px.Cos((Math.PI * t) / D), 2);
            int n = ROWS.Length;
            uint[] RAMP = { C[0], C[1], C[2], C[3], C[4], C[5], C[6], V[4], WHITE };
            float[] top = new float[n], bot = new float[n], bulge = new float[n];
            for (int x = B.x0; x < B.x1; x++)
            {
                double cx = x + 0.5;
                double low = -170;
                // each row hangs as a curtain: a wavy base with rolls bulging out of it
                for (int j = 0; j < n; j++)
                {
                    CloudRow Rw = ROWS[j];
                    double und = (Sample(F, cx * 0.55 + j * 91, 3) * mo + Sample(F, cx * 0.55 + j * 91, 60) * (1 - mo) - 0.5) * 2;
                    double c = Rw.bse + Sag(cx) * (0.4 + j * 0.3) + 7 * und + Rw.wob * 2 * (Sample(F, cx * 1.8 + j * 61 - drift * (0.5 - j * 0.1), 20 + j * 17) - 0.5);
                    double bump = 0;
                    foreach (Roll b in R[j])
                    {
                        double dx = (cx - b.x) / b.rx;
                        if (dx * dx < 1) bump = Math.Max(bump, b.h * Math.Sqrt(1 - dx * dx));
                    }
                    double y1 = c + bump;
                    top[j] = -999;
                    bot[j] = (float)y1;
                    bulge[j] = (float)bump;
                    low = Math.Max(low, y1);
                }
                int prev = -1;
                double prevBot = -999;
                for (int y = B.y0; y < low; y++)
                {
                    double cy = y + 0.5;
                    int j = 0;
                    while (j < n && !(cy >= top[j] && cy < bot[j])) j++;
                    if (j >= n)
                    {
                        prev = -1;
                        continue;
                    }
                    if (j != prev)
                    {
                        if (prev >= 0 && prev < j) prevBot = cy;
                        prev = j;
                    }
                    CloudRow Rw = ROWS[j];
                    double d = bot[j] - cy, th = Bayer(x, y);
                    int[] tone = Rw.tone;
                    // light catches the lower edge of each row, most on the big bulges, and fades up into a dark core
                    int i;
                    if (d < 1) i = bulge[j] > 3 ? tone[3] : tone[3] - 1;
                    else if (cy - prevBot < 1.5 && j > 0) i = Math.Max(0, tone[0] - 1); // shadow under the row in front
                    else if (d < 2 + bulge[j] * 0.25 + th * 2) i = tone[2];
                    else if (d < 6 + bulge[j] * 0.4 + th * 3) i = tone[1];
                    else
                    {
                        double nn = Sample(F, cx * 0.9 + j * 77 - drift * (0.6 - j * 0.12), cy * 1.7 + j * 31);
                        i = tone[0] + (Rw.tex != 0 && nn > 0.6 ? 1 : 0);
                    }
                    double lk = 0;
                    if (lit)
                    {
                        double Lv = litAt(cx, cy);
                        if (Lv > 0.02)
                        {
                            i = (int)Math.Min(8, i + Round(Lv * (d < 1 ? 4.4 : d < 4 ? 3.8 : 3.1)));
                            lk = Lv;
                        }
                    }
                    if (Rw.haze && lk == 0 && d >= 1) q.Blend(x, y, RAMP[i], 0.75);
                    else q.Px(x, y, RAMP[Math.Max(0, i)]);
                    if (lk > 0.03) q.Light(x, y, V[3], lk * (d < 3 ? 0.4 : 0.22));
                }
            }
        }

        // the rain: far drops thin and dim, near drops long with bright heads; the flash lights every drop
        static void DrawRain(Clip q, double t, double S, uint[] C, uint[] V, double f, bool near)
        {
            int LitAt(double x)
            {
                double L = f * (0.3 + 0.7 * Math.Max(0, 1 - Math.Abs(x - BX1) / 170));
                return L > 0.45 ? 2 : L > 0.15 ? 1 : 0;
            }
            if (!near)
            {
                uint[] BODY = { C[4], C[4], C[5] }, HEAD = { C[5], C[6], C[6] };
                Trips(t, D, 300, S * 7 + 1, 22, 28, (u, k, m, i) =>
                {
                    double j = Wrap(k, m), stop = -14 - Rand(S, i, j, 1) * 90, y = -176 + (stop + 176) * u, x = -170 + Rand(S, i, j, 2) * 360 - (stop - y) * SLANT;
                    double L = 3 + Rand(S, i, j, 3) * 2, ux = SLANT / Hypot(SLANT, 1), uy = 1 / Hypot(SLANT, 1);
                    int lit = LitAt(x);
                    uint body = BODY[lit], head = HEAD[lit];
                    q.Line(x - ux * L, y - uy * L, x - ux * L * 0.4, y - uy * L * 0.4, body);
                    q.Line(x - ux * L * 0.4, y - uy * L * 0.4, x, y, head);
                });
                return;
            }
            uint[] NBODY = { C[6], C[6], V[4] }, NHEAD = { C[7], WHITE, WHITE }, NTAIL = { C[4], C[5], V[3] }, COOL = { C[5], C[6], V[4] };
            Trips(t, D, 84, S * 7 + 2, 30, 38, (u, k, m, i) =>
            {
                double j = Wrap(k, m), z = 1 + Rand(S, i, j, 1) * 4, y = -180 + (z + 180) * u, x = -175 + Rand(S, i, j, 2) * 370 - (z - y) * SLANT;
                double L = 9 + Rand(S, i, j, 3) * 5, ux = SLANT / Hypot(SLANT, 1), uy = 1 / Hypot(SLANT, 1);
                int lit = LitAt(x);
                uint body = NBODY[lit], head = NHEAD[lit], tail = NTAIL[lit];
                q.Line(x - ux * L, y - uy * L, x - ux * L * 0.55, y - uy * L * 0.55, tail);
                q.Line(x - ux * L * 0.55, y - uy * L * 0.55, x - ux, y - uy, body);
                q.Px(x, y, head);
                // the drop before this one landed: splash
                double dur = D / m, age = u * dur;
                if (age < 0.4)
                {
                    double jp = Wrap(k - 1, m), zp = 1 + Rand(S, i, jp, 1) * 4, xp = -175 + Rand(S, i, jp, 2) * 370;
                    int lp = LitAt(xp);
                    Splash(q, xp, zp, age, 0.25 + (zp - 1) / 5.3, lp != 0 ? WHITE : C[7], COOL[lp], Rand(S, i, jp, 4));
                }
            });
        }

        // the channel: a jagged trunk, three long forks peeling off it (alternating sides), each with twigs of its own
        static readonly double[] FORKS = { 0.2, 0.38, 0.55 };

        static List<Branch> Channel(double S)
        {
            var R = new Rng(S * 31 + 1);
            List<Branch> P = Tree(R, BX0, BY0, BX1, BY1, 0.26, 0.18);
            List<double[]> pts = P[0].pts;
            int side = R.Sign();
            for (int i = 0; i < FORKS.Length; i++)
            {
                double u = FORKS[i];
                int k = (int)Math.Floor(u * (pts.Count - 1));
                double ax = pts[k][0], ay = pts[k][1];
                double[] e = pts[Math.Min(pts.Count - 1, k + 3)];
                double bx = e[0], by = e[1];
                double a = Atan2(by - ay, bx - ax) + (i % 2 != 0 ? -side : side) * R.Range(0.6, 1.0), l = R.Range(36, 56) * (1 - u * 0.45);
                P.AddRange(Tree(R, ax, ay, ax + Px.Cos(a) * l, ay + Px.Sin(a) * l, 0.3, 0.5, 0.6, 1));
            }
            return P;
        }

        static void Bolt(Clip q, double t, double S, uint[] V)
        {
            List<Branch> P1 = Channel(S);
            if (t < T1)
            {
                double reach = BY0 + (BY1 - BY0) * Px.Pow(Math.Floor(((t - T_LEAD) / (T1 - T_LEAD)) * 9) / 9, 1.25);
                foreach (Branch p in P1)
                    for (int i = 0; i < p.pts.Count - 1; i++)
                    {
                        double ax = p.pts[i][0], ay = p.pts[i][1], bx = p.pts[i + 1][0], by = p.pts[i + 1][1];
                        if (by > reach) break;
                        q.Line(ax, ay, bx, by, p.w >= 1 ? V[2] : V[1]);
                        if (i % 4 == 0) q.Glow(ax, ay, 6, V[3], 0.22 * p.w);
                    }
                return;
            }
            // the restrike runs down the same channel, with fewer, shorter forks
            List<Branch> re = Tree(new Rng(S * 31 + 2), BX0, BY0, BX1, BY1, 0.3, 0.3);
            var P2 = new List<Branch> { P1[0] };
            for (int i = 1; i < re.Count && i - 1 < 2; i++) P2.Add(re[i]);
            int s = t >= T2 ? 2 : 1;
            double a = t - (s == 2 ? T2 : T1), k = Env(t, s == 2 ? T2 : T1, s == 2 ? F2 : F1);
            List<Branch> paths = s == 2 ? P2 : P1;
            if (k > 0) Stroke(q, paths, k, V);
            else if (a < 0.5)
            {
                double g = 1 - a / 0.5;
                foreach (Branch p in paths)
                    if (p.w >= 1)
                        foreach (double[] pt in p.pts)
                            q.Dot(pt[0], pt[1], V[1], g * 0.9);
            }
        }

        // spider lightning crawling along the cloud base while the sheet lightning glows: drawn a frame at a time, the
        // crawl reaching a little further each frame, then gone
        static void Crawler(Clip q, double t, double S, uint[] V)
        {
            double a = t - T_SHEET - 0.22;
            if (a < 0 || a > 0.17) return;
            List<Branch> P = Tree(new Rng(S * 7 + 3), -150, -138, -26, -134, 0.2, 0.7);
            double reach = -150 + 130 * Math.Min(1, (Math.Floor(a * 30) + 1) / 3), k = a < 0.1 ? 1 : 0.5;
            foreach (Branch p in P)
                for (int i = 0; i < p.pts.Count - 1; i++)
                {
                    double ax = p.pts[i][0], ay = p.pts[i][1], bx = p.pts[i + 1][0], by = p.pts[i + 1][1];
                    if (ax > reach) break;
                    q.Line(ax, ay, bx, by, p.w >= 1 && k > 0.6 ? V[4] : V[3]);
                    if (p.w >= 1) q.Dot(ax, ay + 1, V[2], k);
                    if (i % 3 == 0) q.Glow(ax, ay, 10, V[3], 0.35 * k * p.w);
                }
        }

        // a stroke: a violet sheath, a pale blue body, a white-hot core (two pixels wide at the peak), light all along
        static void Stroke(Clip q, List<Branch> paths, double k, uint[] V)
        {
            for (int pass = 0; pass < 3; pass++)
                foreach (Branch p in paths)
                {
                    bool trunk = p.w >= 1;
                    if (!trunk && (k < 0.4 || (pass == 0 && p.w < 0.5))) continue;
                    for (int i = 0; i < p.pts.Count - 1; i++)
                    {
                        double ax = p.pts[i][0], ay = p.pts[i][1], bx = p.pts[i + 1][0], by = p.pts[i + 1][1], l = Math.Max(1, Hypot(bx - ax, by - ay));
                        for (double d = 0; d <= l; d += 0.5)
                        {
                            double x = ax + ((bx - ax) * d) / l, y = ay + ((by - ay) * d) / l;
                            if (pass == 0) q.Circle(x, y, trunk ? (k > 0.6 ? 2.3 : 1.7) : 1.2, k > 0.6 ? V[2] : V[1]);
                            else if (pass == 1) q.Circle(x, y, trunk ? (k > 0.6 ? 1.4 : 0.9) : 0.6, trunk ? V[4] : V[3]);
                            else if (trunk)
                            {
                                q.Px(x, y, k > 0.3 ? WHITE : V[4]);
                                if (k > 0.85) q.Px(x + 1, y, WHITE);
                            }
                            else if (p.w > 0.4 && k > 0.6) q.Px(x, y, V[4]);
                        }
                        if (pass == 0 && i % 3 == 0) q.Glow(ax, ay, trunk ? 18 : 8, V[3], (trunk ? 0.5 : 0.3) * k * p.w);
                    }
                }
        }

        // where the bolt lands: a blinding hit, sparks that skip off the wet floor, steam, a glow that cools
        void Strike(Clip q, double t, double S, uint[] C, uint[] V)
        {
            double a = t - T1;
            if (a < 0 || a > 1.8) return; // the last spray is gone by 1.6 s
            double k = FlashAt(t);
            if (k > 0)
            {
                // arcs skitter out along the wet floor, new ones every frame
                double fr = Math.Floor(t * 30 + 1e-3);
                var Rc = new Rng(S * 977 + fr);
                for (int i = 0; i < 4; i++)
                {
                    int side = i % 2 != 0 ? 1 : -1;
                    double l = Rc.Range(10, 30) * (0.6 + 0.4 * k);
                    foreach (Branch p in Tree(Rc, BX1 + side, BY1 - 1, BX1 + side * l, BY1 + Rc.Range(-2, 3), 0.35, 0.5, 0.6, 1))
                    {
                        q.Polyline(p.pts, k > 0.6 && p.w > 0.5 ? V[4] : V[3]);
                        double[] mid = p.pts[p.pts.Count >> 1];
                        q.Glow(mid[0], mid[1], 8, V[3], 0.3 * k);
                    }
                }
                q.Circle(BX1, BY1 - 1, 2 + 3 * k, V[4]);
                q.Circle(BX1, BY1 - 1, 1 + 2 * k, WHITE);
                q.Glow(BX1, BY1 - 12, 95, V[2], 0.55 * k);
                q.Glow(BX1, BY1 - 4, 40, V[3], 0.5 * k);
                q.Glow(BX1, BY1 - 2, 14, WHITE, 0.6 * k);
            }
            uint[] sparks = { WHITE, V[4], V[3], V[2] };
            var ps = new Particles(new Rng(S * 13 + 5), ground: x => 4, sparks: sparks, haze: V[2], emitter: e);
            Rng R = ps.R;
            uint steam = Mix(C[4], V[3], 0.3);
            void Burst(int n, double pow)
            {
                for (int i = 0; i < n; i++)
                {
                    double an = -Math.PI / 2 + R.Range(-1.35, 1.35), sp = R.Range(110, 420) * pow;
                    ps.Spawn(P.Spark, BX1, BY1 - 1, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.18, 0.6));
                }
                // a skirt of spray and steam blown out along the floor
                for (int i = 0; i < 6 * pow; i++)
                {
                    int sd = R.Sign();
                    ps.Spawn(P.Vapour, BX1 + sd * R.Range(2, 8), BY1 - R.Range(1, 3), sd * R.Range(30, 90), -R.Range(4, 16), R.Range(0.6, 1.2), steam, R.Range(2.5, 4.5), R.Range(0, 0.08));
                }
            }
            Burst(48, 1);
            double tt = 0;
            bool second = false;
            const double dt = 1.0 / 120;
            for (; tt + dt <= a; tt += dt)
            {
                if (!second && tt >= T2 - T1)
                {
                    second = true;
                    Burst(16, 0.7);
                }
                ps.Update(dt);
            }
            DrawParticles(q, ps, sparks, V[2]);
            if (a < 1.6) q.Glow(BX1, BY1, 16, V[2], 0.4 * Px.Pow(1 - a / 1.6, 2));
        }

        // the wet floor: a dark sheen, a broken lip of light, pools that reflect the sky and flash with it, mist kicked
        // up by the rain drifting along the ground
        static void DrawFloor(Clip q, double t, double S, uint[] C, uint[] V, double f)
        {
            Field F = MakeField(S + 3, 256, 128), M = MakeField(S + 4, 256, 128, new[] { 4, 8, 16 });
            double wind = (256 * 3 * t) / D;
            Pass(q.P, Module, -14, -0.5, (x, y, w) =>
            {
                double m = Sample(M, x * 1.1 - wind, y * 4 + 9) - 0.4;
                if (m <= 0) return;
                w.c = f > 0.1 ? Mix(C[4], V[3], f) : C[4];
                w.a = Math.Min(0.3, m * 0.9) * (1 + y / 14);
                w.b = true;
            });
            Pass(q.P, Module, 0, 9, (x, y, w) =>
            {
                bool Pool(double yy) => Sample(F, x * 1.3 + 31, yy * 9) > 0.6 - yy * 0.006;
                bool wet = Pool(y), lip = wet && !Pool(y - 1);
                w.b = true;
                w.c = C[1];
                w.a = 0.5;
                if (y < 1)
                {
                    w.c = Sample(F, x * 2, 50) > 0.5 ? C[4] : C[3];
                    w.a = 1;
                    w.b = false;
                }
                else if (wet)
                {
                    double d = Math.Abs(x - BX1) / 150;
                    if (f > 0.06)
                    {
                        w.c = (d < 0.25 && f > 0.5) || lip ? V[4] : V[3];
                        w.a = Clamp(f * (1.35 - d) * (lip ? 1.4 : 1));
                        w.b = false;
                    }
                    else
                    {
                        w.c = lip ? C[5] : C[3];
                        w.a = lip ? 1 : 0.7;
                    }
                    w.l = V[3];
                    w.k = f * 0.25 * Math.Max(0, 1 - d);
                }
            });
            // the bolt's reflection in the pools, broken by ripples
            if (f > 0.2)
                for (int y = 1; y < 10; y++)
                    if (Rand(y, Math.Floor(t * 30), 3) < 0.8) q.Dot(BX1 + Px.Sin(y * 1.7 + t * 40) * 1.2, y, y < 4 ? WHITE : V[4], f * (1.1 - y / 12.0));
        }
    }
}
