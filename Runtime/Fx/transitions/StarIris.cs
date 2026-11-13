// The twinkles (spinning glints at the tips, motes shed as it turns) follow Sprite Fusion's open-source sparkle
// (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class StarIris : Effect
    {
        const double DURATION = 2.2;

        public static readonly Module Module = Screen("star-iris", "Star iris",
            "A five-point star iris spins shut on the middle of the screen, its gold rim lit and its tips twinkling and shedding motes; it closes on a glint and spins open again from a flash.",
            new[] { "iris", "star", "gold", "wipe" }, DURATION,
            new Options { ["palette"] = PAL["gold"], ["focus"] = new double[] { 160, 96 }, ["seed"] = 1 },
            (o, e) => new StarIris(o, e));

        const double CLOSE = 0.98, OPEN0 = 1.16, OPEN1 = 2.0, RMAX = 430, INNER = 0.46, RW = 4;

        // the star at t: its outer radius and its turn
        static (double r, double a) StarAt(double t)
        {
            if (t < CLOSE) return (RMAX * Px.Pow(1 - Ease.InOutQuad(t / CLOSE), 1.3), -Math.PI / 2 + Ease.InOutCubic(t / CLOSE) * 1.6 * TAU);
            if (t < OPEN0) return (0, 0);
            double u = Clamp((t - OPEN0) / (OPEN1 - OPEN0));
            return (RMAX * Px.Pow(Ease.InOutCubic(u), 1.15), -Math.PI / 2 - Ease.InOutCubic(u) * 1.6 * TAU);
        }

        static double[][] Points(double fx, double fy, double r, double a)
        {
            var o = new double[10][];
            for (int i = 0; i < 10; i++)
            {
                double rr = i % 2 != 0 ? r * INNER : r, an = a + (i * Math.PI) / 5;
                o[i] = new[] { fx + Px.Cos(an) * rr, fy + Px.Sin(an) * rr };
            }
            return o;
        }

        readonly uint[] C;
        readonly uint ink;
        readonly Rng R;
        readonly double fx, fy;
        readonly Particles ps;
        readonly Lights lights;
        readonly double end = DURATION - 0.05;
        double t;
        bool shut, opened;
        (double r, double a) prev = StarAt(0);

        StarIris(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            ink = o.Color("color");
            R = new Rng(o.Seed);
            var focus = (double[])o["focus"];
            fx = focus[0];
            fy = focus[1];
            ps = new Particles(rng: R, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
        }

        public override void Update(double dt)
        {
            t += dt;
            var s = StarAt(t);
            double[][] pts = Points(fx, fy, s.r, s.a);
            double spin = (s.a - prev.a) / dt;
            // the tips shed gold motes as the star turns, flung off along their path
            if (s.r > 2)
                for (int i = 0; i < 10; i += 2)
                {
                    double x = pts[i][0], y = pts[i][1];
                    if (x < 0 || y < 0 || x >= W || y >= H || !R.Chance(0.35)) continue;
                    double an = s.a + (i * Math.PI) / 5, v = spin * s.r * 0.25;
                    ps.Spawn(R.Chance(0.6) ? P.Star : P.Mote, x, y, -Px.Sin(an) * v + Px.Cos(an) * 20, Px.Cos(an) * v + Px.Sin(an) * 20, Math.Min(R.Range(0.25, 0.55), end - t), R.Chance(0.5) ? C[6] : C[5], R.Range(0.6, 1.8));
                }
            if (!shut && t >= CLOSE)
            {
                shut = true;
                lights.Add(fx, fy, 60, C[5], 0.7, 0.3);
            }
            if (!opened && t >= OPEN0)
            {
                opened = true;
                lights.Add(fx, fy, 120, C[5], 0.7, 0.4);
                lights.Add(fx, fy, 40, WHITE, 0.8, 0.12);
                for (int i = 0; i < 24; i++)
                {
                    double a = R.Range(0, TAU), v = R.Range(60, 200);
                    ps.Spawn(P.Star, fx, fy, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 2));
                }
            }
            prev = s;
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p0)
        {
            Painter p = ClipTo(p0);
            var s = StarAt(t);
            if (t >= CLOSE && t < OPEN0) Rect(p0, 0, 0, W, H, ink);
            else if (s.r < RMAX - 2)
            {
                double[][] pts = Points(fx, fy, s.r, s.a);
                // the dark outside the star, row by row: the star's spans are where the outline crossings pair up
                for (int y = 0; y < H; y++)
                {
                    double yc = y + 0.5;
                    var xs = new List<double>();
                    for (int i = 0, j = 9; i < 10; j = i++)
                    {
                        double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                        if (yi > yc != yj > yc) xs.Add(xi + ((yc - yi) * (xj - xi)) / (yj - yi));
                    }
                    double[] sorted = xs.OrderBy(v => v).ToArray();
                    double x = 0;
                    for (int k = 0; k + 1 < sorted.Length; k += 2)
                    {
                        double a = Clamp(Round(sorted[k]), 0, W);
                        if (a > x) Rect(p0, x, y, a - x, 1, ink);
                        x = Math.Max(x, Clamp(Round(sorted[k + 1]), 0, W));
                    }
                    if (x < W) Rect(p0, x, y, W - x, 1, ink);
                }
                // the gold rim: along each edge, out into the dark, white where it faces the light
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    double ax = pts[j][0], ay = pts[j][1], bx = pts[i][0], by = pts[i][1], L = Hypot(bx - ax, by - ay);
                    if (L < 0.5) continue;
                    double ux = (bx - ax) / L, uy = (by - ay) / L, nx = -uy, ny = ux; // outward for this winding
                    bool lit = nx * -0.6 + ny * -0.8 > 0.1;
                    for (double sx = 0; sx <= L; sx += 0.5)
                    {
                        double x0 = ax + ux * sx, y0 = ay + uy * sx;
                        if (x0 < -RW || y0 < -RW || x0 > W + RW || y0 > H + RW) continue;
                        for (double d = 0; d < RW; d += 0.5)
                        {
                            uint c = d < 1 ? (lit ? WHITE : C[6]) : d < 2 ? C[5] : d < 3 ? C[4] : C[3];
                            p.Px(x0 + nx * d, y0 + ny * d, c);
                        }
                        if (sx % 2 == 0) p.Light(x0, y0, C[5], 0.5);
                    }
                }
                // twinkles on the tips, and light pooling in the star as it closes
                for (int i = 0; i < 10; i += 2)
                {
                    double x = pts[i][0], y = pts[i][1];
                    if (x < 2 || y < 2 || x > W - 2 || y > H - 2) continue;
                    Glint(p, x, y, 5 + 3 * Px.Sin(t * 13 + i), t * 2 + i, C[6], C[5]);
                }
                if (s.r < 140) p.Glow(fx, fy, s.r * 0.7 + 8, C[5], 0.35 * (1 - s.r / 140));
            }
            // shutting: a glint where it closed; opening: a flash
            double u = t - CLOSE;
            if (u > -0.05 && u < 0.2) Glint(p, fx, fy, 14 * (1 - Math.Abs(u) / 0.2), u * 4, C[6], C[5]);
            double v2 = t - OPEN0;
            if (v2 >= 0 && v2 < 0.16)
            {
                Glint(p, fx, fy, 18 * (1 - v2 / 0.16), 0.3 - v2 * 5, C[6], C[5]);
                p.Glow(fx, fy, 50, WHITE, 0.5 * (1 - v2 / 0.16));
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
