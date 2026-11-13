// Shared by the scifi effects. Ghosts: the toolkit's simulated particles for loops (sparks that fall and bounce,
// smoke that rises and thins) worked out from their birth each frame, so a loop is a function of t alone and has
// no seam. Also lights that flare in loops, and electric arcs stroked the way Hugo strokes his bolts. Their motion is the toolkit's (src/core/sim.js), which is adapted from Sprite Fusion's open-source
// pixel art VFX (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class ScifiLib
    {
        const double STEP = 1.0 / 240;

        static bool Soft(P k) => k == P.Vapour || k == P.Dust || k == P.Smoke;

        // one toolkit step for one particle, with its own age standing in for the system clock (so it repeats)
        static void Move(Particles.Particle q, double dt, double age, Func<double, double> ground, ref int hops)
        {
            switch (q.k)
            {
                case P.Fire:
                    q.vy -= 95 * dt;
                    q.vx += Px.Sin(age * 17 + q.seed) * 30 * dt;
                    q.vx -= q.vx * 5 * dt;
                    q.vy -= q.vy * 3.2 * dt;
                    break;
                case P.Grit:
                {
                    q.vy += Sim.GRAVITY * dt;
                    q.vx -= q.vx * 0.8 * dt;
                    double g = ground(q.x), ny = q.y + q.vy * dt;
                    q.x += q.vx * dt;
                    if (q.vy > 0 && q.y <= g && ny >= g)
                    {
                        q.y = g - 0.25;
                        if (q.vy > 130 && Rand(q.seed * 1000, hops++) < 0.4)
                        {
                            q.vy = -q.vy * 0.25;
                            q.vx = q.vx * 0.5;
                        }
                        else
                        {
                            q.vx = q.vy = 0;
                            q.life = Math.Min(q.life, 0.5);
                        }
                    }
                    else q.y = ny;
                    return;
                }
                case P.Spark:
                {
                    q.vy += Sim.GRAVITY * 0.45 * dt;
                    q.vx -= q.vx * 2.2 * dt;
                    q.vy -= q.vy * 2.2 * dt;
                    double g = ground(q.x), ny = q.y + q.vy * dt;
                    q.x += q.vx * dt;
                    if (q.vy > 0 && q.y <= g && ny >= g)
                    {
                        q.vy = -q.vy * 0.45;
                        q.vx *= 0.7;
                        q.y = g - 0.25;
                    }
                    else q.y = ny;
                    return;
                }
                case P.Streak:
                    q.vx += q.vx * 3 * dt;
                    q.vy += q.vy * 3 * dt;
                    break;
                case P.Flake:
                    q.vx -= q.vx * 2.2 * dt;
                    q.vy -= q.vy * 2.2 * dt - 35 * dt;
                    q.x += Px.Sin(age * 2.5 + q.seed) * 7 * dt;
                    break;
                case P.Ember:
                    q.vy += (-30 + Px.Sin(age * 3 + q.seed) * 20) * dt;
                    q.vx += Px.Cos(age * 2.3 + q.seed) * 45 * dt;
                    q.vx -= q.vx * 1.6 * dt;
                    q.vy -= q.vy * 1.6 * dt;
                    break;
                case P.Smoke:
                {
                    bool big = (q.flag & PF.Big) != 0;
                    q.vy -= (big ? 23 : 16) * dt;
                    q.vx += Px.Sin(age * 1.3 + q.seed) * 8 * dt;
                    q.vx -= q.vx * 1.6 * dt;
                    q.vy -= q.vy * 1.2 * dt;
                    break;
                }
                case P.Mote:
                    q.vx -= q.vx * 0.6 * dt;
                    q.vy -= q.vy * 0.6 * dt;
                    break;
                default:
                {
                    double drag = Soft(q.k) ? 4 : 1.6;
                    q.vx -= q.vx * drag * dt;
                    q.vy -= q.vy * drag * dt + 5 * dt;
                    break;
                }
            }
            q.x += q.vx * dt;
            q.y += q.vy * dt;
        }

        /// <summary>
        /// Simulated particles for loops. Add(born, ...Particles.Spawn arguments) once, at build time, for each particle born `born`
        /// seconds into the period; At(t) returns a Particles holding every particle alive at t, each moved there from its birth by the
        /// toolkit's physics, ready for Draw(p, layer). A particle born late in the period carries on into the next one, so the loop
        /// has no seam. box: [x0, y0, x1, y1], outside which particles are dropped (keeps sparks on the canvas).
        /// </summary>
        internal sealed class Ghosts
        {
            sealed class Spec
            {
                public double born, x, y, vx, vy, life, size, seed;
                public P k;
                public uint col;
                public PF flag;
            }

            readonly double D;
            readonly Func<double, double> ground;
            readonly double[] box;
            readonly Particles ps;
            readonly List<Spec> specs = new List<Spec>();

            public Ghosts(double period, Func<double, double> ground = null, uint[] sparks = null, uint haze = 0, double[] box = null)
            {
                D = period;
                this.ground = ground ?? (x => double.PositiveInfinity);
                this.box = box;
                ps = new Particles(ground: this.ground, sparks: sparks, haze: haze);
            }

            public void Add(double born, P k, double x, double y, double vx, double vy, double life, uint col = WHITE, double size = 1, PF flag = PF.None)
            {
                int n = specs.Count;
                // a star's twinkle runs on the clock: pick its seed so the twinkle repeats with the period too
                double seed = Rand(n, 41) * 100;
                if (k == P.Star)
                {
                    double m = Math.Ceiling((8 * D) / TAU), f = (TAU * m) / D - 8;
                    if (f < 5) seed = f + 5 * Math.Floor(Rand(n, 43) * 19);
                }
                specs.Add(new Spec { born = ((born % D) + D) % D, k = k, x = x, y = y, vx = vx, vy = vy, life = life, col = col, size = size, flag = flag, seed = seed });
            }

            public int Count => specs.Count;

            /// <summary>The state of one particle `age` seconds after its birth, or null if it's gone.</summary>
            Particles.Particle State(Spec s, double age)
            {
                var q = new Particles.Particle { k = s.k, x = s.x, y = s.y, vx = s.vx, vy = s.vy, life = s.life, max = s.life, col = s.col, size = s.size, seed = s.seed, delay = 0, flag = s.flag };
                int hops = 0;
                double a = 0;
                while (a < age - 1e-9)
                {
                    double dt = Math.Min(STEP, age - a);
                    Move(q, dt, a, ground, ref hops);
                    a += dt;
                    q.life -= dt;
                    if (q.life <= 0)
                    {
                        // a fire puff with SMOKES turns into smoke when it dies, as in the toolkit
                        if (q.k != P.Fire || (q.flag & PF.Smokes) == 0) return null;
                        bool big = (q.flag & PF.Big) != 0;
                        double L = big ? 1.3 + Rand(s.seed * 7, 1) : 0.8 + Rand(s.seed * 7, 1) * 0.6;
                        q.k = P.Smoke;
                        q.vx = q.vx * 0.6;
                        q.vy = q.vy * 0.6 - 6;
                        q.life = L;
                        q.max = L;
                        q.col = 0;
                        q.size = q.size * (big ? 0.85 : 0.7);
                        q.flag = big ? PF.Big : PF.None;
                    }
                }
                if (box != null)
                {
                    double x0 = box[0], y0 = box[1], x1 = box[2], y1 = box[3];
                    if (q.x < x0 || q.x > x1 || q.y < y0 || q.y > y1) return null;
                }
                return q;
            }

            public Particles At(double t)
            {
                ps.List.Clear();
                foreach (Spec s in specs)
                {
                    // its age now, and in each earlier period it may still be alive from (smoke from fire lives longer)
                    double span = s.k == P.Fire && (s.flag & PF.Smokes) != 0 ? s.life + 2.4 : s.life;
                    for (double age = (((t - s.born) % D) + D) % D; age < span; age += D)
                    {
                        Particles.Particle q = State(s, age);
                        if (q != null) ps.List.Add(q);
                    }
                }
                ps.T = t;
                return ps;
            }
        }

        /// <summary>A light that flares at t0 and dies over life, as the toolkit's Lights do, for loops: k falls with life².</summary>
        public static void Flash(Painter p, double t, double t0, double x, double y, double r, uint c, double k, double life)
        {
            double a = t - t0;
            if (a < 0 || a >= life) return;
            double q = 1 - a / life;
            p.Glow(x, y, r * (0.55 + 0.45 * q), c, k * q * q);
        }

        /// <summary>Smallest positive age of an event at t0 in a loop of period D, seen at t.</summary>
        public static double Since(double t, double t0, double D) => (((t - t0) % D) + D) % D;

        /// <summary>
        /// An electric arc along a path [[x, y], ...], drawn the way Hugo strokes his bolts: a dark halo, a bright body, a white-hot
        /// core while it's fresh, light along it. C: [halo, body, glow]; k: 1 fresh, falling to 0; w: weight.
        /// </summary>
        public static void StrokeArc(Painter p, double[][] pts, uint[] C, double k = 1, double w = 1)
        {
            uint halo = C[0], body = C[1], glow = C[2];
            for (int pass = 0; pass <= 2; pass++)
                for (int i = 1; i < pts.Length; i++)
                {
                    double ax = pts[i - 1][0], ay = pts[i - 1][1], bx = pts[i][0], by = pts[i][1], L = Math.Max(1, Hypot(bx - ax, by - ay));
                    for (double d = 0; d <= L; d += 0.5)
                    {
                        double x = ax + ((bx - ax) * d) / L, y = ay + ((by - ay) * d) / L;
                        if (pass == 0) p.Circle(x, y, (k > 0.6 ? 1.6 : 1.1) * w, halo);
                        else if (pass == 1) p.Circle(x, y, (k > 0.6 ? 0.9 : 0.6) * w, body);
                        else if (k > 0.3) p.Px(x, y, WHITE);
                    }
                    if (pass == 0 && i % 3 == 0) p.Glow(ax, ay, 9 * w, glow, 0.45 * k);
                }
        }
    }
}
