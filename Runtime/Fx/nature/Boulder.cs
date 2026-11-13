// The impact (rocks thrown up that tumble and bounce and kick up dust where they land, gravel, dust rolling out
// along the floor and climbing, a ring on the floor, a white flash) is adapted from Sprite Fusion's open-source
// explosion (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Boulder : Effect
    {
        const double DURATION = 2.3;

        public static readonly Module Module = new Module("boulder", "pixelfx/fx/nature/boulder", 264, 120, 40, 104, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#100e12", "#1e1a22", "#302a36", "#463e4c", "#5e5464", "#7c7080", "#a0949e", "#cec4c8" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["size"] = 11,
                ["seed"] = 11,
            },
            (o, e) => new Boulder(o, e))
        {
            Title = "Boulder",
            Desc = "A spinning boulder arcs right shedding dust and pebbles, smashes into the floor in a white flash and bursts into tumbling chunks, gravel and rolling dust.",
            Tags = new[] { "nature", "earth", "rock", "projectile", "impact", "directional", "one-shot" },
            Bg = "ground",
        };

        const double X0 = 6, Y0 = -26, VX = 220, VY = -170, G = 860;
        static readonly double[][] PITS = { new[] { 0.42, 0.12 }, new[] { -0.3, 0.38 } };

        // the boulder's own surface, turning with it: a few shallow pits, a mossy rim along one side
        static void Surface(Painter p, double x, double y, double r, double a, uint[] S, uint[] M)
        {
            double c = Px.Cos(a), s = Px.Sin(a);
            foreach (double[] uv in PITS)
            {
                double u = uv[0], v = uv[1];
                p.Px(x + (u * c - v * s) * r, y + (u * s + v * c) * r, S[3]);
            }
            for (int k = 0; k < 9; k++)
            {
                double g = 3.6 + (k / 8.0) * 1.3, rr = k % 3 == 1 ? 0.78 : 0.92, u = Px.Cos(g) * rr, v = Px.Sin(g) * rr;
                double px = x + (u * c - v * s) * r, py = y + (u * s + v * c) * r;
                p.Px(px, py, M[k % 3 == 1 ? 0 : py < y ? 2 : 1]);
            }
        }

        sealed class Rock
        {
            public double[][] pts;
            public bool dusted;
        }

        readonly uint[] S;
        readonly Rng R;
        readonly double RAD, tHit, xHit;
        readonly uint DUST, DUST2;
        readonly uint[] MOSSC;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly double[][] shape;
        readonly Dictionary<Bits.Bit, Rock> rocks = new Dictionary<Bits.Bit, Rock>();
        double t;
        bool hit, thrown;

        Boulder(Options o, Emitter e)
        {
            S = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            RAD = o.Num("size");
            DUST = Mix(S[6], Hex("#9a8f86"), 0.4);
            DUST2 = Mix(S[5], Hex("#7a7068"), 0.35);
            ps = new Particles(R, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            MOSSC = new[] { Hex("#1f3a1e"), Hex("#3f7a2c"), Hex("#7cb83a") };
            bits = new Bits(x => 0, e);
            lights = new Lights(e);
            shape = NatureLib.StoneShape(R, 8);
            tHit = (-VY + Math.Sqrt(VY * VY - 2 * G * (Y0 + RAD * 0.8))) / G;
            xHit = X0 + VX * tHit;
        }

        static (double x, double y) At(double tt) => (X0 + VX * tt, Y0 + VY * tt + 0.5 * G * tt * tt);

        void AddRock(Bits.Bit b)
        {
            var r = new Rock { pts = NatureLib.StoneShape(R) };
            bits.Add(b);
            rocks[b] = r;
        }

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            if (!thrown)
            {
                thrown = true;
                for (int i = 0; i < 6; i++) ps.Spawn(P.Dust, X0 - R.Range(0, 6), Y0 + R.Range(-4, 4), -R.Range(20, 70), R.Range(-20, 20), R.Range(0.4, 0.7), DUST, R.Range(2.5, 4.5));
                lights.Add(X0, Y0, 30, WHITE, 0.3, 0.1);
            }
            if (t < tHit)
            {
                // it sheds dust and the odd pebble as it flies
                var (x, y) = At(t);
                if (R.Chance(0.5 * f)) ps.Spawn(P.Dust, x - R.Range(2, 6), y + R.Range(-4, 4), -R.Range(10, 40), R.Range(-10, 10), R.Range(0.3, 0.6), DUST, R.Range(2, 3.5));
                if (R.Chance(0.12 * f)) ps.Spawn(P.Grit, x + R.Range(-4, 4), y + R.Range(-2, 4), VX * 0.3, R.Range(-40, 20), R.Range(0.6, 0.9), R.Pick(new[] { S[5], S[4] }), R.Chance(0.3) ? 2 : 1);
            }
            if (!hit && t >= tHit)
            {
                hit = true;
                double x = xHit, y = -RAD * 0.6;
                lights.Add(x, y, 36, WHITE, 0.75, 0.1);
                lights.Add(x, y, 60, Hex("#ffb060"), 0.45, 0.5);
                // big chunks of the boulder, thrown up and on, spinning
                for (int i = 0; i < 9; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.1), sp = R.Range(100, 240);
                    var b = new Bits.Bit { kind = B.Chunk };
                    b.x = x + R.Range(-5, 5);
                    b.y = y + R.Range(-5, 5);
                    b.vx = Px.Cos(a) * sp + 30;
                    b.vy = Px.Sin(a) * sp;
                    b.g = 1;
                    b.drag = 0.7;
                    b.bounce = 0.35;
                    b.life = R.Range(1.3, 1.7);
                    b.a = R.Range(0, TAU);
                    b.va = R.Range(-14, 14);
                    b.s = R.Range(2.8, 5.2);
                    AddRock(b);
                }
                for (int i = 0; i < 14; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(120, 280);
                    var b = new Bits.Bit { kind = B.Chunk };
                    b.x = x + R.Range(-4, 4);
                    b.y = y;
                    b.vx = Px.Cos(a) * sp + 25;
                    b.vy = Px.Sin(a) * sp;
                    b.g = 1;
                    b.drag = 0.8;
                    b.bounce = 0.3;
                    b.life = R.Range(1.1, 1.5);
                    b.a = R.Range(0, TAU);
                    b.va = R.Range(-18, 18);
                    b.s = R.Range(1.4, 2.6);
                    AddRock(b);
                }
                for (int i = 0; i < 60; i++)
                {
                    double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 1.1, sp = R.Range(80, 300) * (1 - 0.3 * Math.Abs(u));
                    ps.Spawn(P.Grit, x + u * 6, -1, Px.Cos(a) * sp + 30, Px.Sin(a) * sp, R.Range(1, 1.8), R.Pick(new[] { S[6], S[5], S[4], S[4], S[3] }), R.Chance(0.4) ? 2 : 1);
                }
                for (int i = 0; i < 12; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.3, 1.3), sp = R.Range(150, 380);
                    ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp + 40, Px.Sin(a) * sp, R.Range(0.15, 0.4));
                }
                // dust rolling out both ways along the floor, and a cloud climbing off the hit
                for (int i = 0; i < 14; i++)
                {
                    int sd = R.Sign();
                    ps.Spawn(P.Smoke, x + sd * R.Range(2, 10), -R.Range(1, 5), sd * R.Range(40, 140) + 10, -R.Range(4, 20), R.Range(1.1, 1.8), R.Chance(0.5) ? DUST : DUST2, R.Range(4.5, 8), R.Range(0, 0.05), PF.Big);
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Smoke, x + R.Range(-6, 6), -R.Range(4, 14), R.Range(-15, 25), -R.Range(30, 80), R.Range(1.2, 1.8), DUST, R.Range(6, 9), R.Range(0, 0.1), PF.Big);
                for (int i = 0; i < 3; i++) ps.Spawn(P.Star, x + R.Range(-8, 8), y + R.Range(-10, 4), 0, -4, R.Range(0.2, 0.4), S[7], R.Range(1, 2));
            }
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
            {
                Rock rk = rocks[b];
                if (b.rest && !rk.dusted)
                {
                    rk.dusted = true;
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 35), -R.Range(2, 12), Math.Min(R.Range(0.3, 0.6), DURATION - 0.05 - t), DUST2, R.Range(1.3, 2.6));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double e = t - tHit;
            // the scar it leaves: a dark dent with cracks running from it, fading at the end
            if (e > 0)
            {
                double fade = 1 - Smooth(DURATION - 0.6, DURATION - 0.15, t), w = 10 * Math.Min(1, e / 0.05);
                for (double x = Math.Floor(-w); x <= w; x++)
                {
                    double d = Math.Abs(x) / w;
                    p.Dot(xHit + x, 0, d < 0.6 ? S[0] : S[1], fade);
                    if (d < 0.5) p.Dot(xHit + x, 1, S[1], fade * 0.8);
                }
                foreach (int sd in new[] { -1, 1 })
                    for (int kk = 0; kk < 4; kk++)
                        p.Dot(xHit + sd * (w + 2 + kk * 3), (kk & 1) != 0 ? 1 : 0, S[1], fade * (1 - kk / 4.0));
                double k = e / 0.45;
                if (k < 1) Sim.Band(p, xHit, 0.5, 46 * Ease.OutCubic(k), 0.18, Math.Max(1, 3 * (1 - k)), 1.15 - k, new[] { S[4], S[6], Hex("#ffd27a"), WHITE }, g => Px.Sin(g) < 0);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            if (t < tHit)
            {
                var (x, y) = At(t);
                double sp = Hypot(VX, VY + G * t), ux = VX / sp, uy = (VY + G * t) / sp;
                // speed lines trailing it
                for (int i = 0; i < 4; i++)
                {
                    double off = (i - 1.5) * RAD * 0.55, nx = -uy * off, ny = ux * off, L = 10 + (i % 2) * 6;
                    p.Line(x - ux * (RAD + 2) + nx, y - uy * (RAD + 2) + ny, x - ux * (RAD + 2 + L) + nx, y - uy * (RAD + 2 + L) + ny, i % 2 != 0 ? S[5] : S[6], 0.8);
                }
                NatureLib.DrawRock(p, x, y, RAD, t * 13, shape, S);
                Surface(p, x, y, RAD * 0.8, t * 13, S, MOSSC);
                p.Glow(x, y, RAD * 2, S[6], 0.12);
            }
            double e = t - tHit;
            if (e >= 0 && e < 0.07)
            {
                // the hero frame: the boulder flashes white as it breaks, lines of force burst out
                NatureLib.DrawRock(p, xHit, -RAD * 0.8, RAD, tHit * 13, shape, S, 1, true);
                Sim.Sparkle(p, xHit, -RAD * 0.6, 12 * (1 - e / 0.07), WHITE, Hex("#ffd27a"), 0.6);
            }
            if (e >= 0 && e < 0.14)
            {
                double k = e / 0.14;
                for (int i = 0; i < 9; i++)
                {
                    double a = -Math.PI + (i / 8.0) * Math.PI, r0 = RAD + 4 + 16 * k, r1 = r0 + 8 * (1 - k);
                    p.Line(xHit + Px.Cos(a) * r0, -RAD * 0.6 + Px.Sin(a) * r0, xHit + Px.Cos(a) * r1, -RAD * 0.6 + Px.Sin(a) * r1, k < 0.5 ? WHITE : S[6]);
                }
            }
            if (e > 0)
            {
                double k = e / 0.45;
                if (k < 1) Sim.Band(p, xHit, 0.5, 46 * Ease.OutCubic(k), 0.18, Math.Max(1, 3 * (1 - k)), 1.15 - k, new[] { S[4], S[6], Hex("#ffd27a"), WHITE }, g => Px.Sin(g) >= 0);
            }
            foreach (Bits.Bit b in bits.List) NatureLib.DrawRock(p, b.x, b.y, b.s, b.a, rocks[b].pts, S, Math.Min(1, b.life / 0.4));
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
