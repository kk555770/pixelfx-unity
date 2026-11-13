// The flame puff (after sim.js's FIRE) and the swirling pull (attract) are adapted from Sprite Fusion's
// open-source black-hole (https://www.spritefusion.com/pixel-art-vfx/black-hole),
// MIT License, Copyright (c) 2026 Hugo Duprez.
//
// Shared bits for the shadow effects: the void palette, light that black shapes cut, smoke lit from one side,
// violet flame, obsidian slivers, ink drops.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class ShadowLib
    {
        /// <summary>Dark magic, dark to bright: void black, deep violet, a white-lilac edge.</summary>
        public static readonly string[] VOID = { "#06040b", "#150c26", "#2a1150", "#461a84", "#6f2cc0", "#9d5cec", "#cfa6ff", "#f6eaff" };

        /// <summary>
        /// Take the light back off every pixel in a local box where dark(x, y) is true, keeping a share keep (0..1):
        /// call it last, so black shapes stay black under every glow, spark and ring drawn before.
        /// </summary>
        public static void Unlight(Painter p, double x0, double y0, double x1, double y1, Func<double, double, bool> dark, double keep = 0)
        {
            Frame f = p.Frame;
            uint[] L = f.Light;
            int w = f.W, ox = f.Ox, oy = f.Oy;
            p.Each(x0, y0, x1, y1, (x, y, sx, sy) =>
            {
                if (!dark(x, y)) return;
                int i = (sy - oy) * w + (sx - ox);
                uint d = L[i];
                if (d == 0) return;
                if (keep <= 0) L[i] = 0;
                else
                    L[i] = 0xff000000u | ((uint)ToInt32(((d >> 16) & 255) * keep) << 16) | ((uint)ToInt32(((d >> 8) & 255) * keep) << 8) | (uint)ToInt32((d & 255) * keep);
            });
        }

        // the pixel cells of the annulus r0 <= d < r1 round (x, y), row by row, jumping the hole: fn(cx, cy, d)
        static void Annulus(double x, double y, double r0, double r1, Action<double, double, double> fn)
        {
            double r02 = r0 > 0 ? r0 * r0 : -1, r12 = r1 * r1;
            for (double py = Math.Floor(y - r1); py <= Math.Ceiling(y + r1); py++)
            {
                double cy = py + 0.5, dy = cy - y, dy2 = dy * dy;
                if (dy2 >= r12) continue;
                double xo = Math.Sqrt(r12 - dy2), xi = dy2 < r02 ? Math.Sqrt(r02 - dy2) : 0;
                for (double px = Math.Floor(x - xo), x1 = Math.Ceiling(x + xo); px <= x1; px++)
                {
                    double cx = px + 0.5, dx = cx - x;
                    if (xi > 0 && dx > -xi && dx < xi)
                    {
                        px = Math.Max(px, Math.Floor(x + xi) - 1);
                        continue;
                    }
                    double d2 = dx * dx + dy2;
                    if (d2 < r12 && d2 >= r02) fn(cx, cy, Math.Sqrt(d2));
                }
            }
        }

        /// <summary>
        /// A ring from radius r - w out to r round (x, y), like p.Ring (c and a may be functions of v: 0 inner to 1
        /// outer, and angle) but visiting only the band's own pixels, so a big thin shock ring stays cheap.
        /// </summary>
        public static void RingFast(Painter p, double x, double y, double r, double w, uint c, double a = 1) => RingFast(p, x, y, r, w, c, null, a, null);
        public static void RingFast(Painter p, double x, double y, double r, double w, Func<double, double, uint> c, double a = 1) => RingFast(p, x, y, r, w, 0, c, a, null);
        public static void RingFast(Painter p, double x, double y, double r, double w, uint c, Func<double, double, double> a) => RingFast(p, x, y, r, w, c, null, 1, a);
        public static void RingFast(Painter p, double x, double y, double r, double w, Func<double, double, uint> c, Func<double, double, double> a) => RingFast(p, x, y, r, w, 0, c, 1, a);

        static void RingFast(Painter p, double x, double y, double r, double w, uint c, Func<double, double, uint> fc, double a, Func<double, double, double> fa)
        {
            if (r <= 0) return;
            double r0 = Math.Max(0, r - w), span = r - r0;
            if (span == 0 || double.IsNaN(span)) span = 1;
            Annulus(x, y, r0, r, (cx, cy, d) =>
            {
                double v = (d - r0) / span, ang = fc != null || fa != null ? Atan2(cy - y, cx - x) : 0;
                p.Dot(cx, cy, fc != null ? fc(v, ang) : c, fa != null ? fa(v, ang) : a);
            });
        }

        /// <summary>Light hugging a circle of radius R at (x, y), falling off over w either side, only where keep(x, y).</summary>
        public static void RingLight(Painter p, double x, double y, double R, double w, uint c, double k, Func<double, double, bool> keep = null)
        {
            if (w < 1 || k <= 0.004) return;
            Annulus(x, y, Math.Max(0, R - w), R + w, (cx, cy, d) =>
            {
                if (keep != null && !keep(cx, cy)) return;
                double f = 1 - Math.Abs(d - R) / w;
                p.Light(cx, cy, c, f * f * k);
            });
        }

        /// <summary>A ring of light hugging a black disc: k at radius r0, falling to nothing at r1, and none inside r0.</summary>
        public static void Halo(Painter p, double x, double y, double r0, double r1, uint c, double k)
        {
            if (r1 <= r0 || k <= 0.004) return;
            double w = r1 - r0;
            Annulus(x, y, Math.Max(0, r0), r1, (cx, cy, d) =>
            {
                double f = 1 - (d - r0) / w;
                p.Light(cx, cy, c, f * f * k);
            });
        }

        /// <summary>
        /// A black star rasterized once: four long arms (L) and four short ones, turned rot, at (0, cy), its cells
        /// in a grid. At(x, y) says if a local point is black; Draw(p, c, rim, skip) fills it and burns a rim on
        /// the cells just outside it (except where skip(x, y)). The arms keep a 1 px black spine to their tips.
        /// </summary>
        public sealed class BlackStar
        {
            public readonly int R0;
            readonly int n;
            readonly double cy;
            readonly byte[] data;

            public BlackStar(double cy, double L, double rot, double shortArm = 0.46, double q = 0.32)
            {
                this.cy = cy;
                R0 = (int)Math.Ceiling(L) + 2;
                n = R0 * 2 + 1;
                data = new byte[n * n];
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    double x = i - R0 + 0.5, y = cy + j - R0 + 0.5;
                    if (InStar(x, y, 0, cy, L, rot, q) || InStar(x, y, 0, cy, L * shortArm, rot + Math.PI / 4, q)) data[j * n + i] = 1;
                }
                for (int a = 0; a < 4; a++)
                {
                    double ca = Px.Cos(rot + (a * Math.PI) / 2), sa = Px.Sin(rot + (a * Math.PI) / 2);
                    for (double d = 0; d < L - 1; d += 0.5)
                    {
                        int i = (int)Math.Floor(ca * d) + R0, j = (int)Math.Floor(sa * d) + R0, k = j * n + i;
                        if (k >= 0 && k < data.Length) data[k] = 1;
                    }
                }
            }

            public bool At(double x, double y)
            {
                double i = Math.Floor(x) + R0, j = Math.Floor(y - cy) + R0;
                return i >= 0 && j >= 0 && i < n && j < n && data[(int)j * n + (int)i] == 1;
            }

            /// <summary>take the light off its black cells (call last, like Unlight)</summary>
            public void Unlight(Painter p)
            {
                Frame f = p.Frame;
                uint[] Lb = f.Light;
                int w = f.W, h = f.H, ox = f.Ox, oy = f.Oy;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    if (data[j * n + i] == 0) continue;
                    double x = i - R0 + 0.5, y = cy + j - R0 + 0.5;
                    long sx = (long)p.CellX(x, y) - ox, sy = (long)p.CellY(x, y) - oy;
                    if (sx >= 0 && sy >= 0 && sx < w && sy < h) Lb[sy * w + sx] = 0;
                }
            }

            public void Draw(Painter p, uint c, uint rim, Func<double, double, bool> skip)
            {
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    double x = i - R0 + 0.5, y = cy + j - R0 + 0.5;
                    if (data[j * n + i] != 0) p.Px(x, y, c);
                    else if (rim != 0 && !skip(x, y) && (At(x - 1, y) || At(x + 1, y) || At(x, y - 1) || At(x, y + 1))) p.Px(x, y, rim);
                }
            }
        }

        /// <summary>Is (x, y) inside a four-point star at (cx, cy), arms L long, turned rot? q &lt; 1 hollows the sides into thin spikes.</summary>
        public static bool InStar(double x, double y, double cx, double cy, double L, double rot, double q = 0.42)
        {
            if (L <= 0) return false;
            double c = Px.Cos(rot), s = Px.Sin(rot), dx = x - cx, dy = y - cy;
            double u = Math.Abs(dx * c + dy * s) / L, v = Math.Abs(-dx * s + dy * c) / L;
            // inside needs the smaller of u, v under 0.5^(1/q): most points fail that before any pow
            return u < 1 && v < 1 && Math.Min(u, v) <= Px.Pow(0.5, 1 / q) && Px.Pow(u, q) + Px.Pow(v, q) <= 1;
        }

        /// <summary>
        /// Pull particles toward (x, y): gravity softened at r0, a swirl round it, drag that grows inside reach.
        /// Those that reach r0 are eaten (removed); returns how many. w(q) weighs each one (0 leaves it alone).
        /// </summary>
        public static int Attract(List<Particles.Particle> list, double x, double y, double G, double reach, double swirl, double r0, double dt, Func<Particles.Particle, double> w = null, double vmax = double.PositiveInfinity)
        {
            int eaten = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var q = list[i];
                double wt = w != null ? w(q) : 1;
                if (wt == 0 || double.IsNaN(wt) || q.delay > 0) continue;
                double vx = q.vx, vy = q.vy;
                int r = Pull(ref vx, ref vy, q.x, q.y, x, y, G, reach, swirl, r0, dt, wt, vmax);
                if (r < 0) continue;
                if (r == 1)
                {
                    list.RemoveAt(i);
                    eaten++;
                    continue;
                }
                q.vx = vx;
                q.vy = vy;
            }
            return eaten;
        }

        public static int Attract(List<Puff> list, double x, double y, double G, double reach, double swirl, double r0, double dt, Func<Puff, double> w = null, double vmax = double.PositiveInfinity)
        {
            int eaten = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var q = list[i];
                double wt = w != null ? w(q) : 1;
                if (wt == 0 || double.IsNaN(wt) || q.delay > 0) continue;
                double vx = q.vx, vy = q.vy;
                int r = Pull(ref vx, ref vy, q.x, q.y, x, y, G, reach, swirl, r0, dt, wt, vmax);
                if (r < 0) continue;
                if (r == 1)
                {
                    list.RemoveAt(i);
                    eaten++;
                    continue;
                }
                q.vx = vx;
                q.vy = vy;
            }
            return eaten;
        }

        // one particle's pull: -1 out of reach, 1 eaten, 0 pulled (its velocity updated)
        static int Pull(ref double vx, ref double vy, double qx, double qy, double x, double y, double G, double reach, double swirl, double r0, double dt, double wt, double vmax)
        {
            double dx = x - qx, dy = y - qy, d2 = dx * dx + dy * dy;
            if (d2 > reach * reach) return -1;
            double d = Math.Sqrt(d2);
            if (d == 0 || double.IsNaN(d)) d = 1;
            if (d < r0) return 1;
            double a = (G / (d2 + r0 * r0)) * Math.Min(1, (reach - d) / (reach * 0.25)) * wt * dt, ux = dx / d, uy = dy / d;
            vx += (ux - uy * swirl) * a;
            vy += (uy + ux * swirl) * a;
            double drag = Math.Min(0.2, (1 - d / reach) * 1.6 * dt);
            vx -= vx * drag;
            vy -= vy * drag;
            double v = Hypot(vx, vy);
            if (v > vmax)
            {
                vx *= vmax / v;
                vy *= vmax / v;
            }
            return 0;
        }

        /// <summary>Puff kinds: Flame (a ball of flame cooling down a ramp), Soot (dark smoke lit on the side facing a light).</summary>
        public enum PK
        {
            Flame = 0,
            Soot = 1,
        }

        public sealed class Puff
        {
            public PK k;
            public double x, y, vx, vy, life, max, size, delay, seed;
            public bool smokes;
            internal int bornX, bornY;
        }

        /// <summary>What a soot puff's rims face: a light at (x, y), strength k, reaching reach.</summary>
        public sealed class PuffLight
        {
            public double x, y, k, reach;

            public PuffLight(double x, double y, double k, double reach)
            {
                this.x = x;
                this.y = y;
                this.k = k;
                this.reach = reach;
            }
        }

        /// <summary>
        /// Violet flame and dark smoke. flame: ramp white-hot to burnt out; soot: [fresh, old] smoke colours; cap: the
        /// soot's dim lit top; rim: [hot, warm, dim] colours of its lit side; floor: (x) => y.
        /// Spawn(kind, x, y, vx, vy, life, size, delay, smokes): smokes turns a dying flame into soot.
        /// Draw(p, kind, light): light is what the soot's rims face (k 0 leaves them dark).
        /// </summary>
        public sealed class Puffs
        {
            public readonly Rng R;
            public List<Puff> List = new List<Puff>();
            double t;
            readonly uint[] F, soot, rim;
            readonly uint cap;
            readonly Func<double, double> floor;
            readonly Emitter emitter;

            public Puffs(Rng rng, uint[] flame, uint[] soot, uint cap, uint[] rim, Func<double, double> floor = null, Emitter emitter = null)
            {
                R = rng;
                F = flame;
                this.soot = soot;
                this.cap = cap;
                this.rim = rim;
                this.floor = floor;
                this.emitter = emitter;
            }

            public void Spawn(PK k, double x, double y, double vx, double vy, double life, double size, double delay = 0, bool smokes = false)
            {
                var q = new Puff { k = k, x = x, y = y, vx = vx, vy = vy, life = life, max = life, size = size, delay = delay, smokes = smokes, seed = R.Next() * 100 };
                Sim.Born(emitter, out q.bornX, out q.bornY);
                List.Add(q);
            }

            public int Count => List.Count;

            public void Update(double dt)
            {
                t += dt;
                for (int i = List.Count - 1; i >= 0; i--)
                {
                    Puff q = List[i];
                    if (q.delay > 0)
                    {
                        q.delay -= dt;
                        continue;
                    }
                    if ((q.life -= dt) <= 0)
                    {
                        if (q.k == PK.Flame && q.smokes)
                        {
                            Spawn(PK.Soot, q.x, q.y - 1, q.vx * 0.5, q.vy * 0.5 - 6, R.Range(0.9, 1.6), q.size * 1.25);
                            Puff s = List[List.Count - 1];
                            s.bornX = q.bornX;
                            s.bornY = q.bornY;
                        }
                        List.RemoveAt(i);
                        continue;
                    }
                    if (q.k == PK.Flame)
                    {
                        q.vy -= 95 * dt;
                        q.vx += Px.Sin(t * 17 + q.seed) * 30 * dt;
                        q.vx -= q.vx * 5 * dt;
                        q.vy -= q.vy * 3.2 * dt;
                    }
                    else
                    {
                        q.vy -= 18 * dt;
                        q.vx += Px.Sin(t * 1.3 + q.seed) * 8 * dt;
                        q.vx -= q.vx * 1.6 * dt;
                        q.vy -= q.vy * 1.3 * dt;
                    }
                    q.x += q.vx * dt;
                    q.y += q.vy * dt;
                    // smoke rolls along the floor rather than through it
                    if (floor != null && q.k == PK.Soot)
                    {
                        double f = floor(q.x) - q.size * 0.6;
                        if (q.y > f)
                        {
                            q.y = f;
                            q.vy = Math.Min(0, q.vy);
                            q.vx *= 1 - 2 * dt;
                        }
                    }
                }
            }

            public void Draw(Painter p, PK kind, PuffLight light = null)
            {
                int n = F.Length;
                foreach (Puff q in List)
                {
                    if (q.delay > 0 || q.k != kind) continue;
                    Sim.Shift(p, emitter, q.bornX, q.bornY);
                    double k = q.life / q.max, age = 1 - k, x = q.x, y = q.y;
                    if (q.k == PK.Flame)
                    {
                        // a ball of flame: a darker rim, a lit core up and left, a tongue on top, a hot heart while young
                        double heat = Math.Min(0.999, Px.Pow(age, 0.62) + (q.seed % 1) * 0.1);
                        int idx = (int)Math.Min(n - 2, Math.Floor(heat * (n - 1)));
                        double r = q.size * (0.55 + 0.45 * Math.Min(1, age * 7)) * (1 - age * 0.55);
                        uint c = F[idx];
                        if (idx >= n - 3) p.Disc(x, y, r, c, Math.Min(1, k * 3) * 0.8);
                        else
                        {
                            p.Circle(x, y, r, F[Math.Min(n - 1, idx + 2)]);
                            p.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, c);
                            if (r > 1.6) p.Circle(x, y - r * 0.9, r * 0.42, c);
                            if (idx <= 3 && r > 1.8) p.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, F[Math.Max(0, idx - 1)]);
                            if (idx < 6) p.Glow(x, y, r * 2.4 + 2, c, 0.2 * k);
                        }
                        continue;
                    }
                    // soot: a soft body and a denser core, a dim cap on top, and lit from the side facing the light by two
                    // discs pushed toward it (a soft wash over the near half, a brighter lip at its edge)
                    double rr = q.size * (0.65 + Math.Min(1, age * 2.5) * 0.45 + age * 0.45);
                    double a = Math.Min(1, age * 10) * Math.Min(1, k * 2.2) * 0.72;
                    uint body = Mix(soot[0], soot[1], age);
                    // one pass for the body: thinner at the edge, denser in the core (two layers' worth)
                    double edge = a * 0.5, core = 1 - (1 - a * 0.5) * (1 - a * 0.62);
                    p.Disc(x, y + rr * 0.06, rr, body, (d, g) => d < 0.72 ? core : edge);
                    double lk = light != null ? light.k : 0;
                    if (lk < 0.5) p.Disc(x - rr * 0.2, y - rr * 0.32, rr * 0.48, cap, a * 0.32 * (1 - age * 0.7) * (1 - lk * 2));
                    if (lk <= 0.02) continue;
                    double lx = light.x - x, ly = light.y - y, dl = Math.Sqrt(lx * lx + ly * ly);
                    if (dl == 0 || double.IsNaN(dl)) dl = 1;
                    double ux = lx / dl, uy = ly / dl;
                    double lit = Math.Min(1, light.k * Clamp(1.3 - dl / light.reach)) * a;
                    if (lit <= 0.02) continue;
                    uint hot = rim[0], warm = rim[1], dim = rim[2];
                    p.Disc(x + ux * rr * 0.26, y + uy * rr * 0.26, rr * 0.72, dim, lit * 0.55);
                    p.Disc(x + ux * rr * 0.5, y + uy * rr * 0.5, rr * 0.46, warm, lit * 0.5);
                    if (rr > 3) p.Disc(x + ux * rr * 0.68, y + uy * rr * 0.68, rr * 0.24, hot, lit * 0.4);
                }
                p.Shift(0, 0);
            }
        }

        /// <summary>
        /// An obsidian shard from core Bits: a thin triangle pointing along its spin angle, a dark violet body, the
        /// long edge facing the light at (lx, ly) lit (brighter with lk), the other in shadow, a white glint at the tip
        /// while the light is strong. C is an 8-step palette.
        /// </summary>
        public static void Shard(Painter p, Bits.Bit b, uint[] C, double lx, double ly, double lk)
        {
            double fa = Math.Min(1, b.life / 0.4), ca = Px.Cos(b.a), sa = Px.Sin(b.a), L = Math.Max(2, b.s), W = Math.Max(2, L * 0.55);
            int side = -sa * (lx - b.x) + ca * (ly - b.y) > 0 ? 1 : -1;
            uint rim = lk > 0.6 ? C[6] : lk > 0.3 ? C[5] : lk > 0.08 ? C[4] : C[3];
            double E = L / 2 + 1.5;
            p.Shade(b.x - E, b.y - E, b.x + E, b.y + E, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, u = dx * ca + dy * sa, v = (-dx * sa + dy * ca) * side;
                if (u < -L / 2 - 0.4 || u > L / 2 + 0.4) return 0;
                double half = ((W / 2) * (L / 2 - u)) / L + 0.4;
                if (Math.Abs(v) > half) return 0;
                o.a = fa;
                if (u > L / 2 - 1.3 && lk > 0.3) return WHITE;
                if (v > half - 1) return u > -L / 4 ? rim : C[Math.Max(3, Array.IndexOf(C, rim) - 1)];
                return v < -half + 1 ? C[1] : C[2];
            });
            if (lk > 0.04) p.Glow(b.x, b.y, 3 + L * 0.6, C[5], 0.2 * lk * fa);
        }

        public sealed class Drop
        {
            public double x, y, vx, vy, size;
        }

        /// <summary>
        /// Ink drops: fly under gravity, land on the floor (y = floor(x)) and call onLand(x, y, speed, size).
        /// Add(x, y, vx, vy, size); Update(dt); Draw(p, body, shine) draws a dark drop with a lit top.
        /// </summary>
        public sealed class Drops
        {
            public readonly List<Drop> List = new List<Drop>();
            readonly Func<double, double> floor;
            readonly Action<double, double, double, double> onLand;
            readonly double g;

            public Drops(Func<double, double> floor = null, Action<double, double, double, double> onLand = null, double g = 0.8)
            {
                this.floor = floor ?? (x => 0);
                this.onLand = onLand;
                this.g = g;
            }

            public void Add(double x, double y, double vx, double vy, double size = 1) => List.Add(new Drop { x = x, y = y, vx = vx, vy = vy, size = size });

            public void Update(double dt)
            {
                for (int i = List.Count - 1; i >= 0; i--)
                {
                    Drop d = List[i];
                    d.vy += Sim.GRAVITY * g * dt;
                    d.vx -= d.vx * 0.4 * dt;
                    d.x += d.vx * dt;
                    d.y += d.vy * dt;
                    double f = floor(d.x);
                    if (d.vy > 0 && d.y >= f)
                    {
                        onLand?.Invoke(d.x, f, d.vy, d.size);
                        List.RemoveAt(i);
                    }
                }
            }

            public void Draw(Painter p, uint body, uint shine)
            {
                foreach (Drop d in List)
                {
                    bool up = d.vy < 0;
                    p.Px(d.x, d.y, body);
                    if (d.size > 1.4) p.Px(d.x, d.y + (up ? 1 : -1), body);
                    p.Px(d.x, d.y - (d.size > 1.4 && !up ? 1 : 0), shine);
                }
            }
        }

        public sealed class Ripple
        {
            public double x, t0, life, w;
        }

        /// <summary>A splat of ink left on the floor where a drop landed outside the pool.</summary>
        public sealed class Splat
        {
            public double x, t0, s;
        }

        /// <summary>A flash of light the effects keep in their own lists: it fades out over its life.</summary>
        public sealed class Lamp
        {
            public double x, y, r, k, life, max;
            public uint c;

            public Lamp(double x, double y, double r, uint c, double k, double life)
            {
                this.x = x;
                this.y = y;
                this.r = r;
                this.c = c;
                this.k = k;
                this.life = life;
                max = life;
            }

            /// <summary>Count down; false once it has gone out.</summary>
            public bool Tick(double dt) => (life -= dt) > 0;
        }

        /// <summary>
        /// A dark pool opened in the floor, seen at a slant (radius rx, centred on the floor at x = 0): the far lip lit,
        /// the near lip dark, ripples running out across it, a sheen along the far side, a wet fringe.
        /// </summary>
        public static void FloorPool(Painter p, uint[] C, double rx, double t, List<Ripple> ripples)
        {
            if (rx < 1) return;
            double ry = rx * 0.17 + 0.6;
            p.Shade(-rx - 3, -ry - 2, rx + 3, ry + 2, (x, y, o) =>
            {
                double dx = x / rx, dy = (y - 0.5) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1.18) return 0;
                if (d > 1)
                {
                    if (dy < 0 && d < 1.09) return Math.Abs(dx) < 0.55 ? C[6] : Math.Abs(dx) < 0.85 ? C[5] : C[4];
                    if (dy >= 0 && d < 1.09) return C[2];
                    return y > -0.5 && Noise(x * 0.7, 9) > 0.15 ? C[1] : 0;
                }
                // ripples: ellipses on the slanted surface, kept about a pixel thick all the way round
                foreach (Ripple r in ripples)
                {
                    double age = t - r.t0, rr = age * 46 * r.w + 1, ex = x - r.x, ey = (y - 0.5) / 0.17, e = Math.Sqrt(ex * ex + ey * ey);
                    if (e == 0 || double.IsNaN(e)) e = 1;
                    double g = Math.Sqrt(ex * ex + Px.Pow(ey / 0.17, 2)) / e;
                    if (Math.Abs(e - rr) < 0.55 * g && age < r.life) return age < r.life * 0.5 ? C[4] : C[3];
                }
                if (dy < -0.55 && dy > -0.85 && Noise(x * 0.25 + t * 1.3, 4) > 0.2) return Noise(x * 0.6 - t * 2, 6) > 0.55 ? C[5] : C[2];
                // the far inner wall catches the glow from below: violet just under the lip, falling to black
                if (dy < -0.35 && d > 0.72) return d > 0.9 ? C[3] : C[2];
                return d > 0.86 ? C[1] : C[0];
            });
        }
    }
}
