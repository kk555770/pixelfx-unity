// The droplet recipe (shaded beads with tails that fly, land and throw up a little crown of their own) is
// adapted from Sprite Fusion's open-source blood (https://www.spritefusion.com/pixel-art-vfx/blood),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Splash : Effect
    {
        public static readonly Module Module = new Module("splash", "pixelfx/fx/water/splash", 176, 100, 88, 78, 2.5, false, 30,
            new Options { ["palette"] = WATER, ["stone"] = true, ["seed"] = 3 },
            (o, e) => new Splash(o, e))
        {
            Title = "Splash",
            Desc = "A stone drops into still water: a white hit, a crown that tears into droplets arcing out to plink down with rings, a jet that bounces back, foam and drifting mist.",
            Tags = new[] { "water", "splash", "impact", "drop", "liquid", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.22, HIT = 0.16, G = 820, JET = 0.3, TEAR = 0.11;
        const int FINGERS = 12;

        // the crown over time u after the hit: it leaps up in a few frames, flares out, then falls back
        static double Height(double u) => 19 * Ease.OutCubic(Clamp(u / 0.09)) * (1 - Ease.InQuad(Clamp((u - 0.12) / 0.3)));
        static double Foot(double u) => 3 + 7.5 * Ease.OutCubic(Clamp(u / 0.45));
        static double Lip(double u) => Foot(u) + Height(u) * 0.42 + 3 * Ease.OutCubic(Clamp(u / 0.25));
        // the jet that bounces back up the middle once the crown has fallen
        static double JetH(double u) => 26 * (Ease.OutCubic(Clamp(u / 0.12)) - Ease.InQuad(Clamp((u - 0.15) / 0.28)));

        sealed class Spike
        {
            public double a, L;
        }

        sealed class Plink
        {
            public double x, y, s, age;
        }

        sealed class Bubble
        {
            public double x, z, t0, r;
            public bool popped;
        }

        sealed class Cell
        {
            public int x, y, r;
            public uint c;
            public double a;
        }

        readonly Options o;
        readonly uint[] C, ST;
        readonly Rng R;
        readonly double phi, seed;
        readonly Particles ps;
        readonly WaterLib.Mist mist = new WaterLib.Mist();
        readonly Lights lights;
        readonly WaterLib.Ripples rings = new WaterLib.Ripples(SY);
        readonly WaterLib.Drops drops;
        readonly List<Plink> plinks = new List<Plink>();
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly Spike[] spikes = new Spike[FINGERS];
        readonly HashSet<string> done = new HashSet<string>();
        double t;

        Splash(Options o, Emitter e)
        {
            this.o = o;
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            seed = o.Seed;
            ST = Rock();
            phi = R.Range(0, TAU);
            ps = new Particles(R, emitter: e);
            lights = new Lights(e);
            for (int k = 0; k < FINGERS; k++) spikes[k] = new Spike { a = ((k * TAU) / FINGERS + phi) % TAU, L = R.Range(0.2, 0.36) };
            drops = new WaterLib.Drops(SY, G, onLand: d =>
            {
                // every drop that comes down plinks: a white fleck, a ring, and the bigger ones throw up a tiny crown
                rings.Add(d.x, d.z, 0.6, 2 + d.s * 1.8, 0.34 + d.s * 0.1, n: 1, k: 0.95, hot: 0.06);
                plinks.Add(new Plink { x = d.x, y = d.z * SY, s = d.s, age = 0 });
                if (d.s > 1 && !d.small)
                    for (int i = 0; i < d.s; i++)
                    {
                        double a = R.Range(0, TAU), v = R.Range(10, 34);
                        drops.Add(new WaterLib.Drop { x = d.x, z = d.z, h = 0.5, vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(55, 110), s = 1, small = true, life = 1 });
                    }
            });
        }

        bool Once(string key, double at)
        {
            if (done.Contains(key) || t < at) return false;
            done.Add(key);
            return true;
        }

        // the lip isn't level: it rises and dips round the crown
        double Scallop(double a, double h) => h * (0.86 + 0.14 * Noise(Px.Cos(a) * 2.2 + Px.Sin(a) * 1.3 + phi * 3, seed));

        // the crown: a thin sheet from its foot to a rolled lip
        void Crown(Painter p, bool back)
        {
            double u = t - HIT, h = Height(u);
            if (u < 0 || h < 0.7) return;
            double r0 = Foot(u), rt = Lip(u);
            bool white = u < 0.045;
            var index = new Dictionary<double, int>();
            var cells = new List<Cell>();
            void Put(double x, double y, uint c, double a, int rank)
            {
                double X = Math.Floor(x), Y = Math.Floor(y), key = (Y + 4096) * 8192 + X + 4096;
                if (!index.TryGetValue(key, out int at))
                {
                    index[key] = cells.Count;
                    cells.Add(new Cell { x = (int)X, y = (int)Y, c = c, a = a, r = rank });
                }
                else if (rank > cells[at].r) cells[at] = new Cell { x = (int)X, y = (int)Y, c = c, a = a, r = rank };
            }
            double n = Math.Ceiling(rt * TAU * 1.6);
            for (int i = 0; i < n; i++)
            {
                double a = (i / n) * TAU, s = Px.Sin(a), c = Px.Cos(a);
                if (s < 0 != back) continue;
                double lane = Math.Floor(((a + phi) * rt) / 2.2);
                bool streak = Rand(lane, 31) < 0.3;
                double H = Scallop(a, h);
                // as it falls, each lane of the sheet gives way from the top at its own moment
                double keep = 1 - Clamp((u - 0.13 - 0.14 * Rand(lane, 7)) / 0.1), steps = Math.Ceiling(H * 1.4) + 2;
                for (double k = 0; k <= steps * keep; k++)
                {
                    double v = k / steps, r = r0 + (rt - r0) * Px.Pow(v, 2.2), x = c * r, y = s * r * SY - H * v;
                    if (white) Put(x, y, WHITE, 1, 3);
                    else if (back) Put(x, y, v > 0.72 ? (streak ? C[5] : C[4]) : streak ? C[3] : C[2], 1, 1);
                    else if (Math.Abs(c) > 0.9) Put(x, y, c < 0 ? (Math.Abs(c) > 0.97 ? WHITE : C[6]) : C[4], 1, 2);
                    else if (v < 0.1) Put(x, y, v < 0.05 ? WHITE : C[6], 1, 2);
                    else if (streak) Put(x, y, c < -0.2 ? C[6] : C[5], 1, 1);
                    else Put(x, y, c < 0 ? C[5] : C[4], 0.22, 0);
                }
                // the rolled lip: a hard white line on the near side, softer on the far, gone where the lane broke
                if (!white && keep >= 1)
                {
                    double x = c * rt, y = s * rt * SY - H;
                    Put(x, y, back ? C[5] : c < 0.45 ? WHITE : C[6], 1, 3);
                    if (!back && c < 0.2) Put(x, y + 1, C[6], 1, 2);
                }
            }
            foreach (Cell q in cells)
            {
                if (q.r == 0) p.Blend(q.x, q.y, q.c, q.a);
                else p.Px(q.x, q.y, q.c);
            }
            // fingers: spikes running on up and out from the lip, a bead on each until it tears off
            double reach = 1 - Smooth(TEAR, TEAR + 0.1, u);
            foreach (Spike f in spikes)
            {
                if (Px.Sin(f.a) < 0 != back || reach <= 0) continue;
                double H = Scallop(f.a, h), L = h * f.L * reach, x0 = Px.Cos(f.a) * rt, y0 = Px.Sin(f.a) * rt * SY - H, x1 = x0 + Px.Cos(f.a) * L * 0.45, y1 = y0 - L;
                if (L >= 1) p.Line(x0, y0, x1, y1, white ? WHITE : back ? C[4] : C[6]);
                if (u > 0.04 && u < TEAR) Bead(p, x1 - (back ? 0 : 0.5), y1 - 1, 0, -1, back ? 1 : 2, C);
                if (!back && Px.Sin(f.a) > 0.3) p.Glow(x0, y0, 6, C[5], 0.22 * reach);
            }
        }

        void Jet(Painter p)
        {
            double u = t - HIT - JET, h = JetH(u);
            if (u < 0 || h < 0.8) return;
            double top = -h;
            // a mound of water heaves up under it
            p.Ellipse(0, 0, 5, 2.4, (d, a) => Px.Sin(a) > 0 ? 0 : d < 0.5 ? WHITE : Px.Cos(a) < 0 ? C[6] : C[5]);
            p.Shade(-3, top - 2, 3, 0, (x, y, @out) =>
            {
                double v = Clamp((y - top) / h), w = 1.15 + 1.5 * v * v * v, q = x / w;
                if (Math.Abs(q) > 1) return 0;
                @out.c = q < -0.5 ? C[6] : q < 0.3 ? WHITE : q < 0.72 ? C[5] : C[4];
                return 0;
            });
            if (u < 0.12)
            {
                // the bulb at its tip, about to pinch off
                p.Circle(0, top - 1, 1.7, C[5]);
                p.Circle(-0.3, top - 1.3, 1.1, C[6]);
                p.Px(-1, top - 2, WHITE);
            }
            p.GlowLine(0, -1, 0, top, 7, C[5], 0.3);
        }

        public override void Update(double dt)
        {
            t += dt;
            if (Once("hit", HIT))
            {
                lights.Add(0, -6, 52, C[5], 0.6, 0.45);
                lights.Add(0, -4, 16, WHITE, 0.5, 0.08);
                rings.Add(0, 0, 8, 56, 2.0, n: 2, gap: 3.4, k: 1.35, hot: 0.16, glint: 1);
                rings.Add(0, 0, 5, 38, 1.7, n: 1, k: 0.9, delay: 0.14);
                // the first sheet of spray, thrown up fast and fine
                for (int i = 0; i < 20; i++)
                {
                    double a = R.Range(0, TAU), v = R.Range(50, 125);
                    drops.Add(new WaterLib.Drop { x = Px.Cos(a) * 3, z = Px.Sin(a) * 3, h = R.Range(1, 6), vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(160, 270), s = R.Chance(0.25) ? 2 : 1, delay = R.Range(0, 0.04) });
                }
                // mist rolls out low along the water
                for (int i = 0; i < 9; i++)
                {
                    double s = i % 2 != 0 ? 1 : -1, x = s * R.Range(4, 12);
                    mist.Add(x, -R.Range(1, 7), s * R.Range(25, 70), -R.Range(4, 16), R.Range(2.5, 4), R.Range(7, 11), R.Range(0.9, 1.5), R.Chance(0.5) ? C[6] : C[5], R.Range(0.3, 0.42), R.Range(0, 0.1));
                }
            }
            if (Once("spray", HIT + 0.04))
            {
                for (int i = 0; i < 20; i++)
                {
                    double a = R.Range(0, TAU);
                    ps.Spawn(P.Flake, Px.Cos(a) * 9, Px.Sin(a) * 9 * SY - R.Range(6, 15), Px.Cos(a) * R.Range(25, 90), -R.Range(30, 100), R.Range(0.7, 1.3), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.5));
                }
                for (int i = 0; i < 5; i++) ps.Spawn(P.Star, R.Range(-13, 13), -R.Range(6, 20), 0, -4, R.Range(0.25, 0.55), C[6], R.Range(1, 2.4), R.Range(0, 0.15));
            }
            if (Once("tear", HIT + TEAR))
            {
                // the beads on the finger tips tear off and arc away
                double h = Height(TEAR), rt = Lip(TEAR);
                foreach (Spike f in spikes)
                {
                    double a = f.a + R.Range(-0.08, 0.08), v = R.Range(32, 88), L = h * f.L;
                    drops.Add(new WaterLib.Drop { x = Px.Cos(f.a) * (rt + L * 0.45), z = Px.Sin(f.a) * rt, h = h + L + 1, vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(90, 190), s = R.Chance(0.3) ? 3 : 2 });
                }
            }
            // as the crown gives way its lip comes apart into drops
            double u = t - HIT;
            if (u > 0.13 && u < 0.3 && R.Chance(0.3))
            {
                double a = R.Range(0, TAU), rt = Lip(u), v = R.Range(15, 45);
                drops.Add(new WaterLib.Drop { x = Px.Cos(a) * rt, z = Px.Sin(a) * rt, h = Scallop(a, Height(u)) * R.Range(0.6, 1), vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(-10, 45), s = R.Chance(0.3) ? 2 : 1 });
            }
            if (Once("fall", HIT + 0.3))
            {
                // the crown falls back, shedding a few heavy drops close in
                for (int i = 0; i < 8; i++)
                {
                    double a = R.Range(0, TAU), r = Lip(0.3), v = R.Range(15, 40);
                    drops.Add(new WaterLib.Drop { x = Px.Cos(a) * r, z = Px.Sin(a) * r, h = R.Range(3, 7), vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(30, 80), s = R.Chance(0.4) ? 2 : 1 });
                }
            }
            if (Once("bead", HIT + JET + 0.12))
            {
                // the jet's tip pinches off into a bead that hangs, then drops back
                drops.Add(new WaterLib.Drop { x = 0, z = 0.2, h = JetH(0.12) + 1, vx = R.Range(-6, 6), vz = 0, vh = 55, s = 3 });
                for (int i = 0; i < 3; i++) mist.Add(R.Range(-3, 3), -JetH(0.12) + R.Range(2, 8), R.Range(-14, 14), -R.Range(4, 12), 2, R.Range(4, 6), R.Range(0.6, 1), C[6], 0.3);
            }
            if (Once("slump", HIT + JET + 0.38))
            {
                rings.Add(0, 0, 3, 30, 1.5, n: 2, gap: 2.6, k: 0.9);
                for (int i = 0; i < 4; i++) mist.Add(R.Range(-6, 6), -R.Range(1, 4), R.Range(-30, 30), -R.Range(3, 10), 2.5, R.Range(6, 9), R.Range(0.8, 1.2), C[5], 0.28);
            }
            // the stone sinks and lets go of its air: a few bubbles come up and pop
            double[] glug = { 0.95, 1.2, 1.5 };
            for (int i = 0; i < glug.Length; i++)
                if (Once("glug" + i, HIT + glug[i])) bubbles.Add(new Bubble { x = R.Range(-5, 5), z = R.Range(-6, 6), t0 = t, r = R.Range(1.3, 2.3), popped = false });
            foreach (Bubble b in bubbles)
                if (!b.popped && t - b.t0 > 0.2)
                {
                    b.popped = true;
                    rings.Add(b.x, b.z, 1, 7, 0.6, n: 1, k: 0.9);
                    plinks.Add(new Plink { x = b.x, y = b.z * SY, s = 1, age = 0 });
                    for (int i = 0; i < 2; i++) drops.Add(new WaterLib.Drop { x = b.x, z = b.z, h = 1, vx = R.Range(-20, 20), vz = R.Range(-10, 10), vh = R.Range(50, 90), s = 1, small = true, life = 1 });
                }
            for (int i = plinks.Count - 1; i >= 0; i--)
                if ((plinks[i].age += dt) > 0.14) plinks.RemoveAt(i);
            drops.Update(dt);
            rings.Update(dt);
            mist.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            double u = t - HIT;
            // the cavity the stone punches, dark inside the foot of the crown
            if (u > 0 && u < 0.26)
            {
                double r = Foot(u) * 0.85 * (1 - Smooth(0.12, 0.26, u));
                p.Ellipse(0, 0, r, r * SY + 0.6, (d, a) => Px.Sin(a) < 0 ? C[1] : C[2]);
            }
            rings.Draw(p, C, -1);
            Crown(p, true);
            drops.Draw(p, C, -1);
        }

        public override void Draw(Painter p, double tt)
        {
            double u = t - HIT;
            rings.Draw(p, C, 1);
            if (u > 0.2)
            {
                double rx = 4 + 11 * Ease.OutCubic(Clamp((u - 0.2) / 0.7)), k = Smooth(0.2, 0.32, u) * (1 - Smooth(0.5, 2.0, u)) * 0.9;
                Lace(p, 0, 0, rx, rx * SY + 0.5, k, C, t, seed);
            }
            Crown(p, false);
            Jet(p);
            foreach (Bubble b in bubbles)
            {
                double g = (t - b.t0) / 0.2;
                if (g > 1) continue;
                double r = b.r * Ease.OutBack(g), x = b.x, y = b.z * SY;
                for (double a = Math.PI; a <= TAU + 0.01; a += 0.5 / Math.Max(1, r)) p.Dot(x + Px.Cos(a) * r, y + Px.Sin(a) * r, C[6]);
                p.Px(x - r * 0.4, y - r * 0.6, WHITE);
                p.Glow(x, y - 1, 4, C[5], 0.25);
            }
            foreach (Plink q in plinks)
            {
                double k = q.age / 0.14;
                if (k < 0.5) p.Px(q.x, q.y, WHITE);
                if (q.s > 1) p.Line(q.x, q.y - 1, q.x, q.y - 1 - 3 * (1 - k), k < 0.4 ? WHITE : C[6]);
                p.Glow(q.x, q.y, 4, C[5], 0.3 * (1 - k));
            }
            drops.Draw(p, C, 1);
            // the hit: a flat white flash on the surface for two frames
            if (u >= 0 && u < 0.07)
            {
                p.Ellipse(0, 0, 10, 2.4, (d, a) => d < 0.6 ? WHITE : C[6]);
                Sim.Sparkle(p, 0, -3, 5 * (1 - u / 0.07), C[6], C[5], 0.5);
            }
            // the stone, falling fast with a streak of air off it
            if (o.Bool("stone") && t < HIT)
            {
                double v = t / HIT, y = -70 + 70 * (0.4 * v + 0.6 * v * v);
                p.Line(0, y - 12, 0, y - 3, ST[4], w => w * 0.8);
                Pebble(p, 0, y, 2.6, ST);
            }
            mist.Draw(p, C[5]);
            ps.Draw(p);
            lights.Draw(p);
            // the disturbed water keeps a cool light on it while it settles
            if (u > 0)
            {
                p.Glow(0, -2, 34, C[4], 0.2 * (1 - Smooth(0, 1.6, u)));
                p.Glow(0, 2, 64, C[3], 0.16 * (1 - Smooth(0.1, 2.1, u)));
            }
        }
    }
}
