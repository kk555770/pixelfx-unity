// The hover, floor shadow and glints follow Sprite Fusion's open-source pickup
// (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class CoinSpin : Effect
    {
        const double DURATION = 1.6, R = 7, TH = 3, HOVER = 17;

        public static readonly Module Module = new Module("coin-spin", "pixelfx/fx/pickups/coin-spin", 56, 56, 28, 48, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1e1206", "#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff" },
                ["seed"] = 3,
            },
            (o, e) => new CoinSpin(o, e))
        {
            Title = "Coin spin",
            Desc = "A thick gold coin turning on its edge: an embossed star face, a reeded rim showing as it goes edge-on, a flash sweeping the face, bobbing over its shadow.",
            Tags = new[] { "pickup", "coin", "gold", "money", "loop" },
            Bg = "ground",
        };

        readonly ScratchLayer L = new ScratchLayer(32, 32);
        readonly Options o;

        CoinSpin(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            double T = DURATION, ph = t / T, th = ph * TAU, y = -HOVER - 2 * Px.Sin(ph * TAU);
            // the shadow narrows as the coin turns edge-on and thins as it rises
            BlobShadow(p, 0, 2.5 + 4.5 * Math.Abs(Px.Cos(th)) + 1.5 * Math.Abs(Px.Sin(th)), 0.85 - (-y - HOVER + 2) * 0.06);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double T = DURATION, ph = t / T, th = ph * TAU, y = -HOVER - 2 * Px.Sin(ph * TAU), seed = o.Seed;
            // warm light round it, swelling when the face catches the light
            double facing = Math.Abs(Px.Cos(th));
            p.Glow(0, y, 20, C[4], 0.3 + 0.12 * facing);
            double flash = Coin3d(L, p, 0, y, R, TH, th, C);
            p.Glow(-1, y - 1, 12, C[6], 0.35 * flash);
            // a star glint where the flash leaves the rim
            if (flash > 0.3) Glint(p, -4, y - 4, 1 + 3.2 * flash, C[6], 0.5 * flash);
            // little twinkles round it, one after another
            for (int i = 0; i < 5; i++)
            {
                double life = 0.35, age = ((t - i / 5.0 * T - Rand(i, seed, 3) * 0.08) % T + T) % T;
                if (age > life) continue;
                double a = Rand(i, seed, 1) * TAU + i * 2.1, d = 10 + Rand(i, seed, 2) * 5, k = Px.Sin(age / life * Math.PI);
                Glint(p, Px.Cos(a) * d, y + Px.Sin(a) * d * 0.85 - age * 8, Round(2 * k), i % 2 != 0 ? C[6] : WHITE, 0.35 * k);
            }
        }
    }
}
