// The blast's particle recipe (fire that cools, sparks, embers, grit, rocks, scorch, floor ring, lights) is adapted
// from Sprite Fusion's open-source explosion (https://www.spritefusion.com/pixel-art-vfx/explosion),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class HomingMissile : Effect
    {
        public static readonly Module Module = new Module("homing-missile", "pixelfx/fx/projectiles/homing-missile", 248, 140, 160, 120, 3.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["metal"] = new[] { "#25243a", "#3e4058", "#61667f", "#8e97b0", "#c4cce0" },
                ["smoke"] = new[] { "#26222e", "#3b3644", "#544e5e", "#6f6979", "#8c8596", "#aca5b5", "#cbc4d2" },
                ["stone"] = "#4a3f69",
                ["seed"] = 8,
            },
            (o, e) => new HomingMissile(o, e))
        {
            Title = "Homing missile",
            Desc = "A missile kicks off, arcs up, rolls over and dives onto its target (the origin), its smoke trail left hanging in the air, and hits in a fiery blast of flame, sparks and rocks.",
            Tags = new[] { "projectile", "missile", "rocket", "homing", "smoke", "explosion", "one-shot" },
            Bg = "ground",
        };

        const double D = 3.8, FLY = 1.15;
        // the path: up out of the launcher, over the top, and down onto the target, faster all the way
        static readonly double[][] PATH = { new double[] { -130, -22 }, new double[] { -114, -110 }, new double[] { -30, -120 }, new double[] { 0, 0 } };

        static (double x, double y) At(double u)
        {
            double v = 1 - u, a = v * v * v, b = 3 * v * v * u, c = 3 * v * u * u, d = u * u * u;
            return (a * PATH[0][0] + b * PATH[1][0] + c * PATH[2][0] + d * PATH[3][0], a * PATH[0][1] + b * PATH[1][1] + c * PATH[2][1] + d * PATH[3][1]);
        }

        static double Heading(double u)
        {
            double v = 1 - u;
            double Dv(int i) => 3 * v * v * (PATH[1][i] - PATH[0][i]) + 6 * v * u * (PATH[2][i] - PATH[1][i]) + 3 * u * u * (PATH[3][i] - PATH[2][i]);
            return Atan2(Dv(1), Dv(0));
        }

        static double Progress(double t) => Px.Pow(Clamp(t / FLY), 1.45);

        // the missile, nose right: fins, a body lit on top, a red warhead with a white tip
        static readonly string[] MISSILE = { "ff..........", ".fhhhhhhhre.", "nmmmmmmmmrrw", ".fsssssssrd.", "ff.........." };
        static readonly uint SOOT = Hex("#0d0a12"), SCORCH = Hex("#17121d"), RIM = Hex("#1a1422");

        readonly uint[] F, M, SM;
        readonly Rng R;
        readonly uint sLight, sBody, sShade, sDust;
        readonly Particles ps;
        readonly Smoke smoke;
        readonly Bits rocks;
        readonly Lights lights;
        readonly Dictionary<char, uint> pal;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t, emit;
        bool hit;

        HomingMissile(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 9);
            M = Fit(o.Ramp("metal"), 5);
            SM = Fit(o.Ramp("smoke"), 7);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            uint st = o.Color("stone");
            sLight = Mix(Scale(st, 1.35), WHITE, 0.08);
            sBody = st;
            sShade = Scale(st, 0.64);
            sDust = Mix(Scale(st, 1.2), Hex("#968c96"), 0.3);
            ps = new Particles(R, ground: floor, sparks: new[] { F[7], F[6], F[4], F[3] }, emitter: e);
            smoke = new Smoke(R);
            rocks = new Bits(floor, e);
            lights = new Lights(e);
            pal = new Dictionary<char, uint> { ['f'] = M[1], ['h'] = M[4], ['m'] = M[3], ['s'] = M[1], ['n'] = M[0], ['r'] = F[2], ['e'] = F[3], ['d'] = F[1], ['w'] = WHITE };
            lights.Add(PATH[0][0], PATH[0][1], 30, F[4], 0.7, 0.25);
            for (int i = 0; i < 6; i++)
                smoke.Add(new SmokeQ { x = PATH[0][0] + 2, y = PATH[0][1] + 2, vx = R.Range(-30, 20), vy = R.Range(-10, 15), r0 = 2, r1 = R.Range(5, 7.5), life = R.Range(1.2, 1.8), a = 0.6, warm = 1, rise = 8 });
        }

        void Blast()
        {
            double Y = -6;
            lights.Add(0, Y - 2, 52, Hex("#ffb060"), 0.8, 0.8);
            lights.Add(0, Y - 2, 30, WHITE, 0.75, 0.1);
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(0, TAU), d = R.Range(0, 6), sp = R.Range(30, 170);
                ps.Spawn(P.Fire, Px.Cos(a) * d, Y + Px.Sin(a) * d, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.75 - 40, R.Range(0.45, 0.95), 0, R.Range(3.2, 6.5));
            }
            for (int i = 0; i < 5; i++) ps.Spawn(P.Fire, R.Range(-3, 3), Y, R.Range(-10, 10), -R.Range(90, 150), R.Range(0.7, 1.1), 0, R.Range(4, 5.5), R.Range(0, 0.08));
            for (int i = 0; i < 70; i++)
            {
                double a = R.Range(Math.PI + 0.12, TAU - 0.12), sp = R.Range(110, 280);
                ps.Spawn(P.Spark, 0, Y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.2, 0.65));
            }
            for (int i = 0; i < 22; i++)
            {
                double a = R.Range(Math.PI, TAU), sp = R.Range(30, 110);
                ps.Spawn(P.Ember, Px.Cos(a) * 4, Y + Px.Sin(a) * 4, Px.Cos(a) * sp, Px.Sin(a) * sp - 50, R.Range(0.9, 1.6), F[4]);
            }
            for (int i = 0; i < 16; i++)
                rocks.Add(new Bits.Bit { kind = B.Chunk, x = R.Range(-5, 5), y = -2, vx = R.Range(-110, 110), vy = -R.Range(140, 290), g = 1, drag = 0.4, bounce = 0.3, life = R.Range(1.5, 2.2), a = R.Range(0, TAU), va = R.Range(-16, 16), s = R.Int(1, 2) });
            for (int i = 0; i < 60; i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.55, sp = R.Range(80, 220) * (1 - 0.4 * Math.Abs(u));
                ps.Spawn(P.Grit, u * 4, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.8), R.Pick(new[] { sLight, sBody, sBody, sShade }), R.Chance(0.4) ? 2 : 1);
            }
            // the smoke: a column off the fire and dust rolling out along the floor, lit by the blast at first
            for (int i = 0; i < 18; i++)
            {
                double a = R.Range(Math.PI * 1.15, Math.PI * 1.85), sp = R.Range(20, 90);
                smoke.Add(new SmokeQ { x = Px.Cos(a) * 4, y = Y + Px.Sin(a) * 4, vx = Px.Cos(a) * sp * 0.7, vy = Px.Sin(a) * sp - 45 * (i / 18.0), r0 = 3, r1 = R.Range(8, 14), life = R.Range(1.7, 2.2), a = 0.62, b = 0.88, warm = 1, rise = 30 + i * 2, drag = 1.8, delay = R.Range(0.05, 0.35) });
            }
            for (int i = 0; i < 12; i++)
            {
                int s = R.Sign();
                smoke.Add(new SmokeQ { x = s * R.Range(0, 6), y = -3, vx = s * R.Range(60, 150), vy = -R.Range(3, 12), r0 = 2.5, r1 = R.Range(6, 9), life = R.Range(1.1, 1.7), a = 0.5, b = 0.75, warm = 0.3, rise = 6, drag = 3 });
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < FLY)
            {
                // the trail: puffs left behind along the path, hanging where they're dropped, swelling and thinning
                double u = Progress(t);
                var (x, y) = At(u);
                double h = Heading(u), cx = Px.Cos(h), cy = Px.Sin(h);
                var (px, py) = At(Progress(Math.Max(0, t - dt)));
                // one puff every pixel and a half of flight, wherever along this step it fell, so the trail has no beads
                for (emit += Hypot(x - px, y - py) / 1.5; emit > 1; emit--)
                {
                    double f = (emit - 1) / Math.Max(1e-6, Hypot(x - px, y - py) / 1.5), ex = x + (px - x) * f, ey = y + (py - y) * f;
                    smoke.Add(new SmokeQ { x = ex - cx * 7 + R.Range(-0.6, 0.6), y = ey - cy * 7 + R.Range(-0.6, 0.6), vx = -cx * R.Range(10, 30) + R.Range(-6, 6), vy = -cy * R.Range(10, 30) + R.Range(-6, 6), r0 = 1.4, r1 = R.Range(3.5, 6), life = R.Range(1.5, 2.3), a = 0.5, b = 0.9, warm = 0.8, rise = 4, drag = 3, grow = 1.2 });
                }
                if (R.Chance(0.35))
                {
                    double a = h + Math.PI + R.Range(-0.5, 0.5), sp = R.Range(40, 140);
                    ps.Spawn(P.Spark, x - cx * 7, y - cy * 7, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.15, 0.35));
                }
            }
            else if (!hit)
            {
                hit = true;
                Blast();
            }
            foreach (Bits.Bit b in rocks.List)
                if (b.rest && !dusted.Contains(b))
                {
                    dusted.Add(b);
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Dust, b.x + R.Range(-1, 1), -0.5, R.Sign() * R.Range(8, 30), -R.Range(2, 14), R.Range(0.3, 0.6), sDust, R.Range(1.2, 2.2));
                }
            ps.Update(dt);
            smoke.Update(dt);
            rocks.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the scorch: white-hot where it hit, cooling through the fire to soot, fading late
            if (hit)
            {
                double k = t - FLY, fade = 1 - Smooth(D - 1, D - 0.2, t), rx = 16 * Math.Min(1, k / 0.06);
                for (double y = 0; y <= 2; y++)
                    for (double x = Math.Floor(-rx); x <= Math.Ceiling(rx); x++)
                    {
                        double d = Hypot((x + 0.5) / rx, (y + 0.5) / 4);
                        if (d > 1) continue;
                        double T = Px.Pow(Clamp(1 - k / 1.4), 1.4) * (1.25 - d) - Rand(x * 31 + y, 3) * 0.35;
                        p.Dot(x, y, T > 0.06 ? F[(int)Math.Max(1, Math.Min(8, Round(T * 8)))] : d < 0.5 ? SOOT : SCORCH, fade);
                    }
                double q = k / 0.45;
                if (q < 1) Sim.Band(p, 0, 0.5, 60 * (1 - Px.Pow(1 - q, 3)), 0.16, Math.Max(1, 4 * (1 - q)), 1.15 - q, new[] { sBody, sLight, F[4], F[7] }, g => Px.Sin(g) < 0);
            }
        }

        public override void Draw(Painter p)
        {
            List<Puff> list = smoke.Puffs();
            Cloud(p, list, SM, new[] { SM[0], Mix(F[1], SM[1], 0.5), Mix(F[2], SM[2], 0.4), F[3], F[4], F[6], F[7] }, dens: 1.4, max: 0.82, lit: 1.4);
            if (hit)
            {
                double q = (t - FLY) / 0.45;
                if (q < 1) Sim.Band(p, 0, 0.5, 60 * (1 - Px.Pow(1 - q, 3)), 0.16, Math.Max(1, 4 * (1 - q)), 1.15 - q, new[] { sBody, sLight, F[4], F[7] }, g => Px.Sin(g) >= 0);
            }
            foreach (Bits.Bit b in rocks.List)
            {
                double r = 0.6 + b.s * 0.75, fade = Math.Min(1, b.life / 0.5), lx = Px.Cos(b.a) * r * 0.4, ly = Px.Sin(b.a) * r * 0.4;
                p.Shade(b.x - r - 1, b.y - r - 1, b.x + r + 1, b.y + r + 1, (x, y, ou) =>
                {
                    double dx = x - b.x, dy = y - b.y, d = Hypot(dx * 1.15, dy);
                    if (d > r + 0.9) return 0;
                    double lit = dx * lx + dy * ly;
                    ou.c = d > r ? RIM : lit < -0.4 ? sLight : lit > 0.6 ? sShade : sBody;
                    ou.a = fade;
                    return 0;
                });
            }
            ps.Draw(p);
            if (t < FLY)
            {
                double u = Progress(t);
                var (x, y) = At(u);
                double h = Heading(u), cx = Px.Cos(h), cy = Px.Sin(h);
                int fr = (int)Math.Floor(t * 30);
                // the motor: a short white-hot jet that flickers, the missile riding its heading
                double J = 5 + 3 * Rand(fr, 7);
                for (double d = 0; d <= J; d += 0.5)
                {
                    double v = d / J;
                    uint c = v < 0.3 ? WHITE : v < 0.6 ? F[6] : F[4];
                    p.Dot(x - cx * (6.5 + d), y - cy * (6.5 + d), c);
                    if (v < 0.5) p.Dot(x - cx * (6.5 + d) - cy * 0.8, y - cy * (6.5 + d) + cx * 0.8, F[5]);
                }
                TurnSprite(p, MISSILE, pal, x, y, h, 6, 2.5);
                p.Glow(x - cx * 8, y - cy * 8, 18, F[4], 0.5);
                p.Glow(x - cx * 7, y - cy * 7, 6, F[7], 0.5);
            }
            // the hit's flash: a white ball for two frames and a ring of flame
            double k = t - FLY;
            if (k >= 0 && k < 0.07)
            {
                double r = 14 * (0.8 + 0.2 * (1 - k / 0.07));
                p.Circle(0, -6, r + 1.2, F[3]);
                p.Circle(0, -6, r, k < 0.035 ? F[7] : F[6]);
                p.Circle(0, -6, r * 0.6, k < 0.035 ? WHITE : F[7]);
            }
            if (k >= 0 && k < 0.3)
            {
                double rr = 8 + 30 * (1 - Px.Pow(1 - k / 0.3, 3));
                p.Ring(0, -6, rr, Math.Max(1, 2.5 * (1 - k / 0.3) + 0.4), F[5], (v, ang) => -6 + Px.Sin(ang) * rr > 0 ? 0 : Math.Min(1, (1 - k / 0.3) * 1.6));
            }
            lights.Draw(p);
        }
    }
}
