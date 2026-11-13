// The streaks pulled in, the rings and lights at the burst and the collapse pop are adapted from Sprite
// Fusion's open-source black-hole (https://www.spritefusion.com/pixel-art-vfx/black-hole),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class SpawnPortal : Effect
    {
        public static readonly Module Module = new Module("spawn-portal", "pixelfx/fx/spawn/spawn-portal", 208, 150, 104, 122, 3.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#12081f", "#2a0f4d", "#481b8c", "#7330d0", "#a35cff", "#cf9bff", "#f1dcff", "#ffffff" },
                ["width"] = 28,
                ["seed"] = 6,
            },
            (o, e) => new SpawnPortal(o, e))
        {
            Title = "Spawn portal",
            Desc = "A rift tears open on the floor and swirls, belches violet smoke and sparks, spits a fountain of wisps up out of itself, then twists shut with a pop.",
            Tags = new[] { "spawn", "portal", "summon", "rift", "arcane", "smoke", "ground", "one-shot" },
            Bg = "ground",
        };

        const double OPEN = 0.3, BURST = 1.0, SHUT = 1.9, POP = 2.22, SY = 0.34;
        static readonly double[] BELCH = { 0.52, 0.76 };
        static readonly uint VOID = Hex("#06030b"), FUME = Hex("#3a3046"), DUST = Hex("#5a5070");
        static readonly uint[] STONE = Ramp("#1a1424", "#2b2639", "#4a4263", "#6c6488", "#9a91b4");

        // the rift's size over its life: a tear that snaps open past round, breathes, swells as it shuts, then collapses
        static double Radius(double t, double RX)
        {
            if (t < OPEN) return RX * Ease.OutBack(Clamp(t / OPEN), 2.2);
            if (t < SHUT) return RX * (1 + 0.035 * Px.Sin(t * 9) + 0.1 * Exp(-Px.Pow(t - BURST - 0.05, 2) / 0.006));
            double k = Clamp((t - SHUT) / (POP - SHUT));
            return RX * (k < 0.35 ? 1 + 0.1 * Px.Sin((k / 0.35) * Math.PI * 0.5) : 1.1 * (1 - Px.Pow((k - 0.35) / 0.65, 1.6)));
        }

        // how far the swirl has turned: lazy, then whipping round as it shuts
        static double SpinAt(double t) => t * 2.4 + 1.5 * Smooth(BURST - 0.1, BURST + 0.3, t) + 10 * Px.Pow(Clamp((t - SHUT) / (POP - SHUT)), 2.2);
        static double LipAt(double th, double spin) => 1 + 0.05 * Noise(th * 3 + spin * 0.6, 5) + 0.035 * Noise(th * 8 - spin * 1.3, 9);

        // the hole: a void, a lit far wall, three arms of light spiralling down into it
        static void Hole(Painter p, uint[] C, double rx, double spin, double heat, double wind = 2.3)
        {
            if (rx < 1) return;
            double ry = rx * SY;
            p.Shade(-rx - 2, -ry - 2, rx + 2, ry + 2, (x, y, o) =>
            {
                double u = x / rx, v = y / ry, q = Hypot(u, v), th = Atan2(v, u), lip = LipAt(th, spin);
                if (q > lip) return 0;
                double w = q / lip;
                // the far inner wall catches the glow from below; the near one is hidden
                o.c = v < 0 && w > 0.72 ? (w > 0.86 ? C[2] : C[1]) : w > 0.55 ? C[0] : VOID;
                return 0;
            });
            double n = Math.Max(40, Round(rx * 5));
            for (int i = 0; i < 3; i++)
                for (int j = 0; j <= n; j++)
                {
                    double q = 0.14 + 0.82 * (j / n), th = spin + (i * TAU) / 3 - wind * Px.Log(q), lip = LipAt(th, spin);
                    double x = Px.Cos(th) * q * lip * rx, y = Px.Sin(th) * q * lip * ry;
                    uint c = q > 0.8 ? (heat > 1.1 ? WHITE : C[6]) : q > 0.55 ? C[5] : q > 0.32 ? C[4] : C[3];
                    p.Dot(x, y, c, q > 0.3 ? 1 : q / 0.3);
                    // a fainter strand riding just inside each arm
                    if (q > 0.4 && j % 2 == 0)
                    {
                        double t2 = th - 0.32, x2 = Px.Cos(t2) * q * 0.94 * lip * rx, y2 = Px.Sin(t2) * q * 0.94 * lip * ry;
                        p.Dot(x2, y2, q > 0.7 ? C[4] : C[3], 0.85);
                    }
                }
            p.Glow(0, 0, rx * 0.45, C[3], 0.35 * heat);
        }

        // the lip: a ragged ring of white-hot light, a dark cut outside it; far half behind, near half in front
        static void Lip(Painter p, uint[] C, double rx, double spin, double heat, bool near)
        {
            if (rx < 1) return;
            double ry = rx * SY, n = Math.Ceiling(rx * 7);
            for (int j = 0; j < n; j++)
            {
                double th = (j / n) * TAU, s = Px.Sin(th);
                if ((s >= 0) != near) continue;
                double l = LipAt(th, spin), x = Px.Cos(th) * rx * l, y = s * ry * l, f = Noise(th * 9 + spin * 3, 13);
                p.Dot(Px.Cos(th) * (rx * l + 1.2), s * (ry * l + 1), C[1]);
                p.Dot(x, y, near ? (f > -0.3 || heat > 1.1 ? WHITE : C[6]) : f > 0.2 ? C[6] : C[5]);
                if (j % 6 == 0) p.Glow(x, y, 7, C[5], (near ? 0.3 : 0.2) * Math.Min(1.4, heat));
            }
        }

        sealed class Swirl
        {
            public double a, r, w, i;
            public uint c;
        }

        sealed class Wisp
        {
            public double x, y, vx, vy, life, max, acc, ph;
            public readonly List<double[]> tr = new List<double[]>();
        }

        sealed class Shock
        {
            public double t0, life, r0, r1, th;
            public bool hot;
        }

        readonly uint[] C, SM;
        readonly double RX;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits rocks;
        readonly Lights lights;
        readonly List<Swirl> swirl = new List<Swirl>();
        readonly List<Wisp> wisps = new List<Wisp>();
        readonly List<Shock> rings = new List<Shock>();
        readonly HashSet<string> done = new HashSet<string>();
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t;

        SpawnPortal(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            RX = o.Num("width");
            R = new Rng(o.Seed);
            // violet smoke, dulled toward a smoky grey so it reads as smoke and not as glass
            SM = new[] { Mix(C[4], FUME, 0.4), Mix(C[3], FUME, 0.3), Mix(C[5], FUME, 0.55) };
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            rocks = new Bits(x => 0, e);
            lights = new Lights(e);
        }

        // a mote riding the swirl on the floor plane, spiralling in
        void AddSwirl(double r0)
        {
            var s = new Swirl { a = R.Range(0, TAU), r = r0, w = R.Range(2.2, 4), i = R.Range(0.3, 0.7) };
            s.c = R.Pick(new[] { WHITE, C[6], C[6], C[5] });
            swirl.Add(s);
        }

        // a streak of light sucked in from the air round the rift
        void Pull()
        {
            double a = R.Range(Math.PI * 1.03, Math.PI * 1.97), D = R.Range(40, 86), T = R.Range(0.24, 0.42);
            double x = Px.Cos(a) * D * 1.08, y = Px.Sin(a) * D * 0.85, l = Hypot(x, y), v = (0.6 * l) / (1 - Exp(-0.6 * T));
            ps.Spawn(P.Mote, x, y, (-x / l) * v, (-y / l) * v, T, R.Pick(new[] { WHITE, C[6], C[5], C[5], C[4] }), R.Range(1, 1.7));
        }

        void Puff(int n, bool big, double sp)
        {
            for (int i = 0; i < n; i++)
            {
                double a = R.Range(0, TAU), d = R.Range(0, 0.55) * RX;
                ps.Spawn(P.Smoke, Px.Cos(a) * d, Px.Sin(a) * d * SY - 2, R.Range(-1, 1) * sp * 0.6, -R.Range(0.4, 1) * sp, R.Range(1.2, 2.2) * (big ? 1.15 : 0.8), R.Pick(SM), R.Range(2.8, 4.6) * (big ? 1.15 : 0.9), big ? R.Range(0, 0.3) : R.Range(0, 0.1), big ? PF.Big : PF.None);
            }
        }

        void Sparks(int n, double up, double spread)
        {
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI / 2 + R.Range(-spread, spread), sp = R.Range(0.5, 1) * up;
                ps.Spawn(P.Spark, R.Range(-0.5, 0.5) * RX, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.35, 0.8));
            }
        }

        bool Once(string k, double at)
        {
            if (done.Contains(k) || t < at) return false;
            done.Add(k);
            return true;
        }

        public override void Update(double dt)
        {
            t += dt;
            // the tear pulls dust and light in while it opens, while it's open, and hard while it shuts
            bool shutting = t > SHUT && t < POP;
            if ((t < OPEN + 0.15 || shutting) && R.Chance(0.55))
            {
                int s = R.Sign();
                double D = R.Range(RX * 1.3, RX * 2.4), T = R.Range(0.18, 0.3), v = (D * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, s * D, -R.Range(0, 3), -s * v, R.Range(-2, 2), T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (t > 0.1 && t < POP - 0.1 && R.Chance(shutting ? 0.85 : t < BURST ? 0.55 : 0.1)) Pull();
            if (t < POP && t > 0.12 && R.Chance(shutting ? 0.5 : 0.25)) AddSwirl(R.Range(1.1, 1.5));
            if (Once("open", OPEN * 0.7))
            {
                lights.Add(0, -8, 50, C[4], 0.75, 0.45);
                rings.Add(new Shock { t0 = t, life = 0.42, r0 = RX * 0.8, r1 = RX * 1.9, th = 2.5, hot = true });
                for (int i = 0; i < 8; i++) ps.Spawn(P.Dust, R.Sign() * R.Range(0.7, 1.1) * RX, -1, R.Sign() * R.Range(10, 45), -R.Range(4, 14), R.Range(0.4, 0.7), DUST, R.Range(1.5, 3));
            }
            // two small belches before the big one: a burp of smoke and a few sparks
            if (Once("b0", BELCH[0]))
            {
                Puff(6, false, 34);
                Sparks(8, 210, 0.55);
                lights.Add(0, -6, 44, C[4], 0.6, 0.3);
            }
            if (Once("b1", BELCH[1]))
            {
                Puff(8, false, 40);
                Sparks(12, 240, 0.6);
                lights.Add(0, -6, 48, C[4], 0.65, 0.3);
            }
            // the burst: something comes up through it
            if (Once("burst", BURST))
            {
                lights.Add(0, -26, 74, C[4], 1, 0.75);
                lights.Add(0, -10, 40, WHITE, 0.85, 0.14);
                rings.Add(new Shock { t0 = t, life = 0.55, r0 = RX * 1.0, r1 = RX * 2.5, th = 3.5, hot = true });
                rings.Add(new Shock { t0 = t + 0.06, life = 0.45, r0 = RX * 0.8, r1 = RX * 1.8, th = 2, hot = false });
                Puff(18, true, 70);
                Sparks(34, 400, 0.8);
                for (int i = 0; i < 5; i++)
                {
                    double a = -Math.PI / 2 + (i - 2) * 0.27 + R.Range(-0.08, 0.08), sp = R.Range(185, 235);
                    var w = new Wisp { x = R.Range(-4, 4), y = -2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, life = R.Range(0.75, 1.0), max = 1, acc = 0 };
                    w.ph = R.Range(0, TAU);
                    wisps.Add(w);
                }
                for (int i = 0; i < 12; i++)
                    rocks.Add(new Bits.Bit
                    {
                        kind = B.Chunk, x = R.Sign() * R.Range(0.75, 1.05) * RX, y = -1, vx = R.Range(-100, 100), vy = -R.Range(150, 280), g = 1, drag = 0.4, bounce = 0.3,
                        life = R.Range(1.3, 1.9), a = R.Range(0, TAU), va = R.Range(-14, 14), s = R.Range(1, 2.4), ramp = STONE,
                    });
                for (int i = 0; i < 14; i++) ps.Spawn(P.Star, R.Range(-0.8, 0.8) * RX, -R.Range(4, 34), R.Range(-30, 30), -R.Range(10, 60), R.Range(0.5, 1.1), R.Pick(new[] { WHITE, C[6], C[5] }), R.Range(0.8, 2));
            }
            // while it's open it keeps venting thin smoke and the odd spark
            if (t > OPEN && t < SHUT && R.Chance(0.16)) Puff(1, false, 22);
            if (t > OPEN && t < SHUT + 0.1 && R.Chance(0.06)) Sparks(1, 160, 0.4);
            // the pop when it closes
            if (Once("pop", POP))
            {
                lights.Add(0, -4, 50, C[4], 0.95, 0.5);
                lights.Add(0, -2, 28, WHITE, 0.9, 0.12);
                rings.Add(new Shock { t0 = t, life = 0.35, r0 = 2, r1 = RX * 1.9, th = 2.5, hot = true });
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(-Math.PI, 0), sp = R.Range(90, 270);
                    ps.Spawn(P.Spark, 0, -1, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.8, R.Range(0.25, 0.6));
                }
                for (int i = 0; i < 9; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(30, 95);
                    ps.Spawn(P.Star, 0, -2, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.5 - 20, R.Range(0.4, 0.9), R.Pick(new[] { WHITE, C[6] }), R.Range(0.8, 1.8));
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Smoke, R.Range(-6, 6), -R.Range(1, 4), R.Range(-34, 34), -R.Range(10, 30), R.Range(0.8, 1.3), SM[2], R.Range(2.5, 4.5));
                // what's left on the floor: specks of light that twinkle out where the rift was
                for (int i = 0; i < 14; i++) ps.Spawn(P.Star, R.Range(-1, 1) * RX * 0.9, -R.Range(0.5, 2), R.Range(-4, 4), -R.Range(0, 4), R.Range(0.6, 1.4), R.Pick(new[] { C[6], C[5], C[5] }), R.Range(0.4, 0.9), R.Range(0, 0.25));
            }
            for (int i = swirl.Count - 1; i >= 0; i--)
            {
                Swirl s = swirl[i];
                double k = shutting ? 2.4 : 1;
                s.a += s.w * k * dt * (1.4 / Math.Max(0.3, s.r));
                s.r -= s.i * k * dt;
                if (s.r < 0.25 || t > POP) swirl.RemoveAt(i);
            }
            // the wisps: comets with smoky tails that arc up and out, then burst
            for (int i = wisps.Count - 1; i >= 0; i--)
            {
                Wisp w = wisps[i];
                w.tr.Insert(0, new[] { w.x, w.y });
                if (w.tr.Count > 12) w.tr.RemoveAt(w.tr.Count - 1);
                // a wisp bursts when it runs out or comes down on the floor
                if ((w.life -= dt) <= 0 || (w.vy > 0 && w.y > -2))
                {
                    for (int j = 0; j < 8; j++)
                    {
                        double a = R.Range(0, TAU), sp = R.Range(30, 120);
                        ps.Spawn(P.Spark, w.x, w.y, Px.Cos(a) * sp + w.vx * 0.2, Px.Sin(a) * sp + w.vy * 0.2, R.Range(0.2, 0.5));
                    }
                    ps.Spawn(P.Star, w.x, w.y, 0, -6, 0.45, WHITE, 2.4);
                    lights.Add(w.x, w.y, 24, C[5], 0.65, 0.25);
                    wisps.RemoveAt(i);
                    continue;
                }
                w.vy += 360 * dt;
                w.vx += Px.Sin((w.max - w.life) * 16 + w.ph) * 160 * dt;
                w.vx -= w.vx * 1.1 * dt;
                w.vy -= w.vy * 1.1 * dt;
                w.x += w.vx * dt;
                w.y += w.vy * dt;
                for (w.acc += dt; w.acc > 0.03; w.acc -= 0.03) ps.Spawn(P.Smoke, w.x, w.y, w.vx * 0.05, w.vy * 0.05 - 4, R.Range(0.6, 1), SM[0], R.Range(1.5, 2.4));
                if (R.Chance(0.3)) ps.Spawn(P.Mote, w.x, w.y, -w.vx * 0.2 + R.Range(-15, 15), -w.vy * 0.2 + R.Range(-15, 15), R.Range(0.2, 0.4), C[6], 0.6);
            }
            for (int i = rings.Count - 1; i >= 0; i--)
                if (t - rings[i].t0 > rings[i].life)
                    rings.RemoveAt(i);
            rocks.Update(dt);
            foreach (Bits.Bit b in rocks.List)
                if (b.rest && !dusted.Contains(b))
                {
                    dusted.Add(b);
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(6, 30), -R.Range(2, 12), R.Range(0.3, 0.6), DUST, R.Range(1.2, 2.4));
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        double Heat(double t) => 0.85 + 0.15 * Px.Sin(t * 7) + 0.6 * Exp(-Px.Pow(t - BURST, 2) / 0.01);

        public override void DrawBack(Painter p)
        {
            double rx = Radius(t, RX), spin = SpinAt(t), heat = Heat(t);
            // the floor glows violet round the hole
            if (t < POP)
            {
                p.Glow(0, -4, rx * 1.8 + 10, C[4], 0.32 * Clamp(rx / RX));
                // and light wells up out of it into the air
                p.GlowLine(0, -2, 0, -44, rx * 0.7 + 6, C[4], 0.16 * Clamp(rx / RX));
            }
            // the slit it tears open from
            if (t < OPEN * 0.6)
            {
                double k = t / (OPEN * 0.6), L = RX * 1.1 * Ease.OutCubic(k);
                p.Line(-L, 0, L, 0, u => Math.Abs(u - 0.5) < 0.3 ? WHITE : C[6]);
                p.GlowLine(-L, 0, L, 0, 7, C[5], 0.6);
            }
            if (t > OPEN * 0.35 && t < POP)
            {
                // the arms wind tighter as it twists shut
                Hole(p, C, rx, spin, heat + 0.4 * Smooth(SHUT, POP, t), 2.3 + 2.6 * Smooth(SHUT, POP - 0.05, t));
                Lip(p, C, rx, spin, heat, false);
            }
            foreach (Shock r in rings) Ring(p, C, r, t, false);
            DrawSwirl(p, false);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            double rx = Radius(t, RX), spin = SpinAt(t), heat = Heat(t);
            if (t > OPEN * 0.35 && t < POP) Lip(p, C, rx, spin, heat, true);
            foreach (Shock r in rings) Ring(p, C, r, t, true);
            DrawSwirl(p, true);
            // the burst: a column of light shoots up out of the hole, thins and lifts away
            double b = t - BURST;
            if (b > 0 && b < 0.45)
            {
                double k = b / 0.45, top = -84 * Ease.OutExpo(Clamp(b / 0.14)), bot = -1 - 60 * Ease.InQuad(k), w0 = RX * 0.55 * (1 - k * 0.8);
                p.Shade(-w0 - 1, top, w0 + 1, bot, (x, y, o) =>
                {
                    double u = (y - bot) / (top - bot), half = w0 * Px.Pow(1 - u, 0.8) * (0.85 + 0.15 * Px.Sin(y * 0.7 + t * 40));
                    double d = Math.Abs(x) / Math.Max(0.5, half);
                    if (d > 1) return 0;
                    o.c = d < 0.35 ? WHITE : d < 0.7 ? C[6] : C[5];
                    return 0;
                });
                p.GlowLine(0, bot, 0, top, w0 + 12, C[5], 0.55 * (1 - k));
                if (b < 0.14) Sim.Flare(p, 0, -4, 36 * (1 - b / 0.14), 1 - b / 0.14, C);
            }
            // the collapse: a white point that pops
            double q = t - POP;
            if (q > -0.08 && q < 0.25) Sim.Sparkle(p, 0, -1, Round((q < 0 ? 4 : 12) * (1 - Clamp(q / 0.25))), WHITE, C[5], 0.7 * (1 - Clamp(q / 0.25)));
            rocks.Draw(p);
            foreach (Wisp w in wisps)
            {
                double k = w.life / w.max;
                for (int i = 1; i < w.tr.Count; i++)
                {
                    double x0 = w.tr[i - 1][0], y0 = w.tr[i - 1][1], x1 = w.tr[i][0], y1 = w.tr[i][1], u = (double)i / w.tr.Count;
                    p.Line(x0, y0, x1, y1, u < 0.25 ? WHITE : u < 0.55 ? C[6] : C[4], 1.15 - u);
                    if (u < 0.35) p.Line(x0 + 1, y0, x1 + 1, y1, C[5], 1);
                }
                p.Circle(w.x, w.y, 2, C[5]);
                p.Circle(w.x, w.y, 1.2, C[6]);
                p.Px(w.x, w.y, WHITE);
                p.Glow(w.x, w.y, 12, C[5], 0.5 * k + 0.2);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }

        void DrawSwirl(Painter p, bool near)
        {
            double rx = Radius(t, RX);
            foreach (Swirl s in swirl)
                for (int j = 0; j < 3; j++)
                {
                    double a = s.a - j * 0.08 * (1.4 / Math.Max(0.3, s.r)), x = Px.Cos(a) * s.r * rx, y = Px.Sin(a) * s.r * rx * SY;
                    if ((Px.Sin(a) >= 0) != near) continue;
                    p.Dot(x, y - 0.5, j != 0 ? C[5] : s.c, j != 0 ? 0.9 - j * 0.3 : 1);
                    if (j == 0) p.Glow(x, y, 3, C[5], 0.35);
                }
        }

        // a shock ring on the floor plane: far half behind, near half in front
        static void Ring(Painter p, uint[] C, Shock r, double t, bool near)
        {
            double k = (t - r.t0) / r.life;
            if (k < 0 || k >= 1) return;
            Sim.Band(p, 0, 0, r.r0 + (r.r1 - r.r0) * Ease.OutCubic(k), SY * 0.62, Math.Max(1, r.th * (1 - k)), 1.15 - k, C, g => (Px.Sin(g) >= 0) == near, r.hot && k < 0.15);
        }
    }
}
