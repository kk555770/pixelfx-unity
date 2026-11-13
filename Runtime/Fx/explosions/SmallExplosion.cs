using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    // Adapted by pixelfx from Sprite Fusion's open-source explosion, MIT, Hugo Duprez.
    public sealed class SmallExplosion : Effect
    {
        const double Duration = 5.4;

        public static readonly Module Module = new Module("small-explosion", "pixelfx/fx/explosions/small-explosion", 300, 240, 150, 196, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["size"] = 1.0,
                ["seed"] = 7.0,
            },
            (o, e) => new SmallExplosion(o, e));

        static readonly uint SOOT = Hex("#0d0a12"), SOOT2 = Hex("#17121d"), OUTLINE = Hex("#1a1422"), WARM = Rgb(255, 176, 96);

        sealed class Flash
        {
            public double r, life, max;
        }

        sealed class Shock
        {
            public double r0, r1, life, max, thick;
            public uint c;
        }

        readonly uint[] F;
        readonly double S, RAD, Y;
        readonly ImpactsLib.Stone st;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Blazes blazes;
        readonly Bits rocks;
        readonly Lights lights;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        readonly List<Flash> flashes = new List<Flash>();
        readonly List<Shock> rings = new List<Shock>();
        double t;

        SmallExplosion(Options o, Emitter e)
        {
            emitter = e;
            F = Fit(o.Ramp("palette"), 8);
            S = o.Num("size");
            RAD = 18 * S;
            double POWER = 0.9 * Math.Pow(S, 0.8), EARTH = 1.6;
            st = new ImpactsLib.Stone(o.Color("stone"));
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(rng: R, ground: floor, sparks: new[] { F[6], F[5], F[3], F[2] }, onLand: (x, y) => blazes.Add(x, y - 0.25, R.Range(0.9, 1.5), R.Range(0.55, 0.85)), emitter: e);
            blazes = new Blazes(ps, e);
            rocks = new Bits(floor, e);
            lights = new Lights(e);
            Y = -RAD * 0.3;

            flashes.Add(new Flash { r = RAD * 1.05, life = 0.06, max = 0.06 });
            rings.Add(new Shock { r0 = RAD * 0.45, r1 = RAD * 2.7, life = 0.34, max = 0.34, thick = 3, c = F[5] });
            rings.Add(new Shock { r0 = RAD * 0.3, r1 = RAD * 1.6, life = 0.22, max = 0.22, thick = 1.8, c = F[3] });
            lights.Add(0, Y, RAD * 3.4, WARM, 0.6 * POWER, 0.7);
            lights.Add(0, Y, RAD * 1.6, WHITE, 0.6, 0.08);
            for (int i = 0; i < 5; i++) ps.Spawn(P.Fire, R.Range(-2.5, 2.5), Y + R.Range(-2.5, 2.5), R.Range(-20, 20), R.Range(-40, -10), R.Range(0.18, 0.3), 0, R.Range(RAD * 0.4, RAD * 0.55));
            for (int i = 0; i < RoundInt(28 * POWER); i++)
            {
                double a = R.Range(0, TAU), d = R.Range(0, RAD * 0.55), sp = R.Range(40, 210) * POWER;
                ps.Spawn(P.Fire, Math.Cos(a) * d, Y + Math.Sin(a) * d, Math.Cos(a) * sp, Math.Sin(a) * sp * 0.8 - 40, R.Range(0.55, 1.15), 0, R.Range(RAD * 0.22, RAD * 0.46), 0, R.Chance(0.45) ? PF.Smokes | PF.Big : PF.None);
            }
            for (int i = 0; i < 5; i++)
            {
                double a = R.Range(0, TAU), d = R.Range(RAD * 0.1, RAD * 0.5);
                ps.Spawn(P.Smoke, Math.Cos(a) * d, Y + Math.Sin(a) * d, Math.Cos(a) * 30, Math.Sin(a) * 20 - 30, R.Range(0.9, 1.5), Rgb(34, 29, 38), R.Range(RAD * 0.15, RAD * 0.26), R.Range(0.06, 0.2), PF.Big);
            }
            for (int i = 0; i < 5; i++) ps.Spawn(P.Fire, R.Range(-RAD * 0.25, RAD * 0.25), Y, R.Range(-12, 12), R.Range(-180, -100), R.Range(0.8, 1.3), 0, R.Range(RAD * 0.24, RAD * 0.36), R.Range(0, 0.1), R.Chance(0.6) ? PF.Smokes | PF.Big : PF.None);
            for (int i = 0; i < RoundInt(10 * POWER); i++)
            {
                double a = R.Range(0, TAU), d = R.Range(0, RAD * 0.7), sp = R.Range(15, 100);
                ps.Spawn(P.Smoke, Math.Cos(a) * d, Y + Math.Sin(a) * d, Math.Cos(a) * sp, Math.Sin(a) * sp * 0.6 - 35, R.Range(1.8, 2.8), 0, R.Range(RAD * 0.26, RAD * 0.44), R.Range(0.12, 0.45), PF.Big);
            }
            for (int i = 0; i < RoundInt(80 * POWER); i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(140, 380) * POWER;
                ps.Spawn(P.Spark, 0, Y, Math.Cos(a) * sp, Math.Sin(a) * sp - 70, R.Range(0.18, 0.6));
            }
            for (int i = 0; i < RoundInt(40 * POWER); i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(30, 150);
                ps.Spawn(P.Ember, Math.Cos(a) * RAD * 0.4, Y + Math.Sin(a) * RAD * 0.4, Math.Cos(a) * sp, Math.Sin(a) * sp - 75, R.Range(1.2, 2.6), F[3]);
            }
            // The JS loop condition draws from the rng on every test, so this one does too.
            for (int i = 0; i < RoundInt(R.Range(9, 14) * Math.Max(0, POWER - 0.55)); i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.35, 1.35), sp = R.Range(170, 330);
                ps.Spawn(P.Brand, 0, Y, Math.Cos(a) * sp, Math.Sin(a) * sp, R.Range(0.7, 1.3));
            }
            for (int i = 0; i < RoundInt(RAD * 0.5); i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Smoke, s * R.Range(0, RAD * 0.6), -R.Range(1, 5 + RAD * 0.04), s * R.Range(3, 9.5) * RAD, -R.Range(2, 15), R.Range(1.1, 2.0), st.dust, R.Range(4, 7) + RAD / 6, R.Range(0, 0.06), PF.Big);
            }
            for (int i = 0; i < 26 * Math.Min(1.5, S); i++)
                rocks.Add(new Bits.Bit
                {
                    kind = B.Chunk, x = R.Range(-0.4, 0.4) * RAD, y = -3, vx = R.Range(-9, 9) * RAD * 0.25, vy = -R.Range(7, 16) * RAD * 0.9,
                    g = 1, drag = 0.4, bounce = 0.3, life = R.Range(1.8, 2.6), a = R.Range(0, TAU), va = R.Range(-16, 16), s = R.Int(1, 3),
                });
            for (int i = 0; i < RoundInt(EARTH * RAD * 4); i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.5, sp = R.Range(5, 15) * RAD * (1 - 0.45 * Math.Abs(u));
                ps.Spawn(P.Grit, u * 0.25 * RAD, -1, Math.Cos(a) * sp, Math.Sin(a) * sp, R.Range(1.2, 2.2), R.Pick(new[] { st.light, st.body, st.body, st.shade }), R.Chance(0.45) ? 2 : 1);
            }
            for (int i = 0; i < RoundInt(EARTH * 12); i++)
                ps.Spawn(P.Smoke, R.Range(-0.3, 0.3) * RAD, -R.Range(2, RAD * 0.6), R.Range(-15, 15), -R.Range(60, 190) * EARTH * 0.6, R.Range(1.6, 2.6), st.dust, R.Range(5, 9) * Math.Sqrt(S) + RAD / 7, R.Range(0, 0.15), PF.Big);
        }

        void Scorch(Painter p, double rx0, double cool, double fade)
        {
            if (fade <= 0) return;
            double rx = rx0 * Math.Min(1, t / 0.08), ry = 4;
            uint[] heat = (uint[])F.Clone();
            Array.Reverse(heat);
            for (int y = 0; y <= 2; y++)
            for (int x = Floor(-rx); x <= Math.Ceiling(rx); x++)
            {
                double dx = (x + 0.5) / rx, dy = (y + 0.5) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1) continue;
                double k = Rand(x * 31 + y, 3), T = Math.Pow(Clamp(1 - t / cool), 1.4) * (1.25 - d) - k * 0.35;
                if (T > 0.06)
                {
                    uint c = heat[Math.Min(heat.Length - 1, Floor((1 - Math.Min(1, T)) * (heat.Length - 1)))];
                    p.Dot(x, y, c, fade);
                    if ((x & 3) == 0) p.Glow(x + 0.5, y, 6, c, 0.18 * T * fade);
                }
                else p.Dot(x, y, d < 0.5 ? SOOT : SOOT2, (d < 0.95 - k * 0.3 ? 1 : 1 - d) * fade);
            }
        }

        void Rock(Painter p, Bits.Bit b)
        {
            double r = 0.6 + b.s * 0.75, fade = Math.Min(1, b.life / 0.5), lx = Math.Cos(b.a) * r * 0.4, ly = Math.Sin(b.a) * r * 0.4;
            emitter.Shift(p, b.bornX, b.bornY);
            p.Shade(b.x - r - 1, b.y - r - 1, b.x + r + 1, b.y + r + 1, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, d = Hypot(dx * 1.15, dy);
                if (d > r + 0.9) return 0;
                double lit = dx * lx + dy * ly;
                o.c = d > r ? OUTLINE : lit < -0.4 ? st.light : lit > 0.6 ? st.shade : st.body;
                o.a = fade;
                return 0;
            });
            p.Shift(0, 0);
        }

        void Band(Painter p, bool far)
        {
            double k = t / 0.5;
            if (k < 1)
                Sim.Band(p, 0, 0.5, 80 * S * Ease.OutCubic(k), 0.16, Math.Max(1, (2 + RAD / 9) * (1 - k)), 1.15 - k, new[] { st.body, st.light, F[3], F[6] },
                    g => far ? Math.Sin(g) < 0 : Math.Sin(g) >= 0);
        }

        public override void Update(double dt)
        {
            t += dt;
            for (int i = flashes.Count - 1; i >= 0; i--)
                if ((flashes[i].life -= dt) <= 0) flashes.RemoveAt(i);
            for (int i = rings.Count - 1; i >= 0; i--)
                if ((rings[i].life -= dt) <= 0) rings.RemoveAt(i);
            rocks.Update(dt);
            foreach (Bits.Bit b in rocks.List)
                if (b.rest && dusted.Add(b))
                    for (int i = 0; i < 3; i++)
                    {
                        ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(10, 55) * 0.6, -R.Range(2, 22), R.Range(0.3, 0.65), st.dust, R.Range(1.3, 2.8));
                        ImpactsLib.BornWith(ps, b);
                    }
            blazes.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            Scorch(p, 22 * S, 1.2 + RAD / 14, 1 - Smooth(Duration - 1.2, Duration - 0.2, t));
            ps.Draw(p, Layer.Back);
            blazes.DrawBack(p);
            Band(p, true);
        }

        public override void Draw(Painter p)
        {
            Band(p, false);
            foreach (Bits.Bit b in rocks.List) Rock(p, b);
            ps.Draw(p, Layer.Front);
            foreach (Flash f in flashes)
            {
                double k2 = f.life / f.max, r = f.r * (0.8 + 0.2 * k2);
                p.Circle(0, Y, r + 1.2, F[2]);
                p.Circle(0, Y, r, k2 > 0.45 ? F[6] : F[5]);
                p.Circle(0, Y, r * 0.6, k2 > 0.45 ? WHITE : F[6]);
            }
            foreach (Shock r in rings)
            {
                double k2 = r.life / r.max, e = 1 - Math.Pow(k2, 3);
                p.Ring(0, Y, r.r0 + (r.r1 - r.r0) * e, Math.Max(1, r.thick * k2 + 0.4), r.c, Math.Min(1, k2 * 1.6));
            }
            lights.Draw(p);
        }
    }
}
