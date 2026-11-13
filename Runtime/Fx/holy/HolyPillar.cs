using System;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class HolyPillar : Effect
    {
        public static readonly Module Module = new Module("holy-pillar", "pixelfx/fx/holy/holy-pillar", 192, 232, 96, 204, 3.5, false, 30,
            new Options { ["palette"] = GOLD, ["shafts"] = 7, ["seed"] = 3 },
            (o, e) => new HolyPillar(o, e))
        {
            Title = "Holy pillar",
            Desc = "Light breaks through from above: a sun opens at the top and a fan of shafts converges on the mark, pulsing, while a sigil burns on the floor; then the fan folds shut.",
            Tags = new[] { "holy", "pillar", "light", "rays", "aoe", "sigil", "ground", "one-shot" },
            Bg = "ground",
        };

        const double TOP = -204, PY = -1, SUN = TOP + 38, SY = 0.24, RX = 26;
        // the sun opens, the fan unfolds and pours down, it holds and pulses, folds shut, the light lifts away
        const double OPEN = 0.3, HIT = 0.66, FOLD = 2.05, SHUT = 2.45, LIFT = 2.75;
        static double Frac(double x) => x - Math.Floor(x);

        sealed class Shaft
        {
            public double th, spread, b, ph, far;
        }

        sealed class Mote
        {
            public double x, y, v, amp, f, ph;
        }

        readonly uint[] C, WING;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Feathers feathers;
        readonly Shaft[] shafts;
        readonly Mote[] motes;
        readonly double DMAX;
        readonly float[] row = new float[192];
        readonly byte[] core = new byte[192];
        double t;
        bool hit, shut, glinted;

        HolyPillar(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            int N = Math.Max(3, ToInt32(o.Num("shafts")));
            R = new Rng(o.Seed);
            double mid = (N - 1) / 2.0;
            WING = new[] { C[2], C[3], C[4], C[5], C[6], WHITE };
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
            feathers = new Feathers(R, 0);
            // the shafts: spread angle, how fast each widens, how bright, its pulse offset
            shafts = new Shaft[N];
            for (int i = 0; i < N; i++)
            {
                double off = i - mid, far = Math.Abs(off) / mid;
                shafts[i] = new Shaft { th = off * (0.4 / mid) + R.Range(-0.02, 0.02), spread = R.Range(0.05, 0.085) * (1 - far * 0.25), b = (1 - far * 0.35) * R.Range(0.8, 1), ph = R.Range(0, 1), far = far };
            }
            motes = new Mote[72];
            for (int i = 0; i < 72; i++) motes[i] = new Mote { x = R.Range(-80, 80), y = R.Range(0, 1), v = R.Range(7, 18), amp = R.Range(0.5, 2.5), f = R.Range(0.6, 1.5), ph = R.Range(0, TAU) };
            for (int i = 0; i < 6; i++)
                feathers.Add(new Feathers.Spec
                {
                    x = R.Range(-46, 46), y = R.Range(-150, -100), vx = R.Range(-8, 8), vy = R.Range(0, 8), a = R.Range(-0.4, 0.4), len = R.Range(7, 9), wid = R.Range(1.9, 2.3),
                    life = R.Range(1.4, 1.7), sink = R.Range(26, 36), back = i % 3 == 0, delay = HIT + 0.1 + i * 0.16 + R.Range(0, 0.08),
                });
            DMAX = PY - TOP;
        }

        // each shaft's angle now: unfolding from the middle with an overshoot, folding back shut at the end
        static double Angle(Shaft s, double tt)
        {
            double st = s.far * 0.1, open = Ease.OutBack(Seg(tt, OPEN + st, OPEN + st + 0.42), 1.5), fold = Ease.InOutCubic(Seg(tt, FOLD - s.far * 0.1, SHUT));
            return s.th * open * (1 - fold);
        }

        // how far down each shaft the light reaches (it pours from the sky), and where it lifts away at the end
        double FrontD(Shaft s, double tt) => DMAX * (1 - Ease.InQuad(Seg(tt, OPEN + s.far * 0.06, HIT + s.far * 0.06)));
        double LiftD(double tt) => DMAX * Ease.InQuad(Seg(tt, SHUT + 0.05, LIFT));
        static double Fade(double tt) => Smooth(OPEN, OPEN + 0.1, tt) * (1 - Smooth(LIFT - 0.05, LIFT + 0.05, tt));
        static double Width(Shaft s, double d, double tt) => (1.4 + d * s.spread) * Lerp(1, 0.25, Smooth(SHUT, LIFT, tt));

        // brightness along a shaft: a pulse of light runs down each one, one after another
        double Pulse(Shaft s, double d, double tt)
        {
            if (tt < HIT || tt > SHUT) return 1;
            double D = DMAX * (1 - Frac((tt - HIT) / 0.6 + s.ph));
            return 1 + 0.9 * Exp(-Px.Pow((d - D) / 12, 2));
        }

        // light of all the shafts at a point (for motes)
        double LightAt(double x, double y)
        {
            double dy = PY - y;
            if (dy <= 0) return 0;
            double I = 0;
            foreach (Shaft s in shafts)
            {
                double a = Angle(s, t), d = dy / Px.Cos(a), xc = Px.Tan(a) * dy, hw = Width(s, d, t) / 2 / Px.Cos(a);
                if (d < LiftD(t) || d < FrontD(s, t)) continue;
                double v = (x - xc) / hw;
                if (Math.Abs(v) < 1) I += (1 - v * v) * s.b;
            }
            return I * Fade(t);
        }

        void Fan(Painter p)
        {
            double k = Fade(t);
            if (k <= 0) return;
            double lift = LiftD(t);
            for (double y = Math.Ceiling(TOP); y < PY; y++)
            {
                double dy = PY - (y + 0.5);
                Array.Clear(row, 0, row.Length);
                Array.Clear(core, 0, core.Length);
                bool any = false;
                foreach (Shaft s in shafts)
                {
                    double a = Angle(s, t), ca = Px.Cos(a), d = dy / ca;
                    if (d < lift || d < FrontD(s, t) - 6) continue;
                    double xc = Px.Tan(a) * dy, hw = Width(s, d, t) / 2 / ca, soft = Smooth(FrontD(s, t) - 6, FrontD(s, t) + 4, d) * Smooth(lift, lift + 6, d);
                    double I0 = s.b * Pulse(s, d, t) * soft * k;
                    for (double x = Math.Floor(xc - hw); x <= Math.Ceiling(xc + hw); x++)
                    {
                        double v = (x + 0.5 - xc) / hw, i = x + 96;
                        if (i < 0 || i >= 192 || Math.Abs(v) >= 1) continue;
                        row[(int)i] = (float)(row[(int)i] + I0 * Px.Pow(1 - v * v, 1.3));
                        any = true;
                    }
                    double ci = Round(xc - 0.5) + 96;
                    if (ci >= 0 && ci < 192) core[(int)ci] = (byte)(ToInt32(Math.Max(core[(int)ci], Math.Min(255, I0 * 200))) & 255);
                }
                if (!any) continue;
                for (int i = 0; i < 192; i++)
                {
                    double I = row[i];
                    if (I < 0.02) continue;
                    double x = i - 96;
                    p.Blend(x, y, C[6], Math.Min(0.6, I * 0.38));
                    p.Light(x, y, C[4], Math.Min(0.9, I * 0.32));
                    // a fine bright line runs down the middle of each shaft
                    if (core[i] != 0) p.Dot(x, y, core[i] > 150 ? WHITE : C[6], Math.Min(1, core[i] / 180.0));
                }
            }
            // where the shafts converge, the light gathers to a point
            if (t > HIT - 0.05) p.Glow(0, PY - 3, 20, C[5], 0.5 * k);
        }

        // the sun: a halo that opens at the top like an iris, rays ticking round it, then closes with a glint
        void Sun(Painter p)
        {
            double r = 15 * Ease.OutBack(Seg(t, 0.02, 0.45), 1.8) * (1 - Ease.InBack(Seg(t, SHUT, LIFT + 0.1), 1.5));
            if (r > 0.5)
            {
                double spin = t * 0.6;
                p.Glow(0, SUN, Math.Min(32, r * 3), C[4], 0.5);
                p.Glow(0, SUN, r * 1.4, WHITE, 0.35);
                p.Circle(0, SUN, r * 0.45, WHITE);
                Oval(p, 0, SUN, r * 0.62, r * 0.62, C[6], 1);
                Oval(p, 0, SUN, r, r, g => Px.Sin(g * 6 + spin * 4) > 0.5 ? WHITE : C[5], 1);
                for (int j = 0; j < 16; j++)
                {
                    double g = spin + (j * TAU) / 16, l = j % 2 != 0 ? 3 : 6;
                    p.Line(Px.Cos(g) * (r + 2), SUN + Px.Sin(g) * (r + 2), Px.Cos(g) * (r + 2 + l), SUN + Px.Sin(g) * (r + 2 + l), u => u < 0.4 ? C[6] : C[4], u => 1.1 - u);
                }
            }
            if (t > LIFT && t < LIFT + 0.3) Sim.Sparkle(p, 0, SUN, 10 * (1 - (t - LIFT) / 0.3), WHITE, C[5], 0.6);
        }

        // the sigil burning on the floor: two rings and a turning band of marks; white-hot, then gold, then cooling
        void Sigil(Painter p, bool back)
        {
            if (t < HIT) return;
            double u = Seg(t, HIT, HIT + 0.2), heat = 1 - Smooth(SHUT, SHUT + 0.8, t), outK = 1 - Smooth(SHUT + 0.4, SHUT + 1, t);
            if (outK <= 0) return;
            Func<double, bool> keep = back ? (Func<double, bool>)(g => Px.Sin(g) < 0) : g => Px.Sin(g) >= 0;
            // it burns in round from the front, both ways
            bool Burnt(double g) => Math.Abs(((g - Math.PI / 2 + Math.PI) % TAU + TAU) % TAU - Math.PI) <= u * Math.PI + 0.01;
            uint Col(double g, bool hot)
            {
                if (!Burnt(g)) return 0;
                bool flick = Rand(Round(g * 40), Math.Floor(t * 20)) < 0.18;
                return t < HIT + 0.08 || (flick && heat > 0.4) ? WHITE : heat > 0.6 ? (hot ? C[6] : C[5]) : heat > 0.3 ? C[4] : heat > 0.1 ? C[3] : C[2];
            }
            Oval(p, 0, 0, RX, RX * SY, g => Col(g, true), outK, g => keep(g) && Burnt(g));
            Oval(p, 0, 0, RX * 0.72, RX * 0.72 * SY, g => Col(g, false), outK, g => keep(g) && Burnt(g));
            // marks between the rings, turning slowly
            for (int j = 0; j < 12; j++)
            {
                double g = t * 0.35 + (j * TAU) / 12;
                if (!keep(g) || !Burnt(g)) continue;
                uint c = Col(g, j % 2 == 0);
                double r0 = RX * 0.78, r1 = RX * (j % 3 == 0 ? 0.95 : 0.88);
                if (c != 0) p.Line(Px.Cos(g) * r0, Px.Sin(g) * r0 * SY, Px.Cos(g) * r1, Px.Sin(g) * r1 * SY, c, outK);
            }
            if (!back)
            {
                for (int i = -2; i <= 2; i++) p.Glow(i * RX * 0.45, -2, 16, C[4], (0.34 - Math.Abs(i) * 0.05) * outK * (0.5 + 0.5 * heat));
                p.Glow(0, -1, 10, WHITE, 0.3 * heat * outK);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // light gathers to the sun as it opens
            if (t < OPEN + 0.1 && R.Chance(0.5))
            {
                double a = R.Range(0, TAU), D = R.Range(22, 34), x = Px.Cos(a) * D, y = SUN + Px.Sin(a) * D, T = R.Range(0.18, 0.28), v = ((D - 10) * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, x, y, (-x / D) * v, ((SUN - y) / D) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(0, -30, 64, C[5], 0.85, 0.5);
                lights.Add(0, -6, 30, WHITE, 0.8, 0.12);
                for (int i = 0; i < 26; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(70, 190);
                    ps.Spawn(P.Spark, R.Range(-3, 3), -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.6));
                }
            }
            // while it holds: embers off the burning sigil, a few feathers drifting down through the shafts
            if (t > HIT && t < SHUT + 0.1 && R.Chance(0.35))
            {
                double g = R.Range(0, TAU), r = RX * (R.Chance(0.5) ? 1 : 0.72);
                ps.Spawn(P.Ember, Px.Cos(g) * r, Px.Sin(g) * r * SY - 1, R.Range(-6, 6), -R.Range(12, 30), R.Range(0.5, 0.8), C[5], 1, 0, Px.Sin(g) < 0 ? PF.Back : PF.None);
            }
            if (t > HIT && t < FOLD && R.Chance(0.05)) ps.Spawn(P.Star, R.Range(-1, 1) * 40, -R.Range(20, 160), 0, R.Range(2, 8), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 1.8));
            // the fan snaps shut on the mark: a flash where the shafts become one
            if (!shut && t >= SHUT)
            {
                shut = true;
                lights.Add(0, -60, 70, C[5], 0.7, 0.35);
                for (int i = 0; i < 12; i++) ps.Spawn(P.Star, R.Range(-4, 4), -R.Range(10, 170), R.Range(-25, 25), R.Range(-10, 10), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 2));
            }
            if (!glinted && t >= LIFT)
            {
                glinted = true;
                lights.Add(0, SUN, 40, WHITE, 0.6, 0.2);
            }
            ps.Update(dt);
            feathers.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            Fan(p);
            Sigil(p, true);
            feathers.Draw(p, WING, "back");
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            Sigil(p, false);
            // dust motes drifting down, lit only where a shaft catches them
            foreach (Mote m in motes)
            {
                double y = TOP + ((((m.y * DMAX + t * m.v) % DMAX) + DMAX) % DMAX), x = m.x + Px.Sin(t * m.f + m.ph) * m.amp, I = LightAt(x, y);
                if (I < 0.2) continue;
                p.Dot(x, y, I > 0.7 ? WHITE : C[6], Math.Min(1, I * 1.3));
                p.Light(x, y, C[5], 0.4 * Math.Min(1, I));
            }
            Sun(p);
            if (t > HIT - 0.02 && t < HIT + 0.25) Sim.Sparkle(p, 0, PY - 2, 12 * (1 - (t - HIT) / 0.25), WHITE, C[5], 0.7);
            if (t > SHUT && t < SHUT + 0.2) Sim.Sparkle(p, 0, PY - 2, 8 * (1 - (t - SHUT) / 0.2), WHITE, C[5], 0.5);
            feathers.Draw(p, WING, "front");
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
