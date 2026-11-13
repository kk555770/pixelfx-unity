// The drops that splat where they land are adapted from Sprite Fusion's open-source blood
// (https://www.spritefusion.com/pixel-art-vfx/blood), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class AcidSplash : Effect
    {
        const double DURATION = 2.8;

        public static readonly Module Module = new Module("acid-splash", "pixelfx/fx/nature/acid-splash", 184, 116, 92, 100, DURATION, false, 30,
            new Options { ["palette"] = new[] { "#0e140a", "#1c3010", "#2c5214", "#457e18", "#6aae1c", "#a0dc2a", "#dcff6a", "#f8ffd0" }, ["seed"] = 6 },
            (o, e) => new AcidSplash(o, e))
        {
            Title = "Acid splash",
            Desc = "A gob of acid bursts into a crown and spatters; it eats a glowing, smoking pit into the floor and boils down into it, spitting bright beads, pale steam curling off.",
            Tags = new[] { "nature", "acid", "poison", "splash", "liquid", "ground", "one-shot" },
            Bg = "ground",
        };

        const double HIT = 0.3, Y0 = -62, V0 = 60, SPREAD = 0.4, DRY = 0.7, GONE = 2.2, W = 30, PIT = 5;

        sealed class Wisp
        {
            public double x, y, t0, life, rise, len, amp, k, ph, drift;
        }

        sealed class Spike
        {
            public double a, h, r;
            public bool shed;
        }

        sealed class Stain
        {
            public double x, t0, w;
        }

        sealed class Pop
        {
            public double x, y, t0;
        }

        // a curl of steam: thin, pale, quick, fading as it goes
        static void DrawWisp(Painter p, Wisp q, double t, uint col)
        {
            double age = t - q.t0, u = age / q.life;
            if (u < 0 || u >= 1) return;
            double head = q.rise * age, tail = Math.Max(0, head - q.len), fade = Smooth(0, 0.15, u) * (1 - Smooth(0.4, 1, u));
            for (double h = tail; h <= head; h += 0.5)
            {
                double s = (h - tail) / Math.Max(1, head - tail), x = q.x + q.amp * Px.Sin(h * q.k + q.ph) * (0.2 + s) + q.drift * age;
                p.Blend(x, q.y - h, col, fade * Px.Sin(Math.PI * s) * 0.42);
            }
        }

        readonly uint[] C;
        readonly Rng R;
        readonly uint SCORCH, BURNT, STEAM;
        readonly Particles ps;
        readonly NatureLib.Drops drops;
        readonly Lights lights;
        readonly List<Stain> stains = new List<Stain>();
        readonly List<Wisp> wisps = new List<Wisp>();
        readonly List<Pop> pops = new List<Pop>();
        readonly Spike[] spikes;
        readonly double[] rag;
        double t;
        bool hit;

        AcidSplash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            SCORCH = Mix(C[0], Hex("#140f0c"), 0.6);
            BURNT = Mix(C[1], Hex("#2a1a10"), 0.6);
            STEAM = Mix(C[7], Hex("#e8ece6"), 0.75);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[4] }, emitter: e);
            drops = new NatureLib.Drops(x => 0);
            lights = new Lights(e);
            spikes = new Spike[13];
            for (int i = 0; i < 13; i++)
            {
                double a = (i / 13.0) * TAU + R.Range(-0.18, 0.18), h = R.Range(4, 10), r = R.Range(0.85, 1.12);
                spikes[i] = new Spike { a = a, h = h, r = r };
            }
            rag = new double[80];
            for (int i = 0; i < 80; i++) rag[i] = Rand(i, o.Seed, 5) - 0.5;
        }

        // the pit it etches: wider as it spreads, deeper as it eats; the acid's level sinks into it as it boils away
        double Pw() => W * Ease.OutCubic(Clamp((t - HIT) / SPREAD)) * 1.02;
        double PitD() => PIT * Ease.OutCubic(Clamp((t - HIT) / 1.1));

        double Depth(double x)
        {
            double w = Pw(), d = Math.Abs(x) / Math.Max(1, w);
            return d >= 1 ? -1 : PitD() * Math.Sqrt(1 - d * d) + rag[(int)((Round(x) + 200) % 80)] * 0.9;
        }

        double Level() => t < HIT ? 0 : -1.6 + (PIT + 1.6) * Ease.InQuad(Smooth(DRY, GONE, t));

        double Width()
        {
            double L = Level(), w = Pw();
            return L <= 0 ? w : w * Math.Sqrt(Math.Max(0, 1 - Px.Pow(L / Math.Max(0.1, PitD()), 2)));
        }

        double Heat() => Smooth(HIT, HIT + 0.3, t) * (1 - Smooth(GONE - 0.3, GONE + 0.25, t));
        double GobY() => Y0 + V0 * t + 0.5 * 950 * t * t;
        double CrownR() => 4 + 11 * Ease.OutCubic(Clamp((t - HIT) / 0.16));
        double CrownH() => 5 * Px.Sin(Math.PI * Clamp((t - HIT) / 0.26));

        double SpikeK()
        {
            double u = Clamp((t - HIT - 0.015) / 0.26);
            return Px.Sin(Math.PI * Px.Pow(u, 0.6));
        }

        (double bx, double by, double tx, double ty) TipOf(Spike s)
        {
            double r = CrownR() * s.r, h = CrownH(), sn = Px.Sin(s.a), sh = s.h * SpikeK();
            double bx = Px.Cos(s.a) * r, by = sn * r * 0.3 - h * (0.75 + 0.25 * Math.Abs(sn));
            return (bx, by, bx + Px.Cos(s.a) * sh * 0.55, by - sh);
        }

        void Steam(double x, double y, bool big)
        {
            double life = Math.Min(R.Range(0.5, 0.9) * (big ? 1.3 : 1), DURATION - 0.05 - t);
            double rise = R.Range(22, 32), len = R.Range(5, 10) * (big ? 1.4 : 1), amp = R.Range(1, 2.5), k = R.Range(0.35, 0.6), ph = R.Range(0, TAU), drift = R.Range(-4, 4);
            wisps.Add(new Wisp { x = x, y = y, t0 = t, life = life, rise = rise, len = len, amp = amp, k = k, ph = ph, drift = drift });
        }

        // the crown: a thin cup wall rising round the impact, a bright rim, spikes off the rim with beads on their tips
        void Crown(Painter p, bool back)
        {
            if (t < HIT || t > HIT + 0.27) return;
            double r = CrownR(), h = CrownH(), ry = r * 0.3;
            for (double x = Math.Floor(-r); x <= Math.Ceiling(r); x++)
            {
                double c = (x + 0.5) / r;
                if (Math.Abs(c) > 1) continue;
                double e = Math.Sqrt(1 - c * c), yb = (back ? -ry : ry) * e, hh = h * (0.75 + 0.25 * e);
                for (double y = Math.Floor(yb - hh); y <= Round(yb); y++)
                {
                    bool rim = y == Math.Floor(yb - hh);
                    if (rim) p.Px(x, y, back ? C[5] : c < 0.1 ? C[7] : C[6]);
                    else p.Blend(x, y, C[(c < -0.25 ? 5 : c > 0.45 ? 3 : 4) - (back ? 1 : 0)], back ? 0.55 : 0.7);
                }
            }
            foreach (Spike s in spikes)
            {
                if ((Px.Sin(s.a) < 0) != back) continue;
                var (bx, by, tx, ty) = TipOf(s);
                p.Line(bx, by, tx, ty, back ? C[4] : C[6]);
                p.Line(bx + 1, by, (bx + tx) / 2 + 0.5, (by + ty) / 2, back ? C[3] : C[5]);
                if (!s.shed)
                {
                    p.Px(tx, ty - 1, back ? C[5] : C[7]);
                    p.Px(tx + 1, ty - 1, C[5]);
                }
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            Width();
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(0, -4, 28, WHITE, 0.6, 0.08);
                lights.Add(0, -10, 56, C[5], 0.5, 0.6);
                for (int i = 0; i < 26; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.35, 1.35), sp = R.Range(60, 250);
                    double x = R.Range(-3, 3);
                    drops.Add(x, -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Chance(0.3) ? 2 : 1);
                }
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(120, 260);
                    ps.Spawn(P.Spark, 0, -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.12, 0.3));
                }
                for (int i = 0; i < 5; i++)
                {
                    double x = R.Range(-10, 10), y = -R.Range(2, 6);
                    Steam(x, y, true);
                }
            }
            // beads break off the crown's spikes as they peak
            if (hit)
                foreach (Spike s in spikes)
                    if (!s.shed && t > HIT + 0.09)
                    {
                        s.shed = true;
                        var (_, _, tx, ty) = TipOf(s);
                        double vx = Px.Cos(s.a) * R.Range(30, 80), vy = -R.Range(50, 130);
                        drops.Add(tx, ty - 1, vx, vy, R.Chance(0.4) ? 2 : 1);
                    }
            drops.Update(dt, (d, g) =>
            {
                // a drop that lands eats a little spot into the floor and hisses
                if (Math.Abs(d.x) > Pw()) stains.Add(new Stain { x = d.x, t0 = t, w = d.s * 0.8 + R.Range(0.3, 1.2) });
                if (t < GONE) Steam(d.x, -1, false);
            });
            // the acid sizzles: tiny bright beads spit off it, pops flash on its surface, steam curls off it and the pit
            double lv = Level(), w2 = Width();
            if (t > HIT + 0.15 && t < GONE && w2 > 2)
            {
                if (R.Chance(0.3 * f))
                {
                    double x = R.Range(-0.85, 0.85) * w2, y = Math.Min(lv, Depth(x)) - 1;
                    pops.Add(new Pop { x = x, y = y, t0 = t });
                    for (int j = 0; j < R.Int(1, 3); j++)
                    {
                        double vx = R.Range(-40, 40), vy = -R.Range(50, 110);
                        drops.Add(x, Math.Min(y - 1, -1), vx, vy, 1);
                    }
                }
                if (R.Chance(0.14 * f))
                {
                    double x = R.Range(-0.9, 0.9) * w2, y = Math.Min(lv, 0) - 1;
                    Steam(x, y, R.Chance(0.25));
                }
            }
            // the pit keeps smoking a while after the acid is gone
            if (t > GONE - 0.2 && t < DURATION - 0.5 && R.Chance(0.08 * f)) Steam(R.Range(-0.7, 0.7) * Pw(), 0, false);
            for (int i = pops.Count - 1; i >= 0; i--)
                if (t - pops[i].t0 > 0.1)
                    pops.RemoveAt(i);
            for (int i = wisps.Count - 1; i >= 0; i--)
                if (t - wisps[i].t0 > wisps[i].life)
                    wisps.RemoveAt(i);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double fade = 1 - Smooth(DURATION - 0.6, DURATION - 0.08, t), hk = Heat(), w = Pw(), lv = Level();
            // spatter spots etched round it
            foreach (Stain s in stains)
            {
                double age = t - s.t0, k = 1 - Smooth(1, 1.8, age);
                uint c = age < 0.15 ? C[7] : age < 0.5 ? C[6] : age < 0.9 ? C[4] : BURNT;
                for (double x = Math.Floor(s.x - s.w); x <= Math.Ceiling(s.x + s.w); x++) p.Dot(x, 0, Math.Abs(x - s.x) < s.w * 0.5 ? SCORCH : c, k * fade);
                if (age < 0.5) p.Glow(s.x, -1, 5, C[5], 0.3 * (1 - age / 0.5));
            }
            Crown(p, true);
            if (t > HIT && w > 1)
            {
                // the pit: an etched bowl in the floor, its edge glowing where the acid is still eating, cooling to a burnt lip
                for (double x = Math.Floor(-w) - 1; x <= Math.Ceiling(w) + 1; x++)
                {
                    double dp = Depth(x + 0.5);
                    if (dp < 0) continue;
                    double bottom = Math.Max(0, Round(dp));
                    bool edge = Math.Abs(x + 0.5) / w > 0.88;
                    for (double y = 0; y <= bottom; y++) p.Dot(x, y, y == bottom ? (hk > 0.5 ? C[6] : hk > 0.15 ? C[4] : BURNT) : SCORCH, fade);
                    p.Dot(x, bottom + 1, hk > 0.5 ? C[5] : hk > 0.15 ? C[3] : SCORCH, fade * 0.8);
                    if (edge) p.Dot(x, -1, hk > 0.4 ? C[7] : hk > 0.1 ? C[5] : BURNT, fade);
                }
                foreach (int sd in new[] { -1, 1 }) p.Glow(sd * w, -1, 8, C[6], 0.45 * hk);
                // the acid in it: a bright skin shimmering on top, darker as it deepens, sinking as it boils off
                if (t < GONE)
                    for (double x = Math.Floor(-w); x <= Math.Ceiling(w); x++)
                    {
                        double dp = Depth(x + 0.5);
                        if (dp < 0) continue;
                        double d = Math.Abs(x + 0.5) / w, dome = lv < 0 ? Round(lv * Math.Sqrt(Math.Max(0, 1 - d * d))) : Round(lv), bottom = Round(dp);
                        if (dome > bottom) continue;
                        double n = Noise(x * 0.25 - t * 2.4, 3) + Noise(x * 0.1 + t * 1.4, 5) * 0.6;
                        for (double y = dome; y <= bottom; y++) p.Px(x, y, y == dome ? (n > 0.7 ? C[7] : C[6]) : y == dome + 1 ? (n > 0.2 ? C[5] : C[4]) : y >= bottom ? C[3] : C[4]);
                    }
                p.Glow(0, -6, w * 1.4 + 10, C[4], 0.3 * hk * (0.85 + 0.15 * Px.Sin(t * 9)));
                p.Glow(0, 1, w * 0.9, C[6], 0.2 * hk);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            // the gob: a teardrop stretched by its fall, lit on its upper left, a trail of droplets above it
            if (t < HIT)
            {
                double y = GobY(), v = V0 + 950 * t, st = 1 + v * 0.0022, rx = 2.6 / Math.Sqrt(st), ry = 2.8 * st;
                p.Shade(-rx - 1, y - ry - 4, rx + 1, y + ry + 1, (x, yy, o) =>
                {
                    double dx = x / rx, dy = (yy - y) / ry;
                    bool tail = dy < 0 && Math.Abs(x) < rx * (1 + dy * 0.9) + 0.4 && dy > -1.7;
                    double d = dx * dx + dy * dy;
                    if (d > 1.25 && !tail) return 0;
                    o.c = d > 1 && !tail ? C[2] : dx < -0.2 && dy < -0.1 ? C[7] : dy > 0.45 ? C[4] : C[5];
                    return 0;
                });
                p.Px(-1, y - ry * 0.3, WHITE);
                for (int i = 1; i <= 3; i++) p.Dot(0, y - ry - 2 - i * 4, C[5], 1 - i * 0.25);
                p.Glow(0, y, 10, C[5], 0.45);
            }
            Crown(p, false);
            // the hero frame: a white burst where it lands
            double e = t - HIT;
            if (e >= 0 && e < 0.07)
            {
                double k = 1 - e / 0.07;
                p.Ellipse(0, -2, 6 + 6 * (1 - k), 2.5, k > 0.5 ? WHITE : C[7]);
                Sim.Sparkle(p, 0, -4, 8 * k, C[7], C[6], 0.6);
            }
            // sizzle: tiny star flashes where beads spit off the surface
            foreach (Pop q in pops)
            {
                double k = (t - q.t0) / 0.1;
                p.Px(q.x, q.y, WHITE);
                foreach (int[] dd in POP_ARMS) p.Dot(q.x + dd[0] * (1 + k * 2), q.y + dd[1] * (1 + k * 2), C[7], 1 - k);
                p.Glow(q.x, q.y, 4, C[6], 0.4 * (1 - k));
            }
            foreach (Wisp q in wisps) DrawWisp(p, q, t, STEAM);
            drops.Draw(p, C);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }

        static readonly int[][] POP_ARMS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, -1 } };
    }
}
