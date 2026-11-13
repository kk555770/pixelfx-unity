// The hover, floor shadow and twinkle glints follow Sprite Fusion's open-source pickup and sparkle
// (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class GemSpin : Effect
    {
        const double DURATION = 2.4, HOVER = 19;

        public static readonly Module Module = new Module("gem-spin", "pixelfx/fx/pickups/gem-spin", 64, 64, 32, 56, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#16040e", "#3e0a1e", "#760e2e", "#b8163c", "#ec3250", "#ff7482", "#ffc4c8", "#ffffff" },
                ["seed"] = 6,
            },
            (o, e) => new GemSpin(o, e))
        {
            Title = "Gem spin",
            Desc = "A brilliant-cut ruby turning on its axis: real facets catch the light in turn, flashes roll over the crown, refracted light slides across the floor, sparkles wink round it.",
            Tags = new[] { "pickup", "gem", "jewel", "treasure", "ruby", "loop" },
            Bg = "ground",
        };

        static readonly Solid GEM = Brilliant(r: 7.5, crown: 3.2, table: 0.5, pavilion: 9.5, girdle: 0.9);

        readonly ScratchLayer L = new ScratchLayer(32, 32);
        readonly Options o;

        GemSpin(Options o, Emitter e) => this.o = o;

        // where the gem is: hovering, one slow bob a loop, turning four facets a loop
        static (double y, double spin) Pose(double t, double T) => (-HOVER - 1.8 * Px.Sin(t / T * TAU), t / T * Math.PI);

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double T = DURATION;
            var (y, spin) = Pose(t, T);
            double lift = (-y - HOVER + 2) / 4;
            BlobShadow(p, 0, 8 - lift * 1.5, 0.75 - lift * 0.2);
            // light refracted through the stone, sliding over the floor as it turns
            for (int k = 0; k < 3; k++)
            {
                double a = spin * 4 + k * TAU / 3, x = Px.Sin(a) * 9, k2 = 0.5 + 0.5 * Px.Cos(a);
                if (k2 < 0.15) continue;
                p.Dot(x, 0, k2 > 0.7 ? C[6] : C[5], k2 * 1.2);
                p.Dot(x + 1, 0, C[4], k2 * 0.8);
                p.Dot(x - 1, 0, C[4], k2 * 0.6);
                p.Glow(x + 0.5, 0, 6, C[5], 0.3 * k2);
            }
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double T = DURATION, seed = o.Seed;
            var (y, spin) = Pose(t, T);
            // its light: a wide soft halo and a hot heart
            p.GlowRing(0, y - 1, 5, 15, C[4], 0.34);
            p.Glow(0, y, 26, C[3], 0.14);
            DrawGem(L, p, GEM, 0, y, spin, C, tilt: 0.22);
            // a star flares where a facet swings square to the light, twice a loop
            double f = spin * 4 / TAU % 1, fk = Math.Max(0, 1 - Math.Abs(f - 0.5) / 0.08);
            if (fk > 0) Glint(p, -3, y - 3, 3.4 * Ease.OutQuad(fk), C[6], 0.45 * fk);
            // sparkles wink on and off round it, one after another
            for (int i = 0; i < 8; i++)
            {
                double life = 0.4, age = ((t - i / 8.0 * T - Rand(i, seed, 3) * 0.1) % T + T) % T;
                if (age > life) continue;
                double a = Rand(i, seed, 1) * TAU + i * 2.4, d = 10 + Rand(i, seed, 2) * 7, k = Px.Sin(age / life * Math.PI);
                Glint(p, Px.Cos(a) * d, y + Px.Sin(a) * d * 0.8 - age * 6, Round(2.4 * k * (0.6 + 0.4 * Rand(i, 9))), i % 3 != 0 ? C[6] : WHITE, 0.4 * k);
            }
        }
    }
}
