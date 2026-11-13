// The blast (fire puffs that cool into smoke, sparks, grit, scorch, rings, lights) is adapted from Sprite Fusion's
// open-source explosion (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class Grenade : Effect
    {
        const double Duration = 4.2;
        const int W = 300, H = 200, OX = 160, OY = 170;

        public static readonly Module Module = new Module("grenade", "pixelfx/fx/explosions/grenade", W, H, OX, OY, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["body"] = new[] { "#141a10", "#2c3a1e", "#4a5c30", "#6e8446", "#9ab06a" },
                ["seed"] = 6,
            },
            (o, e) => new Grenade(o, e))
        {
            Title = "Grenade",
            Desc = "A grenade is lobbed in, bounces, rolls to a stop with its fuse fizzing, shudders, then blows: a white flash, shrapnel streaks, sparks, a small fireball and a scorch.",
            Tags = new[] { "explosion", "grenade", "shrapnel", "fire", "smoke", "ground", "one-shot" },
            Bg = "ground",
        };

        const double BLOW = 1.62, RB = 6.2;
        const double X0 = -OX + 3, X1 = W - OX - 3, Y0 = -OY + 3, Y1 = H - OY - 3;
        static readonly uint DUST = Hex("#7d7280"), FUSE_SMOKE = Hex("#8a8494"), FLOOR0 = Hex("#4a3f69"), FLOOR1 = Hex("#6e6390");
        static readonly uint[] METAL = { Hex("#3a3c46"), Hex("#6a6e7c"), Hex("#a8acb8") };
        static readonly uint[] GRIT = { Hex("#5a4f6b"), Hex("#3e3650"), Hex("#776c88") };
        static readonly uint[] CAP = { Hex("#2a2c36"), Hex("#5e6270"), Hex("#9a9eac"), Hex("#d0d4de") };

        sealed class Body
        {
            public double x = -140, y = -70, vx = 168, vy = -150, a = 0, va = -9;
            public bool rest;
        }

        readonly uint[] F, FR, HR, G;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Lights lights;
        readonly Bits bits;
        // the grenade: lobbed from the left, it bounces, rolls and settles
        readonly Body g = new Body();
        double t, bx;
        bool blown;

        Grenade(Options o, Emitter e)
        {
            emitter = e;
            F = Fit(o.Ramp("palette"), 8);
            FR = FireRamp(F);
            HR = HeatRamp(FR);
            G = Fit(o.Ramp("body"), 5);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(rng: R, ground: floor, sparks: new[] { F[6], F[5], F[3], F[2] }, emitter: e);
            lights = new Lights(e);
            bits = new Bits(floor, e);
        }

        void Blast()
        {
            blown = true;
            bx = g.x;
            double y = -5;
            lights.Add(bx, y - 10, 70, Mix(F[3], F[6], 0.45), 0.85, 0.5);
            lights.Add(bx, y, 30, WHITE, 0.7, 0.07);
            // shrapnel: hot streaks flying straight and far, sparks that fall and bounce
            for (int i = 0; i < 38; i++)
            {
                double a = R.Range(-Math.PI + 0.04, -0.04), sp = R.Range(380, 640);
                ps.Spawn(P.Mote, bx, y, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.12, 0.26), R.Pick(new[] { F[7], F[6], F[5] }), R.Range(0.6, 0.9));
            }
            for (int i = 0; i < 60; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(140, 360);
                ps.Spawn(P.Spark, bx, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 60, R.Range(0.2, 0.55));
            }
            // a small fireball that cools into smoke
            for (int i = 0; i < 4; i++) ps.Spawn(P.Fire, bx + R.Range(-2, 2), y + R.Range(-2, 2), R.Range(-15, 15), R.Range(-30, -8), R.Range(0.16, 0.26), 0, R.Range(6, 8));
            for (int i = 0; i < 16; i++)
            {
                double a = R.Range(-Math.PI - 0.2, 0.2), d = R.Range(0, 6), sp = R.Range(40, 150);
                ps.Spawn(P.Fire, bx + Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.8 - 30, R.Range(0.45, 0.9), 0, R.Range(3.5, 7), 0, R.Chance(0.5) ? PF.Smokes : PF.None);
            }
            for (int i = 0; i < 9; i++)
            {
                double a = R.Range(-Math.PI, 0), d = R.Range(2, 10);
                ps.Spawn(P.Smoke, bx + Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * 25, -R.Range(20, 60), R.Range(1.4, 2.1), 0, R.Range(6, 10), R.Range(0.05, 0.3), PF.Big);
            }
            for (int i = 0; i < 30; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(30, 120);
                ps.Spawn(P.Ember, bx, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 50, R.Range(0.8, 1.8), F[3]);
            }
            // the casing in pieces, and the floor's grit and dust
            for (int i = 0; i < 12; i++)
            {
                double a = R.Range(-Math.PI + 0.2, -0.2), sp = R.Range(150, 330);
                bits.Add(new Bits.Bit
                {
                    kind = B.Shard, x = bx, y = y, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 1, drag = 0.5, bounce = 0.3, life = R.Range(1.4, 2.1), a = R.Range(0, TAU),
                    va = R.Range(-24, 24), s = R.Range(2, 3.5), c = R.Pick(METAL),
                });
            }
            for (int i = 0; i < 70; i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.8, sp = R.Range(100, 300) * (1 - 0.4 * Math.Abs(u));
                ps.Spawn(P.Grit, bx + u * 4, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.8), R.Pick(GRIT), R.Chance(0.35) ? 2 : 1);
            }
            for (int i = 0; i < 10; i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Smoke, bx + s * R.Range(0, 6), -R.Range(1, 4), s * R.Range(60, 180), -R.Range(2, 10), R.Range(1, 1.7), DUST, R.Range(5, 8), R.Range(0, 0.05), PF.Big);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!blown)
            {
                // the lob, the bounces, the roll
                if (!g.rest)
                {
                    g.vy += 900 * dt;
                    g.x += g.vx * dt;
                    g.y += g.vy * dt;
                    if (g.y >= -RB && g.vy > 0)
                    {
                        g.y = -RB;
                        if (g.vy > 90)
                        {
                            // a clink, a puff of dust
                            for (int i = 0; i < 4; i++) ps.Spawn(P.Dust, g.x + R.Range(-2, 2), -0.5, R.Sign() * R.Range(10, 40), -R.Range(4, 16), R.Range(0.3, 0.6), DUST, R.Range(1.5, 2.6));
                            for (int i = 0; i < 3; i++) ps.Spawn(P.Spark, g.x, -1, R.Range(-60, 60), -R.Range(40, 120), R.Range(0.08, 0.16));
                            g.vy = -g.vy * 0.36;
                            g.vx *= 0.8;
                        }
                        else g.vy = 0;
                    }
                    if (g.y >= -RB - 0.01 && g.vy == 0)
                    {
                        g.vx -= g.vx * 2.4 * dt;
                        g.va = -g.vx / RB;
                        if (Math.Abs(g.vx) < 3)
                        {
                            g.rest = true;
                            g.vx = 0;
                        }
                    }
                    g.a += g.va * dt;
                }
                // the fuse: sparks spitting off its tip, a thread of smoke, faster as it runs down
                var (tx, ty) = Fuse(g.x, g.y, g.a);
                double hurry = Smooth(BLOW - 0.5, BLOW, t);
                if (R.Chance((24 + 50 * hurry) * dt)) ps.Spawn(P.Spark, tx, ty, R.Range(-50, 50), -R.Range(30, 110), R.Range(0.08, 0.2));
                if (R.Chance(9 * dt)) ps.Spawn(P.Vapour, tx, ty - 1, R.Range(-4, 4), -R.Range(10, 20), R.Range(0.5, 0.9), FUSE_SMOKE, R.Range(1.2, 2));
                if (t >= BLOW) Blast();
            }
            bits.Update(dt);
            for (int i = bits.List.Count - 1; i >= 0; i--)
                if (bits.List[i].x < X0 + 3 || bits.List[i].x > X1 - 3 || bits.List[i].y < Y0 + 3) bits.List.RemoveAt(i);
            ps.Update(dt);
            Cull(ps, X0, Y0, X1, Y1);
            FadeEdges(ps, X0, X1, 24, Y1);
            lights.Update(dt);
        }

        void Band(Painter p, bool far)
        {
            double k = (t - BLOW) / 0.4;
            if (k < 1) Sim.Band(p, bx, 0.5, 70 * Ease.OutCubic(k), 0.16, Math.Max(1, 3.5 * (1 - k)), 1.15 - k, new[] { FLOOR0, FLOOR1, F[3], F[6] }, g2 => far ? Px.Sin(g2) < 0 : Px.Sin(g2) >= 0);
        }

        public override void DrawBack(Painter p)
        {
            if (blown)
            {
                double u = t - BLOW;
                Scorch(p, HR, u, 22, 1.6, 1 - Smooth(Duration - BLOW - 0.8, Duration - BLOW - 0.15, u), 2);
                Band(p, true);
            }
            DrawPs(p, ps, Layer.Back, FR, emitter);
        }

        public override void Draw(Painter p)
        {
            if (!blown)
            {
                // the grenade, shuddering in its last half second, the fuse glowing
                double shake = Smooth(BLOW - 0.45, BLOW, t), jx = shake * Px.Sin(t * 90) * 0.8;
                DrawGrenade(p, G, g.x + jx, g.y, g.a);
                var (tx, ty) = Fuse(g.x + jx, g.y, g.a);
                p.Px(tx, ty, Px.Sin(t * 50) > 0 ? F[6] : F[5]);
                p.Glow(tx, ty, 6 + 3 * shake, F[4], 0.5 + 0.3 * Px.Sin(t * 37));
            }
            else Band(p, false);
            bits.Draw(p);
            DrawPs(p, ps, Layer.Front, FR, emitter);
            if (blown)
            {
                double u = t - BLOW;
                // the flash: a hot disc for two frames, a flare, a ring of light
                if (u < 0.07)
                {
                    double k2 = 1 - u / 0.07, r = 11 * (0.8 + 0.2 * k2);
                    p.Circle(bx, -6, r + 1.2, F[2]);
                    p.Circle(bx, -6, r, k2 > 0.45 ? F[6] : F[5]);
                    p.Circle(bx, -6, r * 0.6, k2 > 0.45 ? WHITE : F[6]);
                }
                if (u < 0.2) Sim.Flare(p, bx, -6, 40 * Px.Pow(1 - u / 0.2, 1.4), 1 - u / 0.2, F);
                if (u < 0.3)
                {
                    double k3 = u / 0.3, e = 1 - Px.Pow(1 - k3, 3);
                    double r = 8 + 40 * e;
                    p.Ring(bx, -6, r, Math.Max(1, 2.5 * (1 - k3) + 0.4), F[5], (v, an) => Px.Sin(an) * r > 9 ? 0 : Math.Min(1, (1 - k3) * 1.6));
                }
                Pool(p, bx, 1, 70, 12, F[3], 0.55 * Math.Max(0, 1 - u / 0.6));
            }
            lights.Draw(p);
        }

        // where the fuse's tip is: on top of the grenade, turning with it
        static (double x, double y) Fuse(double x, double y, double a)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a);
            return (x + sa * 10.4, y - ca * 10.4);
        }

        // a pineapple grenade turned by a (about 11 x 15 px): an oval body with grooves, lit from the top left, an
        // outline, a metal cap with a ring pin, a lever down its side
        static void DrawGrenade(Painter p, uint[] G, double x, double y, double a)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a);
            uint[] M = CAP;
            p.Shade(x - 13, y - 13, x + 13, y + 13, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, u = dx * ca + dy * sa, v = -dx * sa + dy * ca; // u across, v along (cap at v < 0)
                // the cap, its ring, and the lever pressed down the side
                if (v < -5 && v > -10.2 && Math.Abs(u) < 2.4) return v < -9.4 || Math.Abs(u) > 1.7 ? M[0] : u < -0.6 ? M[3] : M[2];
                if (Hypot(u + 3.4, v + 8) < 1.9 && Hypot(u + 3.4, v + 8) > 0.9) return M[2];
                if (u > 2.2 && u < 3.6 && v > -8.6 && v < 2) return u > 3 ? M[0] : M[1];
                double e = Px.Pow(u / 5.1, 2) + Px.Pow((v - 0.6) / 6.4, 2);
                if (e > 1.2) return 0;
                if (e > 1) return OUTLINE;
                // grooves cut the body into knobbly segments; light from the top left in world space
                double lit = -(dx * 0.6 + dy * 0.8) / 6;
                bool gv = Math.Abs(((v + 30) % 3.1) - 1.55) < 0.45, gu = Math.Abs(((u + 30) % 3.4) - 1.7) < 0.45;
                int i = (int)Math.Max(0, Math.Min(4, Round(2 + lit * 1.7 - (gv || gu ? 1 : 0) + (e < 0.3 && lit > 0.3 ? 1 : 0))));
                return G[i];
            });
        }
    }
}
