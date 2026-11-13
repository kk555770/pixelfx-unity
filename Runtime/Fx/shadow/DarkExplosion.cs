// The pull that swirls motes into the core and the collapse burst are adapted from Sprite Fusion's open-source
// black-hole (https://www.spritefusion.com/pixel-art-vfx/black-hole), the sparks, rings and smoke from his explosion,
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class DarkExplosion : Effect
    {
        public static readonly Module Module = new Module("dark-explosion", "pixelfx/fx/shadow/dark-explosion", 280, 248, 140, 206, 3.8, false, 30,
            new Options { ["palette"] = VOID, ["seed"] = 6 },
            (o, e) => new DarkExplosion(o, e))
        {
            Title = "Dark explosion",
            Desc = "Motes swirl into a pulsing core, a black star cuts across a violet-white flash, and a black sphere burns at the rim, throws lit shards and smoke, then collapses.",
            Tags = new[] { "shadow", "explosion", "void", "dark", "magic", "impact", "one-shot" },
            Bg = "ground",
        };

        // the heart of the blast hangs above the floor; the sphere's radius; the beats of the choreography
        const double Y = -54, RS = 16, REL = 0.52, BURN = 1.62, POP = 1.84;
        static readonly uint SMOKE = Hex("#5d5468");

        // a black sphere: a void body (a faint crescent of reflected light low on the right, a dull glint high on the
        // left), a white rim, and pointed tongues of violet flame that lean up, flicker and crawl round it
        static void Sphere(Painter p, uint[] C, double r, double t, double heat)
        {
            if (r < 0.6) return;
            double E = r + 12 * heat + 2;
            p.Shade(-E, Y - E, E, Y + E, (x, y, o) =>
            {
                double dx = x, dy = y - Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > E) return 0;
                if (d < r - 0.5)
                {
                    double e0 = r - d, ang0 = Atan2(dy, dx);
                    if (e0 < 1.4 && Px.Cos(ang0 - 0.8) > 0.55) return C[2];
                    if (r > 8 && Px.Pow(dx + r * 0.45, 2) + Px.Pow(dy + r * 0.45, 2) < 1.6) return C[3];
                    return C[0];
                }
                double e = d - (r - 0.5);
                if (e < 1)
                {
                    o.c = Px.Sin(Atan2(dy, dx) * 7 + t * 9) > -0.6 ? WHITE : C[7];
                    o.k = 0.4;
                    return 0;
                }
                // the further out, the lower the angle it's sampled at: tongues bend upward like flame
                double ang = Atan2(dy + e * 0.6, dx), top = Math.Max(0, -Px.Sin(ang));
                double a1 = ang - t * 0.9, a2 = ang + t * 1.7;
                double i1 = Round((a1 * 9) / TAU), i2 = Round((a2 * 15) / TAU);
                double T1 = Px.Pow(0.5 + 0.5 * Px.Cos(a1 * 9), 3) * (0.55 + 0.45 * Noise(t * 7 + i1 * 3.7, 1));
                double T2 = Px.Pow(0.5 + 0.5 * Px.Cos(a2 * 15), 4) * (0.5 + 0.5 * Noise(t * 11 + i2 * 5.1, 2));
                double h = heat * (0.3 + 0.95 * top) * (1.5 + 12 * Math.Max(0, T1) + 6 * Math.Max(0, T2));
                if (e > h + 1) return 0;
                double u = (e - 1) / h;
                o.c = u < 0.18 ? WHITE : u < 0.4 ? C[7] : u < 0.62 ? C[6] : u < 0.84 ? C[5] : C[4];
                o.a = u < 0.8 ? 1 : (1 - u) * 5;
                if (u < 0.6 && (((int)Math.Floor(x) + (int)Math.Floor(y)) & 1) == 0) o.k = 0.25 * (1 - u);
                return 0;
            });
        }

        sealed class Ring
        {
            public double r0, r1, life, max, th, delay;
            public bool hot;

            public Ring(double r0, double r1, double life, double th, bool hot, double delay)
            {
                this.r0 = r0;
                this.r1 = r1;
                this.life = life;
                max = life;
                this.th = th;
                this.hot = hot;
                this.delay = delay;
            }
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps, embers;
        readonly Puffs puffs;
        readonly Bits bits;
        readonly List<Lamp> lights = new List<Lamp>();
        readonly List<Ring> rings = new List<Ring>();
        readonly HashSet<Bits.Bit> flat = new HashSet<Bits.Bit>(), wisp = new HashSet<Bits.Bit>();
        double t, acc, dustAcc, flick = 1;
        bool released, collapsing, popped;

        static double CoreR(double tt) => tt < REL - 0.07 ? 1.5 + 5 * Ease.InQuad(tt / REL) : 1;

        static double SphereR(double tt)
        {
            if (tt < REL) return 0;
            if (tt < BURN) return RS * Ease.OutBack(Clamp((tt - REL) / 0.14), 2.4) * (1 + 0.06 * Smooth(REL + 0.3, BURN, tt) + 0.02 * Px.Sin(tt * 11));
            if (tt < POP) return RS * 1.08 * (1 - Ease.InQuad((tt - BURN) / (POP - BURN)));
            return 0;
        }

        static double StarL(double tt) => tt < REL || tt > REL + 0.26 ? 0 : 60 * (1 - Ease.InQuad(Clamp((tt - REL - 0.05) / 0.21)));
        static double StarRot(double tt) => 0.32 + (tt - REL) * 2.2;

        // how hard the sphere lights what's round it: the rims of the smoke, the edges of the shards
        static double Burning(double tt) => tt < REL ? Clamp(tt / REL) * 0.7 : tt < BURN ? 1 : tt < POP ? 1.2 : Math.Max(0, 1 - (tt - POP) / 0.35);

        static (double x, double y) Rim(double a, double r) => (Px.Cos(a) * r, Y + Px.Sin(a) * r);

        DarkExplosion(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { WHITE, C[7], C[6], C[5] }, emitter: e);
            embers = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, emitter: e);
            puffs = new Puffs(R,
                new[] { WHITE, C[7], C[6], C[5], C[5], C[4], C[4], C[3], C[2] },
                new[] { Mix(Mix(C[3], C[1], 0.3), SMOKE, 0.45), Mix(C[1], SMOKE, 0.3) },
                Mix(Mix(C[4], C[2], 0.5), SMOKE, 0.4),
                new[] { C[6], C[5], C[4] },
                floor, e);
            bits = new Bits(floor, e);
        }

        void Light(double x, double y, double r, uint c, double k, double life) => lights.Add(new Lamp(x, y, r, c, k, life));

        void Release()
        {
            Light(0, Y, 100, C[4], 1.25, 0.85);
            Light(0, Y, 86, Mix(C[6], WHITE, 0.5), 1.3, 0.22);
            rings.Add(new Ring(RS, 104, 0.46, 3.6, true, 0));
            rings.Add(new Ring(RS * 0.8, 62, 0.3, 1.6, true, 0.02));
            rings.Add(new Ring(RS, 124, 0.62, 1.2, false, 0.07));
            // shards of the void: big slivers tumbling out and raining onto the floor, fine splinters faster
            for (int i = 0; i < 34; i++)
            {
                bool big = i < 20;
                double a = R.Range(0, TAU);
                var (x, y) = Rim(a, RS * 0.7);
                double sp = big ? R.Range(80, 170) : R.Range(130, 220);
                bits.Add(new Bits.Bit { kind = B.Shard, x = x, y = y, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - R.Range(40, 110), g = 0.75, drag = big ? 1.3 : 1.7, bounce = 0.35, life = R.Range(1.6, 2.6), a = R.Range(0, TAU), va = R.Range(-16, 16), s = big ? R.Range(6, 11) : R.Range(3, 4.5) });
            }
            for (int i = 0; i < 84; i++)
            {
                double a = R.Range(0, TAU);
                var (x, y) = Rim(a, RS * 0.8);
                double sp = R.Range(120, 235);
                ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 50, R.Range(0.2, 0.7));
            }
            for (int i = 0; i < 28; i++)
            {
                double a = R.Range(0, TAU);
                var (x, y) = Rim(a, RS * 0.9);
                double sp = R.Range(30, 130);
                embers.Spawn(P.Ember, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(1.3, 2.5));
            }
            for (int i = 0; i < 24; i++)
            {
                double a = R.Range(0, TAU);
                var (x, y) = Rim(a, RS * 0.9);
                double sp = R.Range(15, 70);
                puffs.Spawn(PK.Soot, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.8 - 14, R.Range(2, 2.8), R.Range(7, 12), R.Range(0.02, 0.3));
            }
            // the shock flattens on the floor and shoves smoke out along it
            for (int i = 0; i < 12; i++)
            {
                int s = i % 2 != 0 ? 1 : -1;
                puffs.Spawn(PK.Soot, s * R.Range(10, 40), -R.Range(2, 5), s * R.Range(50, 120), -R.Range(0, 10), R.Range(1.6, 2.4), R.Range(5, 9), R.Range(0.08, 0.2));
            }
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.6, 1.6);
                var (x, y) = Rim(a, RS);
                double sp = R.Range(30, 80);
                puffs.Spawn(PK.Flame, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.3, 0.6), R.Range(2, 3.6), 0, R.Chance(0.5));
            }
            // what was still falling in is swallowed by the blast
            ps.List.Clear();
            puffs.List.RemoveAll(q => !(q.delay > 0));
        }

        void Pop()
        {
            Light(0, Y, 56, WHITE, 0.9, 0.18);
            Light(0, Y, 120, C[5], 0.8, 0.5);
            rings.Add(new Ring(2, 40, 0.28, 2, true, 0));
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(120, 250);
                ps.Spawn(P.Spark, 0, Y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.2, 0.6));
            }
            for (int i = 0; i < 14; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(20, 90);
                embers.Spawn(P.Ember, 0, Y, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.9, 1.6));
            }
            for (int i = 0; i < 10; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(20, 70);
                puffs.Spawn(PK.Soot, Px.Cos(a) * 3, Y + Px.Sin(a) * 3, Px.Cos(a) * sp, Px.Sin(a) * sp - 15, R.Range(1.1, 1.7), R.Range(3, 6), R.Range(0, 0.08));
            }
            foreach (var q in embers.List)
            {
                double vx = q.vx, vy = q.vy;
                Shove(q.x, q.y, ref vx, ref vy);
                q.vx = vx;
                q.vy = vy;
            }
            foreach (var q in puffs.List)
            {
                double vx = q.vx, vy = q.vy;
                Shove(q.x, q.y, ref vx, ref vy);
                q.vx = vx;
                q.vy = vy;
            }
            // what's left of it climbs away as a column of smoke
            for (int i = 0; i < 12; i++)
                puffs.Spawn(PK.Soot, R.Range(-6, 6), Y + R.Range(-4, 6), R.Range(-10, 10), -R.Range(20, 55), R.Range(1.3, 1.8), R.Range(7, 12), R.Range(0.02, 0.2));
        }

        // the pop's shove on whatever is near the heart
        static void Shove(double x, double y, ref double vx, ref double vy)
        {
            double dx = x, dy = y - Y, d = Hypot(dx, dy);
            if (d == 0 || double.IsNaN(d)) d = 1;
            double f = 90 * Math.Max(0, 1 - d / 70);
            vx += (dx / d) * f;
            vy += (dy / d) * f - f * 0.3;
        }

        public override void Update(double dt)
        {
            t += dt;
            flick = 0.88 + 0.12 * Px.Sin(t * 37) * Px.Sin(t * 23);
            // the charge: motes swirl in from all round, streaks shoot straight at the core, darkness gathers
            if (t < REL - 0.06)
            {
                double k = t / REL;
                for (acc += dt * (90 + 150 * k); acc > 1; acc--)
                {
                    double a = R.Range(0, TAU), D = R.Range(36, 84), x = Px.Cos(a) * D, y = Math.Min(-3, Y + Px.Sin(a) * D), vt = R.Range(50, 110), vi = R.Range(10, 40);
                    ps.Spawn(P.Mote, x, y, -Px.Sin(a) * vt - Px.Cos(a) * vi, Px.Cos(a) * vt - Px.Sin(a) * vi, 0.9, R.Pick(new[] { WHITE, C[6], C[6], C[5] }), R.Range(0.7, 1.2));
                }
                if (R.Chance(0.75))
                {
                    double a = R.Range(0, TAU), D = R.Range(28, 70), T = R.Range(0.18, 0.28), v = (D * 3) / (Exp(3 * T) - 1);
                    ps.Spawn(P.Streak, Px.Cos(a) * D, Y + Px.Sin(a) * D, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? C[7] : C[6]);
                }
                if (t < REL - 0.2 && R.Chance(0.16))
                {
                    double a = R.Range(0, TAU), D = R.Range(34, 60);
                    puffs.Spawn(PK.Soot, Px.Cos(a) * D, Y + Px.Sin(a) * D, -Px.Sin(a) * 40, Px.Cos(a) * 40, R.Range(0.6, 0.9), R.Range(3, 6));
                }
                Attract(ps.List, 0, Y, 2.2e6 * (0.3 + k), 110, 0.55, 2.5, dt, q => q.k == P.Mote ? 1 : 0, 420);
                Attract(puffs.List, 0, Y, 1.2e6 * (0.3 + k), 90, 0.7, 4, dt, q => 1, 160);
            }
            if (t >= REL && !released)
            {
                released = true;
                Release();
            }
            // the sphere burns: flame licks off its rim, the odd spark and ember
            if (released && t < BURN)
            {
                double sr = SphereR(t);
                for (acc += dt * 34; acc > 1; acc--)
                {
                    double a = -Math.PI / 2 + R.Gauss() * 1.1;
                    var (x, y) = Rim(a, sr + 1);
                    double sp = R.Range(10, 35);
                    puffs.Spawn(PK.Flame, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - R.Range(10, 30), R.Range(0.25, 0.45), R.Range(1.2, 2.2), 0, R.Chance(0.2));
                }
                if (R.Chance(dt * 10))
                {
                    double a = -Math.PI / 2 + R.Range(-1.4, 1.4);
                    var (x, y) = Rim(a, sr * 0.9);
                    puffs.Spawn(PK.Soot, x, y, Px.Cos(a) * 20, -R.Range(15, 35), R.Range(1.2, 1.7), R.Range(5, 9));
                }
                if (R.Chance(dt * 8))
                {
                    double a = R.Range(0, TAU);
                    var (x, y) = Rim(a, sr);
                    double sp = R.Range(110, 200);
                    ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.2, 0.5));
                }
                if (R.Chance(dt * 7))
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2);
                    var (x, y) = Rim(a, sr);
                    embers.Spawn(P.Ember, x, y, Px.Cos(a) * 30, -R.Range(20, 50), R.Range(0.9, 1.6));
                }
            }
            // the collapse: the sphere draws its own flame back in, streaks race to the heart
            if (t >= BURN && !collapsing)
            {
                collapsing = true;
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(0, TAU), D = R.Range(26, 48), T = R.Range(0.14, 0.2), v = (D * 3) / (Exp(3 * T) - 1);
                    ps.Spawn(P.Streak, Px.Cos(a) * D, Y + Px.Sin(a) * D, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? WHITE : C[6]);
                }
            }
            if (collapsing && !popped) Attract(puffs.List, 0, Y, 1.4e6, 64, 0.6, 3, dt, q => q.k == PK.Flame ? 1 : 0.35, 200);
            if (t >= POP && !popped)
            {
                popped = true;
                Pop();
            }
            // the shock reaches the floor: dust rolls out under the ring
            foreach (Ring r in rings)
            {
                if (r.delay > 0 || !r.hot || r.th < 3) continue;
                double rr = r.r0 + (r.r1 - r.r0) * (1 - Px.Pow(r.life / r.max, 3)), rf = rr > -Y ? Math.Sqrt(rr * rr - Y * Y) : 0;
                if (rf > 0)
                    for (dustAcc += dt * 60; dustAcc > 1; dustAcc--)
                    {
                        int s = R.Sign();
                        ps.Spawn(P.Dust, s * rf + R.Range(-2, 2), -R.Range(1, 3), s * R.Range(20, 50), -R.Range(4, 14), R.Range(0.4, 0.8), Mix(C[2], C[3], R.Next()), R.Range(1.5, 3));
                    }
            }
            for (int i = lights.Count - 1; i >= 0; i--)
                if (!lights[i].Tick(dt)) lights.RemoveAt(i);
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                Ring q = rings[i];
                if (q.delay > 0) q.delay -= dt;
                else if ((q.life -= dt) <= 0) rings.RemoveAt(i);
            }
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
            {
                // a shard that comes to rest lies flat on the floor
                if (b.rest && flat.Add(b))
                {
                    b.a = Round(b.a / Math.PI) * Math.PI + R.Range(-0.2, 0.2);
                    b.y = -1;
                }
                // and once it has lain a while, it dissolves into a wisp
                if (b.life < 0.45 && wisp.Add(b))
                    puffs.Spawn(PK.Soot, b.x, b.y - 1, R.Range(-4, 4), -R.Range(8, 16), R.Range(0.6, 0.9), R.Range(1.4, 2.4));
            }
            puffs.Update(dt);
            ps.Update(dt);
            embers.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double k = Burning(t);
            foreach (Ring r in rings) FloorRing(p, C, r, true);
            puffs.Draw(p, PK.Soot, new PuffLight(0, Y, k * flick, 84));
            ps.Draw(p, Layer.Back);
            if (k > 0) p.Glow(0, -3, 74, C[4], 0.24 * Math.Min(1, k) * flick);
        }

        public override void Draw(Painter p)
        {
            double sr = SphereR(t), k = Burning(t), L = StarL(t);
            foreach (Ring r in rings) FloorRing(p, C, r, false);
            // the charge: a contracting ring and a black core that beats
            double cr = 0;
            if (t < REL)
            {
                double q = t / REL, ring = 3 + 74 * (1 - Ease.InQuad(q));
                double beat = 1 + 0.3 * Exp(-Px.Pow(t - REL * 0.5, 2) / 0.0015) + 0.4 * Exp(-Px.Pow(t - REL * 0.8, 2) / 0.0015);
                cr = CoreR(t) * beat;
                RingFast(p, 0, Y, ring, 1, q > 0.7 ? C[6] : C[5], 0.2 + 0.7 * q);
                if (t > REL - 0.07) p.Circle(0, Y, 1.6, WHITE);
                else Sphere(p, C, cr, t, 0.3 + 0.3 * q);
                Halo(p, 0, Y, cr, cr + 14 + 30 * q, C[5], (0.45 + 0.7 * q) * beat);
                p.Glow(0, Y, 60 + 30 * q, C[4], 0.12 + 0.3 * q);
            }
            // the release: a violet-white flash with a black star cut across it
            double fu = (t - REL) / 0.12;
            if (fu >= 0 && fu < 1)
            {
                double r = 36 * (1 - 0.3 * fu);
                p.Circle(0, Y, r + 1.5, C[5], 1.2 - fu);
                p.Circle(0, Y, r, fu < 0.35 ? WHITE : C[6], 1.1 - fu);
                if (fu < 0.6) p.Circle(0, Y, r * 0.62, WHITE);
            }
            // the shock rings in the air
            foreach (Ring r in rings)
            {
                if (r.delay > 0) continue;
                double q = r.life / r.max, rr = r.r0 + (r.r1 - r.r0) * (1 - q * q * q), th = Math.Max(1, r.th * q + 0.4);
                // the floor stops it: below the floor line it's the flat ring on the floor instead
                RingFast(p, 0, Y, rr, th, (v, g) => r.hot ? (v > 0.6 ? WHITE : v > 0.3 ? C[6] : C[5]) : v > 0.5 ? C[5] : C[4], (v, g) => Y + Px.Sin(g) * rr > 0.5 ? 0 : Math.Min(1, q * 1.6));
                if (r.hot) RingLight(p, 0, Y, rr, 4, C[5], 0.4 * q, (x, y) => y < 0.5);
            }
            Sphere(p, C, sr, t, t < BURN ? 1 : 1 + 0.5 * Clamp((t - BURN) / (POP - BURN)));
            // the black star, burning at the edge where its arms leave the flash
            BlackStar star = L > 0 ? new BlackStar(Y, L, StarRot(t), 0.5, 0.4) : null;
            if (star != null)
            {
                double age = t - REL, fr = age < 0.12 ? 36 * (1 - 0.3 * (age / 0.12)) + 1.5 : 0, cut = Math.Max(fr, sr);
                star.Draw(p, C[0], age < 0.1 ? C[5] : C[4], (x, y) => x * x + Px.Pow(y - Y, 2) < cut * cut);
            }
            puffs.Draw(p, PK.Flame);
            foreach (Bits.Bit b in bits.List) Shard(p, b, C, 0, Y, Math.Min(1, k) * 0.9);
            ps.Draw(p, Layer.Front);
            embers.Draw(p, Layer.Front);
            // the pop as it goes: a glint and a last hard light
            double pu = (t - POP) / 0.2;
            if (pu >= 0 && pu < 1) Sim.Sparkle(p, 0, Y, 12 * (1 - pu), C[6], C[5], 0.7 * (1 - pu));
            // light: the blast's flare and the sphere's corona; then black takes its light back
            foreach (Lamp l in lights)
            {
                double q = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.55 + 0.45 * q), l.c, l.k * q * q);
            }
            if (sr > 1)
            {
                Halo(p, 0, Y, sr - 0.5, sr + 16, C[6], 0.5 * flick * Math.Min(1, k));
                p.Glow(0, Y, sr + 54, C[4], 0.5 * flick * Math.Min(1, k));
            }
            // black takes its light back: the sphere and the core, then the star's own cells
            double B0 = Math.Max(sr, cr) + 1;
            if (B0 > 2) Unlight(p, -B0, Y - B0, B0, Y + B0, (x, y) => x * x + Px.Pow(y - Y, 2) < Px.Pow(Math.Max(sr, cr) - 0.5, 2));
            if (star != null) star.Unlight(p);
        }

        // where the shock meets the floor: its cross-section with the floor plane, a flat ring racing out
        static void FloorRing(Painter p, uint[] C, Ring r, bool back)
        {
            if (r.delay > 0 || !r.hot || r.th < 3) return;
            double q = r.life / r.max, rr = r.r0 + (r.r1 - r.r0) * (1 - q * q * q);
            if (rr <= -Y) return;
            double rf = Math.Sqrt(rr * rr - Y * Y);
            Sim.Band(p, 0, 0.5, rf, 0.16, Math.Max(1, 3 * q), Math.Min(1, q * 1.5), new[] { C[3], C[4], C[5], C[6] }, g => (Px.Sin(g) < 0) == back);
            if (!back)
                foreach (int s in new[] { -1, 1 }) p.Glow(s * rf, -2, 16, C[5], 0.4 * q);
        }
    }
}
