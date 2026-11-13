// The motes drifting in the light and the glints follow Sprite Fusion's open-source holy-light and sparkle
// (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class Iris : Effect
    {
        const double D = 2.2;

        public static readonly Module Module = Screen("iris", "Iris",
            "A lit iris closes onto a warm spotlight where dust drifts, holds as its rim twinkles, snaps shut into a glint, then pops open from a flash with a white-hot rim throwing sparks.",
            new[] { "iris", "circle", "spotlight", "wipe" }, D,
            new Options
            {
                ["palette"] = new[] { "#1c140c", "#3b2a18", "#6e5233", "#a8875a", "#d9bd8a", "#f3e2bd", "#fff7e8", "#ffffff" },
                ["focus"] = new double[] { 160, 100 },
                ["seed"] = 1,
            },
            (o, e) => new Iris(o, e));

        const double CLOSE = 0.62, SNAP = 0.9, SHUT = 0.99, POP = 1.2, OPEN = 1.75, RS = 24, RW = 5, GLO = 16;

        readonly uint[] C;
        readonly uint ink;
        readonly Rng R;
        readonly double fx, fy, R0;
        readonly Particles ps;
        readonly Lights lights;
        readonly double end = D - 0.05;
        double t, prevR;
        bool shut, popped;

        Iris(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            ink = o.Color("color");
            R = new Rng(o.Seed);
            var focus = (double[])o["focus"];
            fx = focus[0];
            fy = focus[1];
            R0 = Math.Max(Math.Max(Hypot(0 - fx, 0 - fy), Hypot(W - fx, 0 - fy)), Math.Max(Hypot(0 - fx, H - fy), Hypot(W - fx, H - fy))) + RW + 2;
            ps = new Particles(rng: R, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            prevR = R0;
        }

        // the hole's radius: closing, breathing on the spotlight, snapping shut; then popping open
        double Radius(double t) =>
            t < CLOSE ? RS + (R0 - RS) * (1 - Ease.InOutCubic(t / CLOSE))
            : t < SNAP ? RS + Px.Sin((t - CLOSE) * 22) * 0.8 * (1 - (t - CLOSE) / (SNAP - CLOSE))
            : t < SHUT ? RS * (1 - Ease.InCubic((t - SNAP) / (SHUT - SNAP)))
            : t < POP ? 0 : R0 * Ease.OutQuart(Clamp((t - POP) / (OPEN - POP)));

        public override void Update(double dt)
        {
            t += dt;
            double r = Radius(t);
            // dust drifts up through the spotlight while it holds
            if (t > CLOSE - 0.25 && t < SNAP && R.Chance(0.25))
            {
                double a = R.Range(0, TAU), d = Math.Sqrt(R.Next()) * r * 0.9;
                ps.Spawn(P.Ember, fx + Px.Cos(a) * d, fy + Px.Sin(a) * d, R.Range(-6, 6), -R.Range(4, 14), R.Range(0.4, 0.8), 0, 1);
            }
            // the rim twinkles as it holds
            if (t > CLOSE - 0.1 && t < SNAP && R.Chance(0.3))
            {
                double a = R.Range(0, TAU);
                ps.Spawn(P.Star, fx + Px.Cos(a) * (r + 1), fy + Px.Sin(a) * (r + 1), 0, 0, R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.4));
            }
            if (!shut && t >= SHUT)
            {
                shut = true;
                lights.Add(fx, fy, 50, C[5], 0.7, 0.3);
            }
            if (!popped && t >= POP)
            {
                // pop: a white flash, sparks thrown out of the opening
                popped = true;
                lights.Add(fx, fy, 140, C[5], 0.7, 0.45);
                lights.Add(fx, fy, 50, WHITE, 0.9, 0.12);
                for (int i = 0; i < 40; i++)
                {
                    double a = R.Range(0, TAU), v = R.Range(150, 420);
                    ps.Spawn(P.Spark, fx + Px.Cos(a) * 3, fy + Px.Sin(a) * 3, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.2, 0.5));
                }
            }
            // as the rim races out it sheds glinting motes that fly on and fade
            if (t > POP && t < OPEN - 0.1)
            {
                double vr = (r - prevR) / dt;
                for (int k = 0; k < 2; k++)
                {
                    if (!R.Chance(0.6)) continue;
                    double a = R.Range(0, TAU), x = fx + Px.Cos(a) * r, y = fy + Px.Sin(a) * r;
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    ps.Spawn(P.Mote, x, y, Px.Cos(a) * vr * 0.35, Px.Sin(a) * vr * 0.35, Math.Min(R.Range(0.2, 0.45), end - t), R.Chance(0.5) ? C[6] : C[5], 0.7);
                }
            }
            prevR = r;
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p0)
        {
            Painter p = ClipTo(p0);
            double r = Radius(t);
            // the rim's heat: warm while it closes, white-hot as it pops, cooling as it opens
            double hot = t >= POP ? 1 - Smooth(POP, OPEN, t) : t < SNAP ? 0.45 + 0.25 * Smooth(CLOSE - 0.2, CLOSE, t) : 0.7;
            double rw = RW + (t >= POP ? 3 * hot : 0);
            if (t < OPEN + 0.05 && !(t >= SHUT && t < POP && r <= 0))
            {
                for (int y = 0; y < H; y++)
                {
                    double dy = y + 0.5 - fy, xo = Math.Sqrt(Math.Max(0, Px.Pow(r + rw, 2) - dy * dy));
                    if (xo <= 0)
                    {
                        Rect(p0, 0, y, W, 1, ink);
                        continue;
                    }
                    double a0 = Clamp(Math.Floor(fx - xo), 0, W), b1 = Clamp(Math.Ceiling(fx + xo), 0, W);
                    if (a0 > 0) Rect(p0, 0, y, a0, 1, ink);
                    if (b1 < W) Rect(p0, b1, y, W - b1, 1, ink);
                    // the rim band and the light just inside it
                    double g = r > GLO ? Math.Sqrt(Math.Max(0, Px.Pow(r - GLO, 2) - dy * dy)) : 0;
                    for (double x = a0; x < b1; x++)
                    {
                        double dx = x + 0.5 - fx;
                        if (Math.Abs(dx) < g) continue;
                        double d = Hypot(dx, dy) - r;
                        if (d >= 0)
                        {
                            // a bevel lit from the top left: bright there, a step darker round the far side
                            double q = d / rw, lit = 0.5 - 0.5 * (dx * 0.7 + dy * 0.7) / (r + d + 0.01), h = hot * (0.55 + 0.6 * lit);
                            p0.Px(x, y, q >= 1 ? ink : q < 0.2 ? (h > 0.5 ? WHITE : h > 0.3 ? C[6] : C[5]) : q < 0.4 ? (h > 0.38 ? C[6] : C[5]) : q < 0.65 ? (h > 0.3 ? C[5] : C[4]) : q < 0.85 ? C[3] : C[2]);
                            if (q < 0.5) p0.Light(x, y, C[5], 0.45 * h * (1 - q * 2));
                        }
                        else if (d > -GLO) p0.Light(x, y, C[5], (0.18 + 0.3 * hot) * Px.Pow(1 + d / GLO, 2));
                    }
                }
            }
            else if (t >= SHUT && t < POP) Rect(p0, 0, 0, W, H, ink);
            // the spotlight's pool of light, gathering as the iris closes, hottest at its heart
            if (r > 0 && r < 80 && t < SHUT)
            {
                double k = 1 - r / 80;
                p.Glow(fx, fy, r * 1.15 + 6, C[5], 0.18 + 0.32 * k);
                p.Glow(fx, fy, r * 0.6 + 3, C[6], 0.25 * k);
            }
            // light catches the iris as it turns: a glint runs once round the rim while it closes
            if (t > 0.08 && t < SNAP)
            {
                double a = -2.4 + ((t - 0.08) / (SNAP - 0.08)) * TAU, gx = fx + Px.Cos(a) * (r + 1), gy = fy + Px.Sin(a) * (r + 1);
                if (gx > 2 && gy > 2 && gx < W - 2 && gy < H - 2) Glint(p, gx, gy, 7 + 3 * Px.Sin(t * 9), t * 3, C[6], C[5], 0.9);
            }
            // snapping shut: the light collapses into a glint that lingers in the dark
            double s = t - SHUT;
            if (s > -0.06 && s < 0.22) Glint(p, fx, fy, 14 * (1 - Math.Abs(s + 0.01) / 0.23), s * 4, C[6], C[5]);
            // popping open: a flash at the heart
            double u = t - POP;
            if (u >= 0 && u < 0.18)
            {
                Glint(p, fx, fy, 20 * (1 - u / 0.18), 0.4 - u * 3, C[6], C[5]);
                p.Glow(fx, fy, 60, WHITE, 0.6 * (1 - u / 0.18));
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
