using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Poisoned : Effect
    {
        static readonly Pose SICK = new Pose { ["turn"] = 0.8, ["crouch"] = 4, ["lean"] = 4, ["hy"] = 1, ["fx"] = 2, ["fy"] = -20, ["bx"] = 8, ["by"] = -14, ["ffx"] = 3, ["fbx"] = -3 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, new Pose(SICK) { ["x"] = -1, ["crouch"] = 5, ["lean"] = 5 }),
                new PoseKey(0.2, new Pose(SICK) { ["x"] = 0 }),
                new PoseKey(0.5, new Pose(SICK) { ["x"] = 1 }),
                new PoseKey(0.8, new Pose(SICK) { ["x"] = 1, ["crouch"] = 5, ["lean"] = 5 }),
                new PoseKey(1, new Pose(SICK) { ["x"] = 0 }),
                new PoseKey(1.3, new Pose(SICK) { ["x"] = -1 }),
                new PoseKey(1.6, new Pose(SICK) { ["x"] = -1, ["crouch"] = 5, ["lean"] = 5 }),
            },
        };

        public static readonly Module Module = new Module("poisoned", "pixelfx/fx/status/poisoned", 72, 64, 36, 54, 1.6, true, 30,
            new Options { ["palette"] = new[] { "#0e1d14", "#173320", "#23552e", "#318232", "#55b92f", "#8eea3f", "#cfff63", "#f6ffc2" }, ["veins"] = "#2c1a3a", ["seed"] = 5 },
            (o, e) => new Poisoned(o), act: Act, actor: 2,
            // the effect draws the whole (sick, swaying) body, so the stand-in is hidden throughout
            actorAlpha: (t, o) => 0)
        {
            Title = "Poisoned",
            Desc = "Sick to the bone: green rolls up the body in waves, dark veins creep from a wound, toxic sweat drips and hisses; every 0.8 s it wobbles, blisters pop and a ring pulses out.",
            Tags = new[] { "status", "poison", "toxic", "dot", "debuff", "sick", "loop" },
            Bg = "ground",
        };

        const double D = 1.6, TICK = 0.8, G = 760;
        static double Since(double t) => ((t % TICK) + TICK) % TICK;

        sealed class Blister
        {
            public int x, y;
            public double r;
        }

        sealed class BodyPlan
        {
            public List<List<double[]>> veins;
            public int[] wound;
            public Blister[][] blisters;
            public List<int[]> sweat;
        }

        // worked out once per character: the veins (walks out from a wound on the forearm), blister spots, sweat spots
        static readonly ConditionalWeakTable<Mask, Dictionary<double, BodyPlan>> BODIES = new ConditionalWeakTable<Mask, Dictionary<double, BodyPlan>>();

        static BodyPlan BodyOf(Actor a, double seed)
        {
            Dictionary<double, BodyPlan> m = BODIES.GetValue(a.Mask, k => new Dictionary<double, BodyPlan>());
            if (m.TryGetValue(seed, out BodyPlan got)) return got;
            StatusShape S = Shape(a);
            int H = S.H, W = S.W;
            var R = new Rng(seed * 7 + 3);
            int[] lr = S.Rows[RoundInt(H * 0.6)];
            int l = lr[0], r = lr[1], wx = r - 1, wy = RoundInt(H * 0.6);
            var veins = new List<List<double[]>>();
            void Walk(double x, double y, double ang, double len, int depth)
            {
                var pts = new List<double[]>();
                for (int s = 0; s < len; s++)
                {
                    ang += R.Range(-0.5, 0.5);
                    x += Px.Cos(ang);
                    y += Px.Sin(ang);
                    if (!S.On(Round(x), Round(y))) break;
                    pts.Add(new[] { Round(x), Round(y), s / len });
                    if (depth < 2 && s > 2 && R.Chance(0.16)) Walk(x, y, ang + R.Sign() * R.Range(0.6, 1.1), len * 0.55, depth + 1);
                }
                veins.Add(pts);
            }
            for (int i = 0; i < 4; i++) Walk(wx, wy, Math.PI + (i / 4.0 - 0.45) * 2.2, 11, 0);
            double Lum(int x, int y) => Luma(ColorAt(a, x, y));
            List<int[]> skin = S.Body.FindAll(b => Lum(b[0], b[1]) > 0.55 && S.On(b[0] - 1, b[1]) && S.On(b[0] + 1, b[1]));
            int[] Pick(List<int[]> list) => list[(int)Math.Floor(R.Next() * list.Count)];
            var blisters = new Blister[2][];
            for (int i = 0; i < 2; i++)
            {
                blisters[i] = new Blister[3];
                for (int j = 0; j < 3; j++)
                {
                    int[] xy = Pick(S.Body.FindAll(b => b[1] > 4 && S.On(b[0] - 1, b[1]) && S.On(b[0] + 1, b[1]) && S.On(b[0], b[1] - 1)));
                    blisters[i][j] = new Blister { x = xy[0], y = xy[1], r = R.Range(1.1, 1.8) };
                }
            }
            List<StatusEye> E = Eyes(a);
            var sweat = new List<int[]>();
            foreach (StatusEye e in E) sweat.Add(new[] { e.X0 < W / 2.0 ? e.X0 - 1 : e.X1 + 1, e.Y0 - 1 });
            foreach (int side in new[] { -1, 1 })
            {
                List<int[]> hand = S.Body.FindAll(b => b[1] > H * 0.5 && b[1] < H * 0.7 && (side < 0 ? b[0] <= l + 1 : b[0] >= r - 1) && !S.On(b[0], b[1] + 1));
                if (hand.Count > 0) sweat.Add(hand[0]);
            }
            if (sweat.Count == 0) sweat.Add(Pick(skin.Count > 0 ? skin : S.Body));
            var out_ = new BodyPlan { veins = veins, wound = new[] { wx, wy }, blisters = blisters, sweat = sweat };
            m[seed] = out_;
            return out_;
        }

        // the sway: the body leans from the feet, the head moving most; on the tick, every row wobbles a pixel
        static Func<double, double> Offsets(double t, double H)
        {
            double lean = 1.3 * Px.Sin((t / D) * TAU), ph = Since(t);
            bool wob = ph < 0.12;
            double w = wob ? Px.Sin((ph / 0.12) * Math.PI) : 0;
            return my => Round(lean * (1 - my / H) + w * Px.Sin(my * 0.42 + ph * 40));
        }

        static bool Far(double g) => Px.Sin(g) < 0;
        static bool Near(double g) => Px.Sin(g) >= 0;

        readonly Options o;

        Poisoned(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] P = Fit(o.Ramp("palette"), 8);
            double ph = Since(t);
            // a green ring pulses out across the floor on the tick (far half), the floor glows sick
            double k = ph / 0.55;
            if (k < 1) Sim.Band(p, 0, 0, 5 + 22 * Math.Sqrt(k), 0.18, Math.Max(1, 2.5 * (1 - k)), 1 - k, new[] { P[3], P[4], P[5], P[6] }, Far);
            p.Shade(-28, -3, 28, 6, (x, y, out_) =>
            {
                double d = Hypot(x / 24, (y - 0.5) / 4);
                if (d < 1)
                {
                    out_.l = P[4];
                    out_.k = (0.14 + 0.2 * Exp(-ph / 0.2)) * (1 - d) * (1 - d);
                }
                return 0;
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] P = Fit(o.Ramp("palette"), 8);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            int H = S.H;
            BodyPlan B = BodyOf(a, o.Seed);
            Func<double, double> off = Offsets(t, H);
            double ph = Since(t);
            int tick = (int)(Math.Floor(t / TICK) % 2);
            uint vein = o.Color("veins");
            // the body, swaying, its own colours sickened by green that rolls up it in waves
            foreach (int[] b in S.Body)
            {
                int mx = b[0], my = b[1];
                uint c = ColorAt(a, mx, my);
                double l = Luma(c), wave = 0.5 + 0.5 * Px.Sin(TAU * (((double)my / H) * 1.6 + (t / D) * 2));
                uint sick = P[(int)Clamp(1 + Round(l * 6.2), 1, 7)];
                double k = 0.22 + 0.4 * wave * wave;
                p.Px(a.X + mx + off(my), a.Y + my, Mix(c, wave > 0.85 && l > 0.4 ? Mix(sick, P[6], 0.3) : sick, k));
            }
            // veins creeping out from the wound and drawing back
            double grow = Smooth(0, D * 0.55, t) * (1 - Smooth(D * 0.62, D, t)) * 1.15;
            foreach (List<double[]> pts in B.veins)
                foreach (double[] v in pts)
                {
                    double x = v[0], y = v[1], u = v[2];
                    if (u < grow) p.Px(a.X + x + off(y), a.Y + y, u > grow - 0.12 ? P[2] : vein);
                }
            int wx = B.wound[0], wy = B.wound[1];
            p.Px(a.X + wx + off(wy), a.Y + wy, P[6]);
            p.Px(a.X + wx + off(wy) - 1, a.Y + wy, P[3]);
            p.Glow(a.X + wx, a.Y + wy, 6, P[5], 0.3);
            // blisters: on the tick they swell up out of the skin, shine, and pop
            foreach (Blister bl in B.blisters[tick])
            {
                double x = a.X + bl.x + off(bl.y), y = a.Y + bl.y, grow2 = Clamp(ph / 0.16);
                bool pop = ph > 0.38;
                if (!pop)
                {
                    double r = bl.r * grow2;
                    p.Circle(x, y, r + 0.6, P[3]);
                    p.Circle(x - 0.2, y - 0.2, r, P[5]);
                    if (r > 0.8) p.Px(x - 0.5, y - 0.8, WHITE);
                    p.Glow(x, y, 4, P[5], 0.3 * grow2);
                }
                else if (ph < 0.56)
                {
                    double k = (ph - 0.38) / 0.18;
                    for (int j = 0; j < 6; j++)
                    {
                        double an = (j / 6.0) * TAU + bl.x, d = bl.r + 0.5 + k * 3.5;
                        p.Dot(x + Px.Cos(an) * d, y + Px.Sin(an) * d - k * 2, j % 2 != 0 ? P[6] : P[5], 1.2 - k);
                    }
                    p.Glow(x, y, 6, P[6], 0.4 * (1 - k));
                }
            }
            // toxic sweat: beads gather, run, fall and hiss on the floor
            for (int i = 0; i < B.sweat.Count; i++)
            {
                int sx = B.sweat[i][0], sy = B.sweat[i][1];
                double per = D / 2, age = (((t - i * 0.37) % per) + per) % per, gather = 0.35;
                double x = a.X + sx + 0.5 + off(sy), y0 = a.Y + sy;
                if (age < gather)
                {
                    double k = age / gather, run = Round(k * 2);
                    p.Px(x, y0 + run, k > 0.5 ? P[6] : P[5]);
                    if (k > 0.5) p.Px(x, y0 + run - 1, P[4]);
                    continue;
                }
                double f = age - gather, fallH = -1 - (y0 + 2), tf = Math.Sqrt((2 * Math.Max(1, fallH)) / G);
                if (f < tf)
                {
                    double y = y0 + 2 + 0.5 * G * f * f;
                    p.Line(x, y - Math.Min(3, G * f * 0.012), x, y, P[4]);
                    p.Px(x, y, P[6]);
                    continue;
                }
                double h = f - tf;
                if (h > 0.4) continue;
                // the hiss: a green fleck spreads, a wisp of vapour curls up, sizzle sparks
                double kk = h / 0.4;
                p.Line(x - 1 - kk * 2, -1, x + 1 + kk * 2, -1, P[kk < 0.4 ? 6 : 4], 1.2 - kk);
                for (int j = 0; j < 3; j++) p.Dot(x + (j - 1) * (2 + 3 * kk), -2 - j % 2 - 5 * kk * (1 - kk), P[6], 1 - kk);
                p.Disc(x + Px.Sin(kk * 6) * 1.5, -3 - kk * 6, 1 + 1.5 * kk, P[5], 0.35 * (1 - kk));
                p.Glow(x, -1.5, 5, P[5], 0.35 * (1 - kk));
            }
            // the near half of the floor ring
            double k2 = ph / 0.55;
            if (k2 < 1) Sim.Band(p, 0, 0, 5 + 22 * Math.Sqrt(k2), 0.18, Math.Max(1, 2.5 * (1 - k2)), 1 - k2, new[] { P[3], P[4], P[5], P[6] }, Near);
            // a sick light on the body, surging with the tick
            p.Glow(0, a.Y + H * 0.45, 26, P[4], 0.14 + 0.2 * Exp(-ph / 0.18));
        }
    }
}
