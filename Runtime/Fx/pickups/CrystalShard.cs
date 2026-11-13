// The crystal shading (a lit ridge, bright faces turned to the light, a dark rim) and the orbiting motes
// follow Sprite Fusion's open-source ice crystals and pickup (https://www.spritefusion.com/pixel-art-vfx/ice),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class CrystalShard : Effect
    {
        const double DURATION = 3.2, HALF = 6, TILT = 0.18, LEAN = 0.2, HOVER = 27, RX = 15, RY = 3.8;

        public static readonly Module Module = new Module("crystal-shard", "pixelfx/fx/pickups/crystal-shard", 64, 76, 32, 68, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0a04", "#3e1606", "#702a08", "#a8480c", "#e07818", "#ffa838", "#ffd890", "#ffffff" },
                ["seed"] = 8,
            },
            (o, e) => new CrystalShard(o, e))
        {
            Title = "Crystal shard",
            Desc = "A broken amber shard hovers and sways: chipped uneven faces, its top sheared off, light pulsing up through glowing cracks and flaring out of the break; splinters orbit it.",
            Tags = new[] { "pickup", "crystal", "shard", "relic", "broken", "loop" },
            Bg = "ground",
        };

        static double[] Roll(double[] v) => new[] { v[0] * Px.Cos(LEAN) - v[1] * Px.Sin(LEAN), v[0] * Px.Sin(LEAN) + v[1] * Px.Cos(LEAN), v[2] };

        static double[] Unit(double x, double y, double z)
        {
            double l = Px.Hypot(x, y, z);
            return new[] { x / l, y / l, z / l };
        }

        // the broken shard as a convex solid: an uneven six-sided body, a point on top sheared off on one side by a
        // fracture, a lopsided point below, two corners chipped away
        static readonly Solid SOLID = MakeSolid();

        static Solid MakeSolid()
        {
            var planes = new List<Plane>();
            double deg = Math.PI / 180;
            double[] jit = { 0, 8, -6, 4, -9, 5 }, rr = { 1, 0.86, 1.08, 0.94, 0.82, 1.04 }, low = { 6.5, 5.5, 7, 6, 5, 6.8 };
            for (int k = 0; k < 6; k++)
            {
                double a = (k * 60 + jit[k]) * deg, r = 4.8 * rr[k], ct = Atan2(13, r), cb = Atan2(low[k], r);
                planes.Add(new Plane { n = new[] { Px.Cos(a), 0, Px.Sin(a) }, d = r, kind = 6 });
                planes.Add(new Plane { n = new[] { Px.Sin(ct) * Px.Cos(a), Px.Cos(ct), Px.Sin(ct) * Px.Sin(a) }, d = Px.Sin(ct) * r + Px.Cos(ct) * HALF, kind = 7 });
                planes.Add(new Plane { n = new[] { Px.Sin(cb) * Px.Cos(a), -Px.Cos(cb), Px.Sin(cb) * Px.Sin(a) }, d = Px.Sin(cb) * r + Px.Cos(cb) * HALF, kind = 8 });
            }
            double[] shear = Unit(0.7, 0.62, 0.34);
            planes.Add(new Plane { n = shear, d = shear[1] * 7.2 + shear[0] * -0.6, kind = 9 });
            planes.Add(new Plane { n = Unit(-0.7, -0.25, 0.65), d = 4.0, kind = 10 });
            planes.Add(new Plane { n = Unit(0.75, 0.15, 0.6), d = 3.9, kind = 10 });
            planes.Add(new Plane { n = Unit(-0.55, 0.5, 0.3), d = 6.8, kind = 10 });
            // the whole shard leans off upright
            foreach (Plane q in planes) q.n = Roll(q.n);
            return new Solid { planes = planes.ToArray(), r = 8, top = HALF + 13, bottom = HALF + 8 };
        }

        // cracks running through it, in its own frame (y up): they turn with it
        static readonly double[][][] CRACKS =
        {
            new[] { new[] { 0.5, -9, 0.3 }, new[] { -0.8, -5, 0.5 }, new[] { 0.6, -1, 0.2 }, new[] { -0.4, 3, 0.6 }, new[] { 1.4, 6.5, 0.4 }, new[] { 2.2, 8.5, 0.6 } },
            new[] { new[] { 0.6, -1, 0.2 }, new[] { 2.4, 1.5, 1 }, new[] { 3.4, 4.2, 1.2 } },
            new[] { new[] { -0.4, 3, 0.6 }, new[] { -2, 6.5, 0.4 }, new[] { -2.6, 10.5, 0.2 }, new[] { -2.4, 14.5, 0.1 } },
            new[] { new[] { -0.8, -5, 0.5 }, new[] { -2.8, -3, 0.8 } },
        };

        readonly ScratchLayer L = new ScratchLayer(32, 52), SL = new ScratchLayer(16, 16);
        readonly Options o;

        CrystalShard(Options o, Emitter e) => this.o = o;

        static (double y, double spin) Sway(double ph) => (-HOVER - 1.8 * Px.Sin(ph * TAU), 0.3 + 0.62 * Px.Sin(ph * TAU));

        // a pulse of light climbing the cracks, twice a loop: where it is, from below the shard (-0.3) to past its top
        static double Climb(double ph) => ph * 2 % 1 * 1.7 - 0.3;

        // where a point of the shard lands on screen, turned by spin and seen from a little above
        static (double x, double y) Project(double[] pt, double spin, double cy)
        {
            double[] v = Roll(pt);
            double x = v[0], y = v[1], z = v[2], xr = x * Px.Cos(spin) - z * Px.Sin(spin), zr = x * Px.Sin(spin) + z * Px.Cos(spin);
            return (xr, cy - (y * Px.Cos(TILT) - zr * Px.Sin(TILT)));
        }

        void Cracks(Painter p, uint[] C, double spin, double cy, double ph)
        {
            double s0 = Climb(ph);
            uint rim = C[0];
            foreach (double[][] crack in CRACKS)
                for (int i = 1; i < crack.Length; i++)
                {
                    var (x0, y0) = Project(crack[i - 1], spin, cy);
                    var (x1, y1) = Project(crack[i], spin, cy);
                    double n = Math.Ceiling(Hypot(x1 - x0, y1 - y0) * 2);
                    for (int j = 0; j <= n; j++)
                    {
                        double u = j / n, x = x0 + (x1 - x0) * u, y = y0 + (y1 - y0) * u, h = crack[i - 1][1] + (crack[i][1] - crack[i - 1][1]) * u;
                        uint c = L.Get(x, y);
                        if (c == 0 || c == rim) continue;
                        // light runs up the crack: brightest where the pulse is
                        double b = 0.3 + Exp(-Px.Pow(((h + 9) / 22 - s0) / 0.14, 2));
                        p.Px(x, y, b > 0.9 ? WHITE : b > 0.55 ? C[6] : C[5]);
                        if (j % 4 == 0 && b > 0.5) p.Glow(x + 0.5, y + 0.5, 2 + 3 * b, C[5], 0.16 * b);
                    }
                }
        }

        // a splinter: a little two-tone sliver with a dark rim, turned to angle a
        void Splinter(Painter p, uint[] C, double x, double y, double len, double a, bool far)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a), X = Round(x), Y = Round(y);
            SL.Begin(X - 7, Y - 7);
            SL.Fill(X - 7, Y - 7, X + 6, Y + 6, (px, py) =>
            {
                double dx = px - X, dy = py - Y, u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                if (Math.Abs(u) / len + Math.Abs(v) / (u > 0 ? 1.7 : 1.1) > 1) return 0;
                double lit = -(ca * 0.6 + sa * 0.8) * Sign(v != 0 && !double.IsNaN(v) ? v : 1);
                return far ? (v < 0 ? C[4] : C[3]) : Math.Abs(v) < 0.5 && u < 0 ? WHITE : (v < 0) == (lit > 0) ? C[6] : C[4];
            });
            SL.Outline(C[far ? 1 : 0]).Draw(p);
        }

        // the splinters on their tilted ring; back draws the far half, front the near half
        void Splinters(Painter p, uint[] C, double ph, double cy, bool back)
        {
            for (int i = 0; i < 3; i++)
            {
                double a = ph * TAU + i * TAU / 3, s = Px.Sin(a);
                if ((s < 0) != back) continue;
                double x = Px.Cos(a) * RX, y = cy + 1 + s * RY - Px.Cos(a) * 1.5, near = (s + 1) / 2;
                Splinter(p, C, x, y, 3 + 1.6 * near, a * 2 + i * 1.7, back);
                p.Glow(x, y, 6 + 3 * near, C[5], (back ? 0.18 : 0.32) * (0.6 + 0.4 * near));
                if (!back && s > 0.92) Glint(p, x - 1, y - 1, 2.5 * (s - 0.92) / 0.08, WHITE, 0.4);
            }
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION;
            var (y, _) = Sway(ph);
            BlobShadow(p, 0, 6, 0.75);
            Pool(p, 0, 0.5, 20, 3.5, C[4], 0.3);
            p.GlowRing(0, y, 7, 20, C[4], 0.22);
            Splinters(p, C, ph, y, true);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION, seed = o.Seed;
            var (y, spin) = Sway(ph);
            double s0 = Climb(ph);
            DrawGem(L, p, SOLID, 0, y, spin, C, tilt: TILT, gain: 0.6);
            Cracks(p, C, spin, y, ph);
            // lit from within, brighter while the pulse is inside it
            double inside = Exp(-Px.Pow((s0 - 0.45) / 0.45, 2));
            p.Glow(0, y, 10, C[5], 0.2 * inside);
            Splinters(p, C, ph, y, false);
            // when the pulse reaches the break it flares out of the sheared face and throws up embers
            var (fx, fy) = Project(new[] { 1.8, 8.4, 0.9 }, spin, y);
            double outk = Clamp(1 - Math.Abs(s0 - 1.05) / 0.18);
            if (outk > 0)
            {
                Glint(p, fx, fy, 3.5 * outk, WHITE, 0.55 * outk);
                p.Glow(fx, fy, 12, C[5], 0.4 * outk);
            }
            double P2 = DURATION / 2, burst = (1.05 + 0.3) / 1.7 * P2;
            for (int i = 0; i < 7; i++)
            {
                double born = burst + Rand(i, seed, 1) * 0.12, at = ((t - born) % P2 + P2) % P2, life = 0.6 + Rand(i, seed, 2) * 0.45;
                if (at > life) continue;
                double k = 1 - at / life, x = fx + (Rand(i, seed, 3) - 0.5) * 4 + Px.Sin(at * 5 + i) * 2 * (at / life), yy = fy - at * (14 + Rand(i, seed, 4) * 14);
                p.Px(x, yy, k > 0.6 ? WHITE : k > 0.3 ? C[6] : C[5]);
                p.Glow(x + 0.5, yy + 0.5, 3, C[5], 0.35 * k);
            }
        }
    }
}
