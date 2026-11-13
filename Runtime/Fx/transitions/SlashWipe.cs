// The cut's tapered smear (white at the rim, cooling down the ramp with age), the dotted trace it leaves, the
// tip glint, the sparks off the end and the shavings flung off the line are adapted from Sprite Fusion's
// open-source slash (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class SlashWipe : Effect
    {
        const double D = 1.7;

        public static readonly Module Module = Screen("slash-wipe", "Slash wipe",
            "A white-hot blade edge slices across the screen, light ahead of it and sparks spraying back over the cover; a second cut flashes through and the cover splits apart.",
            new[] { "slash", "blade", "cut", "sparks", "wipe" }, D,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#b8c9e8", "#e8f1ff", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["seed"] = 2,
            },
            (o, e) => new SlashWipe(o, e));

        const double SL = 0.55, HALO = 18;
        static readonly double CS = 1 / Hypot(1, SL);
        const double T0 = 0.06, SWEEP = 0.62, E0 = -(H / 2.0) * SL - 4, E1 = W + (H / 2.0) * SL + 24;
        const double CUT = 0.84, CSW = 0.07, CHOLD = 0.05, CTRAIL = 0.12, SLIP = CUT + 0.12, SPLIT = CUT + CSW + CHOLD + CTRAIL;

        // the blade edge: x where it crosses row y, and how fast it's moving
        static double EdgeAt(double t) => E0 + (E1 - E0) * Ease.OutCubic(Clamp((t - T0) / SWEEP));
        static double SpeedAt(double t) => t < T0 || t > T0 + SWEEP ? 0 : ((E1 - E0) * 3 * Px.Pow(1 - (t - T0) / SWEEP, 2)) / SWEEP;
        static double XAt(double e, double y) => e + (H / 2.0 - y) * SL;

        sealed class Half
        {
            public int s;
            public List<double[]> pts;
            public double mx, my, spin;
        }

        readonly uint[] C;
        readonly uint ink;
        readonly int n;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly double ux, uy, nx, ny, cx, cy, LC;
        readonly double[] A, B;
        readonly Half[] halves;
        readonly double end = D - 0.05;
        double t, acc;
        bool entered, tipped, split;

        SlashWipe(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            ink = o.Color("color");
            R = new Rng(o.Seed);
            n = C.Length;
            ps = new Particles(rng: R, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            // the second cut: a "\\" through the middle, from where it enters the screen to where it leaves
            double th = R.Range(0.42, 0.56);
            ux = Px.Cos(th);
            uy = Px.Sin(th);
            nx = uy;
            ny = -ux;
            cx = W / 2.0 + R.Range(-24, 24);
            cy = H / 2.0 + R.Range(-10, 10);
            // the halves overhang the screen a little, so slipping along the cut never uncovers an edge
            var area = new[] { new double[] { -24, -24 }, new double[] { W + 24, -24 }, new double[] { W + 24, H + 24 }, new double[] { -24, H + 24 } };
            var ends = new double[2][];
            for (int si = 0; si < 2; si++)
            {
                int s = si == 0 ? -1 : 1;
                // walk out along the cut until it leaves the screen
                double lo = 0, hi = 400;
                for (int k = 0; k < 30; k++)
                {
                    double m = (lo + hi) / 2, x = cx + s * ux * m, y = cy + s * uy * m;
                    if (x >= 0 && x <= W && y >= 0 && y <= H) lo = m;
                    else hi = m;
                }
                ends[si] = new[] { cx + s * ux * lo, cy + s * uy * lo };
            }
            A = ends[0];
            B = ends[1];
            LC = Hypot(B[0] - A[0], B[1] - A[1]);
            halves = new Half[2];
            for (int hi = 0; hi < 2; hi++)
            {
                int s = hi == 0 ? 1 : -1;
                List<double[]> pts = HalfPlane(area, cx, cy, s * nx, s * ny);
                double mx = 0, my = 0;
                foreach (double[] q in pts)
                {
                    mx += q[0] / pts.Count;
                    my += q[1] / pts.Count;
                }
                halves[hi] = new Half { s = s, pts = pts, mx = mx, my = my, spin = s * R.Range(0.5, 0.9) };
            }
        }

        // where a half of the cover is: it slips along the cut, then the halves fly apart, turning a little
        (double dx, double dy, double rot, double sep) PlaceOf(Half hf)
        {
            double slip = 5 * Ease.OutCubic(Clamp((t - SLIP) / 0.12)) * (hf.s > 0 ? 1 : -0.4), u = Math.Max(0, t - SPLIT);
            double sep = 30 * u + 0.5 * 1500 * u * u, rot = hf.spin * u * u;
            return (ux * slip + hf.s * nx * sep, uy * slip + hf.s * ny * sep, rot, sep);
        }

        static double[] Moved(Half hf, (double dx, double dy, double rot, double sep) pl, double[] q)
        {
            double c = Px.Cos(pl.rot), s = Px.Sin(pl.rot), lx = q[0] - hf.mx, ly = q[1] - hf.my;
            return new[] { hf.mx + lx * c - ly * s + pl.dx, hf.my + lx * s + ly * c + pl.dy };
        }

        public override void Update(double dt)
        {
            t += dt;
            // the blade grinds through: sparks spray back over the cover, shavings flake off, light rides the edge
            double sp = SpeedAt(t), e = EdgeAt(t);
            if (!entered && t >= T0)
            {
                entered = true;
                lights.Add(4, 4, 60, C[5], 0.6, 0.25);
            }
            // in bursts: each a fountain of sparks off one spot where the edge bites
            for (acc += sp * dt * 0.035; acc > 1; acc--)
            {
                double y = R.Range(4, H - 4), x = XAt(e, y) - 1, a0 = Math.PI + R.Range(-0.1, 0.6);
                int n = R.Int(5, 10);
                if (x < 0 || x >= W) continue;
                for (int i = 0; i < n; i++)
                {
                    double a = a0 + R.Range(-0.75, 0.75), v = R.Range(0.2, 1) * (140 + 0.22 * sp);
                    ps.Spawn(P.Spark, x, y + R.Range(-2, 2), Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.15, 0.4));
                    if (R.Chance(0.4)) ps.Spawn(P.Mote, x, y, Px.Cos(a) * v * 0.45, Px.Sin(a) * v * 0.45 - 20, R.Range(0.2, 0.5), R.Chance(0.5) ? C[6] : C[5], 0.7);
                }
                lights.Add(x, y, R.Range(30, 46), C[5], 0.35, 0.14);
            }
            // the second cut lands: a glint at its end, sparks thrown on along it, shavings off its length
            if (!tipped && t >= CUT + CSW)
            {
                tipped = true;
                for (int i = 0; i < 12; i++)
                {
                    double a = Atan2(uy, ux) + R.Range(-0.5, 0.5), v = R.Range(150, 400);
                    ps.Spawn(P.Spark, B[0] - ux * 2, B[1] - uy * 2, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.12, 0.35));
                }
                for (int i = 0; i < 18; i++)
                {
                    double k = R.Range(0.1, 1), x = A[0] + (B[0] - A[0]) * k, y = A[1] + (B[1] - A[1]) * k;
                    int s = R.Sign();
                    double v = R.Range(30, 100);
                    ps.Spawn(P.Mote, x, y, s * nx * v + ux * 20, s * ny * v - 10, R.Range(0.25, 0.55), R.Chance(0.5) ? C[5] : C[4], 0.6);
                }
                for (int i = 0; i < 5; i++) lights.Add(A[0] + (B[0] - A[0]) * (i + 0.5) / 5, A[1] + (B[1] - A[1]) * (i + 0.5) / 5, 46, C[5], 0.45, 0.3);
            }
            // and the halves break apart: sparks spray out of the opening seam both ways
            if (!split && t >= SPLIT)
            {
                split = true;
                for (int i = 0; i < 46; i++)
                {
                    double k = R.Range(0, 1), x = A[0] + (B[0] - A[0]) * k, y = A[1] + (B[1] - A[1]) * k;
                    int s = R.Sign();
                    double v = R.Range(80, 320);
                    double a = Atan2(s * ny, s * nx) + R.Range(-0.7, 0.7);
                    ps.Spawn(P.Spark, x, y, Px.Cos(a) * v, Px.Sin(a) * v, Math.Min(R.Range(0.15, 0.45), end - t));
                }
                for (int i = 0; i < 6; i++) lights.Add(A[0] + (B[0] - A[0]) * (i + 0.5) / 6, A[1] + (B[1] - A[1]) * (i + 0.5) / 6, 56, C[5], 0.5, 0.35);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p0)
        {
            Painter p = ClipTo(p0);
            if (t < CUT + CSW + CHOLD + 0.02 || t < SLIP)
            {
                // the cover behind the blade, row by row: ink, then the smear cooling from the white-hot edge, light ahead
                double e = EdgeAt(t), sp = SpeedAt(t), Ls = Clamp(sp * 0.0065, 1.5, 8), echo = Clamp(sp / 900) * 0.6;
                for (int y = 0; y < H; y++)
                {
                    double xe = XAt(e, y + 0.5), ia = Clamp(Math.Floor(xe - (Ls + 10) / CS), 0, W), ib = Clamp(Math.Ceiling(xe + HALO / CS), 0, W);
                    if (ia > 0) Rect(p0, 0, y, ia, 1, ink);
                    for (double x = ia; x < ib; x++)
                    {
                        double d = (xe - (x + 0.5)) * CS;
                        if (d < 0)
                        {
                            double u = 1 + d / HALO;
                            p0.Light(x, y, C[5], 0.32 * u * u);
                            if (d > -4) p0.Light(x, y, WHITE, 0.3 * Px.Pow(1 + d / 4, 2));
                            continue;
                        }
                        if (d < 1) p0.Px(x, y, WHITE);
                        else if (d < 2) p0.Px(x, y, C[6]);
                        else
                        {
                            double q = (d - 2) / Ls;
                            p0.Px(x, y, q < 0.3 ? C[5] : q < 0.55 ? C[4] : q < 0.8 ? C[3] : q < 1 ? C[2] : ink);
                            // a dotted trace a beat behind the edge, while it's moving fast
                            if (echo > 0 && Math.Abs(d - Ls - 7) < 0.5 && (x + y) % 3 == 0) p0.Dot(x, y, C[4], echo);
                        }
                        if (d < Ls + 2) p0.Light(x, y, C[5], 0.3 * (1 - d / (Ls + 2)));
                    }
                }
            }
            if (t >= SLIP)
            {
                // the two halves: dark, their cut edges white-hot and cooling to ice; light pours out of the seam
                double heat = 1 - Clamp((t - CUT - 0.05) / 0.5);
                foreach (Half hf in halves)
                {
                    var pl = PlaceOf(hf);
                    var pts = hf.pts.ConvertAll(q => Moved(hf, pl, q));
                    double[] a = Moved(hf, pl, A), b = Moved(hf, pl, B);
                    // the cut face of this half: white-hot at the edge, cooling and narrowing to an ice rim
                    double c = Px.Cos(pl.rot), sn = Px.Sin(pl.rot), mx = hf.s * (nx * c - ny * sn), my = hf.s * (nx * sn + ny * c);
                    uint[] rim = { heat > 0.55 ? WHITE : heat > 0.25 ? C[6] : C[5], heat > 0.4 ? C[6] : C[4], heat > 0.3 ? C[5] : C[3], heat > 0.3 ? C[4] : ink };
                    double deep = Clamp(1 + pl.sep * 0.5, 1, 4); // the cut face shows more as the halves part
                    FillConvex(p0, pts, (x, y) =>
                    {
                        double d = (x - a[0]) * mx + (y - a[1]) * my;
                        if (d < deep)
                        {
                            if (d < 2) p0.Light(x, y, C[5], 0.4 * heat * (1 - d / 2));
                            return rim[(int)Math.Max(0, Math.Floor(d))];
                        }
                        return ink;
                    });
                    if (heat > 0)
                        for (int i = 0; i < 8; i++)
                        {
                            double k = (i + 0.5) / 8;
                            p.Glow(a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k, 12 + pl.sep * 0.15, C[5], 0.3 * heat);
                        }
                }
            }
            // the second cut: a smear that rips along the line, thickest just behind its head, white at its core,
            // cooling down the ramp, its tail dissolving; a dotted trace where it's gone; light riding its head
            double uc = t - CUT;
            if (uc >= 0 && uc < CSW + CHOLD + CTRAIL + 0.25)
            {
                double head = LC * Ease.OutExpo(Clamp(uc / CSW)), tail = LC * Smooth(CSW + CHOLD, CSW + CHOLD + CTRAIL, uc), age = Clamp(uc / (CSW + CHOLD + CTRAIL));
                double Wc = 3.6 * (1 - (tail / LC) * 0.55), span = head - tail, sn = Math.Abs(nx);
                if (span > 1 && tail < LC - 1 && t < SPLIT)
                    for (int y = 0; y < H; y++)
                    {
                        // the cut crosses row y at x0; the smear spans Wc either side of it, measured square to the cut
                        double x0 = A[0] + ((y + 0.5 - A[1]) * ux) / uy, hw = (Wc + 1) / sn;
                        for (double x = Math.Max(0, Math.Floor(x0 - hw)); x < Math.Min(W, Math.Ceiling(x0 + hw)); x++)
                        {
                            double px = x + 0.5 - A[0], py = y + 0.5 - A[1], s = px * ux + py * uy, v = (s - tail) / span;
                            if (v < 0 || v > 1) continue;
                            double w = Wc * Px.Pow(Px.Sin(Math.PI * Px.Pow(v, 2.2)), 0.6);
                            if (w < 0.35) continue;
                            double q = Math.Abs(px * nx + py * ny) / w;
                            if (q > 1) continue;
                            double ci = q < 0.34 && age < 0.5 ? 0 : Round(q * (n - 1) * 0.75 + age * 2.4 - v * 0.8 + 0.4);
                            p0.Dot(x, y, C[n - 1 - (int)Clamp(ci, 0, n - 1)], Math.Min(1, v / 0.4));
                            if (q < 0.5) p0.Light(x, y, C[5], 0.4 * (1 - age));
                        }
                    }
                // the trace it leaves behind, until the cover parts
                if (tail > 2 && t < SPLIT)
                    for (double s = 0; s < tail; s += 1.6) p.Dot(A[0] + ux * s, A[1] + uy * s, C[5], 0.7 * (0.4 + (0.6 * s) / LC));
                if (uc < 0.3)
                {
                    double hx = A[0] + ux * head, hy = A[1] + uy * head;
                    p.Glow(hx, hy, 46, C[4], 0.55 * (1 - uc / 0.3));
                }
                double g = uc - CSW * 0.6;
                if (g > 0 && g < 0.24) Sim.Sparkle(p, B[0] - ux * 3, B[1] - uy * 3, 11 * Ease.OutBack(Math.Min(1, g / 0.05)) * (1 - g / 0.24), C[6], C[4], 0.7);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
