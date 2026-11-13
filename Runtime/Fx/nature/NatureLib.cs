// Shared by the nature effects: faceted stones that tumble and break, the stone spike they break from, leaves,
// droplets, bubbles and gas. The gas (puffs that drift off their push, rise, swell and thin, a lit cap) is adapted
// from Sprite Fusion's open-source poison (https://www.spritefusion.com/pixel-art-vfx/poison), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class NatureLib
    {
        /// <summary>Corners of an irregular stone, unit size, in angular order.</summary>
        public static double[][] StoneShape(Rng R, int? count = null)
        {
            int n = count ?? R.Int(5, 7);
            var pts = new double[n][];
            double a0 = R.Range(0, TAU), squash = R.Range(0.7, 1);
            for (int i = 0; i < n; i++)
            {
                double a = a0 + ((i + R.Range(-0.28, 0.28)) * TAU) / n, r = R.Range(0.72, 1.12);
                pts[i] = new[] { Px.Cos(a) * r, Px.Sin(a) * r * squash };
            }
            return pts;
        }

        const double LX = -0.6, LY = -0.8;

        static bool InPoly(double[][] V, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = V.Length - 1; i < V.Length; j = i++)
            {
                double xi = V[i][0], yi = V[i][1], xj = V[j][0], yj = V[j][1];
                if (yi > y != yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        const int ROWS = 400;
        static readonly byte[] MASK = new byte[64 * 64];
        static readonly sbyte[] FACE = new sbyte[64 * 64];
        static readonly float[] RL = new float[ROWS], RR = new float[ROWS], RU = new float[ROWS];

        /// <summary>
        /// A faceted stone at (x, y): corners pts (from StoneShape) scaled by s and turned by a. Each wedge between
        /// neighbouring corners is a facet, lit by how squarely it faces the top left; edges facing the light catch a
        /// bright rim, a dark rim goes round the rest. S: stone ramp, dark to bright (7+ steps). alpha dithers it out.
        /// </summary>
        public static void DrawRock(Painter p, double x, double y, double s, double a, double[][] pts, uint[] S, double alpha = 1, bool white = false)
        {
            int n = S.Length, top = n - 2;
            double ca = Px.Cos(a), sa = Px.Sin(a);
            if (s < 1.4)
            {
                p.Dot(x, y, S[top - 1], alpha);
                p.Dot(x + 1, y, S[top - 3], alpha);
                return;
            }
            int nv = pts.Length;
            var V = new double[nv][];
            for (int i = 0; i < nv; i++)
            {
                double u = pts[i][0], v = pts[i][1];
                V[i] = new[] { (u * ca - v * sa) * s, (u * sa + v * ca) * s };
            }
            var ang = new double[nv];
            for (int i = 0; i < nv; i++) ang[i] = Atan2(V[i][1], V[i][0]);
            // each facet's light: its edge's outward normal against the light
            var lit = new double[nv];
            for (int i = 0; i < nv; i++)
            {
                double ax = V[i][0], ay = V[i][1], bx = V[(i + 1) % nv][0], by = V[(i + 1) % nv][1], ex = bx - ax, ey = by - ay, l = Hypot(ex, ey);
                if (l == 0 || double.IsNaN(l)) l = 1;
                double nx = ey / l, ny = -ex / l;
                if (nx * (ax + bx) + ny * (ay + by) < 0)
                {
                    nx = -nx;
                    ny = -ny;
                }
                lit[i] = nx * LX + ny * LY;
            }
            double X0 = Math.Floor(x) - Math.Ceiling(s * 1.25) - 1, Y0 = Math.Floor(y) - Math.Ceiling(s * 1.25) - 1;
            int N = (int)Math.Min(62, 2 * Math.Ceiling(s * 1.25) + 3);
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                double dx = X0 + i + 0.5 - x, dy = Y0 + j + 0.5 - y;
                int k = j * 64 + i;
                MASK[k] = InPoly(V, dx, dy) ? (byte)1 : (byte)0;
                if (MASK[k] == 0) continue;
                double g = Atan2(dy, dx);
                int f = 0;
                for (int q = 0; q < ang.Length; q++)
                {
                    double a0 = ang[q], a1 = ang[(q + 1) % ang.Length];
                    double d = (g - a0) % TAU, span = (a1 - a0) % TAU;
                    if (d < 0) d += TAU;
                    if (span < 0) span += TAU;
                    if (d <= span)
                    {
                        f = q;
                        break;
                    }
                }
                FACE[k] = (sbyte)f;
            }
            bool At(int i, int j) => i >= 0 && j >= 0 && i < N && j < N && MASK[j * 64 + i] == 1;
            for (int j = -1; j <= N; j++)
            for (int i = -1; i <= N; i++)
            {
                double X = X0 + i, Y = Y0 + j;
                if (!At(i, j))
                {
                    if (At(i - 1, j) || At(i + 1, j) || At(i, j - 1) || At(i, j + 1)) p.Dot(X, Y, S[0], alpha);
                    continue;
                }
                if (white)
                {
                    p.Dot(X, Y, WHITE, alpha);
                    continue;
                }
                double L = lit[FACE[j * 64 + i]];
                bool edge = !At(i - 1, j) || !At(i + 1, j) || !At(i, j - 1) || !At(i, j + 1);
                uint c = L > 0.55 ? S[top] : L > 0.15 ? S[top - 1] : L > -0.3 ? S[top - 2] : S[top - 3];
                if (edge) c = L > 0.3 ? S[top + 1] : L < -0.2 ? S[Math.Max(1, top - 4)] : c;
                p.Dot(X, Y, c, alpha);
            }
        }

        public sealed class Notch
        {
            public double u, d;
            public int side;
        }

        /// <summary>The random look of one spike: notches on its edges, strata across it.</summary>
        public sealed class SpikeLook
        {
            public List<Notch> notches = new List<Notch>();
            public List<double> strata = new List<double>();
            public double slope;
            public int seed;
        }

        /// <summary>What drawSpike's opts carry; unset fields keep the JS defaults.</summary>
        public sealed class SpikeOpts
        {
            public double a = 1;
            public bool white;
            public double glint = -1, clip = 1, half = 0, floor = double.PositiveInfinity;
            public List<List<double[]>> cracks;
            public double crack, heat;
            public uint[] glowRamp;
        }

        /// <summary>
        /// A faceted stone spike standing on (x, base): h tall, w wide at the base, its tip `lean` px over. A ridge
        /// splits a lit left face from a shaded right face; strata step across both; notches chip the edges; a dark
        /// rim goes round it. S: stone ramp (8 steps).
        /// </summary>
        public static void DrawSpike(Painter p, double x, double bas, double h, double w, double lean, SpikeLook sp, uint[] S, SpikeOpts opts = null)
        {
            if (h < 1.5) return;
            opts = opts ?? new SpikeOpts();
            double a = opts.a, glint = opts.glint, clip = opts.clip, floorY = opts.floor;
            bool white = opts.white;
            double Hw(double u, int side)
            {
                double v = (w / 2) * Px.Pow(Math.Max(0, 1 - u), 0.85) + 0.3;
                foreach (Notch n in sp.notches)
                    if (n.side == side && u > n.u && u < n.u + 0.07)
                        v -= n.d * (1 - u * 0.5);
                return Math.Max(0, v);
            }
            double half = opts.half, Y0 = Math.Floor(bas - h) - 2, NR = Math.Min(ROWS, Math.Ceiling(bas) - Y0 + 2);
            // each row's edges, once
            for (int k = 0; k < NR; k++)
            {
                double u = (bas - (Y0 + k + 0.5)) / h;
                RU[k] = (float)u;
                if (u < 0 || u > 1 || Y0 + k + 0.5 > floorY)
                {
                    RL[k] = float.PositiveInfinity;
                    RR[k] = float.NegativeInfinity;
                }
                else
                {
                    RL[k] = (float)(x + lean * u - Hw(u, -1));
                    RR[k] = (float)(x + lean * u + Hw(u, 1));
                }
            }
            bool Inside(double px, double py)
            {
                double kd = py - Y0, fx = px + 0.5;
                if (kd < 0 || kd >= NR) return false;
                int k = (int)kd;
                if (fx < RL[k] || fx > RR[k]) return false;
                if (clip < 1 && RU[k] > clip + (Rand(px, 77) - 0.5) * 0.07) return false;
                if (half == 0) return true;
                double q = ((fx - RL[k]) / Math.Max(0.5, RR[k] - (double)RL[k])) * 2 - 1;
                return half < 0 ? q < -0.18 : q >= -0.18;
            }
            double X0 = Math.Floor(x - w / 2 - Math.Abs(lean) - 2), X1 = Math.Ceiling(x + w / 2 + Math.Abs(lean) + 2);
            for (double py = Math.Floor(bas - h) - 1; py < Math.Ceiling(bas); py++)
            for (double px = X0; px <= X1; px++)
            {
                if (!Inside(px, py))
                {
                    if (py + 0.5 <= floorY && (Inside(px - 1, py) || Inside(px + 1, py) || Inside(px, py + 1) || (Inside(px, py - 1) && py < bas - 1))) p.Dot(px, py, S[0], a);
                    continue;
                }
                if (white)
                {
                    p.Dot(px, py, WHITE, a);
                    continue;
                }
                int k0 = (int)(py - Y0);
                double u = RU[k0], c = x + lean * u, l = RL[k0], r = RR[k0], fx = px + 0.5;
                // which stratum this row sits in: facets jog sideways at each bedding plane, like stacked rock
                int band = 0;
                bool st = false;
                foreach (double s in sp.strata)
                {
                    double d = u * h - s * h - sp.slope * (fx - c);
                    if (d > 0) band++;
                    if (Math.Abs(d) < 0.5) st = true;
                }
                double jog = (Rand((double)band, sp.seed) - 0.5) * 0.3, q = ((fx - l) / Math.Max(0.5, r - l)) * 2 - 1, qr = -0.18 + jog;
                int i;
                if (u > 0.93) i = q < qr + 0.3 ? 7 : 5;
                else if (Math.Abs(q - qr) < Math.Max(0.09, 0.55 / Math.Max(1, r - l))) i = 7;
                else if (q < qr) i = fx - l < 1 ? 7 : q < -0.5 + jog ? 6 : 5;
                else i = r - fx < 1 ? 3 : q < 0.38 + jog ? 4 : 2;
                if (st && i < 7) i -= 1;
                double fleck = Rand(px * 7 + 3, py * 13 + 5);
                if (fleck < 0.035) i = q < qr ? 7 : i - 1;
                else if (fleck > 0.965) i -= 1;
                if (glint >= 0 && q < qr + 0.1)
                {
                    double g = Math.Abs(u - glint);
                    if (g < 0.035) i = 8;
                    else if (g < 0.07) i = Math.Max(i, 7);
                }
                p.Dot(px, py, i >= 8 ? WHITE : S[(int)Clamp(i, 0, S.Length - 1)], a);
            }
            // cracks spreading over it, a lit lip on one side of each
            if (opts.cracks != null && opts.crack > 0)
                foreach (List<double[]> path in opts.cracks)
                {
                    double m = Math.Max(1, Math.Floor(path.Count * opts.crack));
                    for (int k = 1; k < m; k++)
                    {
                        double u0 = path[k - 1][0], v0 = path[k - 1][1], u1 = path[k][0], v1 = path[k][1];
                        double ax = x + lean * u0 + v0 * Hw(u0, v0 < 0 ? -1 : 1), ay = bas - u0 * h;
                        double bx = x + lean * u1 + v1 * Hw(u1, v1 < 0 ? -1 : 1), by = bas - u1 * h;
                        if (u1 > clip) break;
                        if (!Inside(Math.Floor(ax), Math.Floor(ay)) || !Inside(Math.Floor(bx), Math.Floor(by))) continue;
                        double hot = opts.heat;
                        uint[] Gl = opts.glowRamp;
                        if (hot > 0.05 && Gl != null)
                        {
                            // a seam of light inside the crack, a dark lip below it
                            p.Line(ax + 1, ay, bx + 1, by, S[0], a);
                            p.Line(ax, ay, bx, by, hot > 0.7 ? Gl[4] : hot > 0.4 ? Gl[3] : hot > 0.2 ? Gl[2] : Gl[1], a);
                            if (k % 2 == 0) p.Glow(bx, by, 7, Gl[2], 0.35 * hot);
                        }
                        else
                        {
                            p.Line(ax, ay, bx, by, S[0], a);
                            p.Dot(bx + 1, by - 1, S[6], a * 0.7);
                        }
                    }
                }
        }

        public static SpikeLook MakeSpikeLook(Rng R)
        {
            var o = new SpikeLook();
            for (int i = 0, n = R.Int(3, 5); i < n; i++)
            {
                double u = R.Range(0.1, 0.8);
                int side = R.Sign();
                double d = R.Range(0.8, 1.8);
                o.notches.Add(new Notch { u = u, side = side, d = d });
            }
            for (int i = 0, n = R.Int(2, 4); i < n; i++) o.strata.Add(R.Range(0.12, 0.8));
            o.slope = R.Range(-0.25, 0.25);
            o.seed = R.Int(1, 9999);
            return o;
        }

        /// <summary>A crack that branches up a spike: points [u (height 0..1), v (-1 left edge..1 right edge)].</summary>
        public static List<double[]> SpikeCrack(Rng R, double u0, double v0, double dir)
        {
            var pts = new List<double[]> { new[] { u0, v0 } };
            double u = u0, v = v0;
            for (int i = 0; i < 9; i++)
            {
                u += R.Range(0.03, 0.08);
                v += dir * R.Range(0.05, 0.28) + R.Range(-0.12, 0.12);
                if (u > 0.95 || Math.Abs(v) > 1) break;
                pts.Add(new[] { u, v });
            }
            return pts;
        }

        /// <summary>A jagged floor crack from x = 0 out to side * len: points [x, y], y 0..3 into the floor.</summary>
        public static List<double[]> FloorCrack(Rng R, double side, double len)
        {
            var pts = new List<double[]> { new[] { 0.0, 0.0 } };
            for (double x = 0; Math.Abs(x) < len;)
            {
                x += side * R.Range(2, 5);
                pts.Add(new[] { x, (double)R.Int(0, 2) });
            }
            return pts;
        }

        /// <summary>
        /// A leaf len px long and wid px across at its widest, centered on (x, y), turned by ang and flipping about its
        /// midrib by fl. pal: [dark, shade, body, light] (dark to bright). alpha dithers it out.
        /// </summary>
        public static void DrawLeaf(Painter p, double x, double y, double len, double wid, double ang, double fl, uint[] pal, double alpha = 1)
        {
            double ca = Px.Cos(ang), sa = Px.Sin(ang), sq = Px.Cos(fl), w = Math.Max(0.55, Math.Abs(sq) * wid);
            bool under = sq < 0;
            double R = Math.Ceiling(len / 2 + 2), X = Round(x), Y = Round(y);
            int Inside(double i, double j)
            {
                double u = (i + 0.5) * ca + (j + 0.5) * sa + len / 2, v = -(i + 0.5) * sa + (j + 0.5) * ca;
                if (u < 0 || u > len) return 0;
                double half = w * Px.Pow(Px.Sin((Math.PI * u) / len), 0.75);
                return Math.Abs(v) <= half ? (v < 0 ? 1 : 2) : 0;
            }
            for (double j = -R; j <= R; j++)
            for (double i = -R; i <= R; i++)
            {
                int s = Inside(i, j);
                if (s == 0)
                {
                    if (Inside(i - 1, j) != 0 || Inside(i + 1, j) != 0 || Inside(i, j - 1) != 0 || Inside(i, j + 1) != 0) p.Dot(X + i, Y + j, pal[0], alpha);
                    continue;
                }
                double u = (i + 0.5) * ca + (j + 0.5) * sa + len / 2, v = -(i + 0.5) * sa + (j + 0.5) * ca;
                bool rib = Math.Abs(v) < 0.5 && u > len * 0.12 && u < len * 0.88 && w > 1.2;
                p.Dot(X + i, Y + j, rib ? pal[1] : (s == 1) != under ? pal[3] : pal[2], alpha);
            }
            // the stem trails off the base
            p.Dot(X - ca * (len / 2 + 1), Y - sa * (len / 2 + 1), pal[1], alpha);
        }

        /// <summary>A droplet of liquid.</summary>
        public sealed class Drop
        {
            public double x, y, vx, vy, s, delay;
        }

        /// <summary>
        /// Droplets of liquid: arc under gravity with a little drag, drawn as a bright bead with a short tail along
        /// their motion, and land on the floor (onLand(d, y) in update, then they're gone).
        /// </summary>
        public sealed class Drops
        {
            public readonly List<Drop> List = new List<Drop>();
            readonly Func<double, double> ground;

            public Drops(Func<double, double> ground = null) => this.ground = ground ?? (x => 0);

            public void Add(double x, double y, double vx, double vy, double s = 1, double delay = 0) =>
                List.Add(new Drop { x = x, y = y, vx = vx, vy = vy, s = s, delay = delay });

            public void Update(double dt, Action<Drop, double> onLand = null)
            {
                for (int i = List.Count - 1; i >= 0; i--)
                {
                    Drop d = List[i];
                    if (d.delay > 0)
                    {
                        d.delay -= dt;
                        continue;
                    }
                    double drag = 0.6 / d.s, py = d.y;
                    d.vy += Sim.GRAVITY * dt;
                    d.vx -= d.vx * drag * dt;
                    d.vy -= d.vy * drag * 0.5 * dt;
                    d.x += d.vx * dt;
                    d.y += d.vy * dt;
                    double g = ground(d.x);
                    if (d.vy > 0 && py <= g && d.y >= g)
                    {
                        onLand?.Invoke(d, g);
                        List.RemoveAt(i);
                    }
                }
            }

            /// <summary>C: the liquid's ramp, dark to bright (5+ steps).</summary>
            public void Draw(Painter p, uint[] C)
            {
                int n = C.Length;
                foreach (Drop d in List)
                {
                    if (d.delay > 0) continue;
                    double sp = Hypot(d.vx, d.vy);
                    if (sp == 0 || double.IsNaN(sp)) sp = 1;
                    double ux = d.vx / sp, uy = d.vy / sp, len = Math.Min(5, 0.5 + sp * 0.012);
                    p.Line(d.x - ux * len, d.y - uy * len, d.x - ux, d.y - uy, C[n - 4]);
                    p.Px(d.x, d.y, C[n - 1]);
                    if (d.s > 1)
                    {
                        p.Px(d.x - ux, d.y - uy, C[n - 2]);
                        p.Px(d.x + 1, d.y, C[n - 3]);
                    }
                    p.Glow(d.x, d.y, 3 + d.s * 1.5, C[n - 2], 0.3);
                }
            }
        }

        /// <summary>A puff of gas: a darker belly hanging low, a body, a denser core, a lit cap up and to the left (hi: a rim).</summary>
        public static void GasPuff(Painter p, double x, double y, double r, double a, uint belly, uint body, uint cap, uint hi = 0)
        {
            if (a < 0.02 || r < 0.6) return;
            p.Disc(x + r * 0.06, y + r * 0.28, r * 0.9, belly, a * 0.55);
            p.Disc(x, y, r, body, a * 0.45);
            p.Disc(x + r * 0.03, y - r * 0.02, r * 0.76, body, a * 0.5);
            p.Disc(x - r * 0.2, y - r * 0.34, r * 0.52, cap, a * 0.45);
            if (hi != 0) p.Disc(x - r * 0.3, y - r * 0.55, r * 0.26, hi, a * 0.32);
        }

        /// <summary>
        /// A bubble of radius r centered at (x, y): a 1 px skin (lit c1 on its upper left, c2 elsewhere), a
        /// translucent belly, a white glint. dome: only the part above y (a bubble swelling on a surface).
        /// </summary>
        public static void Bubble(Painter p, double x, double y, double r, uint c1, uint c2, uint fill, double a = 1, bool dome = false)
        {
            double R = Math.Ceiling(r) + 1, X = Round(x), Y = Round(y);
            for (double j = -R; j <= (dome ? 0 : R); j++)
            for (double i = -R; i <= R; i++)
            {
                double d = Hypot(i, j);
                if (d > r + 0.45) continue;
                if (d > r - 0.6) p.Dot(X + i, Y + j, i < 0 && j < -r * 0.2 ? c1 : c2, a);
                else p.Blend(X + i, Y + j, fill, 0.3 * a);
            }
            if (r >= 1.5) p.Dot(X - Round(r * 0.45), Y - Round(r * 0.5), WHITE, a);
        }

        public sealed class Puff
        {
            public double x, y, vx, vy, r0, r1, t0, end, rise, seed;
        }

        public struct PuffAt
        {
            public double x, y, r, a, u;
        }

        /// <summary>
        /// Puffs of gas with their own clock: Add(t, x, y, vx, vy, r0, r1, end, rise) launches one at time t that drifts
        /// off its push (fading drag), rises at `rise` px/s, sways, swells from r0 to r1 and thins out by `end`.
        /// </summary>
        public sealed class Gas
        {
            public readonly List<Puff> List = new List<Puff>();
            int n;

            public void Add(double t, double x, double y, double vx, double vy, double r0, double r1, double end, double rise = 0) =>
                List.Add(new Puff { x = x, y = y, vx = vx, vy = vy, r0 = r0, r1 = r1, t0 = t, end = end, rise = rise, seed = (n++ * 37.7) % 100 });

            public void Prune(double t)
            {
                for (int i = List.Count - 1; i >= 0; i--)
                    if (t > List[i].end)
                        List.RemoveAt(i);
            }

            public PuffAt At(Puff q, double t)
            {
                double age = t - q.t0, e = 1 - Exp(-age * 2.2), sw = Math.Min(1, age);
                double x = q.x + (q.vx / 2.2) * e + Px.Sin(t * 1.7 + q.seed) * 2.5 * sw, y = q.y + (q.vy / 2.2) * e - q.rise * age + Px.Sin(t * 1.3 + q.seed * 2) * 1.5 * sw;
                double u = Clamp(age / Math.Max(0.01, q.end - q.t0)), r = q.r0 + (q.r1 - q.r0) * (1 - Px.Pow(1 - Math.Min(1, age / 1.2), 3));
                return new PuffAt { x = x, y = y, r = r, a = Clamp(age / 0.15) * (1 - Clamp((u - 0.4) / 0.6)), u = u };
            }

            public void Draw(Painter p, double t, uint belly, uint body, uint cap, uint hi = 0, double k = 1, uint glow = 0)
            {
                foreach (Puff q in List)
                {
                    PuffAt g = At(q, t);
                    if (g.a <= 0.02) continue;
                    GasPuff(p, g.x, g.y, g.r, g.a * k, belly, body, cap, g.u < 0.5 ? hi : 0);
                    if (glow != 0 && g.r > 4) p.Glow(g.x, g.y, g.r * 1.6, glow, 0.07 * g.a * k);
                }
            }
        }
    }
}
