// The pop (a white disc, a fast ring, sparks bursting, short arcs crawling the area as it dies away) follows
// Sprite Fusion's open-source lightning impact (https://www.spritefusion.com/pixel-art-vfx/lightning),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class EmpBlast : Effect
    {
        public static readonly Module Module = new Module("emp-blast", "pixelfx/fx/scifi/emp-blast", 152, 116, 76, 84, 3.2, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0d0820", "#1c1658", "#2d2aa0", "#3f55e8", "#5a8eff", "#9cc4ff", "#dceaff", "#ffffff" },
                ["fringe"] = "#ff5ad8",
                ["radius"] = 56,
                ["seed"] = 33,
            },
            (o, e) => new EmpBlast(o, e))
        {
            Title = "EMP blast",
            Desc = "A disc charge ticks, pops white, and a bubble of rolling scanlines swells out with arcs crawling its skin; it strobes, tears and shorts out into falling dead pixels.",
            Tags = new[] { "scifi", "emp", "pulse", "shockwave", "electric", "grenade", "glitch", "one-shot" },
            Bg = "ground",
        };

        const double DURATION = 3.2;
        // the beats: it ticks, pops, the bubble swells and holds, strobes, breaks
        const double POP = 0.42, GROW = 0.32, STROBE = 1.25, BREAK = 1.58;

        sealed class Arc
        {
            public double t, life;
            public double[][] pts;
        }

        sealed class Dead
        {
            public double x, y, vx, vy, life, max, s, ph;
            public uint c;
        }

        readonly uint[] C;
        readonly uint F;
        readonly double RM, seed;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Arc> arcs = new List<Arc>();
        readonly List<Dead> dead = new List<Dead>();
        double t, nextArc, starAt = -1;
        bool popped, broke;
        List<double[][]> star = new List<double[][]>();

        EmpBlast(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            F = o.Color("fringe");
            RM = o.Num("radius");
            seed = o.Seed;
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, haze: C[5], emitter: e);
            lights = new Lights(e);
        }

        double Radius() => t < POP ? 0 : RM * (0.12 + 0.88 * Ease.OutExpo(Clamp((t - POP) / GROW))) * (1 + 0.04 * Clamp((t - POP - GROW) / 0.8));

        // which frames the bubble cuts out in while it fails
        bool Off() => t > STROBE && t < BREAK && Rand(seed, Math.Floor(t * 30)) < 0.2 + 0.5 * ((t - STROBE) / (BREAK - STROBE));

        // the charge: a mote of light just off the floor, swelling, blinking faster, crackling
        void Charge(Painter p)
        {
            if (t >= POP) return;
            double k = t / POP, r = 1 + 3.2 * k * k;
            bool tick = Px.Sin(TAU * (2 * t + 10 * t * t)) > 0;
            p.Circle(0, -7, r + 1, C[4]);
            p.Circle(0, -7, r, tick ? WHITE : C[6]);
            double f = Math.Floor(t * 30);
            var rr = new Rng(seed * 97 + f);
            for (int i = 0; i < 1 + Math.Floor(k * 3); i++)
            {
                double a = rr.Range(0, TAU), l = r + 2 + rr.Range(2, 6) * k;
                ScifiLib.StrokeArc(p, Shapes.Bolt(rr, Px.Cos(a) * r, -7 + Px.Sin(a) * r, Px.Cos(a) * l, -7 + Px.Sin(a) * l, 0.4, 2), new[] { C[2], C[5], C[4] }, 0.7, 0.5);
            }
            p.Glow(0, -7, 8 + 16 * k, C[4], 0.4 + 0.5 * k * (tick ? 1 : 0.6));
        }

        // the bubble: thin glass, brighter toward its rim, interference lines rolling down it, tears across it
        void Bubble(Painter p, bool near)
        {
            double r = Radius();
            if (r <= 1 || t >= BREAK || Off()) return;
            double age = t - POP, slot = Math.Floor(t * 30), roll = age * 40, ry = r * 0.16;
            // the tears: a few rows this frame shifted sideways and burning bright
            var tears = new Dictionary<int, int>();
            for (int i = 0; i < 2 + (t > STROBE ? 4 : 0); i++)
                tears[(int)Math.Floor(-Rand(seed, slot, i) * r)] = (Rand(seed, slot, i, 1) < 0.5 ? -1 : 1) * (2 + (int)Math.Floor(Rand(seed, slot, i, 2) * 4));
            for (int y = (int)Math.Floor(-r); y <= Math.Ceiling(ry); y++)
            {
                int shift = tears.TryGetValue(y, out int sv) ? sv : 0;
                for (int x = (int)Math.Floor(-r); x <= Math.Ceiling(r); x++)
                {
                    int sx = x - shift;
                    double d = Hypot(sx + 0.5, y < 0 ? y + 0.5 : 0) / r;
                    // a split edge just outside the rim, as if the signal's colors had slipped
                    if (d > 1)
                    {
                        if (near && y < 0 && d < 1 + 1.4 / r && (x + y) % 2 == 0) p.Px(x, y, F);
                        continue;
                    }
                    // the dome above the floor, the floor disc below it only as its rim
                    if (y >= 0)
                    {
                        double e = Hypot((sx + 0.5) / r, (y + 0.5) / ry);
                        if (e > 1 || e < 0.92 || (y > 0) != near) continue;
                        p.Px(x, y, e > 0.97 ? C[6] : C[4]);
                        continue;
                    }
                    if (!near) continue;
                    double rim = Px.Pow(d, 6);
                    bool line = ((((y - roll) % 4) + 4) % 4) < 1, band = ((((y - roll * 0.5) % 22) + 22) % 22) < 4;
                    if (d > 0.965) p.Px(x, y, shift != 0 ? WHITE : -y > r * 0.55 && sx < 0 ? WHITE : C[6]);
                    else if (d > 0.93) p.Px(x, y, C[4]);
                    else if (shift != 0) p.Px(x, y, Math.Abs(sx) > r * 0.8 ? F : (x & 1) != 0 ? C[6] : C[5]);
                    else p.Blend(x + 0.5, y + 0.5, line && band ? C[6] : line ? C[5] : band ? C[4] : C[3], 0.06 + 0.42 * rim + (line ? 0.14 + 0.24 * rim : 0) + (band ? 0.16 : 0));
                }
                if (shift != 0 && near) p.GlowLine(-r, y, r, y, 4, C[5], 0.3);
            }
            if (near)
            {
                for (double a = 0.15; a < Math.PI - 0.1; a += 0.35) p.Glow(Px.Cos(a) * r, -Px.Sin(a) * r, 8, C[4], 0.12);
                // junk blocks flicker on inside it
                for (int i = 0; i < 3; i++)
                {
                    double k = Rand(seed, slot, i, 7);
                    if (k > 0.5) continue;
                    double a = Rand(seed, slot, i, 8) * Math.PI, d = Rand(seed, slot, i, 9) * r * 0.85, bx = Round(Px.Cos(a) * d), by = Round(-Px.Sin(a) * d);
                    p.Rect(bx, by, 2 + Math.Floor(k * 8), 1 + Math.Floor(k * 4), k < 0.2 ? WHITE : C[5]);
                }
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // charging: energy streaks in to the puck
            if (t < POP - 0.05 && R.Chance(0.6))
            {
                double a = R.Range(-Math.PI, 0), D = R.Range(16, 40), T = R.Range(0.14, 0.22), v = (D * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, Px.Cos(a) * D, -5 + Px.Sin(a) * D * 0.7, -Px.Cos(a) * v, -Px.Sin(a) * v * 0.7, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            // the discharge: a star of thick arcs out of the pop, jittering each frame as it shrinks
            double sa = t - POP, sf = Math.Floor(t * 30);
            if (sa >= 0 && sa < 0.26 && sf != starAt)
            {
                starAt = sf;
                double len = sa < 0.05 ? Ease.OutBack(sa / 0.05, 2) : 1 - (sa - 0.05) / 0.26;
                star = new List<double[][]>();
                for (int i = 0; i < 7; i++)
                {
                    double a = -Math.PI + (i / 6.0) * Math.PI + Rand(seed, i, 3) * 0.3 - 0.15 + R.Range(-0.1, 0.1), L = (20 + 16 * Rand(seed, i, 5)) * Math.Max(0.15, len);
                    star.Add(Shapes.Bolt(R, 0, -7, Px.Cos(a) * L, Math.Min(-1, -7 + Px.Sin(a) * L), 0.28, 4));
                }
            }
            if (sa >= 0.26) star = new List<double[][]>();
            if (t >= POP && !popped)
            {
                popped = true;
                for (int i = 0; i < 16; i++)
                {
                    double a = R.Range(-Math.PI, 0), sp = R.Range(50, 150);
                    ps.Spawn(P.Mote, 0, -7, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.3, 0.6), R.Pick(new[] { WHITE, C[6], C[5] }), 0.8);
                }
                lights.Add(0, -20, 72, C[4], 0.9, 0.5);
                lights.Add(0, -7, 30, WHITE, 0.9, 0.12);
                for (int i = 0; i < 60; i++)
                {
                    double a = R.Range(-Math.PI, 0), sp = R.Range(90, 220);
                    ps.Spawn(P.Spark, 0, -7, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.6));
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Smoke, R.Range(-3, 3), -4, R.Range(-12, 12), -R.Range(10, 24), R.Range(1, 1.6), Hex("#3c3a52"), R.Range(2.5, 4), R.Range(0.05, 0.3));
            }
            // arcs crawl over the bubble's skin, faster as it fails
            if (popped && !broke && t >= nextArc)
            {
                nextArc = t + (t > STROBE ? R.Range(0.02, 0.05) : R.Range(0.05, 0.11));
                double r = Radius(), a0 = R.Range(0.1, Math.PI - 0.1), a1 = a0 + R.Sign() * R.Range(0.25, 0.6), rr = r * R.Range(0.92, 1);
                double[][] pts = Shapes.Bolt(R, Px.Cos(a0) * rr, -Px.Sin(a0) * rr, Px.Cos(a1) * rr, -Math.Max(0.05, Px.Sin(a1)) * rr, 0.3, 4);
                arcs.Add(new Arc { t = t, pts = pts, life = R.Range(0.05, 0.1) });
                if (R.Chance(0.35)) ps.Spawn(P.Spark, Px.Cos(a1) * rr, -Math.Max(0.05, Px.Sin(a1)) * rr, R.Range(-80, 80), R.Range(-90, 10), R.Range(0.15, 0.3));
            }
            if (t >= BREAK && !broke)
            {
                broke = true;
                // it shorts out: the skin bursts into dead pixels, a last crackle, the charge smokes
                double r = Radius();
                lights.Add(0, -r * 0.4, r * 1.3, C[5], 0.8, 0.3);
                for (int i = 0; i < 70; i++)
                {
                    double a = R.Range(0.05, Math.PI - 0.05), d = r * R.Range(0.6, 1);
                    dead.Add(new Dead { x = Px.Cos(a) * d, y = -Px.Sin(a) * d, vx = Px.Cos(a) * R.Range(5, 25), vy = -R.Range(0, 20), life = R.Range(0.4, 1), max = 1, s = R.Chance(0.3) ? 2 : 1, c = R.Pick(new[] { WHITE, C[6], C[5], C[4] }), ph = R.Range(0, 9) });
                }
                for (int i = 0; i < 6; i++)
                {
                    double a0 = R.Range(0.1, Math.PI - 0.1), a1 = a0 + R.Range(-0.5, 0.5);
                    arcs.Add(new Arc { t = t, pts = Shapes.Bolt(R, Px.Cos(a0) * r, -Px.Sin(a0) * r, Px.Cos(a1) * r * 0.6, -Math.Max(0.05, Px.Sin(a1)) * r * 0.6, 0.35, 4), life = 0.1 });
                }
                for (int i = 0; i < 8; i++) ps.Spawn(P.Smoke, R.Range(-3, 3), -4, R.Range(-6, 6), -R.Range(8, 18), R.Range(0.8, 1.2), Hex("#2f2c3c"), R.Range(2, 3.4), R.Range(0, 0.35));
            }
            // the dead charge fizzes now and then
            if (broke && t < DURATION - 0.8 && R.Chance(0.05)) ps.Spawn(P.Spark, R.Range(-3, 3), -3, R.Range(-60, 60), -R.Range(40, 120), R.Range(0.15, 0.3));
            for (int i = arcs.Count - 1; i >= 0; i--)
                if (t - arcs[i].t > arcs[i].life) arcs.RemoveAt(i);
            for (int i = dead.Count - 1; i >= 0; i--)
            {
                Dead d = dead[i];
                if ((d.life -= dt) <= 0) dead.RemoveAt(i);
                d.vy += 60 * dt;
                d.x += d.vx * dt;
                d.y = Math.Min(-1, d.y + d.vy * dt);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Bubble(p, false);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            Charge(p);
            Bubble(p, true);
            // the pop: a white disc, a ring racing out ahead of the bubble, a flare
            double a = t - POP;
            if (a >= 0 && a < 0.06)
            {
                p.Circle(0, -7, 12 * (1 - a / 0.06) + 3, C[6]);
                p.Circle(0, -7, 9 * (1 - a / 0.06) + 1, WHITE);
            }
            if (a >= 0 && a < 0.4)
            {
                double k = a / 0.4, rr = 6 + (RM + 14) * Ease.OutCubic(k);
                Sim.Band(p, 0, 0, rr, 0.16, Math.Max(1, 3 * (1 - k)), 1.2 - k, new[] { C[3], C[5], C[6], WHITE }, g => true, k < 0.3);
            }
            if (a >= 0 && a < 0.16) Sim.Sparkle(p, 0, -7, 10 * (1 - a / 0.16), C[6], C[5], 0.6);
            foreach (double[][] b in star) ScifiLib.StrokeArc(p, b, new[] { C[3], C[6], C[5] }, 1 - (t - POP) / 0.3, 1.3);
            foreach (Arc c in arcs) ScifiLib.StrokeArc(p, c.pts, new[] { C[2], C[5], C[4] }, 1 - (t - c.t) / (c.life + 0.03), 0.8);
            // dead pixels: drifting down, blinking, going out
            foreach (Dead d in dead)
            {
                if (Px.Sin(t * 40 + d.ph) < -0.3 && d.life < 0.6) continue;
                p.Rect(d.x, d.y, d.s, d.s, d.life > 0.5 ? d.c : C[3]);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
