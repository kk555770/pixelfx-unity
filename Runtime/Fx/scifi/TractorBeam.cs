// Rocks torn out of the floor with a puff of dust, then hauled up spinning, and matter drawn in as streaks,
// follow Sprite Fusion's open-source black hole (https://www.spritefusion.com/pixel-art-vfx/black-hole),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class TractorBeam : Effect
    {
        public static readonly Module Module = new Module("tractor-beam", "pixelfx/fx/scifi/tractor-beam", 120, 132, 60, 114, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#06161a", "#0b2e33", "#11505a", "#1a7f84", "#2bb5a8", "#7ef0c8", "#d6fff0", "#ffffff" },
                ["hull"] = new[] { "#0d0f17", "#1c2030", "#2e3448", "#4a5470", "#7a86a8", "#b8c4e0" },
                ["stone"] = new[] { "#1a1622", "#2e2838", "#4a4258", "#6d6480", "#9a90aa" },
                ["seed"] = 9,
            },
            (o, e) => new TractorBeam(o, e))
        {
            Title = "Tractor beam",
            Desc = "A hovering pod projects a cone of light to the floor; rocks tear loose in puffs of dust and spiral up spinning into the lens while rings of light climb the beam.",
            Tags = new[] { "scifi", "beam", "tractor", "lift", "abduct", "debris", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 3, LENS = -84, TOPW = 5.5, BOTW = 32;

        // the cone's half width at height y
        static double Half(double y) => Lerp(BOTW, TOPW, Clamp(y / LENS));

        // where something hauled up the beam is at progress k (0 on the floor, 1 in the lens): quickening as it
        // rises, winding round the axis and in toward it. [x, y, depth (> 0 on the near side)]
        static (double x, double y, double z) Haul(double r0, double th0, double dir, double turns, double k)
        {
            double s = Px.Pow(Clamp(k), 1.8), y = Lerp(-2, LENS + 5, s), th = th0 + dir * TAU * turns * s, rr = r0 * (Half(y) / BOTW) * (1 - 0.5 * s);
            return (Px.Cos(th) * rr, y, Px.Sin(th));
        }

        // a rock's life: it pops out of the floor, wobbles a beat, then spirals up into the lens
        const double POP = 0.1, HOVER = 0.32, RISE = 1.4, LIFE = POP + HOVER + RISE;

        sealed class Rock
        {
            public double id, born, r0, th0, dir, s, a0, va;
        }

        struct RockState
        {
            public double x, y, z, px, py, size, a, lift;
        }

        static RockState RockAt(Rock r, double age)
        {
            double lift = Clamp((age - POP - HOVER) / RISE);
            if (lift > 0)
            {
                var (x, y, z) = Haul(r.r0, r.th0, r.dir, 1.3, lift);
                var (x1, y1, _) = Haul(r.r0, r.th0, r.dir, 1.3, lift - 0.07);
                return new RockState { x = x, y = y, z = z, px = x1, py = y1, size = r.s * (1 - 0.6 * Px.Pow(lift, 2.5)), a = r.a0 + r.va * age * (0.3 + lift * 2), lift = lift };
            }
            double yy = age < POP ? Lerp(3, -2, age / POP) : -2 - 2 * Smooth(0, HOVER, age - POP), wob = age > POP ? Px.Sin(age * 55 + r.th0 * 9) * 0.7 : 0;
            return new RockState { x = Px.Cos(r.th0) * r.r0 + wob, y = yy, z = 1, px = double.NaN, py = double.NaN, size = r.s, a = r.a0 + r.va * age * 0.3, lift = 0 };
        }

        // a tumbling chunk of stone: an uneven outline, lit from above by the lens, shaded underneath
        static void DrawRock(Painter p, RockState q, Rock r, uint[] S, uint[] C, bool back)
        {
            const int n = 6;
            double R = Math.Max(0.9, q.size), ca = Px.Cos(q.a), sa = Px.Sin(q.a);
            uint lit = Mix(S[4], C[6], 0.3 + 0.6 * q.lift);
            double Rad(double ang)
            {
                double f = ((((ang / TAU) * n) % n) + n) % n, i = Math.Floor(f), k = f - i;
                return R * (0.75 + 0.35 * Lerp(Rand(r.id, i), Rand(r.id, (i + 1) % n), k));
            }
            // a faint trail of its rush up the beam
            if (q.lift > 0.15 && !back)
            {
                p.Line(q.px, q.py, q.x, q.y, u => u > 0.6 ? C[5] : C[3], u => 0.7 * u);
                for (int i = 1; i <= 3; i++) p.Disc(Lerp(q.x, q.px, i / 3.0), Lerp(q.y, q.py, i / 3.0) + 1, R * (0.5 + 0.25 * i), Mix(S[3], C[5], 0.4), 0.22 * (1 - i / 4.0) * q.lift);
            }
            p.Shade(q.x - R - 2, q.y - R - 2, q.x + R + 2, q.y + R + 2, (x, y, o) =>
            {
                double dx = x - q.x, dy = y - q.y, u = dx * ca + dy * sa, v = -dx * sa + dy * ca, d = Hypot(u, v), e = Rad(Atan2(v, u));
                if (d > e + 0.8) return 0;
                o.c = d > e ? S[0] : back ? (dy < -0.3 * R ? S[3] : S[1]) : dy < -0.45 * R ? (q.lift > 0.35 && d > e - 1 ? C[6] : lit) : dy < -0.1 * R ? (dx < 0 ? S[4] : S[3]) : dy > 0.35 * R ? S[1] : S[2];
                return 0;
            });
            if (!back && q.lift > 0) p.Glow(q.x, q.y - R, 5 + R, C[5], 0.25 * q.lift);
        }

        // the hovering pod: a cockpit dome on a wide hull, a rim of running lights, the lens underneath
        static void Pod(Painter p, double t, uint[] C, uint[] H, double y0)
        {
            double y = Round(y0);
            p.Shade(-21, y - 12, 21, y + 6, (x, yy, o) =>
            {
                double dy = yy - y;
                // the cockpit: a small glassy dome, a hot glint top left
                double cx = x / 8.5, cy = (dy + 5) / 6;
                if (cy < 0 && cx * cx + cy * cy <= 1)
                {
                    double d = cx * cx + cy * cy, l = -cx * 0.6 - cy * 0.8;
                    o.c = d > 0.8 ? H[0] : l > 0.85 ? WHITE : l > 0.6 ? C[6] : l > 0.2 ? C[4] : C[3];
                    return 0;
                }
                // the hull: a flat lens shape, lit on its upper face, a dark seam, the rim
                double hx = x / 20, hy = (dy + 0.5) / 4.5;
                if (dy <= 0 && hx * hx + hy * hy <= 1)
                {
                    double d = hx * hx + hy * hy, l = -hx * 0.55 - hy * 0.7;
                    o.c = d > 0.88 ? H[0] : dy == -2 ? H[1] : l > 0.55 ? H[5] : l > 0.25 ? H[4] : l > -0.1 ? H[3] : H[2];
                    return 0;
                }
                // the underside, tapering in toward the lens housing, lit from below by its own beam
                double w = 19.5 - dy * 2.4;
                if (dy > 0 && dy <= 5 && Math.Abs(x) <= w) o.c = Math.Abs(x) > w - 1 ? H[0] : dy == 1 ? H[2] : dy >= 4 && Math.Abs(x) < 9 ? C[3] : H[1];
                return 0;
            });
            // running lights chase round the rim
            for (int i = 0; i < 6; i++)
            {
                double lx = -15 + i * 6, k = 0.5 + 0.5 * Px.Cos(TAU * (4 * (t / D) - i / 6.0));
                p.Px(lx, y + 1, k > 0.7 ? WHITE : k > 0.35 ? C[5] : C[2]);
                if (k > 0.5) p.Glow(lx, y + 1, 5, C[5], 0.35 * k);
            }
            // the lens: a white-hot eye in a ring of light
            p.Ellipse(0, y + 5.5, 7.5, 2.2, (d, g) => d < 0.45 ? WHITE : d < 0.75 ? C[6] : d < 0.9 ? C[5] : C[3]);
        }

        // a ring of light round the beam at height y: a hard line in front, a fainter one for the far side
        static void Hoop(Painter p, double y, double rx, uint c, bool back)
        {
            double ry = rx * 0.17;
            for (int i = 0, n = (int)Math.Ceiling(rx * 7); i < n; i++)
            {
                double g = ((double)i / n) * TAU, s = Px.Sin(g);
                if (s >= 0 == back) continue;
                p.Px(Px.Cos(g) * rx, y + s * ry, c);
            }
        }

        readonly uint[] C, S, H;
        readonly double seed;
        readonly Rock[] rocks;
        readonly ScifiLib.Ghosts g;

        TractorBeam(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            S = Fit(o.Ramp("stone"), 5);
            H = Fit(o.Ramp("hull"), 6);
            seed = o.Seed;
            var rn = new Rng(o.Seed);
            // rocks torn up one after another, from spots left and right of the middle
            rocks = new Rock[7];
            for (int i = 0; i < 7; i++)
                rocks[i] = new Rock
                {
                    id = i + o.Seed * 31, born = (i / 7.0) * D + rn.Range(-0.06, 0.06), r0 = rn.Range(8, 25), th0 = (i % 2 != 0 ? 0 : Math.PI) + rn.Range(-0.45, 0.45),
                    dir = rn.Sign(), s = rn.Range(2.4, 4.4), a0 = rn.Range(0, TAU), va = rn.Range(-7, 7),
                };
            g = new ScifiLib.Ghosts(D, x => 0, null, 0, new double[] { -58, -112, 58, 14 });
            // each rock leaves the floor in a puff of dust and a spray of grit
            foreach (Rock r in rocks)
            {
                double x = Px.Cos(r.th0) * r.r0;
                for (int i = 0; i < 5; i++) g.Add(r.born + 0.02, P.Dust, x + rn.Range(-3, 3), -1, rn.Range(-34, 34), -rn.Range(8, 26), rn.Range(0.5, 0.9), Mix(S[3], C[4], 0.3), rn.Range(2.2, 3.6));
                for (int i = 0; i < 6; i++) g.Add(r.born + 0.02, P.Grit, x + rn.Range(-2, 2), -2, rn.Range(-45, 45), -rn.Range(60, 150), rn.Range(0.5, 0.9), rn.Pick(new[] { S[2], S[3], S[4] }), rn.Chance(0.3) ? 2 : 1);
            }
            // dust always swirling round the foot of the beam
            for (int i = 0; i < 16; i++)
            {
                double s = rn.Sign();
                g.Add(rn.Range(0, D), P.Dust, s * rn.Range(14, 34), -rn.Range(0, 3), -s * rn.Range(6, 22), -rn.Range(4, 14), rn.Range(0.9, 1.5), Mix(S[3], C[5], 0.3), rn.Range(2.6, 4.4));
            }
        }

        public override void DrawBack(Painter p, double t)
        {
            // the pool of light where the beam meets the floor, and the far half of its rim
            for (int y = -6; y <= 6; y++)
                for (double x = -BOTW; x <= BOTW; x++)
                {
                    double d = Hypot((x + 0.5) / BOTW, (y + 0.5) / 5.5);
                    if (d <= 1) p.Blend(x + 0.5, y + 0.5, d < 0.35 ? C[6] : C[5], 0.2 + 0.55 * Px.Pow(1 - d, 1.5));
                }
            Hoop(p, 0, BOTW + 1, C[5], true);
            g.At(t).Draw(p, Layer.Back);
            // rocks on the far side of the beam, seen through it
            foreach (Rock r in rocks)
            {
                double age = ScifiLib.Since(t, r.born, D);
                if (age >= LIFE) continue;
                RockState q = RockAt(r, age);
                if (q.z < 0 && q.lift > 0) DrawRock(p, q, r, S, C, true);
            }
        }

        public override void Draw(Painter p, double t)
        {
            double ph = t / D, podY = LENS - 5 + 1.5 * Px.Sin(TAU * 2 * ph);
            // the beam: a glassy cone, white-hot at the lens, hard bright edges with a soft fringe, light drifting in it
            for (int y = (int)Math.Ceiling(LENS + 3); y < 0; y++)
            {
                double w = Half(y), v = (y - LENS) / -LENS, pulse = ((((v * 3.2 + ph * 6) % 1) + 1) % 1) < 0.08 ? 1 : 0;
                for (double x = Math.Floor(-w) - 1; x <= Math.Ceiling(w); x++)
                {
                    double q = Math.Abs(x + 0.5) / w;
                    if (q > 1 + 1.2 / w) continue;
                    if (q > 1) p.Dot(x, y, C[4], 0.6 - 0.3 * v);
                    else if (q > 1 - 1 / w) p.Px(x, y, v < 0.2 ? WHITE : v < 0.45 ? C[6] : C[5]);
                    else
                    {
                        double curtain = 0;
                        for (int s = 0; s < 4; s++)
                            if (Math.Abs((x + 0.5) / w - (Rand(seed, s, 7) - 0.5) * 1.4 - 0.12 * Px.Sin(TAU * ph + s * 1.7)) < 0.045) curtain = 1;
                        double core = Px.Pow(1 - q, 2) * Px.Pow(1 - v, 1.6), a = 0.08 + 0.22 * Px.Pow(q, 4) + 0.6 * core + 0.12 * pulse + 0.18 * curtain;
                        p.Blend(x + 0.5, y + 0.5, core > 0.45 ? WHITE : curtain != 0 || pulse != 0 || v < 0.35 ? C[5] : C[4], Clamp(a));
                    }
                }
                if ((y & 3) == 0)
                    foreach (int s in new[] { -1, 1 }) p.Glow(s * w, y, 8, C[4], 0.18);
            }
            // rings climbing the beam, quickening and narrowing with it
            for (int i = 0; i < 5; i++)
            {
                double k = ScifiLib.Since(t, (i / 5.0) * D, D) / 1.1;
                if (k >= 1) continue;
                double y = Lerp(0, LENS + 5, Ease.InQuad(k)), w = Half(y) + 1;
                Hoop(p, y, w, C[3], true);
                Hoop(p, y, w, k < 0.12 ? WHITE : C[6], false);
                p.Glow(0, y + w * 0.17, w + 4, C[5], 0.12);
            }
            // light streaks drawn up the beam
            Cycle(t, D, 22, seed + 1, r => r.Range(0.6, 1), (age, life, r, i) =>
            {
                double k = age / life, y = Lerp(-2, LENS + 5, k * k), w = Half(y), x = r.Range(-0.85, 0.85) * w, L = 2 + 6 * k;
                p.Line(x, y, x, y + L, u => u < 0.3 ? WHITE : C[5], Math.Min(1, (1 - k) * 3));
            });
            // dust drawn up in soft puffs, thinning as it goes
            Cycle(t, D, 9, seed + 3, r => r.Range(1.4, 2), (age, life, r, i) =>
            {
                double k = age / life;
                var (x, y, _) = Haul(r.Range(8, 26), r.Range(0, TAU), r.Sign(), 0.8, k);
                p.Disc(x, y, Lerp(4, 1.5, k), Mix(S[3], C[5], 0.25 + 0.5 * k), 0.4 * Math.Min(1, k * 6) * (1 - k));
            });
            // grit and motes of light hauled up in a quick spiral, streaking as they go
            Cycle(t, D, 46, seed + 2, r => r.Range(0.8, 1.3), (age, life, r, i) =>
            {
                double k = age / life, r0 = r.Range(6, 30), th0 = r.Range(0, TAU), dir = r.Sign(), turns = r.Range(1, 1.8);
                bool mote = r.Chance(0.45);
                var (x, y, z) = Haul(r0, th0, dir, turns, k);
                var (x1, y1, _) = Haul(r0, th0, dir, turns, k - (mote ? 0.13 : 0.06));
                uint c = mote ? (r.Chance(0.5) ? WHITE : C[6]) : r.Pick(new[] { S[3], S[4], S[4] });
                p.Line(x1, y1, x, y, u => u > 0.5 ? (z > 0 ? c : Scale(c, 0.7)) : mote ? C[4] : S[2]);
                if (mote)
                {
                    p.Px(x, y, WHITE);
                    p.Glow(x, y, 4, C[5], 0.45);
                }
            });
            // twinkles caught in the light
            Cycle(t, D, 8, seed + 4, 0.5, (age, life, r, i) =>
            {
                double k = age / life;
                var (x, y, _) = Haul(r.Range(4, 24), r.Range(0, TAU), r.Sign(), 0.6, r.Range(0.1, 0.7) + k * 0.15);
                double arm = Round(2 * Px.Sin(Math.PI * k));
                if (arm > 0) Sim.Sparkle(p, x, y, arm, C[6], C[5], 0.4);
            });
            // the rocks on the near side and those still on the floor
            foreach (Rock r in rocks)
            {
                double age = ScifiLib.Since(t, r.born, D);
                if (age >= LIFE) continue;
                RockState q = RockAt(r, age);
                if (q.z >= 0 || q.lift <= 0) DrawRock(p, q, r, S, C, false);
                // swallowed: a glint at the lens
                if (age > LIFE - 0.1) Sim.Sparkle(p, q.x, LENS + 3, 5, C[6], C[5], 0.5);
            }
            foreach (Rock r in rocks) ScifiLib.Flash(p, t, r.born + LIFE - 0.08, 0, LENS + 3, 30, C[5], 0.7, 0.28);
            g.At(t).Draw(p, Layer.Front);
            Hoop(p, 0, BOTW + 1, WHITE, false);
            Hoop(p, 1, BOTW, C[5], false);
            // light: the lens, the beam's body, the pool on the floor
            Pod(p, t, C, H, podY);
            double fl = 0.85 + 0.15 * Px.Sin(TAU * 6 * ph);
            p.Glow(0, LENS + 6, 36, C[5], 0.7 * fl);
            p.Glow(0, LENS + 6, 12, WHITE, 0.7 * fl);
            for (double y = LENS + 16; y < -8; y += 14) p.Glow(0, y, Half(y) + 10, C[4], 0.16);
            foreach (double x in new double[] { -20, 0, 20 }) p.Glow(x, -2, 22, C[4], 0.26);
        }
    }
}
