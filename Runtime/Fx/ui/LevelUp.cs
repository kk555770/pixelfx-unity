// The pillar's strands of light and the rings climbing it adapt Sprite Fusion's open-source holy light
// (https://www.spritefusion.com/pixel-art-vfx/holy-light), and the streaks rushing in its power-up
// (https://www.spritefusion.com/pixel-art-vfx/power-up), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Globalization;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class LevelUp : Effect
    {
        const double DURATION = 3.2;

        static readonly Pose COIL = new Pose { ["turn"] = 0.9, ["crouch"] = 7, ["lean"] = 2, ["fx"] = -6, ["fy"] = -13, ["bx"] = 7, ["by"] = -13, ["ffx"] = -5, ["fbx"] = 6 };
        static readonly Pose LEAP = new Pose { ["turn"] = 1.1, ["y"] = -16, ["crouch"] = -1, ["fx"] = -6, ["fy"] = -45, ["bx"] = 7, ["by"] = -44, ["ffx"] = -2, ["ffy"] = -8, ["fbx"] = 5, ["fby"] = -3, ["toeB"] = true };
        static readonly Pose PUMP = new Pose { ["turn"] = 0.9, ["crouch"] = 0, ["fx"] = -6, ["fy"] = -45, ["bx"] = 9, ["by"] = -15, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(-0.3, Poses.READY),
                new PoseKey(0.28, COIL),
                new PoseKey(0.3, LEAP, "snap"),
                new PoseKey(0.46, new Pose(LEAP) { ["y"] = -19 }),
                new PoseKey(0.72, new Pose(LEAP) { ["y"] = 0, ["ffy"] = -4, ["fby"] = -4 }, "in"),
                new PoseKey(0.74, new Pose(COIL) { ["crouch"] = 5 }, "snap"),
                new PoseKey(0.95, PUMP, "out"),
                new PoseKey(1.2, new Pose(PUMP) { ["crouch"] = 1, ["fy"] = -43 }),
                new PoseKey(1.45, PUMP),
                new PoseKey(2.3, new Pose(PUMP) { ["fy"] = -41 }),
                new PoseKey(2.8, Poses.READY),
                new PoseKey(3.2, Poses.READY),
            },
            Hits = new[] { new HitCue { T = 0.28, Stop = 0.05, Shake = 0.14, Amp = 2 } },
            Dust = new[] { new DustCue { T = 0.3, X = 0, N = 7 }, new DustCue { T = 0.74, X = 0, N = 6 } },
        };

        public static readonly Module Module = new Module("level-up", "pixelfx/fx/ui/level-up", 176, 138, 88, 114, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1405", "#5c3008", "#9a5a0e", "#d48e18", "#f5b92c", "#ffde5e", "#fff4bc", "#ffffff" },
                ["text"] = "LEVEL UP!",
                ["seed"] = 5,
            },
            (o, e) => new LevelUp(o, e), act: Act, actor: 2)
        {
            Title = "Level up",
            Desc = "Gold gathers at the feet and bursts up as a pillar of light round the hero; rings climb it, LEVEL UP! drops in letter by letter, a shine sweeps it, sparkles twinkle.",
            Tags = new[] { "ui", "level", "reward", "text", "light", "one-shot" },
            Bg = "ground",
        };

        // release, pillar lifts off, pillar gone, pillar height, text top, first letter, stagger, shine, letters pop
        const double REL = 0.28, RET = 1.02, GONE = 1.34, TOP = 100, TY = -68, DROP = 0.5, STAG = 0.05, LANDED = 1.06, SHINE = 1.24, EXIT = 2.3;

        sealed class Strand
        {
            public double x, sp, per, duty, ph, end;
            public int lvl;
        }

        // strands of light streaming up the pillar in dashes: where across it, how fast, dash period and length
        static readonly Strand[] STRANDS = MakeStrands();

        static Strand[] MakeStrands()
        {
            var s = new Strand[9];
            for (int i = 0; i < 9; i++)
                s[i] = new Strand
                {
                    x = ((double)i / 8 - 0.5) * 1.5 + (Rand(i, 1) - 0.5) * 0.16,
                    sp = 170 + Rand(i, 2) * 170,
                    per = 10 + Rand(i, 3) * 16,
                    duty = 0.3 + Rand(i, 4) * 0.35,
                    ph = Rand(i, 5),
                    lvl = Rand(i, 6) < 0.35 ? 1 : 2,
                    end = 0.5 + Rand(i, 7) * 0.42,
                };
            return s;
        }

        // half-width of the pillar at time t
        static double WidthAt(double t) => t < REL ? 0 : 12 * (1 + 0.4 * Exp(-(t - REL) * 10)) * (1 - 0.82 * Smooth(RET - 0.12, RET + 0.16, t));

        // the pillar: shoots up from the feet (one white-hot frame), then stands as light, not paint: two flowing
        // walls, a white core and strands streaming up in dashes; at the end its foot lifts off and it shoots away
        static void Column(Painter p, uint[] C, double t)
        {
            double a = t - REL;
            if (a < 0 || t > GONE) return;
            double top = -TOP * Ease.OutExpo(Clamp(a / 0.12)), bot = -TOP * Ease.InCubic(Seg(t, RET, GONE)), W = WidthAt(t), k = 1 - Smooth(RET, GONE, t);
            p.GlowLine(0, top, 0, Math.Min(bot, -34), W * 1.6 + 6, C[5], 0.2 * k + 0.06);
            p.GlowLine(0, top, 0, Math.Min(bot, -40), 5, WHITE, 0.18 * k);
            if (a < 0.04)
            {
                for (double y = Math.Floor(top); y < bot; y++)
                {
                    double wr = W * 0.55 * (1 - 0.3 * (-y / TOP));
                    for (double x = Math.Floor(-wr); x <= Math.Ceiling(wr); x++) p.Dot(x, y, Math.Abs(x + 0.5) < wr - 1.5 ? WHITE : C[6], Clamp((y + TOP) / 20) * 1.5);
                }
                return;
            }
            for (double y = Math.Ceiling(top); y < Math.Ceiling(bot); y++)
            {
                double h = -y / TOP, fade = Clamp((y + TOP) / 16) * (t < RET ? 1 : Clamp((bot - y) / 10));
                bool head = y - top < 3 && a < 0.16;
                double wr = W * (1 - 0.3 * h) + 0.9 * Noise2(y * 0.2, t * 12, 3);
                foreach (int s in new[] { -1, 1 })
                {
                    bool run = Fract(y * 0.06 + t * 3.4 + (s > 0 ? 0.5 : 0)) < 0.78;
                    double xw = s * wr;
                    p.Dot(xw, y, head ? WHITE : run ? C[6] : C[5], fade * 1.4);
                    p.Dot(xw + s, y, C[4], fade * (run ? 0.5 : 0.25));
                    if (run) p.Dot(xw - s, y, C[5], fade * 1.2);
                }
                p.Dot(0, y, WHITE, fade * 1.6);
                p.Dot(-1, y, Noise2(y * 0.3, t * 14, 5) > -0.2 ? WHITE : C[6], fade * 1.3);
                if (Noise2(y * 0.25, t * 9, 8) > 0.25) p.Dot(1, y, C[6], fade * 1.2);
                foreach (Strand S in STRANDS)
                {
                    if (h > S.end) continue;
                    double ph = Fract((y + t * S.sp) / S.per + S.ph);
                    if (ph > S.duty) continue;
                    double u = ph / S.duty;
                    p.Px(S.x * wr, y, head || u < 0.2 ? WHITE : u < 0.65 ? C[7 - S.lvl] : C[6 - S.lvl]);
                }
            }
        }

        // the plate of light the pillar stands on
        static void Plate(Painter p, uint[] C, double t, double k)
        {
            if (k <= 0.02) return;
            double rx = 9 + 9 * k + Px.Sin(t * 9), ry = 2.2;
            p.Shade(-rx, -ry, rx, ry, (x, y, o) =>
            {
                double dx = x / rx, dy = y / ry, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1) return 0;
                double u = d / Math.Max(0.05, k);
                o.c = u < 0.3 ? WHITE : u < 0.55 ? C[6] : u < 0.8 ? C[5] : C[4];
                o.a = d < 0.6 * k ? 1 : k * (1 - d) * 2.2;
                return 0;
            });
            p.Glow(0, -1, rx * 2.2, C[4], 0.35 * k);
        }

        // a burst of rays above and below the title: they start on an ellipse just outside the word, so none crosses
        // it, shoot out when the last letter lands, shimmer, and draw back in
        static void Rays(Painter p, uint[] C, double t, double k, double cx, double cy)
        {
            if (k <= 0.02) return;
            for (int i = 0; i < 20; i++)
            {
                double an = ((double)i / 20) * TAU + 0.08, ca = Px.Cos(an), sa = Px.Sin(an);
                bool isLong = i % 2 == 0;
                if (Math.Abs(sa) < 0.4) continue;
                double qa = ca / 52, qb = sa / 12;
                double r0 = 1 / Math.Sqrt(qa * qa + qb * qb) + 2 * k, len = (isLong ? 15 + 7 * Rand(i, 9) : 7 + 4 * Rand(i, 8)) * k;
                if (len < 1) continue;
                double sh = 0.75 + 0.25 * Px.Sin(t * 7 + i * 2.1), x0 = cx + ca * r0, y0 = cy + sa * r0;
                p.Line(x0, y0, x0 + ca * len, y0 + sa * len, u => u < 0.3 ? C[7] : u < 0.6 ? C[6] : C[5], u => sh * (u < 0.55 ? 1.3 : (1.3 * (1 - u)) / 0.45));
            }
        }

        sealed class Let
        {
            public Font.Glyph g;
            public double w, cx, born, pop, off = -18, v = 60, sq, flash;
            public bool on, landed, rest, burst;
        }

        sealed class Sat
        {
            public double x, y, t0, dur, L;
        }

        sealed class Rush
        {
            public double x, y, vx, vy, life, len;
            public uint c;
            public bool back;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Actor A;
        readonly uint SHADOW;
        readonly Particles ps;
        readonly Lights lights;
        readonly Word word;
        const double BASE = TY + 14;
        readonly double X0, right;
        readonly List<Let> lets = new List<Let>();
        readonly Sat[] sats;
        readonly List<double> halos = new List<double>();
        readonly List<Rush> rush = new List<Rush>();
        double t, nextHalo = REL + 0.03, gather;
        bool released;

        LevelUp(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            A = o.Actor;
            SHADOW = Scale(C[0], 0.5);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
            word = Spell(Convert.ToString(o["text"], CultureInfo.InvariantCulture));
            X0 = -word.W;
            int n = 0;
            foreach (Letter L in word.Letters)
            {
                if (L.G == null) continue;
                lets.Add(new Let { g = L.G, w = L.W, cx = X0 + (L.X + L.W / 2.0) * 2, born = DROP + n * STAG, pop = EXIT + n * 0.04 });
                n++;
            }
            right = X0 + word.W * 2;
            // sparkles that pop round the title one after another
            sats = new Sat[8];
            for (int i = 0; i < 8; i++)
            {
                int s = i % 2 != 0 ? 1 : -1;
                double x = s * R.Range(20, 60), y = TY + 7 + (Math.Abs(x) < 50 ? R.Sign() * R.Range(11, 18) : R.Range(-14, 14));
                var st = new Sat { x = x, y = y, t0 = SHINE - 0.1 + i * 0.13 + R.Range(0, 0.05) };
                st.dur = R.Range(0.32, 0.5);
                st.L = R.Range(3, 6);
                sats[i] = st;
            }
        }

        // every particle is gone by the end
        void Spawn(P k, double x, double y, double vx, double vy, double life, uint col = WHITE, double size = 1, double delay = 0, PF flag = PF.None) =>
            ps.Spawn(k, x, y, vx, vy, Math.Min(life, DURATION - 0.05 - t), col, size, delay, flag);

        // a gathering streak: a gold tail and a white head, running straight in
        void DrawRush(Painter p, bool back)
        {
            foreach (Rush m in rush)
            {
                if (m.back != back) continue;
                double sp = Hypot(m.vx, m.vy), ux = m.vx / sp, uy = m.vy / sp, L = m.len;
                p.Line(m.x - ux * L, m.y - uy * L, m.x - ux * L * 0.4, m.y - uy * L * 0.4, m.c);
                p.Line(m.x - ux * L * 0.35, m.y - uy * L * 0.35, m.x, m.y, WHITE);
                p.Glow(m.x, m.y, 4, C[5], 0.25);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the gathering: streaks pour in along the floor to the feet and in from all round to the body, and in the
            // last beat run up the hero from the feet; the late ones are swallowed by the burst
            for (gather += t < REL ? dt * 420 : 0; gather >= 1; gather--)
            {
                double kind = R.Next(), sp = R.Range(140, 250) * (1 + t * 2);
                double x, y, tx, ty;
                if (kind < 0.42)
                {
                    double an = R.Range(0, TAU), D = R.Range(22, 70);
                    x = Px.Cos(an) * D;
                    y = -1 - Math.Abs(Px.Sin(an)) * D * 0.1;
                    tx = Px.Cos(an) * 3;
                    ty = -1;
                }
                else if (kind < 0.84)
                {
                    double an = R.Range(0, TAU), D = R.Range(34, 64);
                    x = Px.Cos(an) * D;
                    y = Math.Min(-3, -20 + Px.Sin(an) * D * 0.85);
                    tx = Px.Cos(an) * 11;
                    ty = -20 + Px.Sin(an) * 14;
                }
                else
                {
                    if (t < REL * 0.4) continue;
                    x = R.Range(-9, 9);
                    y = -1;
                    tx = x * 0.7;
                    ty = -R.Range(26, 44);
                }
                double dx = tx - x, dy = ty - y, l = Hypot(dx, dy);
                if (l == 0 || double.IsNaN(l)) l = 1;
                var m = new Rush { x = x, y = y, vx = (dx / l) * sp, vy = (dy / l) * sp, life = Math.Min(l / sp, REL + 0.06 - t) };
                m.len = R.Range(5, 9);
                m.c = R.Chance(0.45) ? C[6] : C[5];
                m.back = kind > 0.84 && R.Chance(0.5);
                rush.Add(m);
            }
            for (int i = rush.Count - 1; i >= 0; i--)
            {
                Rush m = rush[i];
                if ((m.life -= dt) <= 0) rush.RemoveAt(i);
                else
                {
                    m.x += m.vx * dt;
                    m.y += m.vy * dt;
                }
            }
            if (t < REL && R.Chance(0.12)) Spawn(P.Star, R.Range(-30, 30), -R.Range(0, 3), 0, -R.Range(10, 30), R.Range(0.3, 0.5), C[6], 0.6);
            if (!released && t >= REL)
            {
                released = true;
                lights.Add(0, -24, 90, C[5], 0.6, 0.7);
                lights.Add(0, -8, 40, WHITE, 0.6, 0.12);
                // speed lines flung out from the hero
                for (int i = 0; i < 22; i++)
                {
                    double an = ((double)i / 22) * TAU + R.Range(-0.12, 0.12), sp = R.Range(260, 460);
                    double y0 = -18 + Px.Sin(an) * 8, vy = Px.Sin(an) * sp * 0.8;
                    double life = Math.Min(Math.Min(R.Range(0.1, 0.2), vy > 0 ? -y0 / vy : vy < 0 ? (y0 + 104) / -vy : 1), 78 / Math.Abs(Px.Cos(an) * sp));
                    Spawn(P.Mote, Px.Cos(an) * 6, y0, Px.Cos(an) * sp, vy, life, R.Chance(0.4) ? C[7] : C[6], 1, 0, an > 0.3 && an < Math.PI - 0.3 ? PF.Back : PF.None);
                }
                for (int i = 0; i < 26; i++)
                {
                    double an = -Math.PI / 2 + R.Range(-1.3, 1.3), sp = R.Range(120, 290);
                    Spawn(P.Spark, R.Range(-5, 5), -2, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.35, 0.8));
                }
                for (int i = 0; i < 16; i++)
                    Spawn(P.Star, R.Range(-10, 10), R.Range(-36, -4), R.Range(-30, 30), -R.Range(15, 60), R.Range(0.6, 1.3), R.Pick(new[] { C[7], C[6], C[5] }), R.Chance(0.35) ? R.Range(1.5, 2.6) : R.Range(0.4, 0.9), 0, R.Chance(0.4) ? PF.Back : PF.None);
            }
            double W = WidthAt(t);
            if (t >= nextHalo && t < RET - 0.1)
            {
                halos.Add(t);
                nextHalo += 0.16;
            }
            // streaks race up beside the pillar
            if (t > REL && t < RET + 0.1 && R.Chance(0.5))
            {
                int s = R.Sign();
                double y = R.Range(-4, -60), vy = -R.Range(140, 260);
                Spawn(P.Mote, s * R.Range(0.75, 1.6) * W, y, 0, vy, Math.Min(R.Range(0.16, 0.32), (y + 96) / -vy), R.Chance(0.5) ? C[7] : C[6], R.Range(0.6, 1), 0, R.Chance(0.5) ? PF.Back : PF.None);
            }
            // motes rise from the floor while the light lasts
            if (t > REL && t < 2.4 && R.Chance(0.14 * (1 - Seg(t, 1.6, 2.4))))
                Spawn(P.Ember, R.Range(-20, 20), -R.Range(1, 30), R.Range(-15, 15), -R.Range(20, 55), R.Range(0.8, 1.6), C[5], R.Range(1, 2), 0, R.Chance(0.4) ? PF.Back : PF.None);
            // gold motes lift off the hero while the light lasts
            if (A != null && t > REL + 0.1 && t < 2.2 && R.Chance(0.09))
            {
                Mask m = A.Mask;
                for (int n = 0; n < 12; n++)
                {
                    int i = R.Int(0, m.W - 1), j = R.Int(0, m.H - 1);
                    if (m.Data[j * m.W + i] == 0) continue;
                    Spawn(P.Star, A.X + i + 0.5, A.Y + j + 0.5, R.Range(-8, 8), -R.Range(14, 34), R.Range(0.6, 1.1), R.Pick(new[] { C[7], C[6], C[5] }), R.Chance(0.2) ? 1.5 : 0.6, 0, R.Chance(0.5) ? PF.Back : PF.None);
                    break;
                }
            }
            // twinkles round the title and the hero while it holds
            if (t > 1.0 && t < 2.5 && R.Chance(0.13))
            {
                bool up = R.Chance(0.6);
                double x = up ? R.Range(-58, 58) : R.Range(-20, 20), y = up ? TY + 7 + R.Range(-16, 16) : R.Range(-44, -6);
                Spawn(P.Star, x, y, R.Range(-6, 6), -R.Range(4, 14), R.Range(0.4, 0.8), R.Pick(new[] { C[7], C[6], C[6], C[5] }), R.Chance(0.3) ? R.Range(1.5, 2.4) : 0.6, 0, R.Chance(0.3) ? PF.Back : PF.None);
            }
            // the letters drop in, bounce and settle; then pop away one by one
            foreach (Let L in lets)
            {
                if (t < L.born) continue;
                L.on = true;
                if (!L.rest)
                {
                    L.v += 1700 * dt;
                    L.off += L.v * dt;
                    if (L.off >= 0 && L.v > 0)
                    {
                        L.off = 0;
                        if (L.v > 110)
                        {
                            L.sq = 1;
                            if (!L.landed)
                            {
                                L.landed = true;
                                L.flash = 1;
                                lights.Add(L.cx, BASE - 7, 18, C[5], 0.55, 0.25);
                                foreach (int s in new[] { -1, 1 }) Spawn(P.Star, L.cx + s * L.w, BASE - R.Range(0, 3), s * R.Range(15, 40), -R.Range(5, 25), R.Range(0.25, 0.45), C[6], R.Range(0.5, 1.2));
                            }
                            L.v = -L.v * 0.3;
                        }
                        else
                        {
                            L.v = 0;
                            L.rest = true;
                        }
                    }
                }
                L.sq = Math.Max(0, L.sq - dt / 0.1);
                L.flash = Math.Max(0, L.flash - dt / 0.12);
                if (!L.burst && t >= L.pop + 0.06)
                {
                    L.burst = true;
                    for (int i = 0; i < 3; i++)
                    {
                        double an = R.Range(0, TAU), sp = R.Range(20, 55);
                        Spawn(P.Star, L.cx + R.Range(-3, 3), BASE - 7 + R.Range(-4, 4), Px.Cos(an) * sp, Px.Sin(an) * sp - 15, R.Range(0.3, 0.6), R.Pick(new[] { C[7], C[6] }), i != 0 ? 0.6 : R.Range(1.2, 2));
                    }
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        static readonly (double d, double r1, double th)[] RINGS = { (0, 72, 4), (0.08, 46, 2.5) };

        void Bands(Painter p, Func<double, bool> keep)
        {
            if (t < REL)
            {
                double k = Seg(t, 0.02, REL);
                Sim.Band(p, 0, 0, 4 + 52 * (1 - Ease.InQuad(k)), 0.2, 1.5, k * 1.1, C, keep);
            }
            foreach (var (d, r1, th) in RINGS)
            {
                double k = (t - REL - d) / 0.55;
                if (k > 0 && k < 1) Sim.Band(p, 0, 0, 5 + r1 * Ease.OutCubic(k), 0.2, Math.Max(1, th * (1 - k)), 1.15 - k, C, keep, k < 0.15);
            }
            foreach (double h0 in halos)
            {
                double k = (t - h0) / 0.6;
                if (k > 0 && k < 1) Sim.Band(p, 0, -2 - 72 * Ease.OutQuad(k), (WidthAt(t) * 1.45 + 4) * (1 - 0.4 * k), 0.3, 1.5, (1 - k) * 1.4, C, keep, k < 0.12);
            }
        }

        public override void DrawBack(Painter p, double tt)
        {
            Rays(p, C, t, Ease.OutExpo(Seg(t, LANDED, LANDED + 0.2)) * (1 - Ease.InQuad(Seg(t, EXIT - 0.15, EXIT + 0.1))), 0, TY + 7);
            Bands(p, g => Px.Sin(g) < 0);
            Column(p, C, t);
            Plate(p, C, t, t < REL ? 0.55 * Seg(t, 0.05, REL) : 1 - Smooth(RET - 0.1, GONE + 0.2, t));
            ps.Draw(p, Layer.Back);
            DrawRush(p, true);
        }

        public override void Draw(Painter p, double tt)
        {
            // near halves of the rings, in front of the hero
            Bands(p, g => Px.Sin(g) >= 0);
            // the hero: warming as it gathers, flashed white on the release, then lit gold with a glowing edge
            double lit = Smooth(REL - 0.02, REL + 0.04, t) * (1 - Smooth(RET, 2.5, t));
            TintActor(p, A, t < REL ? 0 : t < REL + 0.06 ? 1 : Exp(-(t - REL - 0.06) / 0.07), new[] { C[4], C[5], C[6] });
            LightActor(p, A, C[5], 0.1 * Seg(t, 0.05, REL) * (1 - lit) + 0.06 * lit, 0.6 * lit, t);
            HaloActor(p, A, C[5], 0.85 * lit * (1 - Smooth(RET, GONE + 0.3, t)) + 0.3 * lit, t);
            // the spark gathering at the feet, then the flare of the release
            if (t < REL)
            {
                double k = Seg(t, 0.05, REL), q = t - REL * 0.6, beat = 1 + 0.4 * Exp(-(q * q) / 0.001);
                Sim.Sparkle(p, 0, -2, (1 + 4 * k * k) * beat, C[6], C[5], 0.3 + 0.4 * k);
            }
            else if (t < REL + 0.22)
            {
                double a = (t - REL) / 0.22;
                Sim.Flare(p, 0, -4, 28 * Px.Pow(1 - a, 1.5), 1 - a, C);
                p.Glow(0, -16, 48, WHITE, 0.3 * (1 - a));
            }
            ps.Draw(p, Layer.Front);
            DrawRush(p, false);
            // the title
            var items = new List<Item>();
            double done = (t - LANDED) / 0.12;
            foreach (Let L in lets)
            {
                if (!L.on) continue;
                double sx = 2, sy = 2, flash = L.flash;
                if (!L.landed && L.v > 150)
                {
                    sx = 1.8;
                    sy = 2.3;
                }
                if (L.sq > 0)
                {
                    sx = 2 * (1 + 0.22 * L.sq);
                    sy = 2 * (1 - 0.22 * L.sq);
                }
                double pu = (t - L.pop) / 0.07, hop = Seg(t, L.pop - 0.1, L.pop);
                if (pu >= 1) continue;
                if (hop > 0 && pu <= 0)
                {
                    sx = 2 * (1 - 0.1 * hop);
                    sy = 2 * (1 + 0.15 * hop);
                }
                if (pu > 0)
                {
                    sx = sy = 2 * (1 + 0.3 * pu);
                    flash = 1;
                }
                flash = Math.Max(flash, done > 0 && done < 1 ? 1 - done : 0);
                double h = 7 * sy, w = L.w * sx;
                items.Add(new Item { G = L.g, X = L.cx - w / 2, Y = BASE + L.off - h - 3 * Ease.OutQuad(hop), Sx = sx, Sy = sy, A = Clamp((t - L.born) / 0.05), Flash = flash });
            }
            double sh = Seg(t, SHINE, SHINE + 0.34);
            double? shine = sh > 0 && sh < 1 ? X0 - 14 + (right - X0 + 30) * Ease.InOutSine(sh) : (double?)null;
            DrawWord(p, items, new Style { Fill = new[] { C[3], C[4], C[5], C[6] }, Edge = WHITE, Under = C[2], Outline = C[0], Shadow = SHADOW, Shine = shine, ShineY = TY, Glint = C[6] });
            if (shine is double shv && shv > X0 && shv < right) p.GlowLine(shv, TY, shv + 8.4, BASE, 9, C[6], 0.3);
            double hold = Smooth(0.9, 1.2, t) * (1 - Smooth(EXIT, EXIT + 0.4, t));
            if (hold > 0) p.Glow(0, TY + 7, 50, C[5], 0.22 * hold + (done > 0 && done < 1 ? 0.3 * (1 - done) : 0));
            // each letter twinkles out where it popped
            foreach (Let L in lets)
            {
                double u = (t - L.pop - 0.07) / 0.16;
                if (u > 0 && u < 1) Sim.Sparkle(p, L.cx, BASE - 7, 6 * (1 - u) * (1 - u), C[6], C[5], 0.5);
            }
            // the glint where the shine leaves the word, and sparkles popping round it
            double g = (t - SHINE - 0.3) / 0.26;
            if (g > 0 && g < 1) Sim.Sparkle(p, right - 2, TY + 1, 7 * Px.Sin(g * Math.PI), C[6], C[5], 0.6);
            foreach (Sat s in sats)
            {
                double u = (t - s.t0) / s.dur;
                if (u > 0 && u < 1) Sim.Sparkle(p, s.x, s.y, s.L * Px.Pow(Px.Sin(u * Math.PI), 0.7), C[6], C[5], 0.5);
            }
            lights.Draw(p);
        }
    }
}
