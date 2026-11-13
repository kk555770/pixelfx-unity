// The tongue, lick, flame-puff, smoke, ember and spark recipe is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Brazier : Effect
    {
        public static readonly Module Module = new Module("brazier", "pixelfx/fx/fire/brazier", 112, 148, 56, 136, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["iron"] = new[] { "#0b0910", "#1c1924", "#302c3a", "#4c4658", "#78708a" },
                ["seed"] = 8,
            },
            (o, e) => new Brazier(o))
        {
            Title = "Brazier",
            Desc = "An iron bowl on a tripod heaped with glowing coals and a big roaring fire, sparks and embers streaming up off it, light on the iron and the floor.",
            Tags = new[] { "fire", "brazier", "prop", "light", "sparks", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 3, RIM = -25, RX = 15, RY = 3;
        static readonly LoopNoise NL = NoiseFor(D);
        // the fire: [x, y from the rim, height share, half-width, back row?]
        static readonly double[][] TONGUES =
        {
            new[] { -7, -1.5, 0.5, 2.0, 1 },
            new[] { 7.2, -1.5, 0.55, 2.0, 1 },
            new[] { -3, -2.5, 0.84, 2.2, 1 },
            new[] { 3.3, -2.5, 0.86, 2.2, 1 },
            new[] { -11.5, 0.5, 0.2, 1.5, 0 },
            new[] { 11.5, 0.5, 0.22, 1.5, 0 },
            new[] { -8.3, 0, 0.34, 2.0, 0 },
            new[] { 8.5, 0, 0.38, 2.0, 0 },
            new[] { -4.7, -0.5, 0.6, 2.8, 0 },
            new[] { 4.9, -0.5, 0.64, 2.8, 0 },
            new[] { 0, -1, 1, 3.6, 0 },
        };

        // the tripod: two legs splayed out and one coming at us, curled feet, a ring holding them together
        static void Stand(Painter p, uint[] I, uint[] H, double fl)
        {
            void Leg(double x0, double y0, double x1, double y1)
            {
                p.Line(x0, y0, x1, y1, I[0]);
                p.Line(x0 + 1, y0, x1 + 1, y1, I[2]);
                p.Line(x0 + 2, y0, x1 + 2, y1, I[0]);
            }
            // the ring: its far half behind the front leg
            p.Ellipse(0, -9, 9, 1.8, (d, a) => d > 0.6 ? I[2] : 0);
            Leg(-6, -17, -12, -1);
            Leg(4, -17, 10, -1);
            p.Rect(-14, -1, 5, 1, I[2]);
            p.Rect(-15, 0, 6, 1, I[0]);
            p.Px(-15, -1, I[3]);
            p.Rect(9, -1, 5, 1, I[2]);
            p.Rect(9, 0, 6, 1, I[0]);
            p.Px(13, -1, I[3]);
            p.Rect(-1, -16, 3, 15, I[0]);
            p.Rect(0, -16, 1, 15, Mix(I[3], H[4], 0.15 * fl));
            p.Rect(-2, -1, 5, 1, I[2]);
            p.Rect(-2, 0, 5, 1, I[0]);
            p.Ellipse(0, -9, 9, 1.8, (d, a) => Px.Sin(a) > 0 && d > 0.55 ? (d > 0.85 ? I[0] : I[3]) : 0);
        }

        // the bowl, front half: a band of rivets under a rim lit by the fire, rounding down to a dark belly
        static void Bowl(Painter p, uint[] I, uint[] H, double fl)
        {
            p.Shade(-RX - 1, RIM - 1, RX + 1, RIM + 9, (x, y, o) =>
            {
                double v = (y - RIM) / 9, hw = RX * Math.Sqrt(Math.Max(0, 1 - v * v * 0.9)) - (v > 0.5 ? (v - 0.5) * 6 : 0);
                if (Math.Abs(x) > hw + 0.5 || y < RIM) return 0;
                if (Math.Abs(x) > hw - 0.5 || v > 0.92) return I[0];
                if (y < RIM + 1) return Mix(I[3], H[4], 0.5 * fl);
                if (y < RIM + 2) return I[2];
                if (y < RIM + 3) return Round(x) % 4 == 0 ? Mix(I[4], H[4], 0.25 * fl) : I[0];
                return x < -RX * 0.45 ? I[2] : x < RX * 0.25 ? I[1] : I[0];
            });
        }

        // coals heaped in the bowl, showing over the rim, breathing in the heat
        static void Coals(Painter p, uint[] I, uint[] H, double t, double S)
        {
            p.Shade(-RX, RIM - 6, RX, RIM + 1, (x, y, o) =>
            {
                // a mound: highest in the middle, standing proud of the rim
                double top = RIM - 1 - 4 * Math.Max(0, 1 - Px.Pow(x / (RX - 1), 2));
                if (y < top || y > RIM + 0.5 || Math.Abs(x) > RX - 0.5) return 0;
                double lump = Rand(Math.Floor((x + Math.Floor(y) * 1.5) / 3), Math.Floor(y / 2), S + 20);
                bool edge = y < top + 1;
                // the gaps between lumps stay dark; the lumps breathe, hottest deep in the heap
                if (Rand(Math.Floor(x), Math.Floor(y), S + 21) < 0.16 || (edge && lump < 0.3)) return I[0];
                double g = (0.5 + 0.5 * Px.Sin((t / D) * Math.PI * 4 + lump * 40)) * (edge ? 0.7 : 1);
                o.k = 0.3 * g;
                return g > 0.72 ? H[2] : g > 0.5 ? H[3] : g > 0.3 ? H[4] : g > 0.15 ? H[5] : H[6];
            });
        }

        readonly Options o;

        Brazier(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F), I = Fit(o.Ramp("iron"), 5);
            double S = o.Seed;
            double fl = 0.74 + 0.16 * NL(t, 7.3, S + 1) + 0.1 * NL(t, 19, S + 2);
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25);
            uint[] SPARKS = { H[1], H[2], H[4], H[5] }, EMB = { H[1], H[2], H[4], H[6] };
            FloorLight(p, 0, 0.5, 40, 7, GLOW, 0.32 * fl);
            Stand(p, I, H, fl);
            // the back of the rim, behind the fire
            p.Ellipse(0, RIM, RX, RY, (d, a) => Px.Sin(a) < 0 && d > 0.7 ? (d > 0.88 ? I[0] : Mix(I[3], H[4], 0.3 * fl)) : 0);
            Burn(p, t, TONGUES, new BurnOpts
            {
                H = H, D = D, NL = NL, S = S, x = 0, y = RIM, height = 32, lean = 0.1, spread = 7,
                puffs = 8, licks = 4, smoke = 14, smokeSize = 3, smokeLife = new[] { 1.6, 2.4 }, embers = 10, glowR = 58, glow = 1.4, wind = 3,
                mid = _ =>
                {
                    Coals(p, I, H, t, S);
                    Bowl(p, I, H, fl);
                },
            });
            // sparks streaming up in the draught: fast streaks that slow, wander and cool to embers high above
            Looped(t, D, 60, S + 600, (r, j) =>
            {
                double side = r.Range(-1, 1);
                return new Q { x = side * 8, y = RIM - r.Range(4, 18), vx = side * r.Range(10, 40), vy = -r.Range(70, 150), life = r.Range(0.7, 1.7), a = r.Range(0, 6.3), w = r.Range(1.8, 3.2), m = r.Range(3, 8), big = r.Chance(0.12) };
            }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, 0, -22, 1.9, 1.9);
                double k = 1 - age / q.life;
                // they bend and wander as the draught slows them
                double wob = q.m * (Px.Sin(age * q.w + q.a) - Px.Sin(q.a)) * Math.Min(1, age * 1.5), wv = q.m * q.w * Px.Cos(age * q.w + q.a) * Math.Min(1, age * 1.5);
                if (Hypot(d.vx + wv, d.vy) > 40) Spark(p, d.x + wob, d.y, d.vx + wv, d.vy, k, SPARKS);
                else Ember(p, d.x + wob, d.y, k, q.big ? 2 : 1, EMB, age * 40 + q.a);
            });
        }
    }
}
