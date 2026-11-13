// The tongue, lick, flame-puff, smoke and ember recipe is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Torch : Effect
    {
        public static readonly Module Module = new Module("torch", "pixelfx/fx/fire/torch", 72, 120, 36, 94, 2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["wood"] = new[] { "#0e080a", "#24130f", "#3b2216", "#5a3520", "#8a5a34", "#b9884f" },
                ["iron"] = new[] { "#0d0b12", "#1e1b26", "#363242", "#5b5566", "#8a8396" },
                ["seed"] = 4,
            },
            (o, e) => new Torch(o))
        {
            Title = "Torch",
            Desc = "A wall torch on an iron bracket: a licking flame leaping off a pitch-soaked wrap, licks and puffs torn off its tips, smoke, embers, a flickering light on the wall.",
            Tags = new[] { "fire", "torch", "prop", "light", "wall", "dungeon", "loop" },
            Bg = "dark",
        };

        const double D = 2, BASE = -16;
        static readonly LoopNoise NL = NoiseFor(D);
        // the flame: [x, y from the wrap's top, height share, half-width, back row?]
        static readonly double[][] TONGUES =
        {
            new[] { -1.9, -1, 0.74, 2.0, 1 },
            new[] { 2.1, -1, 0.7, 2.0, 1 },
            new[] { -3.4, 0.5, 0.42, 1.6, 0 },
            new[] { 3.6, 0.5, 0.46, 1.6, 0 },
            new[] { 0, 0, 1, 2.9, 0 },
        };

        // the bracket: a bevelled iron plate on the wall with two rivets, and a ring round the handle
        static void Plate(Painter p, uint[] I, uint[] H, double fl)
        {
            p.Shade(-5.5, -5.5, 5.5, 11.5, (x, y, o) =>
            {
                if (x < -5 || x > 5 || y < -5 || y > 11) return I[0];
                bool rv = Math.Abs(x) < 0.6 && (Math.Abs(y + 3.5) < 0.6 || Math.Abs(y - 9.5) < 0.6);
                if (rv) return Mix(I[4], H[3], 0.35 * fl);
                if (y < -4 || x < -4) return Mix(I[3], H[4], 0.3 * fl);
                if (y > 10 || x > 4) return I[1];
                return y < 2 ? I[2] : I[1];
            });
        }

        static void Handle(Painter p, uint[] W)
        {
            p.Shade(-3, -12, 3, 15, (x, y, o) =>
            {
                double half = 1.3 + Clamp((14 - y) / 26) * 0.9;
                if (Math.Abs(x) > half + 0.5 || y > 14.5) return 0;
                if (Math.Abs(x) > half - 0.5 || y > 13.5) return W[0];
                double g = Rand(Math.Floor(y / 3), Round(x), 9);
                return x < -0.3 ? (g < 0.3 ? W[2] : W[4]) : g < 0.4 ? W[1] : W[2];
            });
        }

        static void Ring(Painter p, uint[] I, uint[] H, double fl)
        {
            p.Rect(-3, -2, 7, 4, I[0]);
            p.Rect(-2, -1, 5, 1, Mix(I[4], H[3], 0.3 * fl));
            p.Rect(-2, 0, 5, 1, I[2]);
        }

        // the head: rags bound with cord and soaked in pitch, flaring into a cup at the top, charred where it burns
        // with embers breathing in the folds
        static void Wrap(Painter p, uint[] W, uint[] I, uint[] H, double t)
        {
            p.Shade(-6, BASE - 2, 6, -9, (x, y, o) =>
            {
                double v = (y - (BASE - 1.5)) / 7.5, half = 4.6 - v * 1.9;
                if (Math.Abs(x) > half + 0.5 || y < BASE - 1.5 || y > -9.5) return 0;
                if (Math.Abs(x) > half - 0.5 || y > -10.5) return W[0];
                bool cord = Math.Floor(y - x * 0.6 + 40) % 3 == 0;
                double h = Rand(Math.Floor(x), Math.Floor(y), 31);
                if (v < 0.45)
                {
                    if (h < 0.42)
                    {
                        double g = 0.5 + 0.5 * Px.Sin((t / D) * Math.PI * 4 + h * 40);
                        o.k = 0.3 * g;
                        return g > 0.66 ? H[2] : g > 0.33 ? H[4] : H[5];
                    }
                    return h < 0.75 ? W[0] : W[1];
                }
                if (cord) return I[2];
                return x < -1 ? W[3] : x < 1.5 ? W[2] : W[1];
            });
        }

        readonly Options o;

        Torch(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F), W = Fit(o.Ramp("wood"), 6), I = Fit(o.Ramp("iron"), 5);
            double S = o.Seed;
            double fl0 = 0.74 + 0.16 * NL(t, 7.3, S + 1) + 0.1 * NL(t, 19, S + 2);
            Plate(p, I, H, fl0);
            Handle(p, W);
            Ring(p, I, H, fl0);
            Burn(p, t, TONGUES, new BurnOpts
            {
                H = H, D = D, NL = NL, S = S, x = 0, y = BASE, height = 20, lean = 0.12, spread = 4,
                puffs = 10, licks = 5, smoke = 7, smokeSize = 2.3, smokeLife = new[] { 1.3, 2.1 }, embers = 7, glowR = 48, glow = 1.25, wind = 2,
                mid = _ => Wrap(p, W, I, H, t),
            });
        }
    }
}
