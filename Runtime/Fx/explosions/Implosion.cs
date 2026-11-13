// The gathering streaks, the pulse rings and the release (cross flare, rings, light) are adapted from Sprite Fusion's
// open-source energy charge (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class Implosion : Effect
    {
        const int W = 208, H = 184, OX = 104, OY = 92;

        public static readonly Module Module = new Module("implosion", "pixelfx/fx/explosions/implosion", W, H, OX, OY, 1.65, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0a0614", "#1c1236", "#3a2470", "#6a44c0", "#a080f0", "#d8c8ff", "#ffffff" },
                ["seed"] = 2,
            },
            (o, e) => new Implosion(o, e))
        {
            Title = "Implosion",
            Desc = "Everything is sucked in: streaks and grit rush inward, rings contract and a dark sphere collapses onto a crushed white point; a beat of stillness, then a sharp pop.",
            Tags = new[] { "implosion", "energy", "magic", "collapse", "pop", "one-shot" },
            Bg = "dark",
        };

        const double SUCK = 0.8, STILL = 1.02;
        static readonly uint VOID = Hex("#05040a");

        sealed class Speck
        {
            public double a, d, t0, T, spin;
            public uint c;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Speck[] grit;
        readonly double[] rays;
        double t;
        bool popped;

        Implosion(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            R = new Rng(o.Seed);
            ps = new Particles(rng: R, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            lights = new Lights(e);
            // grit pulled in: specks that spiral toward the middle, faster and faster
            uint[] grits = { C[2], C[3], C[4], Hex("#4a4458"), Hex("#6e6880") };
            grit = new Speck[70];
            for (int i = 0; i < 70; i++)
            {
                double t0 = R.Range(0, 0.4);
                grit[i] = new Speck { a = R.Range(0, TAU), d = R.Range(40, 86), t0 = t0, T = R.Range(0.3, SUCK - 0.04 - t0), spin = R.Sign() * R.Range(0.6, 1.6), c = R.Pick(grits) };
            }
            rays = new double[4];
            for (int i = 0; i < 4; i++) rays[i] = (i * TAU) / 4 + Math.PI / 4;
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < SUCK - 0.26)
            {
                // streaks of light rushing in from all round, accelerating, more of them as it goes
                double k = t / SUCK;
                for (int n = 0; n < 1 + Math.Floor(k * 4); n++)
                {
                    if (!R.Chance(0.9)) continue;
                    double a = R.Range(0, TAU), D = R.Range(34, 82) * (1 - 0.3 * k), T = R.Range(0.14, 0.24), v = (D * 3) / (Exp(3 * T) - 1);
                    ps.Spawn(P.Streak, Px.Cos(a) * D, Px.Sin(a) * D, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? C[5] : C[4]);
                }
                // vapour drawn in along the way
                if (R.Chance(14 * dt))
                {
                    double a = R.Range(0, TAU), D = R.Range(50, 80);
                    ps.Spawn(P.Vapour, Px.Cos(a) * D, Px.Sin(a) * D, -Px.Cos(a) * 90, -Px.Sin(a) * 90, R.Range(0.4, 0.6), C[3], R.Range(3, 5));
                }
            }
            if (!popped && t >= STILL)
            {
                popped = true;
                lights.Add(0, 0, 90, C[4], 1, 0.4);
                lights.Add(0, 0, 30, WHITE, 0.9, 0.08);
                for (int i = 0; i < 34; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(120, 330);
                    ps.Spawn(R.Chance(0.6) ? P.Mote : P.Star, 0, 0, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.45), R.Pick(new[] { C[6], C[5], C[4] }), R.Range(0.8, 1.8));
                }
            }
            ps.Update(dt);
            Cull(ps, -OX + 3, -OY + 3, W - OX - 3, H - OY - 3);
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            double k = Clamp(t / SUCK);
            if (t < STILL)
            {
                // the void: a dark sphere edged with light, collapsing onto the core
                double vr = 64 * (1 - Ease.InCubic(k)) + 3;
                if (k < 1)
                {
                    p.Disc(0, 0, vr, VOID, 0.35 + 0.35 * k);
                    p.Ring(0, 0, vr + 1, 1, k < 0.5 ? C[3] : C[4], 0.5 + 0.5 * k);
                    p.GlowRing(0, 0, vr, 5, C[4], 0.18 + 0.3 * k);
                }
                // rings pulled in after it, one after another, brighter as they go
                for (int i = 0; i < 4; i++)
                {
                    double u = (t - i * 0.17) / 0.34;
                    if (u <= 0 || u >= 1) continue;
                    double r = (70 - i * 8) * (1 - Ease.InQuad(u)) + 2;
                    p.Ring(0, 0, r, u > 0.7 ? 2 : 1, u > 0.6 ? C[5] : C[3], 0.4 + 0.6 * u);
                }
                // the grit, spiralling in
                foreach (Speck q in grit)
                {
                    double u = (t - q.t0) / q.T;
                    if (u <= 0 || u >= 1) continue;
                    double d = q.d * (1 - Ease.InCubic(u)), a = q.a + q.spin * Ease.InQuad(u) * 2, x = Px.Cos(a) * d, y = Px.Sin(a) * d;
                    double x2 = Px.Cos(a - q.spin * 0.1) * (d + 4 * u * u + 0.5), y2 = Px.Sin(a - q.spin * 0.1) * (d + 4 * u * u + 0.5);
                    uint tail = Mix(q.c, VOID, 0.4), head = q.c;
                    p.Line(x2, y2, x, y, v => v < 0.5 ? tail : head);
                }
                // the core: growing hotter as it's crushed, then a white point trembling in the stillness
                bool still = t > SUCK;
                double jit = still ? (Math.Floor(t * 40) % 2 != 0 ? 0.5 : -0.5) : 0, cr = still ? 1.6 : 1 + 3.5 * (1 - k) * k * 4;
                p.Disc(jit, 0, cr + 3.5, VOID, 0.85);
                p.Circle(jit, 0, cr + 1.2, C[4]);
                p.Circle(jit, 0, cr, WHITE);
                p.Glow(0, 0, 10 + 26 * k, C[4], 0.3 + 0.5 * k);
                if (still) p.Glow(0, 0, 8, WHITE, 0.4 + 0.2 * Px.Sin(t * 60));
            }
            else
            {
                // the pop: a white point bursting, a cross flare, a fast thin ring and a slower one
                double q = (t - STILL) / 0.5, e = Ease.OutCubic(Clamp(q));
                if (q < 0.08) p.Circle(0, 0, 6 * (1 - q / 0.08) + 1, WHITE);
                if (q < 0.35)
                {
                    double L = (1 - q / 0.35) * 34 + 3;
                    foreach (double a in rays)
                        for (double d = 0; d <= L; d += 0.5)
                            p.Dot(Px.Cos(a) * d, Px.Sin(a) * d, d < L * 0.4 ? WHITE : C[5], d < L * 0.7 ? 1 : 1 - (d / L - 0.7) / 0.3);
                    foreach (double a in rays)
                        for (double d = 0; d <= L * 0.45; d += 0.5)
                            p.Dot(Px.Cos(a + Math.PI / 4) * d, Px.Sin(a + Math.PI / 4) * d, C[5], 1 - d / (L * 0.45));
                }
                if (q < 1)
                {
                    p.Ring(0, 0, 4 + 74 * e, Math.Max(1, 2.5 * (1 - q)), q < 0.2 ? WHITE : C[5], 1.15 - q);
                    p.Ring(0, 0, 2 + 40 * Ease.OutCubic(Clamp(q * 1.4)), 1, C[4], 1 - q);
                }
                if (q < 0.6) Sim.Sparkle(p, 0, 0, 5 * (1 - q / 0.6), WHITE, C[5], 0.6);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
