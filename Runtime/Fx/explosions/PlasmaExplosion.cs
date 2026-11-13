// The release (flash, rings, flare, light) and the arcs are adapted from Sprite Fusion's open-source energy
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class PlasmaExplosion : Effect
    {
        const int W = 224, H = 200, OX = 112, OY = 100;

        public static readonly Module Module = new Module("plasma-explosion", "pixelfx/fx/explosions/plasma-explosion", W, H, OX, OY, 1.7, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0a0828", "#1a1c6e", "#2c3cc0", "#4a7cf0", "#7cc0ff", "#c8ecff", "#ffffff" },
                ["accent"] = new[] { "#1c0a30", "#4a1a8c", "#8a3ce0", "#c48cff", "#f0d8ff" },
                ["seed"] = 7,
            },
            (o, e) => new PlasmaExplosion(o, e))
        {
            Title = "Plasma explosion",
            Desc = "A blue-violet energy blast with no smoke: a white flash, a ball of plasma swelling into a thinning shell, shock rings in and out of depth, lightning arcs crackling off it.",
            Tags = new[] { "explosion", "plasma", "energy", "electric", "magic", "sci-fi", "one-shot" },
            Bg = "dark",
        };

        const double BALL = 0.55;

        readonly uint[] C, V;
        readonly double seed;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        double t;

        PlasmaExplosion(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            V = Fit(o.Ramp("accent"), 5);
            seed = o.Seed;
            R = new Rng(o.Seed);
            ps = new Particles(rng: R, sparks: new[] { C[6], C[5], C[4], V[3] }, emitter: e);
            lights = new Lights(e);
            lights.Add(0, 0, 96, C[4], 1, 0.6);
            lights.Add(0, 0, 40, WHITE, 0.9, 0.1);
            lights.Add(0, 0, 70, V[2], 0.5, 1.1);
            for (int i = 0; i < 46; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(140, 360);
                ps.Spawn(P.Mote, Px.Cos(a) * 4, Px.Sin(a) * 4, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.42), R.Pick(new[] { C[6], C[5], C[4], V[3] }), R.Range(0.7, 1.1));
            }
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(40, 140);
                ps.Spawn(P.Flake, Px.Cos(a) * 10, Px.Sin(a) * 10, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.8, 1.5), R.Pick(new[] { C[5], V[3], V[4] }), R.Range(0.6, 1.6));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the shell sheds sparkles and motes as it thins
            if (t > 0.12 && t < 0.9 && R.Chance(30 * dt))
            {
                double a = R.Range(0, TAU), r = Shell(t);
                ps.Spawn(P.Star, Px.Cos(a) * r, Px.Sin(a) * r, Px.Cos(a) * R.Range(10, 40), Px.Sin(a) * R.Range(10, 40), R.Range(0.3, 0.6), R.Chance(0.5) ? C[5] : V[4], R.Range(0.5, 1.8));
            }
            ps.Update(dt);
            Cull(ps, -OX + 3, -OY + 3, W - OX - 3, H - OY - 3);
            lights.Update(dt);
        }

        // the tilted ring's far half, behind the ball
        public override void DrawBack(Painter p) => Tilted(p, t, C, V, true);

        public override void Draw(Painter p)
        {
            // the ball: a white heart, cyan and blue bands, a violet rim boiling at the edge; it swells, then hollows out
            if (t < BALL)
            {
                double u = t / BALL, r = Shell(t), hollow = Smooth(0.35, 1, u), boil = Math.Floor(t * 20);
                p.Shade(-r - 3, -r - 3, r + 3, r + 3, (x, y, o) =>
                {
                    double d = Hypot(x, y), a = Atan2(y, x), edge = r * (1 + 0.1 * Noise(a * 3 + boil * 0.7, 3));
                    if (d > edge) return 0;
                    double v = d / edge;
                    if (v < hollow * 0.92) return 0;
                    // turbulent bands swirling round the heart, and bright filaments twisting through them
                    double n = Noise(a * 4 + d * 0.25 + boil * 0.5, 5), w = v + n * 0.14;
                    bool fil = Math.Abs(Px.Sin(a * 5 + d * 0.32 - t * 14 + n * 2.5)) < 0.1;
                    o.k = 0.25 * (1 - u);
                    if (v > 0.9) return V[2];
                    if (fil && v > 0.3) return u < 0.5 ? WHITE : C[5];
                    return w < 0.32 - u * 0.25 ? WHITE : w < 0.52 ? C[5] : w < 0.72 ? C[4] : w < 0.86 ? (u < 0.5 ? C[3] : V[3]) : V[2];
                });
                p.Glow(0, 0, r * 1.6 + 6, C[4], 0.6 * (1 - u));
            }
            if (t < 0.07) p.Circle(0, 0, 9 - t * 60, WHITE);
            if (t < 0.25) Sim.Flare(p, 0, 0, 52 * Px.Pow(1 - t / 0.25, 1.3), 1 - t / 0.25, C);
            // shock rings: a fast bright one, a slower violet one
            double u1 = t / 0.42;
            if (u1 < 1)
            {
                p.Ring(0, 0, 8 + 84 * Ease.OutCubic(u1), Math.Max(1, 3 * (1 - u1)), u1 < 0.25 ? WHITE : C[5], 1.15 - u1);
                p.GlowRing(0, 0, 8 + 84 * Ease.OutCubic(u1), 5, C[4], 0.35 * (1 - u1));
            }
            double u2 = (t - 0.06) / 0.6;
            if (u2 > 0 && u2 < 1) p.Ring(0, 0, 6 + 60 * Ease.OutCubic(u2), Math.Max(1, 2 * (1 - u2)), V[3], 1.1 - u2);
            // arcs: lightning crackling out of the heart, re-struck every few frames, shorter as the charge runs out
            double left = 1 - Smooth(0.15, 0.85, t), strike = Math.Floor(t * 18);
            if (left > 0)
            {
                Rng r = NewRng(seed, strike, 17);
                double n = 2 + Round(4 * left);
                for (int i = 0; i < n; i++)
                {
                    double a = r.Range(0, TAU), L = r.Range(26, 58) * (0.45 + 0.55 * left);
                    double[][] path = Shapes.Bolt(r, 0, 0, Px.Cos(a) * L, Px.Sin(a) * L, 0.28, 4);
                    Arc(p, path, C, V, 0.8 * left);
                    if (r.Chance(0.5))
                        foreach (double[][] f in Shapes.Forks(r, path, 1, 14, 0.35)) Arc(p, f, C, V, 0.5 * left);
                }
                // and little arcs crawling over the shell
                double rs = Shell(t);
                for (int i = 0; i < 3; i++)
                {
                    double a = r.Range(0, TAU), b = a + r.Range(0.3, 0.6);
                    Arc(p, Shapes.Bolt(r, Px.Cos(a) * rs, Px.Sin(a) * rs, Px.Cos(b) * rs, Px.Sin(b) * rs, 0.4, 3), C, V, 0.6 * left);
                }
            }
            Tilted(p, t, C, V, false);
            ps.Draw(p);
            lights.Draw(p);
        }

        // the ball's radius: punched out fast, then easing
        static double Shell(double t) => 5 + 30 * Ease.OutCubic(Clamp(t / BALL));

        // a ring on a tilted plane racing out round the blast: far half behind, near half in front
        static void Tilted(Painter p, double t, uint[] C, uint[] V, bool back)
        {
            double u = (t - 0.03) / 0.5;
            if (u <= 0 || u >= 1) return;
            Sim.Band(p, 0, 4, 12 + 86 * Ease.OutCubic(u), 0.28, Math.Max(1, 4 * (1 - u)), 1.15 - u, new[] { V[1], V[2], C[4], C[5] }, g => (Px.Sin(g) < 0) == back, u < 0.15);
        }

        // one arc: a soft glow along it, a cyan halo line, a white core
        static void Arc(Painter p, double[][] path, uint[] C, uint[] V, double k)
        {
            if (k <= 0.05) return;
            for (int i = 1; i < path.Length; i++)
            {
                double ax = path[i - 1][0], ay = path[i - 1][1], bx = path[i][0], by = path[i][1];
                p.GlowLine(ax, ay, bx, by, 5, V[2], 0.18 * k);
                p.Line(ax + 1, ay, bx + 1, by, C[4], k * 0.8);
                p.Line(ax, ay, bx, by, i < path.Length * 0.6 ? WHITE : C[5], Math.Min(1, k * 1.4));
            }
        }
    }
}
