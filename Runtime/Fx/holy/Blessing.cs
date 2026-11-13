using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class Blessing : Effect
    {
        static readonly Pose OPEN = new Pose { ["turn"] = 1.15, ["fx"] = -13, ["fy"] = -31, ["bx"] = 14, ["by"] = -31, ["hy"] = -1, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, OPEN),
                new PoseKey(0.75, new Pose(OPEN) { ["fy"] = -33, ["by"] = -33 }),
                new PoseKey(1.5, new Pose(OPEN) { ["crouch"] = -1, ["fy"] = -35, ["by"] = -35 }),
                new PoseKey(2.25, new Pose(OPEN) { ["fy"] = -33, ["by"] = -33 }),
                new PoseKey(3, OPEN),
            },
        };

        public static readonly Module Module = new Module("blessing", "pixelfx/fx/holy/blessing", 80, 128, 40, 108, 3, true, 30,
            new Options { ["palette"] = GOLD, ["seed"] = 7 },
            (o, e) => new Blessing(o, e), act: Act, actor: 2)
        {
            Title = "Blessing",
            Desc = "A soft cone of light rests on the character from above; sparkles drift down through it, twinkling, and wink out where they land on the head and shoulders.",
            Tags = new[] { "holy", "blessing", "buff", "light", "sparkle", "character", "loop" },
            Bg = "ground",
        };

        const double TOP = -108, T = 3;
        static double HalfW(double y) => Lerp(5, 20, (y - TOP) / -TOP);
        // the rays in the cone, each shimmering a whole number of times a loop so it closes
        static readonly double[][] RAYS = { new[] { -0.62, 0.16, 1, 0.3 }, new[] { -0.2, 0.2, 2, 1.7 }, new[] { 0.22, 0.14, 1, 4.1 }, new[] { 0.6, 0.18, 2, 2.6 } };

        static double Ray(double q, double t)
        {
            double I = 0.3 * Math.Max(0, 1 - q * q);
            foreach (double[] r in RAYS)
            {
                double o = r[0], w = r[1], k = r[2], ph = r[3];
                I += Exp(-Px.Pow((q - o) / w, 2)) * (0.55 + 0.45 * Px.Sin((t / T) * TAU * k + ph)) * 0.6;
            }
            return I * Smooth(1.25, 0.75, Math.Abs(q));
        }

        sealed class Land
        {
            public double q, v, sway, ph, size, land;
        }

        // where each falling sparkle meets the character or the floor (cached per seed and character)
        static readonly Dictionary<string, Land[]> LANDS = new Dictionary<string, Land[]>();

        static Land[] Landings(Options o, Shape S)
        {
            string key = $"{o.Seed}:{S.X}:{S.Y}:{S.M.GetHashCode()}";
            lock (LANDS)
            {
                if (LANDS.TryGetValue(key, out Land[] got)) return got;
                var output = new Land[20];
                for (int i = 0; i < 20; i++)
                {
                    Rng R = NewRng(o.Seed, i);
                    double q = R.Range(-0.9, 0.9), v = R.Range(22, 34), sway = R.Range(1, 3), ph = R.Range(0, TAU), size = R.Range(0.8, 2.4);
                    double land = double.PositiveInfinity;
                    for (double a = 0; a < 6; a += 0.02)
                    {
                        double y = TOP + 6 + v * a, x = q * HalfW(y) + Px.Sin(a * 2.2 + ph) * sway;
                        if (y >= -1 || S.At(x, y))
                        {
                            land = a;
                            break;
                        }
                    }
                    output[i] = new Land { q = q, v = v, sway = sway, ph = ph, size = size, land = land };
                }
                LANDS[key] = output;
                return output;
            }
        }

        readonly Options o;

        Blessing(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            // the cone: soft light with a faint gold haze in it
            p.Each(-24, TOP, 24, 0.5, (x, y, sx, sy) =>
            {
                double I = Ray(x / HalfW(y), t) * (0.6 + 0.4 * Smooth(TOP, 0, y));
                if (I < 0.02) return;
                p.BlendPx(sx, sy, C[6], I * 0.13);
                p.Add(sx, sy, C[4], I * 0.22);
            });
            // two of the rays show as fine lines, light running slowly down them
            foreach (double[] r in new[] { RAYS[1], RAYS[2] })
            {
                double o2 = r[0], k = r[2], ph = r[3];
                for (double y = TOP; y < -1; y++)
                {
                    double pulse = 0.5 + 0.5 * Px.Sin(y * 0.11 - (t / T) * TAU * 3 + ph);
                    p.Dot(o2 * HalfW(y), y, pulse > 0.85 ? WHITE : C[6], (0.1 + 0.45 * pulse) * (0.5 + 0.5 * Px.Sin((t / T) * TAU * k + ph)) * Smooth(TOP, TOP + 20, y));
                }
            }
            p.Glow(0, -4, 30, C[4], 0.22);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor A = o.Actor;
            Shape S = A != null ? ShapeOf(A) : null;
            // light catching the top of the head and the shoulders
            if (A != null)
            {
                Mask m = A.Mask;
                for (int i = 0; i < m.W; i++)
                    for (int j = 0; j < m.H; j++)
                    {
                        uint c = A.Pixels[j * m.W + i];
                        if (c == 0 || Luma(c) < 0.15) continue;
                        p.Px(A.X + i, A.Y + j, Mix(c, C[6], 0.35));
                        break;
                    }
                p.Glow(0, -30, 20, C[5], 0.22);
            }
            // the floor catches it: a lit line and a pool fading out into the stone
            for (int y = 0; y <= 3; y++)
                for (int x = -24; x <= 24; x++)
                {
                    double u = Hypot(x / 24.0, y / 4.0);
                    if (u >= 1) continue;
                    p.Dot(x, y, u < 0.3 ? (y != 0 ? C[5] : WHITE) : u < 0.6 ? C[5] : u < 0.85 ? C[4] : C[3], y == 0 ? Math.Sqrt(1 - u) : (1 - u) * 0.7);
                }
            // sparkles drifting down, twinkling, winking out where they land
            if (S != null)
            {
                Land[] L = Landings(o, S);
                Cycle(t, T, L.Length, o.Seed, g => 3, (age, life, R, i) =>
                {
                    Land s = L[i];
                    double y = TOP + 6 + s.v * age, x = s.q * HalfW(y) + Px.Sin(age * 2.2 + s.ph) * s.sway, fade = Math.Min(1, age / 0.3);
                    if (age > s.land)
                    {
                        double w = (age - s.land) / 0.14;
                        if (w < 1)
                        {
                            double ly = TOP + 6 + s.v * s.land, lx = s.q * HalfW(ly) + Px.Sin(s.land * 2.2 + s.ph) * s.sway;
                            Sim.Sparkle(p, lx, ly, 3 * (1 - w) + 1, WHITE, C[6], 0.5 * (1 - w));
                        }
                        return;
                    }
                    double tw = 0.6 + 0.4 * Px.Sin(age * (7 + (i % 4)) + s.ph);
                    if (s.size > 1.2) Sim.Sparkle(p, x, y, s.size * tw * fade, tw > 0.85 ? WHITE : C[6], C[5], 0.35 * fade);
                    else
                    {
                        p.Dot(x, y, tw > 0.8 ? WHITE : C[6], fade);
                        p.Glow(x, y, 3, C[5], 0.35 * fade);
                    }
                });
            }
            // fine motes hanging in the light
            Cycle(t, T, 16, o.Seed + 9, g => g.Range(1.2, 2.4), (age, life, R, i) =>
            {
                double u = age / life, y = TOP + 10 + R.Range(0, 90) + u * R.Range(6, 14), q = R.Range(-1, 1), x = q * HalfW(y) + Px.Sin(age * 1.7 + q * 5) * 1.5, k = Px.Sin(u * Math.PI) * Ray(q, t);
                if (k > 0.12) p.Dot(x, y, k > 0.45 ? WHITE : C[6], Math.Min(1, k * 2.2));
            });
        }
    }
}
