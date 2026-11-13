// Shared by the ice effects: panes of ice that tumble and skid, polygon helpers, a character turned to ice,
// cracks that race through a shape. Files starting with _ are helpers, not effects.
// The crack walk and the faceted crystal shading are adapted from Sprite Fusion's open-source ice
// (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class IceLib
    {

        /// <summary>Is (x, y) inside the polygon pts [[x, y], ...]? (even-odd rule)</summary>
        public static bool InPoly(double[][] pts, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                if (yi > y != yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>A polygon's centroid and area.</summary>
        public static (double x, double y, double area) Centroid(double[][] pts)
        {
            double a = 0, cx = 0, cy = 0;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                double x0 = pts[j][0], y0 = pts[j][1], x1 = pts[i][0], y1 = pts[i][1], k = x0 * y1 - x1 * y0;
                a += k;
                cx += (x0 + x1) * k;
                cy += (y0 + y1) * k;
            }
            if (Math.Abs(a) < 1e-6)
            {
                int n = pts.Length;
                double sx = 0, sy = 0;
                foreach (double[] q in pts) sx += q[0];
                foreach (double[] q in pts) sy += q[1];
                return (sx / n, sy / n, 0);
            }
            return (cx / (3 * a), cy / (3 * a), Math.Abs(a) / 2);
        }

        /// <summary>Cut a polygon along the line through (x, y) at angle a: the pieces with three corners or more.</summary>
        public static List<double[][]> SplitPoly(double[][] pts, double x, double y, double a)
        {
            double nx = -Px.Sin(a), ny = Px.Cos(a);
            double Side(double[] q) => (q[0] - x) * nx + (q[1] - y) * ny;
            var L = new List<double[]>();
            var Rt = new List<double[]>();
            for (int i = 0; i < pts.Length; i++)
            {
                double[] p = pts[i], q = pts[(i + 1) % pts.Length];
                double sp = Side(p), sq = Side(q);
                (sp >= 0 ? L : Rt).Add(p);
                if (sp >= 0 != sq >= 0)
                {
                    double u = sp / (sp - sq);
                    double[] m = { p[0] + (q[0] - p[0]) * u, p[1] + (q[1] - p[1]) * u };
                    L.Add(m);
                    Rt.Add(m);
                }
            }
            var o = new List<double[][]>();
            if (L.Count >= 3) o.Add(L.ToArray());
            if (Rt.Count >= 3) o.Add(Rt.ToArray());
            return o;
        }

        /// <summary>A color turned to ice: its brightness picks a step of the ice ramp C (dark to bright), never the darkest.</summary>
        public static uint IceOf(uint c, uint[] C)
        {
            int n = C.Length;
            double l = Luma(c);
            return C[(int)Clamp(Round(((l < 0.12 ? 2 : 2 + l * 5.2) * (n - 1)) / 7), 1, n - 1)];
        }

        /// <summary>Fill a polygon with translucent color (true blending, no dither): glass, ice, light.</summary>
        public static void GlassPoly(Painter p, double[][] pts, uint c, double a)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (double[] q in pts)
            {
                x0 = Math.Min(x0, q[0]);
                x1 = Math.Max(x1, q[0]);
                y0 = Math.Min(y0, q[1]);
                y1 = Math.Max(y1, q[1]);
            }
            for (double y = Math.Floor(y0); y <= Math.Ceiling(y1); y++)
                for (double x = Math.Floor(x0); x <= Math.Ceiling(x1); x++)
                    if (InPoly(pts, x + 0.5, y + 0.5)) p.Blend(x, y, c, a);
        }

        /// <summary>A pane of ice (see Panes).</summary>
        public sealed class Pane
        {
            public double[][] pts;
            public double x, y, vx, vy, a, va, f, vf, lit = 0.6, g = 1, drag = 0.3, bounce = 0.35, life = 1, max;
            /// <summary>Breaks on that many hard landings (0: never; JS true counts as 1).</summary>
            public double brittle;
            public double? crumble, tone, ridge;
            public bool solid, rest;
            public double restT;
            public int hits;
            internal int bornX, bornY;
        }

        /// <summary>
        /// Panes of ice: flat polygons that fly, spin and tumble end over end (they squash as they turn edge-on and
        /// flash as they square up to the light), bounce on the floor, skid, settle and fade. Drawn as glass: a
        /// translucent face, edges lit where they face the light, a glint.
        /// </summary>
        public sealed class Panes
        {
            public readonly List<Pane> List = new List<Pane>();
            readonly Func<double, double> ground;
            readonly Action<Pane, double, double, double> onLand;
            readonly Action<Pane> onCrumble;
            readonly Action<Pane, double, double> onSkid;
            readonly Emitter emitter;

            public Panes(Func<double, double> ground = null, Action<Pane, double, double, double> onLand = null, Action<Pane> onCrumble = null,
                Action<Pane, double, double> onSkid = null, Emitter emitter = null)
            {
                this.ground = ground ?? (x => double.PositiveInfinity);
                this.onLand = onLand;
                this.onCrumble = onCrumble;
                this.onSkid = onSkid;
                this.emitter = emitter;
            }

            public int Count => List.Count;

            public void Add(Pane q)
            {
                q.max = q.life;
                Sim.Born(emitter, out q.bornX, out q.bornY);
                List.Add(q);
            }

            /// <summary>The pane's corners as drawn: tumbled (squashed across its own width), turned and placed.</summary>
            public static double[][] Corners(Pane q)
            {
                double ca = Px.Cos(q.a), sa = Px.Sin(q.a), k = Px.Cos(q.f);
                var o = new double[q.pts.Length][];
                for (int i = 0; i < q.pts.Length; i++)
                {
                    double x = q.pts[i][0], yy = q.pts[i][1] * k;
                    o[i] = new[] { q.x + x * ca - yy * sa, q.y + x * sa + yy * ca };
                }
                return o;
            }

            static double Seat(Pane q)
            {
                double low = double.NegativeInfinity;
                foreach (double[] c in Corners(q)) low = Math.Max(low, c[1]);
                return low;
            }

            public void Update(double dt)
            {
                for (int i = List.Count - 1; i >= 0; i--)
                {
                    Pane q = List[i];
                    if ((q.life -= dt) <= 0)
                    {
                        List.RemoveAt(i);
                        continue;
                    }
                    double floor = ground(q.x) - 0.5;
                    if (q.rest && q.crumble.HasValue && (q.restT = q.restT + dt) > q.crumble.Value)
                    {
                        onCrumble?.Invoke(q);
                        List.RemoveAt(i);
                        continue;
                    }
                    if (q.rest)
                    {
                        // skidding: friction slows the slide and the spin, the pane stays seated on the floor
                        if (Math.Abs(q.vx) > 8) onSkid?.Invoke(q, q.x, floor + 0.5);
                        q.x += q.vx * dt;
                        q.vx -= q.vx * 7 * dt;
                        q.a += q.va * dt;
                        q.va -= q.va * 9 * dt;
                        q.vf -= q.vf * 9 * dt;
                        q.f += q.vf * dt;
                        q.y -= Seat(q) - floor;
                        continue;
                    }
                    q.vy += Sim.GRAVITY * q.g * dt;
                    q.vx -= q.vx * q.drag * dt;
                    q.vy -= q.vy * q.drag * dt;
                    q.x += q.vx * dt;
                    q.y += q.vy * dt;
                    q.a += q.va * dt;
                    q.f += q.vf * dt;
                    double low = Seat(q);
                    if (q.g <= 0 || low < floor || q.vy <= 0) continue;
                    q.y -= low - floor;
                    if (q.vy > 70)
                    {
                        q.hits++;
                        onLand?.Invoke(q, q.x, floor + 0.5, q.vy);
                    }
                    if (q.brittle != 0 && q.vy > 70 && q.hits >= q.brittle)
                    {
                        List.RemoveAt(i);
                        continue;
                    }
                    if (q.vy > 70 && q.bounce > 0)
                    {
                        q.vy = -q.vy * q.bounce;
                        q.vx *= 0.75;
                        q.va *= 0.6;
                        q.vf *= 0.5;
                    }
                    else
                    {
                        q.rest = true;
                        q.vy = 0;
                    }
                }
            }

            public void Draw(Painter p, uint[] C)
            {
                int n = C.Length;
                foreach (Pane q in List)
                {
                    Sim.Shift(p, emitter, q.bornX, q.bornY);
                    double a = Math.Min(1, q.life / 0.35), k = Px.Cos(q.f), face = Math.Abs(k);
                    double[][] pts = Corners(q);
                    // the face: glassy, brighter the more it faces us and the more light it catches; it flashes as it squares up
                    double lv = q.lit * (0.35 + 0.65 * face);
                    bool flash = face > 0.97 && !q.rest;
                    if (face > 0.25)
                    {
                        if (q.solid && q.tone.HasValue)
                        {
                            // a chunk of one face: its tone, brighter as it squares up to the light
                            p.Poly(pts, flash ? C[n - 1] : C[(int)Clamp(Round(1 + q.tone.Value * 5 * (0.6 + 0.4 * face)), 1, n - 2)], a);
                        }
                        else if (q.solid)
                        {
                            // shaded like the blade it broke from: a lit side, a pale ridge, a deep side
                            double rr = q.ridge ?? -Math.PI / 2, cr = Px.Cos(rr), sr = Px.Sin(rr), ca = Px.Cos(q.a), sa = Px.Sin(q.a);
                            double rx = cr * ca - sr * k * sa, ry = cr * sa + sr * k * ca;
                            double nx = -ry, ny = rx;
                            if (nx * -0.55 + ny * -0.84 < 0)
                            {
                                nx = -nx;
                                ny = -ny;
                            }
                            uint lit = flash ? C[n - 1] : C[(int)Clamp(Round(3 + lv * 3), 3, n - 2)], deep = C[(int)Clamp(Round(1 + lv * 2), 1, 3)];
                            double qx = q.x, qy = q.y;
                            p.Poly(pts, (x, y) =>
                            {
                                double d = (x - qx) * nx + (y - qy) * ny;
                                return d > 0.6 ? lit : d > -0.6 ? C[n - 2] : deep;
                            }, a);
                        }
                        else GlassPoly(p, pts, flash ? C[n - 1] : C[(int)Clamp(Round(3 + lv * 3), 3, n - 2)], (0.3 + 0.35 * lv + (flash ? 0.3 : 0)) * a);
                    }
                    double cx = 0, cy = 0;
                    int top = 0;
                    foreach (double[] pt in pts)
                    {
                        cx += pt[0];
                        cy += pt[1];
                    }
                    cx /= pts.Length;
                    cy /= pts.Length;
                    // edges: lit where they face the light (up and left), cool where they face away
                    for (int i = 0; i < pts.Length; i++)
                    {
                        double x0 = pts[i][0], y0 = pts[i][1], x1 = pts[(i + 1) % pts.Length][0], y1 = pts[(i + 1) % pts.Length][1];
                        double nx = y1 - y0, ny = x0 - x1;
                        if (nx * ((x0 + x1) / 2 - cx) + ny * ((y0 + y1) / 2 - cy) < 0)
                        {
                            nx = -nx;
                            ny = -ny;
                        }
                        double l = Px.Hypot(nx, ny);
                        if (l == 0 || double.IsNaN(l)) l = 1;
                        double lit = (-nx * 0.55 - ny * 0.84) / l;
                        p.Line(x0, y0, x1, y1, face < 0.25 || lit > 0.35 ? C[n - 1] : lit > -0.25 ? C[n - 2] : q.solid ? C[1] : C[Math.Max(2, n - 4)], a);
                        if (pts[i][1] < pts[top][1]) top = i;
                    }
                    if (face > 0.7) p.Dot(pts[top][0], pts[top][1], WHITE, a);
                    p.Glow(cx, cy, 5, C[n - 3], (flash ? 0.5 : 0.14) * a);
                }
                p.Shift(0, 0);
            }
        }

        /// <summary>
        /// A jagged ice blade standing on its base (bx, by), tip at (tx, ty), w wide at the base. Shaded in an ice ramp
        /// C (dark to bright, 8 steps): a lit facet on the left with a pale edge, a bright ridge, a deep facet on the
        /// right, a white tip, a dark rim, and a notch cut into one side so it reads as a shard, not a cone.
        /// a: alpha; white: flash it white; glint: 0..1 a highlight across it at that height, or -1; jag: 0..1 where the
        /// notch starts, or -1; dim: steps darker, for back rows.
        /// </summary>
        public static void Blade(Painter p, double bx, double by, double tx, double ty, double w, uint[] C, double a = 1, bool white = false, double glint = -1, double jag = -1, double dim = 0)
        {
            double dx = tx - bx, dy = ty - by, L = Px.Hypot(dx, dy);
            if (L < 1.5 || w < 0.5) return;
            double ux = dx / L, uy = dy / L, nx = -uy, ny = ux;
            uint K(double i) => C[(int)Clamp(i - dim, 0, C.Length - 1)];
            double hw = w / 2;
            // half widths on the left (lit) and right (shadow) sides at height u; the notch steps the right side in
            double Left(double u) => hw * (1 - u) + 0.35;
            double Right(double u) => hw * (1 - u) * (jag >= 0 && u > jag ? 0.55 : 1) + 0.35;
            double Ridge(double u) => -w * 0.12 * (1 - u);
            double pad = hw + 2;
            p.Shade(Math.Min(bx, tx) - pad, Math.Min(by, ty) - pad, Math.Max(bx, tx) + pad, Math.Max(by, ty), (x, y, o) =>
            {
                double rx = x - bx, ry = y - by, u = (rx * ux + ry * uy) / L, s = rx * nx + ry * ny;
                if (u < 0) return 0;
                double l = Left(Math.Min(u, 1)), r = Right(Math.Min(u, 1));
                if (u > 1 || s < -l || s > r)
                {
                    // a dark rim just outside the blade
                    double over = u > 1 ? (u - 1) * L : 0, outside = Math.Max(over, s < -l ? -l - s : s - r);
                    if (outside < 1 && u < 1 + 1 / L)
                    {
                        o.c = K(0);
                        o.a = a;
                    }
                    return 0;
                }
                o.a = a;
                if (white) return o.c = WHITE;
                double rd = Ridge(u);
                uint c;
                if (u > 0.88) c = WHITE;
                else if (Math.Abs(s - rd) < 0.55) c = K(6);
                else if (s < rd) c = s + l < 1 ? K(6) : (s + l) / Math.Max(1, rd + l) > 0.55 ? K(5) : K(4);
                else c = r - s < 1.1 ? K(1) : (s - rd) / Math.Max(1, r - rd) > 0.5 ? K(2) : K(3);
                // the notch's ledge catches light; a little glitter in the lit facet
                if (jag >= 0 && Math.Abs(u - jag) * L < 0.8 && s > rd) c = K(6);
                else if (s < rd && c != K(6) && (ToInt32(Math.Floor(x) * 73856093) ^ ToInt32(Math.Floor(y) * 19349663)) % 997 < 30) c = K(7);
                if (glint >= 0 && s <= rd + 0.6)
                {
                    double g = Math.Abs(u - glint) * L;
                    if (g < 0.8) c = WHITE;
                    else if (g < 1.8) c = K(6);
                }
                o.c = c;
                return 0;
            });
        }

        /// <summary>A snowflake design: pairs of side branches along each arm ([where 0..1, length 0..1 of the arm]), maybe a plate, maybe forked tips.</summary>
        public sealed class FlakeDesign
        {
            public double[][] branches;
            public bool plate, fork;
        }

        public static FlakeDesign FlakeDesignOf(Rng R)
        {
            int n = R.Int(1, 3);
            var branches = new List<double[]>();
            for (int i = 0; i < n; i++)
            {
                double u = 0.38 + (0.4 * (i + R.Range(0.2, 0.8))) / n;
                branches.Add(new[] { u, R.Range(0.24, 0.36) * (1 - i * 0.15) });
            }
            bool plate = R.Chance(0.45);
            return new FlakeDesign { branches = branches.ToArray(), plate = plate, fork = R.Chance(0.45) };
        }

        static readonly double[][] PoseA = { new double[] { 1, 0 }, new double[] { -1, 0 }, new double[] { 0, 1 }, new double[] { 0, -1 } };
        static readonly double[][] PoseB = { new double[] { 1, 1 }, new double[] { -1, -1 }, new double[] { 1, -1 }, new double[] { -1, 1 } };
        static readonly double[][] Middling = { new[] { 0.58, 0.36 } };
        static readonly int[] Sides = { -1, 1 };

        /// <summary>
        /// A six-armed snowflake of radius r turned by rot, crisp 1 px lines: pale arms, side branches at 60 degrees,
        /// a plate or forked tips by design, a white heart, light. hot (0..1) makes it twinkle white.
        /// </summary>
        public static void Snowflake(Painter p, double x, double y, double r, double rot, FlakeDesign d, uint[] C, double a = 1, double hot = 0)
        {
            int n = C.Length;
            rot = Round(rot / (Math.PI / 18)) * (Math.PI / 18); // turn in 10 degree ticks, like sprite frames
            if (r < 1.2)
            {
                p.Dot(x, y, WHITE, a);
                return;
            }
            if (r < 2.6)
            {
                // too small for branches: a six-point star that flips between two poses as it turns
                double pose = Math.Floor(((rot % (Math.PI / 3)) + Math.PI / 3) / (Math.PI / 6)) % 2;
                double[][] pts = pose != 0 && !double.IsNaN(pose) ? PoseA : PoseB;
                foreach (double[] q in pts) p.Dot(x + q[0], y + q[1], hot > 0.5 ? WHITE : C[n - 3], a);
                p.Dot(x, y, WHITE, a);
                p.Glow(x, y, 4, C[n - 3], 0.2 * a);
                return;
            }
            // detail by size: a bare star when small, one pair of branches when middling, the full design when big
            uint arm = hot > 0.5 ? WHITE : C[n - 2], br = hot > 0.5 ? C[n - 2] : C[n - 3];
            bool big = r >= 7;
            double[][] branches = r < 4 ? new double[0][] : big ? Take(d.branches, 2) : Middling;
            for (int k = 0; k < 6; k++)
            {
                double g = rot + (k * TAU) / 6, ca = Px.Cos(g), sa = Px.Sin(g);
                p.Line(x, y, x + ca * r, y + sa * r, arm, a);
                foreach (double[] b in branches)
                {
                    double u = b[0], l = b[1], bx = x + ca * r * u, by = y + sa * r * u, bl = Math.Max(1, r * l);
                    foreach (int s in Sides) p.Line(bx, by, bx + Px.Cos(g + (s * Math.PI) / 3) * bl, by + Px.Sin(g + (s * Math.PI) / 3) * bl, br, a);
                }
                if (d.fork && big && !d.plate)
                    foreach (int s in Sides) p.Dot(x + ca * r + Px.Cos(g + s * 0.9) * 1.2, y + sa * r + Px.Sin(g + s * 0.9) * 1.2, br, a);
                p.Dot(x + ca * r, y + sa * r, WHITE, a);
            }
            if (d.plate && big)
            {
                double q = r * 0.34;
                for (int k = 0; k < 6; k++)
                {
                    double g0 = rot + Math.PI / 6 + (k * TAU) / 6, g1 = g0 + TAU / 6;
                    p.Line(x + Px.Cos(g0) * q, y + Px.Sin(g0) * q, x + Px.Cos(g1) * q, y + Px.Sin(g1) * q, C[n - 3], a);
                }
            }
            p.Px(x, y, WHITE);
            p.Glow(x, y, 3 + r * 1.2, C[n - 4], (0.14 + 0.35 * hot) * a);
            if (hot > 0.3) p.Glow(x, y, 2 + r * 0.5, WHITE, 0.3 * hot * a);
        }

        static double[][] Take(double[][] list, int n)
        {
            var o = new double[Math.Min(n, list.Length)][];
            Array.Copy(list, o, o.Length);
            return o;
        }

        /// <summary>Nothing outlives the effect: trims the life of every particle, bit and pane that would last past D.</summary>
        public static void EndBy(double t, double D, Particles ps, Bits bits = null, Panes panes = null)
        {
            double left = Math.Max(0.001, D - 0.05 - t);
            foreach (Particles.Particle q in ps.List)
                if (q.life > left) q.life = left;
            if (bits != null)
                foreach (Bits.Bit q in bits.List)
                    if (q.life > left) q.life = left;
            if (panes != null)
                foreach (Pane q in panes.List)
                    if (q.life > left) q.life = left;
        }

        /// <summary>
        /// Cracks racing out from (x, y) through a shape: n jagged lines that wander, fork now and then and stop at
        /// the shape's edge (inside(x, y) says what's in it). Each point carries its distance from the start along the
        /// crack, so DrawFractures can grow them at a speed. Returns polylines [[x, y, d], ...].
        /// </summary>
        public static List<double[][]> Fractures(Rng R, Func<double, double, bool> inside, double x, double y, int n, double[] step = null, double len = 18,
            double fork = 0.16, double? a0 = null, double spread = TAU)
        {
            var output = new List<double[][]>();
            step = step ?? new[] { 1.6, 2.6 };
            double A0 = a0 ?? R.Range(0, TAU);
            void Walk(double wx, double wy, double d, double a, double wlen, int depth)
            {
                var pts = new List<double[]> { new[] { wx, wy, d } };
                for (int s = 0; s < wlen; s++)
                {
                    a += R.Range(-0.55, 0.55);
                    double st = R.Range(step[0], step[1]), nx = wx + Px.Cos(a) * st, ny = wy + Px.Sin(a) * st;
                    if (!inside(nx, ny)) break;
                    wx = nx;
                    wy = ny;
                    d += st;
                    pts.Add(new[] { wx, wy, d });
                    if (depth < 2 && pts.Count > 2 && R.Chance(fork)) Walk(wx, wy, d, a + R.Sign() * R.Range(0.6, 1.2), Math.Ceiling(wlen * 0.5), depth + 1);
                }
                if (pts.Count > 1) output.Add(pts.ToArray());
            }
            for (int i = 0; i < n; i++) Walk(x, y, 0, A0 + ((double)i / n) * spread + R.Range(-0.3, 0.3) * Math.Min(1, spread / TAU), len, 0);
            return output;
        }

        /// <summary>
        /// Draw cracks grown out to distance reach from their start: a bright line over a dark one a pixel lower (an
        /// engraved fracture), a white-hot tip while they run, light leaking out along them. ox, oy shift them.
        /// </summary>
        public static void DrawFractures(Painter p, List<double[][]> cracks, double reach, uint[] C, double ox = 0, double oy = 0, bool hot = true, double a = 1)
        {
            int n = C.Length;
            bool Seg(double[][] pts, int i, out double sx0, out double sy0, out double sx1, out double sy1, out bool tip)
            {
                double x0 = pts[i - 1][0], y0 = pts[i - 1][1], d0 = pts[i - 1][2], x1 = pts[i][0], y1 = pts[i][1], d1 = pts[i][2];
                sx0 = sy0 = sx1 = sy1 = 0;
                tip = false;
                if (d0 >= reach) return false;
                double u = Math.Min(1, (reach - d0) / Math.Max(0.01, d1 - d0));
                sx0 = x0 + ox;
                sy0 = y0 + oy;
                sx1 = x0 + ox + (x1 - x0) * u;
                sy1 = y0 + oy + (y1 - y0) * u;
                tip = reach - d1 < 3;
                return true;
            }
            foreach (double[][] pts in cracks)
                for (int i = 1; i < pts.Length; i++)
                {
                    if (!Seg(pts, i, out double x0, out double y0, out double x1, out double y1, out _)) break;
                    p.Line(x0, y0 + 1, x1, y1 + 1, C[Math.Min(2, n - 1)], a);
                }
            foreach (double[][] pts in cracks)
                for (int i = 1; i < pts.Length; i++)
                {
                    if (!Seg(pts, i, out double x0, out double y0, out double x1, out double y1, out bool s4)) break;
                    bool tip = hot && s4;
                    p.Line(x0, y0, x1, y1, tip ? WHITE : C[n - 2], a);
                    if (tip) p.Glow(x1, y1, 5, C[n - 3], 0.5 * a);
                    else if (i % 2 != 0) p.Glow(x1, y1, 3, C[n - 3], 0.18 * a);
                }
        }
    }
}
