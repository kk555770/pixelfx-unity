// The slanted streaks landing with a ring and a kick of bits follow Sprite Fusion's open-source rain
// (https://www.spritefusion.com/pixel-art-vfx/rain), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class StarFall : Effect
    {
        const int N = 11;
        const double SPREAD = 46, H = 124, SLANT = 0.38, TRAIL = 0.17;

        public static readonly Module Module = new Module("star-fall", "pixelfx/fx/arcane/star-fall", 232, 172, 124, 150, 2.5, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 13 },
            (o, e) => new StarFall(o, e))
        {
            Title = "Star fall",
            Desc = "Stars streak down on long trails across an area, each landing with a white flash, a ring across the floor and a kick of sparks.",
            Tags = new[] { "arcane", "star", "meteor", "rain", "aoe", "magic", "ground", "one-shot" },
            Bg = "ground",
        };

        sealed class Star
        {
            public double x, y, sx, sy, t0, T, ph;
            public bool landed, big;
        }

        sealed class Hit
        {
            public double x, y, t;
            public bool big;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Star[] stars = new Star[N];
        readonly List<Hit> hits = new List<Hit>();
        double t;

        StarFall(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            // where they land: spread over the area in a shuffled order, a little in front of or behind the line
            var xs = new double[N];
            for (int i = 0; i < N; i++) xs[i] = -SPREAD + ((i + R.Range(0.2, 0.8)) * 2 * SPREAD) / N;
            for (int i = N - 1; i > 0; i--)
            {
                int j = R.Int(0, i);
                double tmp = xs[i];
                xs[i] = xs[j];
                xs[j] = tmp;
            }
            for (int i = 0; i < N; i++)
            {
                double x = xs[i], y = R.Range(-3, 2), h = H * R.Range(0.85, 1);
                var s = new Star { x = x, y = y, sx = x - h * SLANT, sy = y - h, t0 = 0.04 + i * 0.105 + R.Range(0, 0.05) };
                s.T = R.Range(0.3, 0.4);
                s.big = R.Chance(0.35);
                s.ph = R.Range(0, TAU);
                stars[i] = s;
            }
        }

        static (double x, double y, double q) At(Star s, double tt)
        {
            double q = Clamp((tt - s.t0) / s.T), e = q * (0.35 + 0.65 * q);
            return (s.sx + (s.x - s.sx) * e, s.sy + (s.y - s.sy) * e, q);
        }

        void DrawStar(Painter p, Star s)
        {
            var (x, y, _) = At(s, t);
            // the trail: a long tapered streak back up its path, white at the head, thinning into violet
            const int n = 20;
            for (int k = n; k >= 1; k--)
            {
                var (ax, ay, _) = At(s, t - (k * TRAIL) / n);
                var (bx, by, _) = At(s, t - ((k - 1) * TRAIL) / n);
                double u = (double)k / n;
                if (t - (k * TRAIL) / n < s.t0) continue;
                uint c = u < 0.15 ? WHITE : u < 0.4 ? C[6] : u < 0.7 ? C[5] : C[4];
                // a second line beside the first near the head gives it body; the tail thins to a dithered thread
                if (u < (s.big ? 0.55 : 0.35)) p.Line(ax + 1, ay, bx + 1, by, u < 0.2 ? C[6] : C[5], 1.1 - u * 1.4);
                p.Line(ax, ay, bx, by, c, 1.3 - u);
                if (k % 3 == 0) p.Glow(ax, ay, 7 * (1 - u) + 2, C[5], 0.3 * (1 - u));
            }
            // the head: a twinkling four-point star
            if (s.big) Sim.Flare(p, x, y, 7 + 1.5 * Px.Sin(t * 40 + s.ph), 0.8, C);
            else Sim.Sparkle(p, x, y, 3.5 + Px.Sin(t * 40 + s.ph), C[6], C[5], 0.6);
            p.Circle(x, y, s.big ? 1.6 : 1.1, WHITE);
            p.Glow(x, y, s.big ? 18 : 13, C[5], 0.6);
            p.Glow(x, y, 5, WHITE, 0.35);
        }

        void DrawHit(Painter p, Hit h, bool far)
        {
            double k = (t - h.t) / 0.45;
            if (k < 0 || k >= 1) return;
            Sim.Band(p, h.x, h.y, 3 + (h.big ? 22 : 16) * Ease.OutCubic(k), 0.26, Math.Max(1, 2.5 * (1 - k)), 1.15 - k, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) < 0 == far, k < 0.2);
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Star s in stars)
            {
                if (t < s.t0 || s.landed) continue;
                var (x, y, q) = At(s, t);
                if (R.Chance(0.55)) ps.Spawn(P.Star, x + R.Range(-1.5, 1.5), y + R.Range(-1.5, 1.5), R.Range(-12, 12), R.Range(-8, 4), R.Range(0.35, 0.7), R.Pick(new[] { WHITE, C[6], C[5] }), R.Chance(0.7) ? R.Range(0.4, 0.9) : R.Range(1.2, 1.8));
                if (q >= 1)
                {
                    s.landed = true;
                    hits.Add(new Hit { x = s.x, y = s.y, t = t, big = s.big });
                    lights.Add(s.x, s.y - 12, s.big ? 46 : 38, C[5], 0.9, 0.32);
                    lights.Add(s.x, s.y - 3, 16, WHITE, 0.8, 0.08);
                    for (int i = 0; i < (s.big ? 14 : 9); i++)
                    {
                        double a = -Math.PI / 2 + R.Range(-1.35, 1.35), sp = R.Range(50, 150);
                        ps.Spawn(P.Spark, s.x, Math.Min(-1, s.y - 1), Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.5));
                    }
                    for (int i = 0; i < 4; i++) ps.Spawn(P.Star, s.x + R.Range(-4, 4), s.y - R.Range(2, 8), R.Range(-15, 15), -R.Range(10, 35), R.Range(0.4, 0.8), R.Pick(new[] { WHITE, C[6] }), R.Range(0.8, 2));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the area lit beneath while they fall
            double k = Clamp(t / 0.3) * (1 - Clamp((t - 1.7) / 0.7));
            if (k > 0)
                foreach (int x in new[] { -36, 0, 36 })
                    p.Glow(x, -1, 40, C[4], 0.14 * k);
            foreach (Hit h in hits) DrawHit(p, h, true);
            // a glowing mark where each landed, cooling away
            foreach (Hit h in hits)
            {
                double k2 = 1 - (t - h.t) / 0.7;
                if (k2 <= 0) continue;
                p.Dot(h.x, h.y, k2 > 0.6 ? WHITE : C[6], k2 * 1.5);
                foreach (int dx in new[] { -1, 1 }) p.Dot(h.x + dx, h.y, C[5], k2 * 1.2);
                p.Glow(h.x, h.y - 1, 10, C[5], 0.35 * k2);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            foreach (Star s in stars)
                if (t >= s.t0 && !s.landed) DrawStar(p, s);
            foreach (Hit h in hits)
            {
                DrawHit(p, h, false);
                double k = (t - h.t) / 0.22;
                if (k < 0 || k >= 1) continue;
                if (k < 0.35) p.Circle(h.x, h.y - 2, 5 * (1 - k / 0.35) + 1, WHITE);
                Sim.Flare(p, h.x, h.y - 2, (h.big ? 16 : 12) * Px.Pow(1 - k, 1.5) + 2, 1 - k, C);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
