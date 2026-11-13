// The puddle (a domed wet surface with glints sliding along it, bubbles that swell and pop) is adapted from
// Sprite Fusion's open-source poison puddle (https://www.spritefusion.com/pixel-art-vfx/poison),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Slime : Effect
    {
        public static readonly Module Module = new Module("slime", "pixelfx/fx/critters/slime", 112, 88, 56, 72, 1.8, true, 30,
            new Options
            {
                ["palette"] = new[] { "#061412", "#0b2f2b", "#12594a", "#1f8f4f", "#47c752", "#a6f06a", "#f0ffd2" },
                ["hop"] = 24,
                ["seed"] = 1,
            },
            (o, e) => new Slime(o))
        {
            Title = "Slime",
            Desc = "A glossy slime hops in place: it squashes to wind up, stretches into the jump, rounds out in the air, splats down flinging droplets that creep back, then wobbles and blinks.",
            Tags = new[] { "critter", "slime", "monster", "hop", "goo", "loop" },
            Bg = "ground",
        };

        const double D = 1.8, T_SQ = 0.5, T_UP = 0.84, T_LIFT = 0.9, T_LAND = 1.44, SNAP = 0.965;
        const double W0 = 30, H0 = 22, VC = 0.4, G = 640;
        const int NDROP = 12;

        static double Sq(double x) => x * x;

        sealed class Body
        {
            public double s, by, lean, round, q, age, W, H;
        }

        static readonly double[][] STRETCH = { new[] { 0, 0.36 }, new[] { 0.4, 0.02 }, new[] { 0.55, -0.05 }, new[] { 0.85, 0.08 }, new double[] { 1, 0.16 } };

        // the body's shape and squash at loop time t: s > 0 stretched tall, s < 0 squashed flat
        static Body PoseAt(double t, double hop)
        {
            double age = Wrap(t - T_LAND, D);
            double s, by = 0, lean = 0, round = 0.1, q = -1;
            if (t >= T_LIFT && t < T_LAND)
            {
                q = (t - T_LIFT) / (T_LAND - T_LIFT);
                by = -hop * 4 * q * (1 - q);
                s = Curve(q, STRETCH);
                round = 0.1 + 0.8 * Smooth(0, 0.16, q) * (1 - Smooth(0.86, 1, q));
            }
            else if (t >= T_UP && t < T_LIFT) s = Lerp(-0.3, 0.36, Ease.OutCubic(Seg(t, T_UP, T_LIFT)));
            else if (t >= T_SQ && t < T_UP)
            {
                double u = Seg(t, T_SQ, T_UP);
                s = -0.3 * Ease.InOutSine(u) + 0.018 * Px.Sin(t * TAU * 16) * Smooth(0.55, 1, u);
            }
            else
            {
                // the splat holds for a beat, then rings out into a wobble; the top sways a little as it settles
                s = age < 0.06 ? -0.46 : -0.46 * Spring(age - 0.06, 2.1, 4.6);
                lean = 0.07 * Exp(-age * 4) * Px.Sin(age * TAU * 1.9);
                if (t < T_SQ) s += 0.02 * Px.Sin((t / T_SQ) * TAU);
            }
            return new Body { s = s, by = by, lean = lean, round = round, q = q, age = age, W = W0 / Math.Sqrt(1 + s), H = H0 * (1 + s) };
        }

        // half the body's width at height v (0 base .. 1 top): a dome over a soft foot with rounded corners
        static double HalfW(double v, Body P)
        {
            if (v < 0 || v > 1) return -1;
            double W = P.W, H = P.H, round = P.round;
            if (v >= VC)
            {
                double q1 = (v - VC) / (1 - VC);
                return (W / 2) * Math.Sqrt(1 - q1 * q1);
            }
            double q = (VC - v) / VC, hb = v * H, rc = 3;
            double hw = (W / 2) * Math.Sqrt(Math.Max(0, 1 - round * q * q));
            if (hb < rc) hw -= rc - Math.Sqrt(rc * rc - Sq(rc - hb));
            return hw;
        }

        static bool Inside(Body P, double x, double y)
        {
            double v = (P.by - 1 - y) / P.H, hw = HalfW(v, P);
            return hw >= 0 && Math.Abs(x - P.lean * v * P.H) <= hw;
        }

        // shaded the way a pixel artist shades jelly: a dark dome over light pooling low inside the body, a shadow
        // crescent, a glossy streak hugging the top-left contour with a hot spot under it, light bounced up off the floor
        static void DrawBody(Grid g, uint[] C, Body P)
        {
            double yb = P.by - 1, half = P.W / 2 + 3;
            double cx = P.lean * 0.45 * P.H, cy = yb - P.H * 0.5, lx = cx - P.W * 0.24, ly = cy - P.H * 0.36;
            bool Lit(double x, double y, double k) => Inside(P, lx + (x - lx) / k, ly + (y - ly) / k);
            g.Clear();
            g.Shade(-half, yb - P.H - 1, half, yb, (x, y, o) =>
            {
                if (!Inside(P, x, y)) return 0;
                o.t = 1;
                o.a = 0.92;
                int i = Lit(x, y, 0.42) ? 5 : Lit(x, y, 0.7) ? 4 : 3;
                // light pooled in the jelly along the bottom, brightest just inside the rim
                double v = (yb - y) / P.H;
                if (v < 0.3 && Inside(P, x, y + 1.4)) i = !Inside(P, x, y + 2.4) ? 5 : v < 0.2 ? 4 : i;
                if (!Inside(P, x + 2, y + 2.4))
                {
                    i = 2;
                    o.a = 1;
                }
                double ang = Atan2(y - cy, (x - cx) * (P.H / P.W));
                if (!Inside(P, x - 1.6, y - 1.6) && Inside(P, x - 0.7, y - 0.7) && ang > -2.75 && ang < -1.55)
                {
                    i = ang > -2.45 && ang < -1.8 ? 6 : 5;
                    o.a = 1;
                }
                if (i == 2 && Inside(P, x + 1, y + 1.2) && !Inside(P, x + 2, y + 2.4) && ang > 0.3 && ang < 1.5) i = 4;
                return C[i];
            });
            // the hot spot: a tiny bright window reflection under the streak
            double hx = Round(cx - P.W * 0.27), hy = Round(cy - P.H * 0.14);
            if (Inside(P, hx - 1, hy) && Inside(P, hx + 1, hy + 1))
            {
                g.Set(hx, hy, C[6]);
                g.Set(hx, hy + 1, C[5]);
            }
        }

        // the eyes ride the squash: squint while winding up, wide in the air, screwed shut on the splat, a blink at rest
        static void Face(Grid g, uint[] C, Body P, double t)
        {
            const double ev = 0.47;
            double yb = P.by - 1, ey = Round(yb - P.H * ev), ex = Math.Max(3, Round(P.W * 0.16)), xc = Round(P.lean * ev * P.H);
            bool air = P.q >= 0, shut = P.age < 0.16 && !air, blink = t > 0.22 && t < 0.29;
            double wind = t >= T_SQ && t < T_LIFT ? Seg(t, T_SQ, T_UP) : 0, look = air ? (P.q < 0.45 ? -1 : P.q > 0.7 ? 1 : 0) : 0;
            double eh = Clamp(Round(4 * (1 + P.s * 0.8) - wind * 2 + (air && P.q < 0.6 ? 1 : 0)), 1, 5);
            foreach (int side in new[] { -1, 1 })
            {
                double x = xc + side * ex - (side > 0 ? 1 : 0);
                if (shut)
                {
                    int a = side < 0 ? 0 : 1, b = 1 - a;
                    g.Set(x + a, ey - 1, C[0]);
                    g.Set(x + b, ey, C[0]);
                    g.Set(x + a, ey + 1, C[0]);
                    continue;
                }
                if (blink)
                {
                    g.Set(x, ey + 1, C[0]);
                    g.Set(x + 1, ey + 1, C[0]);
                    continue;
                }
                double top = ey + 2 - eh + look;
                for (int k = 0; k < eh; k++)
                {
                    g.Set(x, top + k, C[0]);
                    g.Set(x + 1, top + k, C[0]);
                }
                g.Set(x + (look > 0 ? 1 : 0), top + (look > 0 ? 1 : 0), C[6]);
                if (eh >= 4) g.Set(x + 1, top + eh - 1, C[2]);
            }
            double my = Round(yb - P.H * 0.31);
            if (air && P.q < 0.75)
            {
                g.Set(xc, my, C[0]);
                g.Set(xc - 1, my, C[0]);
                g.Set(xc, my + 1, C[1]);
                g.Set(xc - 1, my + 1, C[1]);
            }
            else if (!shut)
            {
                g.Set(xc, my, C[1]);
                g.Set(xc - 1, my, C[1]);
            }
        }

        // a bubble works its way up through the jelly while it rests and pops at the top
        const double POP = 0.4;

        static void BubbleIn(Grid g, uint[] C, Body P, double t)
        {
            if (t > POP) return;
            double v = Lerp(0.18, 0.86, Ease.InQuad(t / POP)), hw = HalfW(v, P), x = P.lean * v * P.H - hw * Lerp(0.35, 0.05, t / POP), y = P.by - 1 - v * P.H;
            g.Set(x, y, C[6]);
            g.Set(x + 1, y, C[5]);
            g.Set(x, y + 1, C[5]);
            g.Set(x + 1, y + 1, C[4]);
        }

        sealed class Drop
        {
            public double x, y, vx, vy, at, lx, size, k, splat;
            public bool landed, gone;
        }

        // droplets flung off the splat: they arc, land with a little splat, and creep back to the body to rejoin it
        static Drop Droplet(int i, double seed, double age)
        {
            Rng r = NewRng(seed, i, 7);
            double a = -Math.PI / 2 + r.Range(-1.2, 1.2), sp = r.Range(60, 118), size = r.Chance(0.35) ? 1 : r.Chance(0.7) ? 2 : 3;
            double fa = a + Math.PI / 2, x0 = Px.Sin(fa) * 21, y0 = -1 - Px.Cos(fa) * 12.5;
            Flight fl = Toss(x0, y0, Px.Cos(a) * sp * 0.75, Px.Sin(a) * sp, age, G, -1);
            var f = new Drop { x = fl.x, y = fl.y, vx = fl.vx, vy = fl.vy, at = fl.at, lx = fl.lx, landed = fl.landed, size = size };
            if (f.landed)
            {
                double lx = f.lx != 0 && !double.IsNaN(f.lx) ? f.lx : 1;
                double home = Sign(lx) * (W0 / 2 - 2), dur = Math.Min(0.55, 0.22 + Math.Abs(f.lx - home) / 60), u = Seg(age - f.at - 0.14, 0, dur);
                f.x = Lerp(f.lx, home, Ease.InOutSine(u));
                f.k = 1 - u;
                f.gone = u >= 1;
                f.splat = Clamp(1 - (age - f.at) / 0.14);
            }
            return f;
        }

        // the wet patch it sits in: wider while squashed, splashed out on landing, left behind while it jumps
        static double PuddleR(double t, Body P)
        {
            if (P.q >= 0) return (W0 / Math.Sqrt(1.36) / 2 + 3) * (1 - 0.22 * P.q);
            return P.W / 2 + 3 + 9 * Exp(-P.age * 5) * (1 - Exp(-P.age * 40)) * (t >= T_LAND || t < T_SQ ? 1 : 0);
        }

        static readonly double[][] DRIPS = { new[] { 1.02, -4 }, new double[] { 1.18, 3 } };
        static readonly double[][] FLECKS = { new double[] { 1, 0 }, new double[] { -1, 0 }, new[] { 0.6, -0.7 }, new[] { -0.6, -0.7 }, new double[] { 0, -1 } };

        readonly Options o;
        readonly Grid GRID = new Grid(-30, -64, 60, 66);

        Slime(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double hop = o.Num("hop");
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            Body P = PoseAt(t, hop);
            double yb = P.by - 1, age = P.age;
            Grid g = GRID;
            DrawBody(g, C, P);
            Face(g, C, P, t);
            BubbleIn(g, C, P, t);
            g.Outline((x, y, nx, ny) => ny < 0 && nx <= 0 && y < yb - P.H * 0.5 ? C[1] : C[0]);
            g.Draw(p);

            // the bubble pops out of the top: a ring and two flecks
            if (t >= POP && t < POP + 0.16)
            {
                double k = (t - POP) / 0.16, x = P.lean * P.H - HalfW(0.86, P) * 0.05, y = yb - P.H - 0.5;
                for (double a = 0; a < TAU; a += 0.6) p.Dot(x + Px.Cos(a) * (1 + k * 3), y + Px.Sin(a) * (1 + k * 3) * 0.7, C[6], 1.2 - k);
                foreach (int s in new[] { -1, 1 }) p.Dot(x + s * (1 + k * 4), y - 1 - k * 6 + k * k * 4, C[5], 1.3 - k);
                p.Glow(x, y, 6, C[5], 0.4 * (1 - k));
            }
            // droplets in flight
            for (int i = 0; i < NDROP; i++)
            {
                Drop f = Droplet(i, o.Seed, age);
                if (f.landed || Inside(P, f.x, f.y)) continue;
                double sv = Hypot(f.vx, f.vy);
                if (sv == 0 || double.IsNaN(sv)) sv = 1;
                double ux = f.vx / sv, uy = f.vy / sv, L = Math.Min(2.5, sv / 50);
                p.Line(f.x - ux * L, f.y - uy * L, f.x, f.y, C[3]);
                if (f.size >= 2)
                {
                    p.Px(f.x - 1, f.y, C[4]);
                    p.Px(f.x - 1, f.y + 1, C[3]);
                    p.Px(f.x, f.y + 1, C[2]);
                }
                if (f.size >= 3)
                {
                    p.Px(f.x + 1, f.y, C[3]);
                    p.Px(f.x - 1, f.y - 1, C[4]);
                    p.Px(f.x, f.y - 1, C[5]);
                }
                p.Px(f.x, f.y, f.size >= 2 ? C[6] : C[5]);
                p.Glow(f.x, f.y, 5 + f.size, C[4], 0.32);
            }
            // spores drifting up out of the puddle, glowing
            Cycle(t, D, 7, o.Seed * 13 + 1, r => r.Range(1.2, 1.8), (ag, L, r, i) =>
            {
                double k = ag / L, x = r.Range(-24, 24) + Px.Sin(ag * 2.1 + i * 1.7) * 3, y = -2 - r.Range(9, 17) * ag - 5 * ag * ag;
                double a = Math.Min(1, k * 4) * Math.Min(1, (1 - k) * 2.5), tw = 0.6 + 0.4 * Px.Sin(ag * 7 + i * 2.3);
                bool big = r.Chance(0.35);
                p.Dot(x, y, tw > 0.8 ? C[6] : C[5], a);
                if (big)
                {
                    p.Dot(x + 1, y, C[5], a);
                    p.Dot(x, y + 1, C[4], a);
                    p.Dot(x + 1, y + 1, C[4], a * 0.6);
                }
                p.Glow(x, y, big ? 9 : 6, C[5], (big ? 0.45 : 0.32) * a * tw);
            });
            // drips let go of its underside while it's in the air and plop into the puddle
            foreach (double[] dr in DRIPS)
            {
                double t0 = dr[0], dx = dr[1];
                double a2 = Wrap(t - t0, D);
                if (a2 > 0.6) continue;
                Body P0 = PoseAt(t0, hop);
                Flight f = Toss(dx, P0.by - 0.5, 0, 12, a2, G, -1);
                if (!f.landed)
                {
                    p.Px(dx, f.y, C[5]);
                    p.Px(dx, f.y - 1, C[4]);
                    p.Glow(dx, f.y, 4, C[4], 0.3);
                }
                else if (a2 - f.at < 0.14)
                {
                    double k = (a2 - f.at) / 0.14;
                    foreach (int s2 in new[] { -1, 1 }) p.Dot(dx + s2 * (1 + k * 3), -2 - k * 3 + k * k * 3, C[5], 1.2 - k);
                }
            }
            // a glint on the dome at the top of the hop
            if (P.q > 0.4 && P.q < 0.64)
            {
                double k = Px.Sin(Math.PI * Seg(P.q, 0.4, 0.64));
                Sim.Sparkle(p, P.lean * 0.8 * P.H - P.W * 0.21, yb - P.H * 0.8, 1 + 2.5 * k, C[6], C[5], 0.5 * k);
            }
            // light: the slime glows through and tints the air round it, the puddle lights the floor, a flash on the splat
            double cx = P.lean * P.H * 0.4, cy = yb - P.H * 0.45, near = Clamp(1 + P.by / (hop * 1.1));
            double R = PuddleR(t, P), br = 0.9 + 0.1 * Px.Sin((t / D) * TAU * 2);
            p.Glow(cx * 0.5, -14, 64, C[3], 0.3 * br);
            p.Glow(cx, cy, 30 + P.W * 0.3, C[4], 0.2);
            p.Glow(cx, cy + 3, 14, C[5], 0.16);
            p.Glow(0, -2, R * 1.3 + 6, C[4], 0.12 + 0.08 * near);
            if (t >= T_LAND || t < T_SQ) p.Glow(0, -3, 40, C[5], 0.42 * Exp(-age * 9));
        }

        public override void DrawBack(Painter p, double time)
        {
            double hop = o.Num("hop");
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            Body P = PoseAt(t, hop);
            double age = P.age;
            // the puddle: a low dome of goo, lit along its top with glints sliding over it, darker where it meets the floor
            double R = PuddleR(t, P), hp = Math.Min(2.4, 0.9 + R * 0.06);
            for (double x = Math.Floor(-R); x <= Math.Ceiling(R); x++)
            {
                double d = Math.Abs(x + 0.5) / R;
                if (d > 1) continue;
                double top = -1 - Round(Math.Sqrt(1 - d * d) * hp);
                for (double y = top; y <= 0; y++)
                {
                    uint c = y == top ? C[5] : y == 0 ? C[2] : C[4];
                    if (d > 0.88) c = y == top ? C[4] : C[3];
                    if (y == top && d < 0.88)
                    {
                        double n = Lnoise(t, D, 7, x * 0.18, 3) + Lnoise(t, D, 4, x * 0.07, 4) * 0.6;
                        if (n > 0.55) c = C[6];
                    }
                    else if (y == top + 1 && d < 0.8 && Lnoise(t, D, 3, x * 0.12, 5) > 0.3) c = C[5];
                    p.Px(x, y, c);
                }
                if (d > 0.96) p.Px(x, top - 1, C[1]);
            }
            // bubbles swell out of the puddle and pop, flicking flecks
            Cycle(t, D, 4, o.Seed * 17 + 3, r => r.Range(0.45, 0.75), (ag, L, r, i) =>
            {
                double x = r.Range(-0.85, 0.85) * R, k = ag / L, rb = r.Range(1.4, 2.6) * (0.3 + 0.7 * Ease.OutCubic(Math.Min(1, k * 1.25)));
                if (Math.Abs(x) < P.W / 2 + 1 && P.q < 0) return;
                double sy = -1 - Round(Math.Sqrt(Math.Max(0, 1 - Sq(x / R))) * hp);
                if (k < 0.85)
                {
                    for (double y = Math.Floor(sy - rb); y <= sy; y++)
                    for (double xx = Math.Floor(x - rb - 1); xx <= Math.Ceiling(x + rb + 1); xx++)
                    {
                        double dx = xx + 0.5 - x, dy = y + 0.5 - sy, dd = Hypot(dx, dy);
                        if (dd > rb + 0.5) continue;
                        if (dd > rb - 0.7) p.Px(xx, y, dx < 0 && dy < -rb * 0.3 ? C[6] : C[4]);
                        else p.Blend(xx, y, C[5], 0.3);
                    }
                    p.Glow(x, sy - rb * 0.5, rb * 2 + 3, C[5], 0.2);
                }
                else
                {
                    double q = (k - 0.85) / 0.15;
                    foreach (double[] fl in FLECKS) p.Dot(x + fl[0] * (2 + 3 * q), sy + fl[1] * (2 + 3 * q), C[6], 1.2 - q);
                }
            });
            // landed droplets: a flattened splat, then a bead of goo creeping home, a wet trail behind it
            for (int i = 0; i < NDROP; i++)
            {
                Drop f = Droplet(i, o.Seed, age);
                if (!f.landed || f.gone) continue;
                double x = Round(f.x);
                int w = (int)f.size + (f.splat > 0 ? 1 : 0);
                for (int j = 0; j < w; j++) p.Px(x - (w >> 1) + j, -1, j == 0 ? C[5] : C[4]);
                if (f.size >= 2 && f.splat <= 0)
                {
                    p.Px(x, -2, C[5]);
                    p.Px(x - 1, -2, C[4]);
                }
                p.Px(x, 0, C[3]);
                double dir = Sign(f.lx - f.x);
                for (int j = 1; j < Math.Min(8, Math.Abs(f.lx - f.x)); j++) p.Dot(x + dir * j, 0, C[2], 0.7 - j * 0.08);
                p.Glow(x, -1, 5, C[4], 0.25 * f.k + 0.1);
            }
            // the goo string it pulls off the floor as it leaves; when it snaps, a drip falls back and the rest pulls up
            if (t >= T_LIFT && t < SNAP)
            {
                double bot = P.by - 1, u = (t - T_LIFT) / (SNAP - T_LIFT);
                for (double y = Math.Floor(bot); y < 0; y++)
                {
                    double m = Px.Sin(((y - bot) / -bot) * Math.PI);
                    p.Px(-1, y, C[4]);
                    if (m * u < 0.45) p.Px(0, y, C[3]);
                }
            }
            else if (t >= SNAP && t < SNAP + 0.3)
            {
                double a2 = t - SNAP, ys = PoseAt(SNAP, hop).by - 1;
                Flight f = Toss(-1, ys * 0.45, 0, 0, a2, G * 1.3, -2);
                if (!f.landed)
                {
                    p.Px(-1, f.y, C[5]);
                    p.Px(-1, f.y - 1, C[3]);
                }
                double nub = 3 * (1 - a2 / 0.12);
                for (int j = 0; j < nub; j++) p.Px(-1, P.by + j, j == 0 ? C[3] : C[4]);
            }
        }
    }
}
