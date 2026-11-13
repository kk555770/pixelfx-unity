// The flame tongues, embers and thin smoke are
// adapted from Sprite Fusion's open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Burning : Effect
    {
        static readonly Pose A_ = new Pose { ["turn"] = 1, ["crouch"] = 2, ["lean"] = 2, ["fx"] = -13, ["fy"] = -38, ["bx"] = 12, ["by"] = -17, ["ffx"] = -3, ["ffy"] = -7, ["fbx"] = 5 };
        static readonly Pose B_ = new Pose { ["turn"] = 0.9, ["crouch"] = 3, ["lean"] = -2, ["fx"] = -12, ["fy"] = -17, ["bx"] = 13, ["by"] = -38, ["ffx"] = -4, ["fbx"] = 4, ["fby"] = -7 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, A_), new PoseKey(0.15, B_, "snap"), new PoseKey(0.3, A_, "snap"), new PoseKey(0.45, B_, "snap"), new PoseKey(0.6, A_, "snap"),
                new PoseKey(0.75, B_, "snap"), new PoseKey(0.9, A_, "snap"), new PoseKey(1.05, B_, "snap"), new PoseKey(1.2, A_, "snap"),
            },
        };

        const double D = 1.2, TICK = 0.6, WIND = -14;
        static double Since(double t) => ((t % TICK) + TICK) % TICK;
        static double Flare(double t) => Exp(-Since(t) / 0.12);

        public static readonly Module Module = new Module("burning", "pixelfx/fx/status/burning", 72, 80, 36, 70, 1.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["char"] = new[] { "#120c0a", "#2a1a14", "#4a2c1c" },
                ["smoke"] = "#5b5566",
                ["seed"] = 3,
            },
            (o, e) => new Burning(o), act: Act, actor: 2,
            // the stand-in is hidden for the one white frame of each tick (the effect draws the body then)
            actorAlpha: (t, o) => Since(t) < 0.034 ? 0 : 1)
        {
            Title = "Burning",
            Desc = "Burning over time: small flames lick from four hot spots, trailing sparks and thin smoke; every 0.6 s they flare, the body flashes white then orange, scorch marks spread.",
            Tags = new[] { "status", "fire", "burn", "dot", "debuff", "loop" },
            Bg = "ground",
        };

        sealed class TongueSpec
        {
            public double dx, h, n;
        }

        sealed class Spot
        {
            public int[] at;
            public int side, n;
            public double h, x, y;
            public TongueSpec[] tongues;
        }

        sealed class Scorch
        {
            public double x, y, r;
        }

        sealed class Spots
        {
            public Spot[] list;
            public Scorch[][] scorch;
        }

        // the hot spots on the outline: the hair tips, a shoulder, the back, a sleeve; scorch spots near them per tick
        static readonly ConditionalWeakTable<Mask, Dictionary<double, Spots>> SPOTS = new ConditionalWeakTable<Mask, Dictionary<double, Spots>>();

        static Spots SpotsOf(Actor a, double seed)
        {
            Dictionary<double, Spots> s = SPOTS.GetValue(a.Mask, m => new Dictionary<double, Spots>());
            if (s.TryGetValue(seed, out Spots got)) return got;
            StatusShape S = Shape(a);
            int H = S.H, W = S.W;
            var R = new Rng(seed * 13 + 5);
            int[] At(double q, int side)
            {
                int my = RoundInt(H * q);
                int[] lr = S.Rows[my];
                return new[] { side < 0 ? lr[0] : lr[1], my };
            }
            List<int[]> hairs = StableSort(S.Up.FindAll(b => b[0] < W * 0.42), (p, q) =>
            {
                double d = p[1] - q[1];
                return d != 0 ? d : p[0] - q[0];
            });
            int[] hair = hairs.Count > 0 ? hairs[0] : null;
            List<int[]> shoulders = StableSort(S.Up.FindAll(b => b[0] > W / 2.0 && b[1] > H * 0.4 && b[1] < H * 0.62), (p, q) => p[1] - q[1]);
            int[] shoulder = shoulders.Count > 0 ? shoulders[0] : At(0.5, 1);
            var specs = new[]
            {
                new Spot { at = hair, side = -1, n = 3, h = 4.5 },
                new Spot { at = shoulder, side = 1, n = 2, h = 4 },
                new Spot { at = At(0.62, -1), side = -1, n = 2, h = 3.5 },
                new Spot { at = At(0.6, 1), side = 1, n = 2, h = 3.5 },
            };
            for (int i = 0; i < specs.Length; i++)
            {
                Spot sp = specs[i];
                sp.x = a.X + sp.at[0] + 0.5;
                sp.y = a.Y + sp.at[1] + 1.5;
                sp.tongues = new TongueSpec[sp.n];
                for (int j = 0; j < sp.n; j++)
                {
                    var tg = new TongueSpec();
                    tg.dx = (j - (sp.n - 1) / 2.0) * 1.6 + R.Range(-0.4, 0.4);
                    tg.h = sp.h * R.Range(0.8, 1.2);
                    tg.n = i * 17 + j * 5 + seed;
                    sp.tongues[j] = tg;
                }
            }
            Spot[] list = specs;
            // scorch marks: each tick chars two patches on the clothes near the spots
            var scorch = new Scorch[2][];
            for (int tick = 0; tick < 2; tick++)
            {
                scorch[tick] = new Scorch[2];
                for (int k = 0; k < 2; k++)
                {
                    // on the clothes (mid-tones, where char shows), near a hot spot
                    Spot sp = list[(tick * 2 + k + 1) % list.Length];
                    double Lum(int x, int y) => Luma(ColorAt(a, x, y));
                    List<int[]> cand = S.Body.FindAll(b => Hypot(a.X + b[0] + 0.5 - sp.x, a.Y + b[1] + 0.5 - sp.y) < 9 && b[1] > H * 0.42 && Lum(b[0], b[1]) > 0.3 && Lum(b[0], b[1]) < 0.6);
                    if (cand.Count == 0) cand = S.Body.FindAll(b => b[1] > H * 0.45);
                    int[] xy = cand[(int)Math.Floor(R.Next() * cand.Count)];
                    scorch[tick][k] = new Scorch { x = a.X + xy[0] + 0.5, y = a.Y + xy[1] + 0.5, r = R.Range(3.2, 4.4) };
                }
            }
            var out_ = new Spots { list = list, scorch = scorch };
            s[seed] = out_;
            return out_;
        }

        readonly Options o;

        Burning(Options o) => this.o = o;

        static uint[] Fire(Options o)
        {
            uint[] F = Fit(o.Ramp("palette"), 9);
            Array.Reverse(F);
            return F;
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] F = Fire(o);
            Actor a = o.Actor;
            double fl = Flare(t), flick = 0.8 + 0.2 * Tnoise(t, D, 7.5, 3);
            // warm flicker on the floor
            p.Shade(-30, -3, 30, 7, (x, y, out_) =>
            {
                double d = Hypot(x / 26, (y - 0.5) / 4.5);
                if (d < 1)
                {
                    out_.l = Mix(F[3], F[4], 0.5);
                    out_.k = (0.16 + 0.22 * fl) * flick * (1 - d) * (1 - d);
                }
                return 0;
            });
            // thin smoke drifting up and away from each spot
            uint smokeC = Mix(o.Color("smoke"), F[6], 0.2);
            Spot[] list = SpotsOf(a, o.Seed).list;
            Cycle(t, D, 16, o.Seed + 4, r => r.Range(0.8, 1.2), (age, life, r, i) =>
            {
                Spot sp = list[i % list.Length];
                double x = sp.x + Travel(WIND * 0.6, WIND * 0.5, 0.8, age) + Px.Sin(age * 4 + i) * 1.2, y = sp.y - 3 - Travel(18, 6, 1, age);
                Smoke(p, x, y, r.Range(1.1, 1.9), age / life, smokeC, 0.9);
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fire(o), CH = Fit(o.Ramp("char"), 3);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            Spots spots = SpotsOf(a, o.Seed);
            Spot[] list = spots.list;
            double ph = Since(t), fl = Flare(t);
            int tick = (int)(Math.Floor(t / TICK) % 2);
            // the tick: the body's own pixels flash white for a frame, then orange, fading
            if (ph < 0.034)
                foreach (int[] b in S.Body)
                    p.Px(a.X + b[0], a.Y + b[1], WHITE);
            else if (ph < 0.22)
            {
                double k = 1 - (ph - 0.034) / 0.186;
                foreach (int[] b in S.Body) p.Blend(a.X + b[0], a.Y + b[1], F[4], 0.55 * k);
            }
            // scorch marks spreading over the clothes from this tick, ember-edged, cooling and fading by the next
            foreach (Scorch sc in spots.scorch[tick])
            {
                double grow = Clamp(ph / 0.14), fade = 1 - Clamp((ph - 0.25) / 0.33), r = sc.r * (0.4 + 0.6 * grow);
                if (fade <= 0) continue;
                foreach (int[] b in S.Body)
                {
                    int mx = b[0], my = b[1];
                    double x = a.X + mx + 0.5, y = a.Y + my + 0.5, d = Hypot(x - sc.x, (y - sc.y) * 1.2) + Rand(mx, my, 7) * 0.9;
                    if (d > r) continue;
                    bool edge = d > r - 1;
                    if (edge) p.Dot(x - 0.5, y - 0.5, Rand(mx, my, Math.Floor(t * 20)) < 0.5 ? F[3] : F[4], fade * 1.2);
                    else p.Dot(x - 0.5, y - 0.5, d < r * 0.5 ? CH[0] : CH[1], fade * 1.1);
                }
                p.Glow(sc.x, sc.y, 6, F[4], 0.3 * fade);
            }
            // the hot spots: little tongues licking up, flaring on the tick
            foreach (Spot sp in list)
            {
                foreach (TongueSpec tg in sp.tongues)
                {
                    double h = tg.h * (0.85 + 0.25 * Tnoise(t, D, 5, tg.n)) * (1 + 1.6 * fl);
                    Tongue(p, sp.x + tg.dx, sp.y, h, 1.1 + 0.3 * fl, WIND * 0.08 * h / 4 + Tnoise(t, D, 3, tg.n + 9) * 0.8, F, t, D, tg.n);
                }
                p.Glow(sp.x, sp.y - 2, 9 + 6 * fl, F[4], 0.22 + 0.35 * fl);
            }
            // sparks streaming up and away
            Cycle(t, D, 20, o.Seed + 6, r => r.Range(0.5, 0.9), (age, life, r, i) =>
            {
                Spot sp = list[i % list.Length];
                double x = sp.x + r.Range(-1.5, 1.5) + Travel(WIND + r.Range(-10, 6), WIND * 0.6, 1.2, age) + Px.Sin(age * 9 + i) * 1.2 * age;
                double y = sp.y - 2 + Travel(-r.Range(25, 55), -18, 1.4, age);
                Ember(p, x, y, age / life, new[] { F[1], F[3], F[5] }, age + i);
            });
            // the tick throws a burst of sparks off every spot
            if (ph < 0.3)
                for (int k = 0; k < list.Length; k++)
                {
                    Spot sp = list[k];
                    for (int j = 0; j < 4; j++)
                    {
                        double an = -Math.PI / 2 + (Rand(k, j, tick) - 0.5) * 2.2, v = 50 + 50 * Rand(k, j, 2), x = sp.x + Px.Cos(an) * Travel(v, 0, 3, ph), y = sp.y - 2 + Px.Sin(an) * Travel(v, 0, 3, ph) + 60 * ph * ph;
                        p.Px(x, y, ph < 0.12 ? F[1] : F[3]);
                        p.Light(x, y, F[3], 0.4 * (1 - ph / 0.3));
                    }
                }
        }
    }
}
