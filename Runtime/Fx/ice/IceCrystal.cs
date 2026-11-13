// A floating pickup that bobs, glints, glows and has motes circling in front and behind follows the recipe
// in Sprite Fusion's open-source pickup gem (https://www.spritefusion.com/pixel-art-vfx/pickup),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class IceCrystal : Effect
    {
        const double D = 3;

        public static readonly Module Module = new Module("ice-crystal", "pixelfx/fx/ice/ice-crystal", 96, 100, 48, 92, D, true, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["height"] = 34,
                ["seed"] = 21,
            },
            (o, e) => new IceCrystal(o))
        {
            Title = "Ice crystal",
            Desc = "A floating ice crystal turning slowly in its own cold light: its facets brighten and darken as they turn, glints flash on them, frost motes circle it.",
            Tags = new[] { "ice", "crystal", "gem", "pickup", "float", "magic", "loop" },
            Bg = "ground",
        };

        const double TIP = 0.28; // how far it leans toward us, so the upper facets show
        static readonly double[] LIGHT = Norm(new[] { -0.55, -0.75, -0.45 }); // toward the light: upper left, in front

        static double[] Norm(double[] v)
        {
            double l = Px.Hypot(v[0], v[1], v[2]);
            if (l == 0 || double.IsNaN(l)) l = 1;
            return new[] { v[0] / l, v[1] / l, v[2] / l };
        }

        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        sealed class Face
        {
            public double[][] pts;
            public double lit, spec, cx, cy;
        }

        // the crystal: a hexagonal prism with a tall point above and a shorter one below, turned by th, tipped toward
        // the viewer. Returns the visible faces (projected corners, brightness, glint).
        static List<Face> CrystalFaces(double th, double R)
        {
            double ct = Px.Cos(TIP), st = Px.Sin(TIP);
            double[] P(double x, double y, double z) => new[] { x, y * ct - z * st, y * st + z * ct }; // tip toward the viewer (z toward us is negative)
            double[][] Ring(double y)
            {
                var o = new double[6][];
                for (int k = 0; k < 6; k++) o[k] = P(Px.Cos(th + (k * TAU) / 6) * R, y, Px.Sin(th + (k * TAU) / 6) * R);
                return o;
            }
            double[][] top = Ring(-R * 0.45), bot = Ring(R * 0.45);
            double[] A = P(0, -R * 2.2, 0), Z = P(0, R * 1.7, 0);
            var faces = new List<double[][]>();
            for (int k = 0; k < 6; k++)
            {
                int j = (k + 1) % 6;
                faces.Add(new[] { A, top[j], top[k] });
                faces.Add(new[] { top[k], top[j], bot[j], bot[k] });
                faces.Add(new[] { bot[k], bot[j], Z });
            }
            var output = new List<Face>();
            foreach (double[][] f in faces)
            {
                double[] a = f[0], b = f[1], c = f[2];
                double[] u = { b[0] - a[0], b[1] - a[1], b[2] - a[2] }, v = { c[0] - a[0], c[1] - a[1], c[2] - a[2] };
                double[] n = Norm(new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] });
                double cx = 0, cy = 0, cz = 0;
                foreach (double[] q in f) cx += q[0];
                foreach (double[] q in f) cy += q[1];
                foreach (double[] q in f) cz += q[2];
                cx /= f.Length;
                cy /= f.Length;
                cz /= f.Length;
                if (n[0] * cx + n[1] * cy + n[2] * cz < 0) n = new[] { -n[0], -n[1], -n[2] }; // outward
                if (n[2] >= -0.02) continue; // facing away
                double lit = Math.Max(0, n[0] * LIGHT[0] + n[1] * LIGHT[1] + n[2] * LIGHT[2]);
                // a glint where the face mirrors the light toward the viewer (half way between the light and the eye)
                double[] h = Norm(new[] { LIGHT[0], LIGHT[1], LIGHT[2] - 1 });
                double spec = Math.Max(0, n[0] * h[0] + n[1] * h[1] + n[2] * h[2]);
                output.Add(new Face { pts = f.Select(q => new[] { q[0], q[1] }).ToArray(), lit = lit, spec = spec, cx = cx, cy = cy });
            }
            return output;
        }

        // the convex hull of a set of points (monotone chain), for the crystal's silhouette
        static List<double[]> HullOf(IEnumerable<double[]> pts)
        {
            List<double[]> P = pts.OrderBy(q => q[0]).ThenBy(q => q[1]).ToList();
            double Cross3(double[] o, double[] a, double[] b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
            var lo = new List<double[]>();
            var hi = new List<double[]>();
            foreach (double[] q in P)
            {
                while (lo.Count >= 2 && Cross3(lo[lo.Count - 2], lo[lo.Count - 1], q) <= 0) lo.RemoveAt(lo.Count - 1);
                lo.Add(q);
            }
            P.Reverse();
            foreach (double[] q in P)
            {
                while (hi.Count >= 2 && Cross3(hi[hi.Count - 2], hi[hi.Count - 1], q) <= 0) hi.RemoveAt(hi.Count - 1);
                hi.Add(q);
            }
            var o2 = lo.Take(lo.Count - 1).ToList();
            o2.AddRange(hi.Take(hi.Count - 1));
            return o2;
        }

        readonly Options o;

        IceCrystal(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, bob = Px.Sin(TAU * ph) * 2.5, Y = -o.Num("height") + bob;
            // its shadow on the floor, smaller and darker as it dips, and a cold pool of light
            double s = 1 - bob / 12, rx = 11 * s;
            for (double x = Math.Floor(-rx); x <= rx; x++)
            {
                double f = 1 - Px.Pow(x / rx, 2);
                p.Blend(x + 0.5, 0, C[0], 0.65 * f);
                if (Math.Abs(x) < rx * 0.6) p.Blend(x + 0.5, 1, C[0], 0.4 * f);
            }
            p.Glow(0, -1, 24, C[4], 0.2 + 0.05 * Px.Sin(TAU * ph * 2));
            Motes(p, C, t, o, Y, false);
        }

        static void Motes(Painter p, uint[] C, double t, Options o, double Y, bool front)
        {
            Cycle(t, D, 16, o.Seed, g => g.Range(1.6, 2.6), (age, life, r, i) =>
            {
                double th = r.Range(0, TAU) + age * r.Range(1.6, 2.6), R = r.Range(14, 22), y = Y + r.Range(-14, 12) - age * r.Range(3, 7), s = Px.Sin(th), k = age / life;
                if (s >= 0 != front) return;
                double x = Px.Cos(th) * R, yy = y + s * R * 0.3, a = Math.Min(1, age / 0.2) * (k > 0.75 ? (1 - k) / 0.25 : 1);
                if (front)
                {
                    p.Dot(x, yy, i % 4 == 0 ? WHITE : C[6], a);
                    if (i % 4 == 0)
                        foreach (int[] q in Cross) p.Dot(x + q[0], yy + q[1], C[5], a);
                    p.Glow(x, yy, 4, C[5], 0.3 * a);
                }
                else p.Dot(x, yy, C[4], a * 0.9);
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, bob = Px.Sin(TAU * ph) * 2.5, Y = -o.Num("height") + bob, R = 8;
            // its own cold light
            p.Glow(0, Y, 34, C[3], 0.26);
            p.Glow(0, Y, 14, C[5], 0.18 + 0.08 * Px.Sin(TAU * ph * 3));
            // a sixth of a turn a loop: a hexagon looks the same after it, so the loop never seams
            List<Face> faces = CrystalFaces((TAU / 6) * ph, R);
            // a dark rim round the silhouette, then the facets: deep in shadow, pale toward the light, white in a glint
            List<double[]> hull = HullOf(faces.SelectMany(f => f.pts));
            double mx = 0, my = 0;
            foreach (double[] q in hull)
            {
                mx += q[0];
                my += q[1];
            }
            mx /= hull.Count;
            my /= hull.Count;
            p.Poly(hull.Select(q =>
            {
                double x = q[0], y = q[1], d = Hypot(x - mx, y - my);
                if (d == 0 || double.IsNaN(d)) d = 1;
                return new[] { x + ((x - mx) / d) * 1.2, y + Y + ((y - my) / d) * 1.2 };
            }).ToArray(), C[1]);
            foreach (Face f in faces)
            {
                uint c = f.spec > 0.985 ? WHITE : C[(int)Clamp(Round(1 + Px.Pow(f.lit, 1.4) * 6.6), 1, 7)];
                p.Poly(f.pts.Select(q => new[] { q[0], q[1] + Y }).ToArray(), c);
            }
            // edges: pale where two faces meet, a dark rim round the outside, a white ridge on the lit side
            foreach (Face f in faces)
                for (int i = 0; i < f.pts.Length; i++)
                {
                    double x0 = f.pts[i][0], y0 = f.pts[i][1], x1 = f.pts[(i + 1) % f.pts.Length][0], y1 = f.pts[(i + 1) % f.pts.Length][1];
                    p.Line(x0, y0 + Y, x1, y1 + Y, f.lit > 0.6 ? C[7] : f.lit > 0.3 ? C[6] : C[4]);
                }
            // glints: a star flashes on a face as it turns into the light
            foreach (Face f in faces)
                if (f.spec > 0.96) Sim.Sparkle(p, f.cx, f.cy + Y, Round(2 + (f.spec - 0.96) * 150), C[6], C[4], 0.5);
            // a glint at the tip now and then
            double g = (ph * 2) % 1;
            if (g < 0.12) Sim.Sparkle(p, 0, Y - R * 2.2 * Px.Cos(TIP), Round(5 * Px.Sin((g / 0.12) * Math.PI)), C[6], C[4], 0.6);
            Motes(p, C, t, o, Y, true);
        }
    }
}
