// The blast recipe (fire puffs that cool into smoke, sparks, embers, burning brands, grit, rocks, scorch, rings) is
// adapted from Sprite Fusion's open-source explosion (https://www.spritefusion.com/pixel-art-vfx/explosion),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class BigExplosion : Effect
    {
        const double Duration = 7.2;
        const int W = 360, H = 300, OX = 180, OY = 262;

        public static readonly Module Module = new Module("big-explosion", "pixelfx/fx/explosions/big-explosion", W, H, OX, OY, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["seed"] = 11,
            },
            (o, e) => new BigExplosion(o, e))
        {
            Title = "Big explosion",
            Desc = "A huge ground blast in three beats: a white dome, a rolling fireball that climbs into a mushrooming smoke column, heavy rocks, and fires left burning.",
            Tags = new[] { "explosion", "fire", "smoke", "debris", "mushroom", "big", "ground", "one-shot" },
            Bg = "ground",
        };

        const double RAD = 30, POWER = 1.45, EARTH = 1.6;
        const double X0 = -OX + 3, X1 = W - OX - 3, Y0 = -OY + 3, Y1 = H - OY - 3;
        // the plume's smoke, lit on top to sooty underneath
        static readonly uint[] SM = Ramp("#83777f", "#6e6470", "#575060", "#3f3947", "#29242f");
        static readonly uint SMOKE0 = Hex("#221d26");

        sealed class Beat
        {
            public double t, x, y, rad, power;
            public bool main;
        }

        // three detonations, a beat apart, climbing as they go
        static readonly Beat[] BLASTS =
        {
            new Beat { t = 0, x = 0, y = -9, rad = RAD, power = POWER, main = true },
            new Beat { t = 0.13, x = -17, y = -30, rad = 17, power = 0.9 },
            new Beat { t = 0.25, x = 15, y = -46, rad = 15, power = 0.8 },
        };

        sealed class Flash
        {
            public double x, y, r, life, max;
        }

        sealed class Shock
        {
            public double x, y, r0, r1, life, max, thick;
            public uint c;
        }

        sealed class RockState
        {
            public double heat, pvy;
            public bool dusted;
            public List<TrailPt> pts;
        }

        readonly uint[] F, FR, HR;
        readonly uint WARM;
        readonly Stone st;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Blazes blazes;
        readonly Bits rocks;
        readonly Lights lights;
        readonly Dictionary<Bits.Bit, RockState> state = new Dictionary<Bits.Bit, RockState>();
        readonly List<Flash> flashes = new List<Flash>();
        readonly List<Shock> rings = new List<Shock>();
        readonly Plume plume;
        readonly Column column;
        double t;
        int next;

        BigExplosion(Options o, Emitter e)
        {
            emitter = e;
            F = Fit(o.Ramp("palette"), 8);
            FR = FireRamp(F);
            HR = HeatRamp(FR);
            st = new Stone(o.Color("stone"));
            R = new Rng(o.Seed);
            WARM = Mix(F[3], F[6], 0.45);
            Func<double, double> floor = x => 0;
            ps = new Particles(rng: R, ground: floor, sparks: new[] { F[6], F[5], F[3], F[2] }, onLand: (x, y) => blazes.Add(x, y - 0.25, R.Range(3, 4.4), R.Range(0.7, 1.25)), emitter: e);
            blazes = new Blazes(ps, e);
            rocks = new Bits(floor, e);
            lights = new Lights(e);
            plume = new Plume(R, new Plume.Opts
            {
                x = 0, y = -34, rise = 118, tau = 1.25, drift = 4, tube = new double[] { 9, 21 }, ring = new double[] { 2, 19 }, grow = 1.0, n = 42, size = new double[] { 6, 11 }, swell = 0.9,
                spin = 2.6, spinTau = 1.7, heat = 1.2, cool = 0.42, under = 0.5, tilt = 0.22, squash = 0.72, fade = new[] { 4.4, 6.4 }, spread = 22,
            });
            column = new Column(R, new Column.Opts { w = 5, foot = 18, speed = 85, size = new double[] { 7, 11 }, n = 28, sway = 2.5 });
        }

        double Top()
        {
            Plume.Shape S = plume.ShapeNow();
            return S.y + S.tube * 0.45;
        }

        void Blast(Beat b)
        {
            double x = b.x, y = b.y, rad = b.rad, power = b.power;
            bool main = b.main;
            // the later beats flash and ring in the air; the first is a dome on the floor (drawn from t)
            if (!main) flashes.Add(new Flash { x = x, y = y, r = rad * 1.05, life = 0.06, max = 0.06 });
            if (!main)
            {
                rings.Add(new Shock { x = x, y = y, r0 = rad * 0.45, r1 = rad * 2.7, life = 0.36, max = 0.36, thick = 3, c = F[5] });
                rings.Add(new Shock { x = x, y = y, r0 = rad * 0.3, r1 = rad * 1.6, life = 0.24, max = 0.24, thick = 1.8, c = F[3] });
            }
            // light: a big warm flare (raised for a ground blast, so it lights the air and the floor gets a pool)
            lights.Add(x, main ? y - 24 : y, main ? 72 : rad * 3.4, WARM, 0.6 * power, 0.8);
            lights.Add(x, main ? y - 12 : y, main ? 44 : rad * 1.6, WHITE, 0.6, 0.1);
            double down = main ? 0.25 : Math.PI; // a ground blast throws nothing into the floor
            for (int i = 0; i < 5; i++) ps.Spawn(P.Fire, x + R.Range(-2.5, 2.5), y + R.Range(-2.5, 2.5), R.Range(-20, 20), R.Range(-40, -10), R.Range(0.18, 0.3), 0, R.Range(rad * 0.4, rad * 0.55));
            for (int i = 0; i < RoundInt(26 * power); i++)
            {
                double a = main ? R.Range(-Math.PI - down, down) : R.Range(0, TAU), d = R.Range(0, rad * 0.55), sp = R.Range(40, 200) * power;
                ps.Spawn(P.Fire, x + Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.8 - 40, R.Range(0.55, 1.15), 0, R.Range(rad * 0.22, rad * 0.46), 0, R.Chance(0.45) ? PF.Smokes | PF.Big : PF.None);
            }
            for (int i = 0; i < 5; i++)
            {
                double a = R.Range(-Math.PI, 0), d = R.Range(rad * 0.1, rad * 0.5);
                ps.Spawn(P.Smoke, x + Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * 30, Px.Sin(a) * 20 - 30, R.Range(0.9, 1.5), SMOKE0, R.Range(rad * 0.15, rad * 0.26), R.Range(0.06, 0.2), PF.Big);
            }
            for (int i = 0; i < RoundInt(8 * power); i++)
            {
                double a = R.Range(-Math.PI, 0), d = R.Range(0, rad * 0.7), sp = R.Range(15, 90);
                ps.Spawn(P.Smoke, x + Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.6 - 35, R.Range(1.8, 2.8), 0, R.Range(rad * 0.26, rad * 0.4), R.Range(0.12, 0.45), PF.Big);
            }
            for (int i = 0; i < RoundInt(80 * power); i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(170, main ? 470 : 380);
                ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 70, R.Range(0.2, 0.65));
            }
            for (int i = 0; i < RoundInt(30 * power); i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(30, 150);
                ps.Spawn(P.Ember, x + Px.Cos(a) * rad * 0.4, y + Px.Sin(a) * rad * 0.4, Px.Cos(a) * sp, Px.Sin(a) * sp - 75, R.Range(1.4, 3), F[3]);
            }
            if (!main) return;
            // burning brands that land and keep the ground on fire
            for (int i = 0; i < 14; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.3, 1.3), sp = R.Range(160, 340);
                ps.Spawn(P.Brand, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.8, 1.5));
            }
            // the ground's part: dust rolling out along the floor, a dust column, grit and heavy rocks thrown up
            for (int i = 0; i < 18; i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Smoke, s * R.Range(0, RAD * 0.6), -R.Range(1, 6), s * R.Range(3, 9) * RAD, -R.Range(2, 15), R.Range(1.3, 2.2), st.dust, R.Range(8, 12), R.Range(0, 0.06), PF.Big);
            }
            for (int i = 0; i < RoundInt(EARTH * 12); i++)
                ps.Spawn(P.Smoke, R.Range(-0.3, 0.3) * RAD, -R.Range(2, RAD * 0.6), R.Range(-15, 15), -R.Range(60, 190) * EARTH * 0.6, R.Range(1.6, 2.6), st.dust, R.Range(8, 12), R.Range(0, 0.15), PF.Big);
            for (int i = 0; i < RoundInt(EARTH * RAD * 4); i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.55, sp = R.Range(5, 15) * RAD * (1 - 0.45 * Math.Abs(u));
                ps.Spawn(P.Grit, u * 0.25 * RAD, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.4, 2.4), R.Pick(new[] { st.light, st.body, st.body, st.shade }), R.Chance(0.45) ? 2 : 1);
            }
            for (int i = 0; i < 26; i++)
            {
                bool big = i < 10, hot = R.Chance(0.45);
                int sx = R.Sign();
                var bit = new Bits.Bit
                {
                    kind = B.Chunk, x = R.Range(-0.4, 0.4) * RAD, y = -4, vx = sx * R.Range(30, big ? 120 : 150), vy = -R.Range(big ? 280 : 320, big ? 480 : 560), g = 1, drag = 0.3,
                    bounce = 0.3, life = R.Range(3.6, 5), a = R.Range(0, TAU), va = R.Range(-14, 14), s = big ? R.Range(2.6, 3.8) : R.Range(1.2, 2.4),
                };
                state[bit] = new RockState { heat = hot ? R.Range(0.6, 1) : 0 };
                rocks.Add(bit);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            while (next < BLASTS.Length && t >= BLASTS[next].t) Blast(BLASTS[next++]);
            if (t > 0.08) plume.Update(dt);
            Plume.Shape S = plume.ShapeNow();
            double k = 1 - Smooth(2.2, 3.1, t);
            // the column: smoke and fire boil up off the floor into the fireball, dust is drawn in along the ground
            if (t > 0.2 && t < 3.1)
            {
                if (R.Chance(8 * k * dt)) ps.Spawn(P.Smoke, R.Range(-10, 10), -R.Range(1, 6), R.Range(-8, 8), -R.Range(40, 80), R.Range(1.5, 2.3), st.dust, R.Range(5, 8), 0, PF.Big);
                if (t < 1.5 && R.Chance(16 * dt)) ps.Spawn(P.Fire, R.Range(-6, 6), -R.Range(4, 14), R.Range(-10, 10), -R.Range(90, 150), R.Range(0.5, 0.8), 0, R.Range(4, 7.5), 0, R.Chance(0.5) ? PF.Smokes | PF.Big : PF.None);
                if (t > 0.45 && R.Chance(12 * k * dt))
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(40, 120), -R.Range(1, 4), -s * R.Range(10, 30), -R.Range(0, 5), R.Range(1.3, 1.9), st.dust, R.Range(3.5, 5.5), 0, PF.Big);
                }
            }
            // the fireball sheds embers, then ash drifts down out of the cloud
            if (t > 0.3 && t < 3 && R.Chance(10 * plume.Hot() * dt))
            {
                double a = R.Range(0, TAU);
                ps.Spawn(P.Ember, S.x + Px.Cos(a) * (S.ring + S.tube), S.y + Px.Sin(a) * S.tube * 0.5, Px.Cos(a) * R.Range(40, 90), -R.Range(10, 60), R.Range(1.2, 2.4), F[3]);
            }
            if (t > 1.4 && t < 4.6 && R.Chance(7 * dt))
                ps.Spawn(P.Flake, S.x + R.Range(-1, 1) * (S.ring + S.tube), S.y + S.tube * 0.6, R.Range(-6, 6), R.Range(0, 10), R.Range(2, 3), R.Pick(new[] { SM[1], SM[2], F[2] }), R.Range(0.6, 1.2));
            column.Update(dt, Top(), t > 0.22 && t < 2.5 ? 1 : 0);
            Draft(ps, dt, x: 0, top: S.y + S.tube * 0.4, w: 9 + 7 * Math.Min(1, t), up: 170, pull: 130, low: 12, reach: 130, k: 1 - Smooth(2.5, 3.8, t));
            for (int i = flashes.Count - 1; i >= 0; i--)
                if ((flashes[i].life -= dt) <= 0) flashes.RemoveAt(i);
            for (int i = rings.Count - 1; i >= 0; i--)
                if ((rings[i].life -= dt) <= 0) rings.RemoveAt(i);
            // rocks: molten ones cool and trail smoke and flame; every hard landing kicks up dust
            foreach (Bits.Bit b in rocks.List) state[b].pvy = b.vy;
            rocks.Update(dt);
            for (int i = rocks.List.Count - 1; i >= 0; i--)
            {
                Bits.Bit b = rocks.List[i];
                RockState q = state[b];
                if (b.x < X0 + 4 || b.x > X1 - 4)
                {
                    rocks.List.RemoveAt(i);
                    continue;
                }
                if (q.heat > 0)
                {
                    q.heat = Math.Max(0, q.heat - dt * 0.3);
                    if (!b.rest && q.heat > 0.25)
                    {
                        (q.pts ??= new List<TrailPt>()).Add(new TrailPt { x = b.x, y = b.y, t = t });
                        if (R.Chance(14 * dt))
                        {
                            ps.Spawn(P.Fire, b.x, b.y, b.vx * 0.1, b.vy * 0.1 - 8, R.Range(0.16, 0.3), 0, R.Range(1.2, 2.2));
                            BornWith(ps, b);
                        }
                    }
                    else if (b.rest && R.Chance(3 * q.heat * dt))
                    {
                        ps.Spawn(P.Smoke, b.x, b.y - 1, R.Range(-3, 3), -R.Range(5, 12), R.Range(0.8, 1.4), 0, R.Range(1.6, 2.6));
                        BornWith(ps, b);
                    }
                }
                bool landed = (q.pvy > 0 && b.vy <= 0) || (b.rest && !q.dusted);
                if (landed && q.pvy > 60)
                {
                    for (int j = 0; j < 2 + Round(b.s); j++)
                    {
                        ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(10, 50), -R.Range(2, 20), R.Range(0.3, 0.7), st.dust, R.Range(1.3, 1 + b.s));
                        BornWith(ps, b);
                    }
                    if (q.heat > 0.5 && !q.dusted) blazes.Add(b.x, -0.25, R.Range(1.2, 2.2), 0.45);
                }
                if (b.rest) q.dusted = true;
                if (q.pts != null)
                    while (q.pts.Count > 0 && t - q.pts[0].t > 0.45) q.pts.RemoveAt(0);
            }
            blazes.Update(dt);
            ps.Update(dt);
            Cull(ps, X0, Y0, X1, Y1);
            lights.Update(dt);
        }

        void Band(Painter p, bool far)
        {
            double k = t / 0.6;
            if (k < 1) Sim.Band(p, 0, 0.5, 132 * Ease.OutCubic(k), 0.16, Math.Max(1, 6 * (1 - k)), 1.15 - k, new[] { st.body, st.light, F[3], F[6] }, g => far ? Px.Sin(g) < 0 : Px.Sin(g) >= 0);
        }

        public override void DrawBack(Painter p)
        {
            Scorch(p, HR, t, 52, 3.4, 1 - Smooth(Duration - 1.6, Duration - 0.3, t), 3);
            Band(p, true);
            DrawPs(p, ps, Layer.Back, FR, emitter);
            column.Draw(p, Top(), FR, SM, 1 - t / 1.4, plume.Hot());
            plume.Draw(p, FR, SM);
            blazes.DrawBack(p);
        }

        public override void Draw(Painter p)
        {
            Band(p, false);
            // the first beat: a white hero frame, a dome of white-hot air that cools into the fire, a flare, a shell of light
            if (t < 0.04)
            {
                Dome(p, F, 0, 44, 1);
                p.Glow(0, -30, 68, WHITE, 0.6);
            }
            else if (t < 0.11) Dome(p, F, 0, 38 - (t - 0.04) * 120, 1 - (t - 0.04) / 0.07);
            foreach (Bits.Bit b in rocks.List)
            {
                RockState q = state[b];
                if (q.pts != null)
                {
                    Sim.Shift(p, emitter, b.bornX, b.bornY);
                    Trail(p, q.pts, t, FR, SM, 0.45, 0.6 + b.s * 0.3, q.heat);
                    p.Shift(0, 0);
                }
                Rock(p, emitter, b, st, FR, q.heat);
            }
            DrawPs(p, ps, Layer.Front, FR, emitter);
            if (t < 0.24) Sim.Flare(p, 0, -14, 70 * Px.Pow(1 - t / 0.24, 1.4), 1 - t / 0.24, F);
            if (t < 0.5)
            {
                double u = t / 0.5, r = 34 + 80 * Ease.OutCubic(u);
                p.Ring(0, 0, r, Math.Max(1, 3 * (1 - u) + 0.5), u < 0.3 ? F[6] : u < 0.6 ? F[5] : F[3], (v, a) => Px.Sin(a) > 0 ? 0 : Math.Min(1, (1 - u) * 1.6));
                GlowDome(p, 0, 0, r, 6, F[4], 0.35 * (1 - u));
            }
            foreach (Flash f in flashes)
            {
                double k2 = f.life / f.max, r = f.r * (0.8 + 0.2 * k2);
                p.Circle(f.x, f.y, r + 1.2, F[2]);
                p.Circle(f.x, f.y, r, k2 > 0.45 ? F[6] : F[5]);
                p.Circle(f.x, f.y, r * 0.6, k2 > 0.45 ? WHITE : F[6]);
            }
            foreach (Shock r in rings)
            {
                double k2 = r.life / r.max, e = 1 - Px.Pow(k2, 3);
                p.Ring(r.x, r.y, r.r0 + (r.r1 - r.r0) * e, Math.Max(1, r.thick * k2 + 0.4), r.c, Math.Min(1, k2 * 1.6));
            }
            // the fireball lights the floor under it while it burns
            double hot = plume.Hot(), flash = Math.Max(0, 1 - t / 0.9);
            Pool(p, 0, 1, 150, 22, F[3], 0.24 * hot + 0.5 * flash * flash);
            lights.Draw(p);
        }
    }
}
