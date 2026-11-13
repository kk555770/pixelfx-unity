// The tint that floods the character and the gas round it are adapted from Sprite Fusion's open-source poison
// aura (https://www.spritefusion.com/pixel-art-vfx/poison), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class Curse : Effect
    {
        static readonly Pose COWER = new Pose { ["turn"] = 1, ["crouch"] = 6, ["lean"] = 2, ["hy"] = 1, ["fx"] = -6, ["fy"] = -38, ["bx"] = 7, ["by"] = -38, ["ffx"] = -4, ["fbx"] = 5 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.2, Poses.READY),
                new PoseKey(0.3, new Pose { ["turn"] = 0.9, ["hy"] = -1, ["fx"] = -9, ["fy"] = -19, ["bx"] = 10, ["by"] = -19, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(0.55, COWER, "snap"),
                new PoseKey(0.8, new Pose(COWER) { ["crouch"] = 7 }),
                new PoseKey(1.2, new Pose(COWER) { ["crouch"] = 6, ["lean"] = 3 }),
                new PoseKey(1.72, new Pose(COWER) { ["crouch"] = 7, ["lean"] = 2 }),
                new PoseKey(2.1, new Pose { ["turn"] = 1, ["crouch"] = 4, ["lean"] = 3, ["hy"] = 2, ["fx"] = -8, ["fy"] = -15, ["bx"] = 9, ["by"] = -15, ["ffx"] = -4, ["fbx"] = 5 }),
                new PoseKey(2.9, new Pose { ["turn"] = 1, ["crouch"] = 3, ["lean"] = 1, ["fx"] = -8, ["fy"] = -16, ["bx"] = 9, ["by"] = -16, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(3.4, Poses.READY),
            },
            Hits = new[] { new HitCue { T = 0.55, Stop = 0.09, Shake = 0.2, Amp = 2 } },
        };

        public static readonly Module Module = new Module("curse", "pixelfx/fx/shadow/curse", 112, 124, 56, 108, 3.4, false, 30,
            new Options { ["palette"] = VOID, ["bone"] = new[] { "#170f26", "#4a3a6e", "#7f6ea6", "#b3a4d4", "#e2d8f6" }, ["seed"] = 9 },
            (o, e) => new Curse(o, e), act: Act, actor: 2)
        {
            Title = "Curse",
            Desc = "Wisps gather over the head, a skull stamps down with a flash, its eyes burn as the curse floods the body violet, then it cracks and crumbles.",
            Tags = new[] { "shadow", "curse", "debuff", "skull", "status", "dark", "magic", "one-shot" },
            Bg = "ground",
        };

        // where the skull hangs, and the beats: it stamps in, burns, cracks, breaks
        const double SY = -60, STAMP = 0.55, CRACK = 1.72, BREAK = 1.98;

        // the skull, drawn by hand (light from the upper left): k outline, d m l h bone dark to bright, e sockets,
        // n nose, t teeth, g the gaps between them
        static readonly string[] SKULL =
        {
            "......kkkkk......",
            "....kkhhhhlkk....",
            "...khhhhhhlllk...",
            "..khhhhhllllllk..",
            ".khhhhlllllllmmk.",
            ".khhhllllllllmmk.",
            "khhllllllllllmmdk",
            "khlldddllldddmmdk",
            "khleeeeelleeeeedk",
            "kleeeeeelleeeeedk",
            "kleeeeeemleeeeedk",
            "kmleeeelmmleeedkk",
            ".kmmlllmnnmlmddk.",
            "..kmmlmnnnnmddk..",
            "..kdmlllmmlmddk..",
            "...kgtgtgtgtgk...",
            "...kdgtgtgtgdk...",
            "....kkkkkkkkk....",
        };
        const int SW = 17, SH = 18;
        static readonly double[][] EYES = { new[] { -3.5, 1.5 }, new[] { 3.5, 1.5 } };
        static bool InSkull(double u, double v) => u >= 0 && v >= 0 && u < SW && v < SH && SKULL[(int)v][(int)u] != '.';

        // cracks: jagged paths from the crown down to the sockets and out to the temple, each pixel with its turn to split
        static readonly Dictionary<int, double> CRACKS = MakeCracks();

        static Dictionary<int, double> MakeCracks()
        {
            var o = new Dictionary<int, double>();
            int[][][] paths =
            {
                new[] { new[] { 8, 1 }, new[] { 7, 3 }, new[] { 8, 5 }, new[] { 6, 6 }, new[] { 5, 7 } },
                new[] { new[] { 9, 1 }, new[] { 10, 3 }, new[] { 10, 4 }, new[] { 12, 6 }, new[] { 12, 7 } },
                new[] { new[] { 4, 3 }, new[] { 3, 5 }, new[] { 2, 6 } },
                new[] { new[] { 13, 4 }, new[] { 14, 6 } },
            };
            foreach (int[][] path in paths)
            {
                int len = path.Length - 1;
                for (int k = 0; k < len; k++)
                {
                    int x0 = path[k][0], y0 = path[k][1], x1 = path[k + 1][0], y1 = path[k + 1][1], steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                    for (int s = 0; s <= steps; s++)
                    {
                        double x = Round(x0 + ((double)(x1 - x0) * s) / steps), y = Round(y0 + ((double)(y1 - y0) * s) / steps);
                        int key = (int)(y * SW + x);
                        if (!o.ContainsKey(key)) o[key] = (k + (double)s / Math.Max(1, steps)) / len;
                    }
                }
            }
            return o;
        }

        // the skull at scale s (squashed by sq as it lands), shaken by (ox, oy); white for the stamp's first frames;
        // crack 0..1 splits it from the crown, light leaking out of the splits
        static void Skull(Painter p, uint[] C, uint[] K, double s, double sq, double ox, double oy, bool white, double crack)
        {
            var PAL = new Dictionary<char, uint> { ['k'] = K[0], ['d'] = K[1], ['m'] = K[2], ['l'] = K[3], ['h'] = K[4], ['e'] = C[0], ['n'] = C[0], ['t'] = K[3], ['g'] = K[0] };
            double cx = ox, cy = SY + oy, hw = (SW / 2.0) * s + 1, hh = (SH / 2.0) * s * sq + 1;
            p.Shade(cx - hw, cy - hh, cx + hw, cy + hh, (x, y, o) =>
            {
                double u = Math.Floor((x - cx) / s + SW / 2.0), v = Math.Floor((y - cy) / (s * sq) + SH / 2.0);
                if (!InSkull(u, v)) return 0;
                char ch = SKULL[(int)v][(int)u];
                if (white) return ch == 'e' || ch == 'n' ? C[5] : WHITE;
                if (CRACKS.TryGetValue((int)v * SW + (int)u, out double ck) && ck < crack) return crack > 0.55 && ((int)u + (int)v) % 2 != 0 ? C[6] : K[0];
                return PAL[ch];
            });
        }

        // a socket burning: tongues of violet flame from a white root, licking up out of the eye
        static void EyeFire(Painter p, uint[] C, double x, double y, double t, double k, int i)
        {
            if (k <= 0) return;
            // two thin tongues lick up out of the socket and past the brow, swaying, cooling to violet
            foreach (double j in new[] { -0.6, 0.7 })
            {
                double h = (1.5 + 3 * (0.5 + 0.5 * Noise(t * 8 + j * 3.1 + i * 7, 4))) * k, sw = Noise(t * 4 + j * 2 + i * 3, 8) * 1.2;
                for (double d = 1; d <= h; d += 0.5)
                {
                    double u = d / h;
                    p.Dot(x + j + sw * u * u, y - d, u < 0.3 ? C[7] : u < 0.6 ? C[6] : u < 0.85 ? C[5] : C[4], u < 0.75 ? 1 : (1 - u) * 4);
                }
            }
            // the pupil: a hard white point in the dark
            p.Px(x, y, WHITE);
            p.Px(x - 1, y, C[6]);
            p.Glow(x, y, 8, C[5], 0.5 * k);
        }

        readonly uint[] C, K;
        readonly Rng R;
        readonly Actor A;
        readonly Particles ps;
        readonly Puffs puffs, dust;
        readonly Bits bits;
        readonly List<Lamp> lights = new List<Lamp>();
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        readonly double head;
        double t, acc;
        bool stamped, broke;

        Curse(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            K = Fit(o.Ramp("bone"), 5);
            R = new Rng(o.Seed);
            A = o.Actor;
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { WHITE, C[7], C[6], C[5] }, haze: C[4], emitter: e);
            puffs = new Puffs(R, new[] { WHITE, C[7], C[6], C[5], C[4], C[3], C[2], C[1], C[0] }, new[] { Mix(C[3], C[2], 0.5), Mix(C[1], C[0], 0.3) }, Mix(C[4], C[2], 0.5), new[] { C[6], C[5], C[4] }, floor, e);
            dust = new Puffs(R, new[] { WHITE, WHITE, WHITE, WHITE, WHITE, WHITE, WHITE, WHITE, WHITE }, new[] { Mix(K[2], K[1], 0.4), Mix(K[1], K[0], 0.5) }, K[3], new[] { K[4], K[3], K[2] }, floor, e);
            bits = new Bits(floor, e);
            head = A != null ? A.Y : -38;
        }

        void Light(double x, double y, double r, uint c, double k, double life) => lights.Add(new Lamp(x, y, r, c, k, life));

        public override void Update(double dt)
        {
            t += dt;
            // wisps and motes gather over the head, spiralling in
            if (t < STAMP - 0.05)
            {
                double k = t / STAMP;
                for (acc += dt * (40 + 60 * k); acc > 1; acc--)
                {
                    double a = R.Range(0, TAU), D = R.Range(22, 44);
                    ps.Spawn(P.Mote, Px.Cos(a) * D, SY + Px.Sin(a) * D * 0.8, -Px.Sin(a) * 60, Px.Cos(a) * 60, 0.8, R.Pick(new[] { WHITE, C[7], C[6], C[5] }), R.Range(0.6, 1));
                }
                if (R.Chance(dt * 14))
                {
                    double a = R.Range(0, TAU), D = R.Range(20, 34);
                    puffs.Spawn(PK.Soot, Px.Cos(a) * D, SY + Px.Sin(a) * D * 0.7, -Px.Sin(a) * 40, Px.Cos(a) * 30, R.Range(0.5, 0.7), R.Range(3, 5.5));
                }
                Attract(ps.List, 0, SY, 9e5 * (0.4 + k), 70, 0.6, 2.5, dt, q => q.k == P.Mote ? 1 : 0, 300);
                Attract(puffs.List, 0, SY, 5e5 * (0.4 + k), 60, 0.7, 3.5, dt, q => 1, 120);
            }
            if (t >= STAMP && !stamped)
            {
                stamped = true;
                ps.List.Clear();
                puffs.List.Clear();
                Light(0, SY, 64, C[5], 1, 0.35);
                Light(0, SY, 30, WHITE, 1, 0.1);
                Light(0, head + 12, 40, C[4], 0.6, 0.5);
                for (int i = 0; i < 22; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(80, 160);
                    ps.Spawn(P.Spark, Px.Cos(a) * 6, SY + Px.Sin(a) * 6, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.2, 0.5));
                }
                for (int i = 0; i < 6; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(20, 45);
                    puffs.Spawn(PK.Soot, Px.Cos(a) * 8, SY + Px.Sin(a) * 6, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.6 - 10, R.Range(0.6, 0.9), R.Range(2.5, 4.5));
                }
            }
            // it burns: embers and thin smoke off the eyes, a violet haze round it, the curse dripping onto the head
            if (stamped && !broke)
            {
                foreach (double[] eye in EYES)
                {
                    double ex = eye[0], ey = eye[1];
                    if (R.Chance(dt * 9)) ps.Spawn(P.Ember, ex, SY + ey - 3, R.Range(-10, 10), -R.Range(20, 45), R.Range(0.5, 0.9), C[6]);
                    if (R.Chance(dt * 7)) puffs.Spawn(PK.Soot, ex + R.Range(-1, 1), SY + ey - 3, R.Range(-5, 5), -R.Range(14, 26), R.Range(0.5, 0.8), R.Range(1.4, 2.4));
                }
                if (R.Chance(dt * 8)) ps.Spawn(P.Vapour, R.Range(-7, 7), SY + R.Range(-6, 6), R.Range(-12, 12), R.Range(-12, 4), R.Range(0.5, 0.8), R.Chance(0.5) ? C[3] : C[4], R.Range(5, 8));
                // the curse runs down from the skull into the head
                if (R.Chance(dt * 12)) ps.Spawn(P.Mote, R.Range(-4, 4), SY + 9, R.Range(-6, 6), R.Range(40, 70), R.Range(0.25, 0.4), R.Chance(0.5) ? C[6] : C[5], 0.8);
                if (R.Chance(dt * 10)) puffs.Spawn(PK.Soot, R.Range(-3.5, 3.5), SY + 9, R.Range(-3, 3), R.Range(14, 26), R.Range(0.45, 0.7), R.Range(1.2, 2.2));
                if (t > STAMP + 0.3 && R.Chance(dt * 5)) ps.Spawn(P.Star, R.Range(-1, 1) * (A != null ? A.W / 2.0 : 10), head + R.Range(4, A != null ? A.H - 4 : 30), 0, -6, R.Range(0.3, 0.5), C[6], R.Range(1, 2));
            }
            if (t >= BREAK && !broke)
            {
                broke = true;
                // the skull breaks: chunks of bone tumble and bounce, grit rains, dust and the last of the fire rise
                for (int i = 0; i < 24; i++)
                {
                    double x, y;
                    do
                    {
                        x = R.Range(-8.5, 8.5);
                        y = R.Range(-9, 9);
                    } while (!InSkull(Math.Floor(x + SW / 2.0), Math.Floor(y + SH / 2.0)));
                    bits.Add(new Bits.Bit { kind = B.Chunk, x = x, y = SY + y, vx = x * R.Range(2.5, 6) + R.Range(-12, 12), vy = R.Range(-70, 10), g = 0.85, drag = 0.4, bounce = 0.35, life = R.Range(0.8, 1.2), a = R.Range(0, TAU), va = R.Range(-14, 14), s = R.Range(1, 2.4), ramp = new[] { K[0], K[1], K[2], K[3], K[4] } });
                }
                for (int i = 0; i < 26; i++) ps.Spawn(P.Grit, R.Range(-7, 7), SY + R.Range(-8, 8), R.Range(-32, 32), R.Range(-60, 20), R.Range(0.8, 1.3), R.Pick(new[] { K[2], K[3], K[3], K[4] }), R.Chance(0.3) ? 2 : 1);
                for (int i = 0; i < 7; i++) dust.Spawn(PK.Soot, R.Range(-7, 7), SY + R.Range(-8, 6), R.Range(-25, 25), R.Range(-20, 5), R.Range(0.5, 0.8), R.Range(2, 4), R.Range(0, 0.1));
                foreach (double[] eye in EYES) puffs.Spawn(PK.Flame, eye[0], SY + eye[1], Sign(eye[0]) * 6, -30, 0.5, 2.6, 0, false);
                Light(0, SY, 40, C[5], 0.5, 0.25);
            }
            for (int i = lights.Count - 1; i >= 0; i--)
                if (!lights[i].Tick(dt)) lights.RemoveAt(i);
            // chunks that land kick up a little dust
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
                if (b.rest && dusted.Add(b))
                    dust.Spawn(PK.Soot, b.x, -1.5, R.Range(-12, 12), -R.Range(4, 10), R.Range(0.35, 0.6), R.Range(1.5, 2.5));
            ps.Update(dt);
            puffs.Update(dt);
            dust.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            puffs.Draw(p, PK.Soot, new PuffLight(0, SY, stamped && !broke ? 0.8 : 0.3, 50));
            dust.Draw(p, PK.Soot);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            // the curse floods down the body from the head, a bright front ahead of it, then ebbs
            double flood = Smooth(STAMP + 0.04, STAMP + 0.42, t), ebb = 1 - Smooth(BREAK + 0.15, BREAK + 0.75, t);
            if (A != null && flood > 0 && ebb > 0)
            {
                double k = ebb, pulse = 0.85 + 0.15 * Px.Sin(t * 9);
                for (int my = 0; my < A.H; my++)
                {
                    double u = (double)my / A.H;
                    if (u > flood + 0.06) break;
                    bool edge = flood < 1 && Math.Abs(u - flood) < 0.05, band = flood >= 1 && Math.Abs(u - (((t - STAMP) * 1.3) % 1.4) + 0.2) < 0.06;
                    for (int mx = 0; mx < A.W; mx++)
                    {
                        uint c0 = A.Pixels[my * A.W + mx];
                        if (c0 == 0) continue;
                        double l = Luma(c0);
                        uint cc = l > 0.72 ? C[6] : l > 0.5 ? C[5] : l > 0.33 ? C[4] : l > 0.18 ? C[3] : C[2];
                        p.Blend(A.X + mx, A.Y + my, edge ? C[7] : band ? C[6] : cc, edge ? 0.85 : band ? 0.7 * k : 0.6 * k * pulse);
                    }
                }
                // a crawling violet edge round the cursed body
                p.Outline(A.Mask, A.X, A.Y, (mx, my) => Px.Sin(my * 0.7 - t * 14 + mx * 0.3) > 0.2 ? C[6] : C[4], (mx, my) => (double)my / A.H <= flood ? k : 0);
                p.Glow(0, A.Y + A.H * 0.45, 30, C[4], 0.3 * k * pulse);
            }
            // the gathering: a dim swirl of light where the skull will be
            if (t < STAMP)
            {
                double q = t / STAMP;
                p.Glow(0, SY, 14 + 16 * q, C[5], 0.2 + 0.5 * q);
                if (q > 0.15) RingFast(p, 0, SY, 4 + 30 * (1 - Ease.InQuad(q)), 1, q > 0.7 ? C[5] : q > 0.4 ? C[4] : C[3]);
            }
            // the skull: it slams down from big and pale, squashes, bobs while it burns, shakes as it cracks
            if (t >= STAMP && t < BREAK)
            {
                double u = (t - STAMP) / 0.1, s = u < 1 ? 1.7 - 0.7 * Ease.InQuad(u) : 1, land = t - STAMP - 0.1;
                double sq = land > 0 ? 1 - 0.16 * Exp(-land * 18) * Px.Cos(land * 40) : 1;
                double shake = t > CRACK ? (t - CRACK) * 4 : 0, ox = Round(Px.Sin(t * 61) * shake), oy = land > 0 ? Round(Px.Sin(t * 3.4) * 1.2) : 0;
                Skull(p, C, K, s, sq, ox, oy, t < STAMP + 0.067, Smooth(CRACK, BREAK, t));
                double burn = Smooth(STAMP + 0.1, STAMP + 0.25, t);
                if (u >= 1)
                    for (int i = 0; i < EYES.Length; i++) EyeFire(p, C, ox + EYES[i][0], SY + oy + EYES[i][1], t, burn * (1 + 0.4 * Smooth(CRACK, BREAK, t)), i);
                if (u < 1.2) Sim.Sparkle(p, 0, SY, 14 * (1 - u / 1.2), C[7], C[5], 0.6);
                p.Glow(0, SY, 26, C[4], 0.25);
            }
            // the stamp's shock ring
            double ru = (t - STAMP - 0.08) / 0.3;
            if (ru > 0 && ru < 1) RingFast(p, 0, SY + 2, 10 + 26 * Ease.OutCubic(ru), Math.Max(1, 2.5 * (1 - ru)), (v, g) => v > 0.5 ? WHITE : C[6], 1.2 - ru);
            bits.Draw(p);
            puffs.Draw(p, PK.Flame);
            ps.Draw(p, Layer.Front);
            foreach (Lamp l in lights)
            {
                double q = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.6 + 0.4 * q), l.c, l.k * q * q);
            }
            // bone keeps its own colours under the glow (all but the stamp's white frame)
            if (t >= STAMP + 0.067 && t < BREAK)
                Unlight(p, -10, SY - 10, 10, SY + 10, (x, y) =>
                {
                    double u = Math.Floor(x + SW / 2.0), v = Math.Floor(y - SY + SH / 2.0);
                    return InSkull(u, v) && SKULL[(int)v][(int)u] != 'e';
                }, 0.2);
        }
    }
}
