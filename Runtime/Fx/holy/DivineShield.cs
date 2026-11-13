using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class DivineShield : Effect
    {
        static readonly Pose BRACE = new Pose { ["turn"] = 0.7, ["crouch"] = 3, ["lean"] = 1, ["fx"] = 5, ["fy"] = -33, ["bx"] = 10, ["by"] = -20, ["ffx"] = 4, ["fbx"] = -2 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, BRACE),
                new PoseKey(0.6, new Pose(BRACE) { ["crouch"] = 4 }),
                new PoseKey(1.2, new Pose(BRACE) { ["fy"] = -35, ["crouch"] = 3 }),
                new PoseKey(1.8, new Pose(BRACE) { ["crouch"] = 4 }),
                new PoseKey(2.4, BRACE),
            },
        };

        const double DURATION = 2.4;

        public static readonly Module Module = new Module("divine-shield", "pixelfx/fx/holy/divine-shield", 88, 84, 44, 68, DURATION, true, 30,
            new Options { ["palette"] = GOLD, ["radius"] = 29, ["seed"] = 8 },
            (o, e) => new DivineShield(o, e), act: Act, actor: 2)
        {
            Title = "Divine shield",
            Desc = "A bubble of gold light round the character: a bright rim and a thin gold film, meridians turning like a slow globe, a sheen sweeping across and sparkles winking on it.",
            Tags = new[] { "holy", "shield", "bubble", "barrier", "buff", "character", "loop" },
            Bg = "ground",
        };

        const double CY = -22, TILT = 0.34, FLOOR_SY = 0.26;
        const int N = 4;

        // a point of the sphere (lat, lon) seen from a little above: screen x, y and depth z (> 0 faces us)
        static (double x, double y, double z) OnSphere(double r, double lat, double lon)
        {
            double X = r * Px.Cos(lat) * Px.Sin(lon), Y = r * Px.Sin(lat), Z = r * Px.Cos(lat) * Px.Cos(lon);
            return (X, CY - (Y * Px.Cos(TILT) - Z * Px.Sin(TILT)), Y * Px.Sin(TILT) + Z * Px.Cos(TILT));
        }

        // the sheen: a diagonal band crossing the bubble once a loop, then a rest
        static double Sheen(double t, double T) => -1.7 + 3.4 * Clamp(((t / T) % 1) / 0.55);

        readonly Options o;

        DivineShield(Options o, Emitter e) => this.o = o;

        sealed class Seen
        {
            public double x, y, f, hot;
        }

        void Lattice(Painter p, double t, bool back)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double r = o.Num("radius"), T = DURATION, phi = (t / T) * (Math.PI / N), sw = Sheen(t, T);
            var index = new Dictionary<double, int>();
            var seen = new List<Seen>();
            // gather the lines' pixels first, so each is drawn once
            void Dot((double x, double y, double z) q)
            {
                double x = q.x, y = q.y, z = q.z;
                if ((z < 0) != back) return;
                double key = Math.Floor(x) * 1000 + Math.Floor(y), f = Math.Abs(z) / r, qq = (x - (y - CY)) / (r * 1.414), hot = Math.Abs(qq - sw) < 0.12 ? 1 : 0;
                Seen v = index.TryGetValue(key, out int at) ? seen[at] : null;
                if (v == null || f < v.f)
                {
                    var nv = new Seen { x = x, y = y, f = f, hot = hot != 0 ? hot : (v != null ? v.hot : 0) };
                    if (v == null)
                    {
                        index[key] = seen.Count;
                        seen.Add(nv);
                    }
                    else seen[at] = nv;
                }
            }
            // meridians, turning
            for (int k = 0; k < 2 * N; k++)
            {
                double lon = phi + (k * Math.PI) / N;
                for (double lat = -Math.PI / 2; lat <= Math.PI / 2; lat += 0.7 / r) Dot(OnSphere(r, lat, lon));
            }
            // the equator
            for (double lon = 0; lon < TAU; lon += 0.7 / r) Dot(OnSphere(r, 0, lon));
            // behind: faint and broken. In front: bright where the surface turns away, a translucent wash of gold
            // where it faces us, so the character still shows through
            foreach (Seen s in seen)
            {
                if (back) p.Blend(s.x, s.y, C[4], 0.32);
                else if (s.hot != 0) p.Px(s.x, s.y, WHITE);
                else if (s.f < 0.4) p.Px(s.x, s.y, C[6]);
                else p.Blend(s.x, s.y, s.f < 0.7 ? C[6] : C[5], s.f < 0.7 ? 0.8 : 0.55);
            }
        }

        void Film(Painter p, double t, bool back)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double r = o.Num("radius"), sw = Sheen(t, DURATION);
            p.Each(-r - 1, CY - r - 1, r + 1, CY + r + 1, (x, y, sx, sy) =>
            {
                double d = Hypot(x, y - CY) / r;
                if (d >= 1) return;
                double q = (x - (y - CY)) / (r * 1.414), hot = Math.Max(0, 1 - Math.Abs(q - sw) / 0.22);
                double a = (back ? 0.07 : 0.03) + 0.26 * Px.Pow(d, 6) + 0.2 * hot * (back ? 0.3 : 1);
                p.BlendPx(sx, sy, hot > 0.5 && !back ? C[6] : C[5], a);
            });
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double r = o.Num("radius"), rf = Math.Sqrt(r * r - CY * CY);
            Film(p, t, true);
            Lattice(p, t, true);
            // where it meets the floor: the far half of a bright seal
            Oval(p, 0, 0, rf, rf * FLOOR_SY, C[5], 0.8, g => Px.Sin(g) < 0);
            p.Glow(0, CY, r * 1.1, C[4], 0.14);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double r = o.Num("radius"), T = DURATION, rf = Math.Sqrt(r * r - CY * CY);
            Actor A = o.Actor;
            // the bubble lights the character from all round
            if (A != null)
                foreach (int[] q in ShapeOf(A).Out)
                    p.Light(q[0], q[1], C[5], 0.3);
            Film(p, t, false);
            Lattice(p, t, false);
            // the rim: bright where the light sits (top left) and where the sheen crosses it
            double sw = Sheen(t, T);
            for (int j = 0, n = (int)Math.Ceiling(r * TAU * 1.4); j < n; j++)
            {
                double g = ((double)j / n) * TAU, x = Px.Cos(g) * r, y = CY + Px.Sin(g) * r, q = (x - (y - CY)) / (r * 1.414);
                double lit = Px.Cos(g + 2.36);
                bool hot = Math.Abs(q - sw) < 0.15;
                p.Dot(x, y, hot || lit > 0.75 ? WHITE : lit > -0.2 ? C[6] : C[5], 1);
                p.Dot(Px.Cos(g) * (r - 1), CY + Px.Sin(g) * (r - 1), C[4], 0.35 + 0.3 * lit);
            }
            p.GlowRing(0, CY, r, 6, C[4], 0.32);
            // where the meridians cross the equator, and at the pole they all turn round, the lattice catches the light
            double phi = (t / T) * (Math.PI / N);
            for (int k = 0; k < 2 * N; k++)
            {
                var (x, y, z) = OnSphere(r, 0, phi + (k * Math.PI) / N);
                if (z < r * 0.15) continue;
                p.Px(x, y, WHITE);
                if (z > r * 0.45)
                    foreach (int[] d in Sim.Cross)
                        p.Blend(x + d[0], y + d[1], C[6], 0.7);
                p.Glow(x, y, 4, C[5], 0.3);
            }
            var (px0, py0, _) = OnSphere(r, Math.PI / 2, 0);
            Sim.Sparkle(p, px0, py0, 2 + Round(1.5 + 1.5 * Px.Sin((t / T) * TAU * 2)), WHITE, C[6], 0.45);
            // two glints chase each other round the rim
            foreach (double off in new[] { 0, Math.PI })
            {
                double g = (t / T) * TAU + off + 0.8;
                Sim.Sparkle(p, Px.Cos(g) * r, CY + Px.Sin(g) * r, 2, WHITE, C[6], 0.4);
            }
            // a bubble's shine: a bright curl up on the left, a dot under it, a faint one low on the right
            for (double g = 3.55; g < 4.2; g += 0.04) p.Px(Px.Cos(g) * r * 0.74, CY + Px.Sin(g) * r * 0.74, g > 3.7 && g < 4.05 ? WHITE : C[6]);
            p.Px(Px.Cos(3.4) * r * 0.62, CY + Px.Sin(3.4) * r * 0.62, WHITE);
            for (double g = 0.45; g < 0.8; g += 0.05) p.Dot(Px.Cos(g) * r * 0.8, CY + Px.Sin(g) * r * 0.8, C[5], 0.6);
            // the near half of the floor seal and a pool of light
            Oval(p, 0, 0, rf, rf * FLOOR_SY, g => Px.Sin(g) > 0.6 ? WHITE : C[6], 1, g => Px.Sin(g) >= 0);
            for (int i = -2; i <= 2; i++) p.Glow(i * rf * 0.5, 0, 12, C[4], 0.18);
            // sparkles winking on its skin as it turns, motes rising inside
            Cycle(t, T, 9, o.Seed, g => g.Range(0.35, 0.6), (age, life, R, i) =>
            {
                double lat = R.Range(-1.2, 1.3);
                var (x, y, z) = OnSphere(r, lat, R.Range(0, TAU) + phi);
                double k = Px.Sin((age / life) * Math.PI);
                if (z < 0) return;
                Sim.Sparkle(p, x, y, 1 + 2.5 * k * R.Range(0.6, 1), WHITE, C[6], 0.45 * k);
            });
            Cycle(t, T, 12, o.Seed + 1, g => g.Range(1.2, 2.2), (age, life, R, i) =>
            {
                double x0 = R.Range(-0.7, 0.7) * r, u = age / life, x = x0 + Px.Sin(u * 6 + R.Range(0, 6)) * 2, y = CY + r * 0.75 - u * r * 1.4;
                if (Hypot(x, y - CY) > r - 2) return;
                p.Dot(x, y, u < 0.5 ? C[6] : C[5], Px.Sin(u * Math.PI));
                p.Light(x, y, C[5], 0.4 * Px.Sin(u * Math.PI));
            });
        }
    }
}
