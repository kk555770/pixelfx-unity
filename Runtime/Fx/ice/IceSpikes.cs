// The eruption recipe (a frost line racing ahead, each spike punching up white with a spray of shards, a puff
// and a glint, the floor frosting behind) is adapted from Sprite Fusion's open-source ice
// (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class IceSpikes : Effect
    {
        const double DURATION = 3;

        public static readonly Module Module = new Module("ice-spikes", "pixelfx/fx/ice/ice-spikes", 264, 112, 24, 96, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["reach"] = 172,
                ["height"] = 56,
                ["seed"] = 3,
            },
            (o, e) => new IceSpikes(o, e))
        {
            Title = "Ice spikes",
            Desc = "A frost line races along the floor and clusters of jagged ice blades burst up behind it in a rising wave, glint, flash white together and shatter into toppling shards.",
            Tags = new[] { "ice", "spikes", "ground", "wave", "directional", "magic", "one-shot" },
            Bg = "ground",
        };

        const double START = 0.04, RUN = 0.44; // the frost line runs out
        const double SHIMMER = 0.74; // a glint runs down the line
        const double TREMBLE = 1.2, FLASH = 1.36, SHATTER = 1.42; // they shiver, flash white together and break
        const double MELT = 1.75; // the frost goes, oldest first

        sealed class BladeDef
        {
            public double dx, h, w, lean, delay, jag;
            public bool back, main;
        }

        sealed class Cluster
        {
            public double x, q, H, W, t0;
            public BladeDef[] blades;
            public BladeDef main;
            public bool up;
        }

        struct BladePose
        {
            public double g, x, tx, ty;
            public bool white;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double REACH;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Panes panes;
        readonly Cluster[] clusters;
        readonly Cluster crest;
        double t;
        bool broke, trembled;

        IceSpikes(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            REACH = o.Num("reach");
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            panes = new Panes(floor,
                onCrumble: q =>
                {
                    Splinter(q.x, 0, 0, 3);
                    ps.Spawn(P.Vapour, q.x, -1, R.Range(-12, 12), -R.Range(2, 8), R.Range(0.4, 0.7), C[6], R.Range(1.4, 2.2));
                },
                onLand: (q, x, y, v) =>
                {
                    if (v < 110) return;
                    ps.Spawn(P.Vapour, x, y - 1, R.Range(-20, 20), -R.Range(2, 8), R.Range(0.4, 0.7), C[6], R.Range(1.4, 2.4));
                    Splinter(x, y, q.vx, 3);
                    if (R.Chance(0.3)) ps.Spawn(P.Star, x, y - 2, 0, -5, R.Range(0.2, 0.4), WHITE, R.Range(0.8, 1.6));
                },
                emitter: e);

            // clusters along the line: a main blade leaning into the wave and two to four splayed round it, heights
            // swelling to a crest past the middle and falling off at the end
            double height = o.Num("height");
            var list = new List<Cluster>();
            for (double x = 10; x < REACH - 2; x += R.Range(20, 26))
            {
                double q = x / REACH, H = height * Env(q) * R.Range(0.88, 1.06), W = Clamp(H * 0.36, 5, 14), lean = H * R.Range(0.2, 0.34);
                // a big blade leaning into the wave, a smaller one behind it leaning further, a stub in front of its foot
                var blades = new List<BladeDef>
                {
                    new BladeDef { dx = W * R.Range(0.35, 0.6), h = H * R.Range(0.5, 0.7), w = W * 0.7, lean = H * R.Range(0.4, 0.6), back = true, delay = R.Range(0.02, 0.05), jag = R.Chance(0.5) ? R.Range(0.35, 0.6) : -1 },
                    new BladeDef { dx = 0, h = H, w = W, lean = lean, back = false, delay = 0, jag = R.Chance(0.7) ? R.Range(0.32, 0.58) : -1, main = true },
                };
                if (H > 14) blades.Add(new BladeDef { dx = -W * R.Range(0.35, 0.55), h = H * R.Range(0.24, 0.4), w = W * 0.55, lean = -H * R.Range(0, 0.12), back = false, delay = R.Range(0.03, 0.07), jag = -1 });
                list.Add(new Cluster { x = x, q = q, H = H, W = W, blades = blades.ToArray(), main = blades[1], t0 = ReachedAt(x) + 0.012, up = false });
            }
            clusters = list.ToArray();
            crest = clusters.Aggregate(clusters[0], (a, c) => c.H > a.H ? c : a);
        }

        // swelling to a crest past the middle
        static double Env(double q) => 0.28 + 0.72 * Exp(-Px.Pow((q - 0.64) / 0.3, 2));
        double Front(double tt) => REACH * (1 - Px.Pow(1 - Seg(tt, START, START + RUN), 1.7));
        double ReachedAt(double x) => START + RUN * (1 - Px.Pow(1 - Clamp(x / REACH), 1 / 1.7));

        void Splinter(double x, double y, double vx, int n)
        {
            for (int i = 0; i < n; i++)
                bits.Add(new Bits.Bit { kind = B.Shard, x = x + R.Range(-2, 2), y = y - 1, vx = vx * 0.5 + R.Range(-45, 45), vy = -R.Range(30, 100), g = 0.8, drag = 0.6, bounce = 0.35, life = R.Range(0.4, 0.75), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1.2, 2.4), c = C[R.Int(4, 7)] });
        }

        // a blade's base, tip and look right now
        BladePose PoseOf(Cluster c, BladeDef b)
        {
            double g = Ease.OutBack(Clamp((t - c.t0 - b.delay) / 0.09), 2.2), x = c.x + b.dx;
            return new BladePose { g = g, x = x, tx = x + b.lean * g, ty = -b.h * g, white = t - c.t0 - b.delay < 0.03 || (t >= FLASH && t < SHATTER) };
        }

        void DrawClusters(Painter p)
        {
            if (t >= SHATTER) return;
            for (int pass = 0; pass < 3; pass++)
                foreach (Cluster c in clusters)
                {
                    if (t < c.t0) continue;
                    double jx = t >= TREMBLE && t < FLASH ? ((Math.Floor(t * 40) + Round(c.x)) % 2 != 0 ? 1 : 0) : 0;
                    foreach (BladeDef b in c.blades)
                    {
                        if ((pass == 0) != b.back || (pass == 1) != b.main) continue;
                        BladePose s = PoseOf(c, b);
                        if (s.g <= 0) continue;
                        double gl = (t - SHIMMER - c.q * 0.34) / 0.2;
                        Blade(p, s.x + jx, 0, s.tx + jx, s.ty, b.w, C, white: s.white, jag: b.jag, dim: b.back ? 1 : 0, glint: gl >= 0 && gl <= 1 ? gl : -1);
                    }
                    if (pass == 2)
                    {
                        // light: a pop at the base as it bursts, a cold glow up its length while it stands
                        double pop = Clamp(1 - (t - c.t0) / 0.15);
                        bool wh = t >= FLASH;
                        p.Glow(c.x + c.main.lean * 0.4, -c.H * 0.45, c.H * 0.9 + 10, C[4], (0.16 + 0.42 * pop + (wh ? 0.4 : 0)) * (0.85 + 0.15 * Px.Sin(t * 3 + c.x)));
                        if (pop > 0) p.Glow(c.x, -2, 14, WHITE, 0.55 * pop);
                    }
                }
        }

        // the frosted floor: a crust with rime tufts, a bloom under the surface, a white-hot head while it runs
        void DrawFrost(Painter p)
        {
            double fr = Front(t);
            for (int x = -3; x <= Math.Min(fr, REACH + 6); x++)
            {
                double q = Clamp(x / REACH), age = t - ReachedAt(Math.Max(0, x)), a = 1 - Smooth(MELT + q * 0.35, MELT + q * 0.35 + 0.6, t);
                if (a <= 0 || age < 0) continue;
                double h = Rand(x, 17), grow = Smooth(0, 0.3, age);
                bool head = fr - x < 3 && fr < REACH;
                p.Dot(x, 0, head || age < 0.06 || h < 0.3 ? WHITE : C[6], a);
                double L = h < 0.45 ? Round(grow * (1 + Rand(x, 5) * 2.6)) : 0;
                for (int j = 1; j <= L; j++) p.Dot(x, -j, j == L ? WHITE : C[5], a);
                if (L > 1 && Rand(x, 9) < 0.5) p.Dot(x + 1, -L + 1, C[6], a);
                double D = grow * (2 + Rand(x, 11) * 4) * (1 - q * 0.3);
                for (int j = 1; j <= D; j++) p.Dot(x, j, Rand(x * 31 + j, 7) < 0.06 ? WHITE : j < 2 ? C[5] : j < 4 ? C[4] : C[3], a * (1.1 - j / (D + 1)));
                if (head) p.Dot(x, -1, WHITE);
            }
            if (fr < REACH && t > START)
            {
                p.Glow(fr, -2, 16, C[6], 0.6);
                p.Glow(fr, -1, 7, WHITE, 0.6);
            }
        }

        void BreakBlade(Cluster c, BladeDef b)
        {
            BladePose s = PoseOf(c, b);
            double ax = s.tx - s.x, ay = s.ty, L = Hypot(ax, ay);
            if (L == 0 || double.IsNaN(L)) L = 1;
            double nx = -ay / L, ny = ax / L, hw = b.w / 2;
            double[][] tri = { new[] { s.x - nx * hw, -ny * hw }, new[] { s.tx, s.ty }, new[] { s.x + nx * hw, ny * hw } };
            double ang = Atan2(ay, ax) + Math.PI / 2;
            var parts = new List<double[][]> { tri };
            if (b.h > 9) parts = parts.SelectMany(poly => SplitPoly(poly, s.x + ax * 0.4, ay * 0.4, ang)).ToList();
            if (b.h > 22) parts = parts.SelectMany(poly => poly.Any(q => q[1] < ay * 0.5) ? SplitPoly(poly, s.x + ax * 0.72, ay * 0.72, ang) : new List<double[][]> { poly }).ToList();
            foreach (double[][] part in parts)
            {
                double mx = 0, my = 0;
                foreach (double[] q in part)
                {
                    mx += q[0];
                    my += q[1];
                }
                mx /= part.Length;
                my /= part.Length;
                double up = Clamp(-my / 40);
                panes.Add(new Pane
                {
                    pts = part.Select(q => new[] { q[0] - mx, q[1] - my }).ToArray(),
                    x = mx,
                    y = my,
                    vx = R.Range(-25, 60),
                    vy = -R.Range(40, 130) * (0.5 + up),
                    a = 0,
                    va = R.Range(-8, 8),
                    f = R.Range(-0.7, 0.7),
                    vf = R.Range(-6, 6),
                    lit = b.back ? 0.45 : 0.75,
                    g = 0.9,
                    drag = 0.6,
                    bounce = 0.3,
                    solid = true,
                    ridge = Atan2(ay, ax),
                    brittle = b.h > 30 ? 2 : 1,
                    crumble = R.Range(0.12, 0.4),
                    life = R.Range(1, 1.4),
                });
            }
            for (int i = 0; i < 2 + b.h / 7; i++)
            {
                double u = R.Next();
                bits.Add(new Bits.Bit { kind = R.Chance(0.15) ? B.Chunk : B.Shard, x = s.x + ax * u + R.Range(-1.5, 1.5), y = ay * u, vx = R.Range(-60, 90), vy = -R.Range(60, 230), g = 0.9, drag = 0.6, bounce = 0.3, life = R.Range(0.7, 1.3), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1.2, 3.2), c = C[R.Int(4, 7)], ramp = new[] { C[1], C[3], C[4], C[6], C[7] } });
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the cast: a puff and a spray where the line starts
            if (t - dt <= START && t > START)
            {
                for (int i = 0; i < 4; i++) ps.Spawn(P.Vapour, R.Range(0, 6), -R.Range(0, 3), R.Range(10, 60), -R.Range(2, 12), R.Range(0.4, 0.7), C[6], R.Range(2, 3.2));
                lights.Add(4, -4, 26, C[5], 0.6, 0.25);
            }
            // the running front throws frost: streaks along the floor, motes, vapour
            double fr = Front(t);
            if (t > START && fr < REACH - 1)
            {
                if (R.Chance(0.5)) ps.Spawn(P.Streak, fr - R.Range(4, 12), -R.Range(1, 3), R.Range(40, 80), -R.Range(0, 6), R.Range(0.08, 0.14), R.Chance(0.5) ? C[6] : WHITE);
                if (R.Chance(0.3)) ps.Spawn(P.Flake, fr, -1, R.Range(10, 50), -R.Range(20, 70), R.Range(0.5, 0.9), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.2));
                if (R.Chance(0.12)) ps.Spawn(P.Vapour, fr, -R.Range(0, 3), R.Range(20, 60), -R.Range(2, 10), R.Range(0.4, 0.7), C[6], R.Range(1.6, 2.8));
            }
            foreach (Cluster c in clusters)
            {
                if (c.up || t < c.t0) continue;
                c.up = true;
                // each cluster bursts up: shards sprayed forward, a puff, motes, a glint at the tip once it stands
                bool big = c == crest;
                double n = 3 + Round(c.H / 8);
                for (int i = 0; i < n; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.0), sp = R.Range(60, 190) * (0.45 + c.H / 44);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = c.x + R.Range(-3, 3), y = -1, vx = Px.Cos(a) * sp + 25, vy = Px.Sin(a) * sp, g = 0.8, drag = 0.6, bounce = 0.3, life = R.Range(0.6, 1.1), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1.2, 3), c = C[R.Int(5, 7)] });
                }
                ps.Spawn(P.Vapour, c.x, -1, R.Range(-10, 40), -R.Range(2, 10), R.Range(0.5, 0.9), C[6], R.Range(2.2, 3.4) * (0.6 + c.H / 60));
                for (int i = 0; i < 2; i++) ps.Spawn(P.Flake, c.x + R.Range(-4, 4), -R.Range(2, c.H * 0.5), R.Range(-20, 40), -R.Range(20, 60), R.Range(0.6, 1.1), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.3));
                if (c.H > 16) ps.Spawn(P.Star, c.x + c.main.lean, -c.H * 0.96, 0, -4, R.Range(0.3, 0.5), WHITE, R.Range(1.4, 2.6), 0.08);
                lights.Add(c.x, -c.H * 0.4, 16 + c.H * 0.6, C[5], 0.5, 0.18);
                if (big)
                {
                    lights.Add(c.x, -c.H * 0.5, 54, WHITE, 0.5, 0.14);
                    for (int i = 0; i < 4; i++) ps.Spawn(P.Vapour, c.x + R.Range(-6, 6), -R.Range(0, 4), R.Range(-40, 60), -R.Range(4, 16), R.Range(0.6, 1), C[6], R.Range(3, 4.5));
                }
            }
            // while they stand: glints wink on the blades
            if (t > START + RUN && t < TREMBLE && R.Chance(0.09))
            {
                Cluster c = R.Pick(clusters);
                BladeDef b = R.Pick(c.blades);
                BladePose s = PoseOf(c, b);
                double u = R.Range(0.3, 0.95);
                ps.Spawn(P.Star, s.x + (s.tx - s.x) * u, s.ty * u, 0, -3, R.Range(0.25, 0.5), R.Chance(0.6) ? WHITE : C[6], R.Range(0.6, 2));
            }
            // they shiver: chips fall
            if (!trembled && t >= TREMBLE)
            {
                trembled = true;
                foreach (Cluster c in clusters)
                    for (int i = 0; i < 2; i++)
                        bits.Add(new Bits.Bit { kind = B.Shard, x = c.x + R.Range(-4, 4), y = -R.Range(4, c.H * 0.8), vx = R.Range(-20, 20), vy = -R.Range(0, 30), g = 0.8, drag = 0.5, bounce = 0.3, life = R.Range(0.3, 0.6), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1, 1.8), c = C[R.Int(5, 7)] });
            }
            if (!broke && t >= SHATTER)
            {
                broke = true;
                // all at once: every blade breaks into toppling pieces, splinters and ice dust, light along the line
                foreach (Cluster c in clusters)
                {
                    foreach (BladeDef b in c.blades) BreakBlade(c, b);
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Vapour, c.x + R.Range(-6, 6), -R.Range(1, 8), R.Range(-30, 50), -R.Range(4, 18), R.Range(0.6, 1.1), C[6], R.Range(2.4, 4) * (0.7 + c.H / 60));
                    if (R.Chance(0.7)) ps.Spawn(P.Star, c.x + R.Range(-6, 6), -R.Range(4, c.H), R.Range(-10, 10), -R.Range(6, 20), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.2));
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Flake, c.x + R.Range(-6, 6), -R.Range(4, c.H), R.Range(-30, 60), -R.Range(30, 90), R.Range(0.9, 1.5), R.Chance(0.6) ? WHITE : C[6], R.Range(0.5, 1.5));
                    lights.Add(c.x, -c.H * 0.5, 22 + c.H, WHITE, 0.45, 0.12);
                }
                for (int i = 0; i < 5; i++) lights.Add((REACH * (i + 0.5)) / 5, -18, 40, C[4], 0.4, 0.5);
            }
            // the shatter's dust settles; glints on the debris
            if (broke && t < SHATTER + 0.8 && R.Chance(0.1) && bits.List.Count > 0)
            {
                Bits.Bit b = R.Pick(bits.List.ToArray());
                ps.Spawn(P.Star, b.x, b.y - 1, 0, -3, R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.5));
            }
            ps.Update(dt);
            bits.Update(dt);
            panes.Update(dt);
            lights.Update(dt);
            EndBy(t, DURATION, ps, bits, panes);
        }

        public override void DrawBack(Painter p)
        {
            DrawFrost(p);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            DrawClusters(p);
            // the cold light the frosted floor gives off
            double k = (1 - Smooth(MELT, MELT + 0.9, t)) * Smooth(START, START + 0.3, t), fr = Front(t);
            for (int i = 0; i < 6; i++) p.Glow((fr * (i + 0.5)) / 6, -3, 26, C[4], 0.15 * k);
            if (t >= FLASH && t < SHATTER + 0.1)
                foreach (Cluster c in clusters) Sim.Sparkle(p, c.x + c.main.lean, -c.H, 6 * (1 - Clamp((t - FLASH) / 0.16)), C[6], C[4], 0.4);
            panes.Draw(p, C);
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
