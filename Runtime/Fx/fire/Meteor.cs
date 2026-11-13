// The impact's particle recipe (fire puffs that cool into smoke, sparks, embers, burning brands that land and
// keep burning, rocks, grit, dust, scorch) is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Meteor : Effect
    {
        const double DURATION = 5;

        public static readonly Module Module = new Module("meteor", "pixelfx/fx/fire/meteor", 272, 232, 150, 208, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["rock"] = new[] { "#120e16", "#2a2230", "#463a4a", "#6a5a66" },
                ["seed"] = 9,
            },
            (o, e) => new Meteor(o, e))
        {
            Title = "Meteor",
            Desc = "A burning rock streaks in from the upper left in a comet of fire and smashes into the floor: a white flash, a fireball, rocks and brands thrown, a glowing crater that cools.",
            Tags = new[] { "fire", "meteor", "impact", "explosion", "spell", "debris", "one-shot" },
            Bg = "ground",
        };

        const double HIT = 0.62, FROM_X = -112, FROM_Y = -142;
        static readonly uint SOOT = Hex("#0d0a12"), SOOT2 = Hex("#17121d"), OUTLINE = Hex("#1a1422"), WARM = Rgb(255, 176, 96);
        static readonly double[][] COMET = { new[] { 36, 7, 0.0 }, new[] { 26, 7.5, -2.5 }, new[] { 26, 7.5, 2.5 }, new[] { 16, 8, 0.0 } };

        readonly uint[] H, RK;
        readonly Stone st;
        readonly Rng R;
        readonly double S, ux, uy;
        readonly Particles ps;
        readonly Blazes blazes;
        readonly Bits bits;
        readonly Lights lights;
        readonly Emitter emitter;
        readonly uint GLOW;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t, lastX, lastY;
        bool hit;

        Meteor(Options o, Emitter e)
        {
            uint[] F = Fit(o.Ramp("palette"), 9);
            H = HotFirst(F);
            RK = Fit(o.Ramp("rock"), 4);
            st = new Stone(o.Color("stone"));
            R = new Rng(o.Seed);
            S = o.Seed * 11;
            emitter = e;
            ps = new Particles(R, ground: x => 0, sparks: new[] { H[1], H[2], H[4], H[5] }, onLand: (x, y) => blazes.Add(x, y - 0.25, R.Range(0.9, 1.6), R.Range(0.5, 0.85)), emitter: e);
            blazes = new Blazes(ps, e);
            bits = new Bits(x => 0, e);
            lights = new Lights(e);
            GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25);
            double len = Hypot(FROM_X, FROM_Y);
            ux = -FROM_X / len;
            uy = -FROM_Y / len;
            (lastX, lastY) = Pos(0);
        }

        // the flight: speeding up a touch as it falls
        static (double x, double y) Pos(double tt)
        {
            double k = Ease.InQuad(Clamp(tt / HIT)) * 0.35 + Clamp(tt / HIT) * 0.65;
            return (FROM_X * (1 - k), FROM_Y * (1 - k));
        }

        // the meteor: a lumpy rock, white-hot on its leading face, cracks glowing over the dark back, an outline
        void Body(Painter p, double x, double y, double R0, double cool, double alpha = 1)
        {
            p.Shade(x - R0 - 2, y - R0 - 2, x + R0 + 2, y + R0 + 2, (px, py, o) =>
            {
                o.a = alpha;
                double dx = px - x, dy = py - y, d = Hypot(dx, dy), a = Atan2(dy, dx), rr = R0 * (1 + 0.18 * Noise(a * 2.2 + 3, S) + 0.1 * Noise(a * 5, S + 1));
                if (d > rr + 1) return 0;
                if (d > rr) return OUTLINE;
                double face = (dx * ux + dy * uy) / Math.Max(1, d), heat = (1 - cool) * (face * 0.6 + 0.5 - (d / rr) * 0.25);
                if (heat > 0.62) return heat > 0.85 ? H[0] : heat > 0.74 ? H[1] : H[2];
                if (heat > 0.42) return H[4];
                double h = Rand(Math.Floor(px - x), Math.Floor(py - y), S + 3);
                if (h < 0.22 * (1 - cool * 0.7)) return (1 - cool) > 0.5 ? H[4] : H[6];
                return dx * -0.6 + dy * -0.8 > 0 ? RK[2] : d / rr > 0.6 ? RK[0] : RK[1];
            });
        }

        // the crater: hot rock pooled in the dent cooling to soot, a lip of broken floor
        void Crater(Painter p)
        {
            if (t < HIT) return;
            double a = t - HIT, end = 1 - Smooth(DURATION - 1, DURATION - 0.15, t), rx = 36 * Math.Min(1, a / 0.08), ry = 6;
            uint[] ramp = { H[0], H[1], H[2], H[3], H[4], H[5], H[6], H[7], H[8] };
            for (int y = -1; y <= 3; y++)
                for (double x = Math.Floor(-rx); x <= Math.Ceiling(rx); x++)
                {
                    double dx = (x + 0.5) / rx, dy = (y + 0.5) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > 1) continue;
                    double k = Rand(x * 31 + y, S + 5), T = Px.Pow(Clamp(1 - a / 2.6), 1.3) * (1.25 - d) - k * 0.35;
                    if (T > 0.06)
                    {
                        uint c = ramp[(int)Math.Min(8, Math.Floor((1 - Math.Min(1, T)) * 8))];
                        p.Dot(x, y, c, end);
                        if ((ToInt32(x) & 3) == 0) p.Glow(x + 0.5, y, 7, c, 0.2 * T * end);
                    }
                    else p.Dot(x, y, d < 0.55 ? SOOT : SOOT2, (d < 0.95 - k * 0.3 ? 1 : 1 - d) * end);
                }
            // the lip: broken floor heaped round the edge, lit by the glow inside
            for (int x = -40; x <= 40; x++)
            {
                double e = Math.Abs(x) / 40.0, hh = e < 0.7 ? 0 : Round(2.4 * Px.Sin(((e - 0.7) / 0.3) * Math.PI) * (0.6 + 0.4 * Rand(x, S + 7)));
                for (int j = 0; j < hh; j++) p.Dot(x, -1 - j, j == hh - 1 ? Mix(st.light, H[4], 0.5 * Clamp(1 - a / 2)) : st.shade, end);
                if (hh != 0 && !double.IsNaN(hh)) p.Dot(x, 0, OUTLINE, end);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < HIT)
            {
                // the flight: a comet of fire shed behind, smoke, sparks, embers
                var (x, y) = Pos(t);
                double vx = (x - lastX) / dt, vy = (y - lastY) / dt;
                lastX = x;
                lastY = y;
                for (int i = 0; i < 3; i++)
                {
                    if (!R.Chance(0.8 * dt * 60)) continue;
                    ps.Spawn(P.Fire, x - ux * R.Range(2, 8) + R.Range(-3, 3), y - uy * R.Range(2, 8) + R.Range(-3, 3), vx * 0.15 + R.Range(-20, 20), vy * 0.15 + R.Range(-30, 10), R.Range(0.3, 0.6), 0, R.Range(3, 6), 0,
                        R.Chance(0.65) ? PF.Smokes | PF.Big : PF.None);
                }
                if (R.Chance(0.6 * dt * 60)) ps.Spawn(P.Spark, x, y, vx * 0.35 + R.Range(-80, 80), vy * 0.35 + R.Range(-80, 40), R.Range(0.2, 0.5));
                if (R.Chance(0.3 * dt * 60)) ps.Spawn(P.Ember, x, y, vx * 0.1 + R.Range(-30, 30), vy * 0.1 + R.Range(-40, 0), R.Range(0.8, 1.6), H[3], R.Chance(0.2) ? 2 : 1);
            }
            else if (!hit)
            {
                hit = true;
                // the impact: flash, fireball thrown up and forward with the rock's momentum, a column, smoke, sparks
                lights.Add(0, -40, 100, WARM, 0.95, 0.9);
                lights.Add(0, -20, 46, WHITE, 0.9, 0.12);
                for (int i = 0; i < 34; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.3, 1.3) + 0.25, sp = R.Range(50, 210);
                    ps.Spawn(P.Fire, R.Range(-6, 6), -R.Range(2, 10), Px.Cos(a) * sp, Px.Sin(a) * sp * 0.75 - 30, R.Range(0.5, 1.1), 0, R.Range(5, 9), 0, R.Chance(0.5) ? PF.Smokes | PF.Big : PF.None);
                }
                for (int i = 0; i < 9; i++) ps.Spawn(P.Fire, R.Range(-6, 6), -6, R.Range(-14, 14), -R.Range(130, 220), R.Range(0.8, 1.2), 0, R.Range(6, 8.5), R.Range(0, 0.12), PF.Smokes | PF.Big);
                for (int i = 0; i < 20; i++)
                {
                    double a = R.Range(-Math.PI, 0), d = R.Range(4, 18);
                    ps.Spawn(P.Smoke, Px.Cos(a) * d, -R.Range(6, 24), Px.Cos(a) * R.Range(10, 40), -R.Range(30, 110), R.Range(1.8, 2.8), 0, R.Range(8, 12), R.Range(0.15, 0.5), PF.Big);
                }
                for (int i = 0; i < 16; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(4, 18), -R.Range(1, 5), s * R.Range(60, 130) + 10, -R.Range(2, 12), R.Range(1.2, 2.0), st.dust, R.Range(5, 8), R.Range(0, 0.06), PF.Big);
                }
                for (int i = 0; i < 90; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.3, 1.3) + 0.15, sp = R.Range(100, 300);
                    ps.Spawn(P.Spark, 0, -4, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.7));
                }
                for (int i = 0; i < 36; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(40, 150);
                    ps.Spawn(P.Ember, Px.Cos(a) * 8, -8 + Px.Sin(a) * 6, Px.Cos(a) * sp, Px.Sin(a) * sp - 70, R.Range(1.2, 2.6), H[3]);
                }
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1, 1.05), sp = R.Range(130, 240);
                    ps.Spawn(P.Brand, 0, -6, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.7, 1.3));
                }
                for (int i = 0; i < 28; i++)
                    bits.Add(new Bits.Bit
                    {
                        kind = B.Chunk, x = R.Range(-8, 8), y = -3, vx = R.Range(-80, 110), vy = -R.Range(140, 340), g = 1, drag = 0.4, bounce = 0.3, life = R.Range(1.8, 2.8), a = R.Range(0, TAU), va = R.Range(-16, 16), s = R.Int(1, 3),
                        ramp = new[] { OUTLINE, st.shade, st.body, st.light, Mix(st.light, WHITE, 0.3) },
                    });
                for (int i = 0; i < 80; i++)
                {
                    double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.7 + 0.15, sp = R.Range(90, 270) * (1 - 0.4 * Math.Abs(u));
                    ps.Spawn(P.Grit, u * 6, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.2, 2.2), R.Pick(new[] { st.light, st.body, st.body, st.shade }), R.Chance(0.45) ? 2 : 1);
                }
            }
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
                if (b.rest && dusted.Add(b))
                    for (int i = 0; i < 3; i++)
                    {
                        ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 35), -R.Range(2, 18), R.Range(0.3, 0.6), st.dust, R.Range(1.3, 2.6));
                        ps.BornAt(b.bornX, b.bornY);
                    }
            blazes.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Crater(p);
            SimDraw(p, ps, H, Layer.Back, emitter);
            blazes.DrawBack(p);
            double k = (t - HIT) / 0.5;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0.5, 110 * Ease.OutCubic(k), 0.16, Math.Max(1, 5 * (1 - k)), 1.15 - k, new[] { st.body, st.light, H[4], H[1] }, g => Px.Sin(g) < 0);
        }

        public override void Draw(Painter p, double tt)
        {
            double k = (t - HIT) / 0.5;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0.5, 110 * Ease.OutCubic(k), 0.16, Math.Max(1, 5 * (1 - k)), 1.15 - k, new[] { st.body, st.light, H[4], H[1] }, g => Px.Sin(g) >= 0, k < 0.12);
            foreach (Bits.Bit b in bits.List) FireLib.Rock(p, emitter, b, st);
            p.Shift(0, 0);
            if (t < HIT)
            {
                // the comet: tongues of flame streaming back off the rock, the rock, its light; the floor lights up below
                var (x, y) = Pos(t);
                FrameNoise NZ = (rate, seed, off) => Noise(t * rate + off, seed);
                FloorLight(p, 0, 0.5, 30 + 40 * (t / HIT), 7, GLOW, 0.5 * Px.Pow(t / HIT, 2));
                for (int i = 0; i < COMET.Length; i++)
                {
                    double L = COMET[i][0], w = COMET[i][1], off = COMET[i][2];
                    double n = (S + i) * 13;
                    FlameBlade(p, x - uy * off, y + ux * off, -ux, -uy, L * (0.85 + 0.25 * NZ(5, n)), w, NZ(3, n + 3) * 3, NZ, n, H, LAYERS, 0.5, 1.6);
                }
                Body(p, x, y, 6.5, 0);
                p.Glow(x, y, 46, GLOW, 0.55);
                p.Glow(x, y, 12, H[1], 0.4);
            }
            else
            {
                // the core of the meteor sits half-buried in the crater, cooling
                double a = t - HIT, cool = Clamp(a / 2.8), end = 1 - Smooth(DURATION - 1.1, DURATION - 0.2, t);
                if (end > 0)
                {
                    Body(p, 0, 0, 5.5, cool, end);
                    p.Glow(0, -2, 22, GLOW, 0.4 * (1 - cool) * end);
                }
            }
            SimDraw(p, ps, H, Layer.Front, emitter);
            // the hero frame: a white-hot flash and a flare of rays
            double f = (t - HIT) / 0.08;
            if (f > 0 && f < 1)
            {
                p.Circle(0, -6, 11 * (1 - f * 0.3) + 1.5, H[3]);
                p.Circle(0, -6, 11 * (1 - f * 0.3), f < 0.5 ? H[1] : H[2]);
                p.Circle(0, -6, 7 * (1 - f * 0.3), f < 0.5 ? WHITE : H[1]);
            }
            double fr = (t - HIT) / 0.24;
            if (fr > 0 && fr < 1) Sim.Flare(p, 0, -6, 44 * Px.Pow(1 - fr, 1.4), 1 - fr, new[] { H[6], H[4], H[2], H[1] });
            lights.Draw(p);
        }
    }
}
