using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Frozen : Effect
    {
        static readonly Pose STIFF = new Pose { ["turn"] = 0.9, ["crouch"] = 1, ["fx"] = -6, ["fy"] = -15, ["bx"] = 7, ["by"] = -15, ["ffx"] = -2, ["fbx"] = 3 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, new Pose(STIFF) { ["crouch"] = 2 }), new PoseKey(0.22, STIFF), new PoseKey(1, STIFF), new PoseKey(1, new Pose(STIFF) { ["crouch"] = 2 }, "snap"),
                new PoseKey(1.22, STIFF), new PoseKey(2, new Pose(STIFF) { ["crouch"] = 2 }),
            },
        };

        public static readonly Module Module = new Module("frozen", "pixelfx/fx/status/frozen", 72, 64, 36, 54, 2, true, 30,
            new Options { ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" }, ["seed"] = 4 },
            (o, e) => new Frozen(o), act: Act, actor: 2,
            // the effect draws the whole (chilled, shivering) body, so the stand-in is hidden throughout
            actorAlpha: (t, o) => 0)
        {
            Title = "Frozen",
            Desc = "Chilled to the bone: frost ferns creep over the body, icicles hang from chin and hands and drip, breath steams, snow settles, a shiver every second, frost spreads underfoot.",
            Tags = new[] { "status", "ice", "freeze", "frozen", "cold", "chill", "debuff", "loop" },
            Bg = "ground",
        };

        const double D = 2, SHIVER = 1;
        static double Since(double t) => ((t % SHIVER) + SHIVER) % SHIVER;
        static int Shake(double t) => Since(t) < 0.22 ? (Math.Floor(t * 50) % 2 != 0 ? 1 : -1) : 0;

        sealed class Icicle
        {
            public int x, y;
            public double len, phase, period;
        }

        sealed class Plan
        {
            public List<double[]> ferns;
            public List<Icicle> icicles;
            public List<int[]> snow;
            public double[] mouth;
        }

        // worked out once per character: fern paths creeping in from the outline, icicle roots, snow ledges, the mouth
        static readonly ConditionalWeakTable<Mask, Dictionary<double, Plan>> PLANS = new ConditionalWeakTable<Mask, Dictionary<double, Plan>>();

        static Plan PlanOf(Actor a, double seed)
        {
            Dictionary<double, Plan> m = PLANS.GetValue(a.Mask, k => new Dictionary<double, Plan>());
            if (m.TryGetValue(seed, out Plan got)) return got;
            StatusShape S = Shape(a);
            int H = S.H, W = S.W;
            var R = new Rng(seed * 11 + 1);
            // ferns: a stem walking in from an edge with fronds branching off it at alternate sides, each pixel ordered by when it grows
            var ferns = new List<double[]>();
            int[][] shuffled = S.Side.FindAll(s => s[1] > H * 0.2 && s[1] < H * 0.85).ToArray();
            JsSort(shuffled, (p, q) => R.Next() - 0.5);
            int[][] roots = shuffled.Take(6).ToArray();
            for (int i = 0; i < roots.Length; i++)
            {
                int x0 = roots[i][0], y0 = roots[i][1], dir = roots[i][2];
                double x = x0, y = y0, ang = dir < 0 ? -0.5 : Math.PI + 0.5;
                ang += R.Range(-0.4, 0.4);
                for (int s = 0; s < 9; s++)
                {
                    x += Px.Cos(ang) * 1.1;
                    y += Px.Sin(ang) * 1.1;
                    double px = Round(x), py = Round(y);
                    if (!S.On(px, py)) break;
                    ferns.Add(new[] { px, py, s / 9.0, i });
                    if (s % 2 == 1)
                        foreach (int side in new[] { -1, 1 })
                        {
                            double ba = ang + side * 1.05, L = 1 + Round(R.Range(0, 2) * (1 - s / 9.0));
                            for (int k = 1; k <= L; k++)
                            {
                                double bx = Round(x + Px.Cos(ba) * k), by = Round(y + Px.Sin(ba) * k);
                                if (S.On(bx, by)) ferns.Add(new[] { bx, by, (s + k * 0.6) / 9, i });
                            }
                        }
                    ang += R.Range(-0.25, 0.25);
                }
            }
            // icicles under the hair tips, the jaw and the hands: bottom edges with clear space under them
            int Clear(int x, int y)
            {
                int n = 0;
                while (n < 7 && y + 1 + n < H && !S.On(x, y + 1 + n)) n++;
                return n;
            }
            List<int[]> hangs = S.Body.FindAll(b => !S.On(b[0], b[1] + 1) && S.On(b[0], b[1] - 1) && b[1] < H * 0.72 && b[1] > 4)
                .Select(b => new[] { b[0], b[1], Clear(b[0], b[1]) }).Where(h => h[2] >= 4).ToList();
            int[] Best(Func<int[], bool> f, Func<int[], int[], double> key)
            {
                List<int[]> l = StableSort(hangs.Where(f), key);
                return l.Count > 0 ? l[0] : null;
            }
            int[][] picks =
            {
                Best(h => h[1] < H * 0.36, (p, q) => p[0] - q[0]),
                Best(h => h[1] < H * 0.36, (p, q) => q[0] - p[0]),
                Best(h => h[1] >= H * 0.36 && h[1] < H * 0.5 && h[0] < W / 2.0, (p, q) => q[0] - p[0]),
                Best(h => h[1] >= H * 0.36 && h[1] < H * 0.5 && h[0] >= W / 2.0, (p, q) => p[0] - q[0]),
                Best(h => h[1] >= H * 0.5 && h[0] < W / 2.0, (p, q) => p[0] - q[0]),
                Best(h => h[1] >= H * 0.5 && h[0] >= W / 2.0, (p, q) => q[0] - p[0]),
            };
            var icicles = new List<Icicle>();
            foreach (int[] h in picks)
            {
                if (h == null) continue;
                int i = icicles.Count;
                var ic = new Icicle { x = h[0], y = h[1] + 1 };
                ic.len = Math.Min(h[2] - 1, R.Range(3, 5.5));
                ic.phase = R.Next();
                ic.period = i % 2 != 0 ? 1 : 2;
                icicles.Add(ic);
            }
            // snow ledges: the upward edges of the head and the shoulders
            List<int[]> snow = S.Up.FindAll(u => u[1] < H * 0.6).Select(u => new[] { u[0], u[1], 1 + (Rand(u[0], seed, 3) < 0.45 ? 1 : 0) }).ToList();
            List<StatusEye> E = Eyes(a);
            double[] mouth = E.Count == 2 ? new[] { (E[0].X1 + E[1].X0 + 1) / 2.0, Math.Max(E[0].Y1, E[1].Y1) + 2 } : new[] { W / 2.0, H * 0.38 };
            var out_ = new Plan { ferns = ferns, icicles = icicles, snow = snow, mouth = mouth };
            m[seed] = out_;
            return out_;
        }

        // frost on the floor round the feet: a white crust, a pale bloom under it, its edge creeping out and back
        static void Frost(Painter p, uint[] C, double t, double reach)
        {
            for (double x = -Math.Ceiling(reach) - 1; x <= reach + 1; x++)
            {
                double q = Math.Abs(x + 0.5) / reach, h = Rand(x, 17), k = 1 - q * q;
                if (q > 1 + h * 0.15) continue;
                bool tw = Px.Sin((t / D) * TAU * 2 + Rand(x, 3) * TAU) > 0.94;
                p.Px(x, 0, tw || h < 0.3 ? C[7] : q > 0.85 ? C[5] : C[6]);
                if (h < 0.3 * k) p.Px(x, -1, C[6]);
                for (int j = 1; j <= 1 + Round(2 * k); j++) p.Dot(x, j, j == 1 ? C[5] : C[4], (1.05 - j / 4.0) * 0.85);
            }
        }

        readonly Options o;

        Frozen(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double reach = 13 + 3.5 * Px.Sin((t / D) * TAU);
            p.Shade(-28, -3, 28, 6, (x, y, out_) =>
            {
                double d = Hypot(x / 24, (y - 0.5) / 4);
                if (d < 1)
                {
                    out_.l = C[4];
                    out_.k = 0.24 * (1 - d) * (1 - d);
                }
                return 0;
            });
            Frost(p, C, t, reach);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            int H = S.H;
            Plan P = PlanOf(a, o.Seed);
            int dx = Shake(t);
            double ph = Since(t);
            double X(double mx) => a.X + mx + dx;
            double Y(double my) => a.Y + my;
            // the body, chilled: its colours pulled toward ice; the rim frosted and lit
            foreach (int[] b in S.Body)
            {
                int mx = b[0], my = b[1];
                uint c = ColorAt(a, mx, my), ice = C[(int)Clamp(1 + Round(Luma(c) * 5.6), 1, 7)];
                bool rim = !S.On(mx - 1, my) || !S.On(mx, my - 1) || !S.On(mx + 1, my);
                p.Px(X(mx), Y(my), rim && Luma(c) < 0.3 ? (my < H * 0.5 || !S.On(mx - 1, my) ? C[5] : C[3]) : Mix(c, ice, 0.38));
            }
            // frost ferns creeping in from the outline; each shiver cracks most of it off and it creeps back
            double grow = 0.2 + 0.65 * Smooth(0.05, 0.95, ph);
            foreach (double[] f in P.ferns)
            {
                double x = f[0], y = f[1], u = f[2];
                if (u < grow) p.Px(X(x), Y(y), u > grow - 0.1 ? WHITE : Rand(x, y, 5) < 0.5 ? C[6] : C[5]);
            }
            // snow settled on the head and shoulders
            foreach (int[] s in P.snow)
            {
                int x = s[0], y = s[1], d = s[2];
                p.Px(X(x), Y(y) - 1, C[7]);
                if (d > 1) p.Px(X(x), Y(y) - 2, Rand(x, y, 9) < 0.5 ? C[7] : C[6]);
            }
            // icicles hanging under the chin, hands and hair: they grow, a drop gathers and falls, they shrink back
            foreach (Icicle ic in P.icicles)
            {
                double per = ic.period, u = ((t / per + ic.phase) % 1 + 1) % 1, L = ic.len * (0.55 + 0.45 * Smooth(0, 0.7, u)), x = X(ic.x), y0 = Y(ic.y);
                for (int j = 0; j < L; j++)
                {
                    double v = j / L;
                    p.Px(x, y0 + j, v > 0.75 ? WHITE : v < 0.3 ? C[7] : C[6]);
                    if (v < 0.5) p.Px(x + 1, y0 + j, C[4]);
                }
                // the drop: it swells at the tip, lets go and falls to the floor
                if (u > 0.7 && u < 0.86) p.Px(x, y0 + L, C[6]);
                else if (u >= 0.86)
                {
                    double f = (u - 0.86) * per, y = y0 + L + 0.5 * 760 * f * f;
                    if (y < -1)
                    {
                        p.Px(x, y, C[6]);
                        p.Px(x, y - 1, C[5]);
                    }
                }
                p.Glow(x, y0 + L * 0.6, 4, C[5], 0.18);
            }
            // breath steaming from the mouth between shivers
            double mx0 = P.mouth[0], my0 = P.mouth[1];
            for (int i = 0; i < 4; i++)
            {
                double age = ph - 0.35 - i * 0.05;
                if (age < 0 || age > 0.65) continue;
                double u = age / 0.65, x = X(mx0) + 1 + Travel(16, 0, 2.2, age) + i, y = Y(my0) - Travel(6, 3, 1, age) - i * 0.5, rad = 0.8 + 2.4 * Math.Sqrt(u);
                p.Disc(x, y, rad, C[7], 0.5 * (1 - u) * Math.Min(1, u * 8));
                p.Disc(x - rad * 0.2, y - rad * 0.3, rad * 0.5, WHITE, 0.4 * (1 - u));
            }
            // snow falling and settling; flakes flung off by the shiver
            Cycle(t, D, 9, o.Seed + 2, r => r.Range(1.2, 1.8), (age, life, r, i) =>
            {
                double x = r.Range(-14, 14) + Px.Sin(age * 3 + r.Next() * TAU) * 1.5, y = a.Y - 16 + age * 14;
                if (y > a.Y + 2) return;
                p.Dot(x, y, r.Next() < 0.5 ? WHITE : C[6], Math.Min(1, age * 4));
            });
            if (ph < 0.5)
                for (int i = 0; i < 8; i++)
                {
                    double side = i % 2 != 0 ? 1 : -1, vx = side * (20 + 30 * Rand(i, 4)), vy = -(20 + 30 * Rand(i, 5)), x = side * (6 + 6 * Rand(i, 6)) + vx * ph, y = a.Y + 3 * Rand(i, 7) + vy * ph + 0.5 * 300 * ph * ph;
                    p.Dot(x, y, i % 3 != 0 ? C[6] : WHITE, 1.2 - ph * 2);
                }
            if (ph < 0.25) Sim.Sparkle(p, X(Round(S.W * 0.3)), Y(4), Round(3 * (1 - ph / 0.25)), C[6], C[5], 0.4);
            // cold light: off the body, lifting with each shiver
            p.Glow(0, a.Y + H * 0.45, 30, C[4], 0.2 + 0.12 * Exp(-ph / 0.2));
            p.Glow(0, a.Y + 2, 16, C[5], 0.1);
        }
    }
}
