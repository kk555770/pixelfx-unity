// The glancing star, the tracer that streaks off, the skid on the surface and the spark spray are adapted from
// Sprite Fusion's open-source impact (https://www.spritefusion.com/pixel-art-vfx/impact), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Ricochet : Effect
    {
        const double Duration = 1.1;

        public static readonly Module Module = new Module("ricochet", "pixelfx/fx/impacts/ricochet", 232, 84, 64, 60, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffe26b", "#fff3a6", "#ffffff" },
                ["stone"] = "#5b5079",
                ["seed"] = 8,
            },
            (o, e) => new Ricochet(o, e))
        {
            Title = "Ricochet",
            Desc = "A tracer streaks down onto the floor, glances off with a white-hot star and skips away at an angle, leaving a glowing skid while sparks skitter along the ground.",
            Tags = new[] { "impact", "ricochet", "bullet", "tracer", "sparks", "ground", "directional", "one-shot" },
            Bg = "ground",
        };

        const double HIT = 0.05, SPEED = 760, IN = 0.32, OUT = -0.21; // when it lands, how fast it flies, the angles in and out

        // a tracer: a white head, a hot body cooling to a dim tail, from (hx, hy) back along angle a for L px
        static void Tracer(Painter p, double hx, double hy, double a, double L, uint[] C, double k = 1)
        {
            double c = Px.Cos(a), s = Px.Sin(a);
            for (double d = 0; d <= L; d += 0.5)
            {
                double u = d / L;
                p.Dot(hx - c * d, hy - s * d, u < 0.12 ? WHITE : u < 0.45 ? C[5] : u < 0.75 ? C[4] : C[2], u < 0.45 ? 1 : k * (1.25 - u));
            }
            p.Glow(hx, hy, 9, C[4], 0.6 * k);
        }

        readonly uint[] C;
        readonly ImpactsLib.Stone st;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly ImpactsLib.Spike[] sp;
        double t;
        bool hit;

        Ricochet(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            st = new ImpactsLib.Stone(o.Color("stone"));
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[6], C[5], C[4], C[2] }, emitter: e);
            lights = new Lights(e);
            // the star leans hard along the way out, and barely digs into the floor
            sp = ImpactsLib.Spikes(R, 9, OUT, len0: 6, len1: 9, w0: 1.4, w1: 1.9, fwd: 2.6, back: 0.6);
            for (int i = 0; i < sp.Length; i++)
            {
                if (Px.Sin(sp[i].a) > 0.2) sp[i] = ImpactsLib.Stretched(sp[i], 0.3);
                if (Math.Abs(Px.Sin(sp[i].a - OUT)) > 0.6) sp[i] = ImpactsLib.Stretched(sp[i], 0.65);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(0, -2, 44, C[4], 0.65, 0.25);
                lights.Add(0, -1, 14, WHITE, 0.6, 0.06);
                // sparks skitter on along the floor, skipping, and a few spray higher
                for (int i = 0; i < 18; i++)
                {
                    bool low = i < 12;
                    double a = OUT + (low ? R.Range(-0.22, 0.12) : R.Range(-0.7, 0.25)), v = R.Range(260, 520) * (low ? 1 : 0.7);
                    ps.Spawn(P.Spark, R.Range(-1, 2), -1, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.15, 0.42) * (low ? 1.3 : 1), WHITE, 1, low ? R.Range(0, 0.05) : 0);
                }
                // grit kicked up and a scuff of dust
                uint[] grit = { st.light, st.body, st.lip };
                for (int i = 0; i < 7; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-0.2, 0.9), v = R.Range(60, 170);
                    ps.Spawn(P.Grit, R.Range(0, 4), -1, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.6, 0.95), R.Pick(grit), i < 2 ? 2 : 1);
                }
                for (int i = 0; i < 4; i++) ps.Spawn(P.Dust, R.Range(0, 8), -1, R.Range(20, 70), -R.Range(4, 16), R.Range(0.25, 0.45), Mix(st.dust, WHITE, R.Range(0.1, 0.3)), R.Range(1.2, 2));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the skid: a gouge along the floor that glows where the round dragged, cooling from its far end
            double u = t - HIT;
            if (u >= 0)
            {
                double heat = Px.Pow(Clamp(1 - u / 0.85), 1.2), fade = 1 - Smooth(Duration - 0.4, Duration - 0.05, t), L = 12 * Math.Min(1, u / 0.03);
                for (int x = -3; x <= L; x++)
                {
                    double q = (x + 3) / 15.0, h = heat * (1.15 - q * 0.6);
                    p.Dot(x, 0, h > 0.8 ? WHITE : h > 0.55 ? C[5] : h > 0.35 ? C[4] : h > 0.18 ? C[3] : h > 0.08 ? C[2] : ImpactsLib.SOOT, fade);
                    if (x > -2 && x < L - 2 && (x & 1) == 0) p.Dot(x, 1, h > 0.4 ? C[3] : ImpactsLib.SOOT, fade * 0.7);
                }
                if (heat > 0) p.GlowLine(-3, 0, L, 0, 5, C[3], 0.45 * heat);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            ps.Draw(p, Layer.Front);
            if (t < HIT)
            {
                // incoming: the tracer races down to the floor
                double d = (HIT - t) * SPEED;
                Tracer(p, -Px.Cos(IN) * d, -Px.Sin(IN) * d, IN, 22, C);
            }
            else
            {
                double u = t - HIT;
                // glancing off: a stretched star, then the round skips away, its tail stretching out behind it
                if (u < 0.03) ImpactsLib.Star(p, 0, -1, sp, 1, 1, new[] { C[2], C[5], C[4], WHITE }, 1, 2);
                else if (u < 0.06) ImpactsLib.Star(p, 0, -1, sp, 1.25, 0.55, new[] { C[1], C[4], C[3], C[5] }, 1, 2);
                if (u < 0.2)
                {
                    double head = 4 + u * SPEED, k = 1 - u / 0.2;
                    Tracer(p, Px.Cos(OUT) * head, -1 + Px.Sin(OUT) * head, OUT, Math.Min(head - 2, 18 + u * 200), C, k);
                }
                // a thin half ring hugging the floor, swelling and cooling
                double g = (u - 0.02) / 0.2;
                if (g >= 0 && g < 1)
                {
                    double r = 5 + 12 * (1 - Px.Pow(1 - g, 3));
                    uint c = g < 0.3 ? C[5] : g < 0.6 ? C[4] : C[3];
                    for (double j = 0, n = Math.Ceiling(r * Math.PI * 1.3); j <= n; j++)
                    {
                        double a = -Math.PI + j / n * Math.PI;
                        p.Dot(Px.Cos(a) * r, -1 + Px.Sin(a) * r, c, 1.15 - g);
                    }
                }
                double pop = Exp(-u / 0.045);
                if (pop > 0.02)
                {
                    p.Glow(0, -2, 22, C[3], 0.75 * pop);
                    p.Glow(0, -1, 8, WHITE, 0.5 * pop);
                }
            }
            lights.Draw(p);
        }
    }
}
