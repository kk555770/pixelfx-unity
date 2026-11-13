using System;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class Sunburst : Effect
    {
        const double T = 4;

        public static readonly Module Module = new Module("sunburst", "pixelfx/fx/holy/sunburst", 128, 128, 64, 64, T, true, 30,
            new Options { ["palette"] = GOLD, ["rays"] = 8, ["seed"] = 3 },
            (o, e) => new Sunburst(o, e))
        {
            Title = "Sunburst",
            Desc = "A white-hot core in a gold corona; long and short rays turn slowly round it, breathing, with glints running out along them that wink at the tips.",
            Tags = new[] { "holy", "sun", "radiant", "rays", "light", "glow", "loop" },
            Bg = "dark",
        };

        const double CORE = 4.5, RING = 7.5, R0 = 9;
        static double Frac(double x) => x - Math.Floor(x);

        // one tapered ray from radius R0 out to R0 + L at angle a: white at the root, cooling to gold, thinning into light
        static void Ray(Painter p, uint[] C, double a, double L, double w0, double k)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a), x1 = ca * (R0 + L), y1 = sa * (R0 + L), pad = w0 + 1;
            p.Shade(Math.Min(0, x1) - pad, Math.Min(0, y1) - pad, Math.Max(0, x1) + pad, Math.Max(0, y1) + pad, (x, y, o) =>
            {
                double along = x * ca + y * sa - R0, u = along / L;
                if (u < 0 || u > 1) return 0;
                double across = Math.Abs(-x * sa + y * ca), w = (w0 / 2) * (1 - u * 0.85) + 0.25;
                if (across > w) return 0;
                bool spine = across < 0.5, rim = across > w - 0.6 && w > 0.9;
                o.c = spine ? (u < 0.4 ? WHITE : u < 0.75 ? C[6] : C[5]) : rim && u > 0.15 ? C[3] : u < 0.3 ? C[6] : u < 0.62 ? C[5] : C[4];
                o.a = u < 0.72 ? k : k * (1 - (u - 0.72) / 0.28) * 1.2;
                return 0;
            });
            p.GlowLine(ca * R0, sa * R0, ca * (R0 + L * 0.8), sa * (R0 + L * 0.8), 5, C[5], 0.16 * k);
        }

        readonly Options o;

        Sunburst(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double N = o.Num("rays") * 2, rot = (t / T) * ((2 * TAU) / N);
            p.Glow(0, 0, 62, C[4], 0.3 + 0.06 * Px.Sin((t / T) * TAU * 2));
            // fine threads of light between the rays
            for (int i = 0; i < N; i++)
            {
                double a = rot + ((i + 0.5) * TAU) / N, L = 14 + 6 * Px.Sin(a * 3 + (t / T) * TAU);
                p.Line(Px.Cos(a) * (R0 + 1), Px.Sin(a) * (R0 + 1), Px.Cos(a) * (R0 + L), Px.Sin(a) * (R0 + L), u => u < 0.3 ? C[6] : C[5], u => 0.7 - u * 0.8);
            }
            // the rays, long and short by turns, breathing as a wave runs round them
            for (int i = 0; i < N; i++)
            {
                double a = rot + (i * TAU) / N;
                bool lng = i % 2 == 0;
                double breathe = 0.86 + 0.14 * Px.Sin(a * 3 - (t / T) * TAU * 2);
                double L = (lng ? 36 : 21) * breathe;
                Ray(p, C, a, L, lng ? 4 : 2.6, 1);
                // a glint runs out along each long ray and winks at the tip
                if (lng)
                {
                    double g = Frac((t / T) * 2 + (a * 3) / TAU), r = R0 + g * L;
                    if (g < 0.9)
                    {
                        p.Px(Px.Cos(a) * r, Px.Sin(a) * r, WHITE);
                        p.Glow(Px.Cos(a) * r, Px.Sin(a) * r, 5, C[6], 0.45 * (1 - g));
                    }
                    else Sim.Sparkle(p, Px.Cos(a) * (R0 + L), Px.Sin(a) * (R0 + L), 1 + 3 * Px.Sin(((g - 0.9) / 0.1) * Math.PI), WHITE, C[6], 0.5);
                }
            }
            // the corona and the core
            p.Shade(-RING - 2, -RING - 2, RING + 2, RING + 2, (x, y, oo) =>
            {
                double d = Hypot(x, y);
                if (d > RING) return 0;
                double flick = 0.5 + 0.5 * Px.Sin(Atan2(y, x) * 5 + (t / T) * TAU * 3);
                oo.c = d < CORE ? WHITE : d < CORE + 1.5 ? (flick > 0.6 ? WHITE : C[6]) : flick > 0.5 ? C[5] : C[4];
                return 0;
            });
            p.Glow(0, 0, 16, WHITE, 0.28);
            // twinkles round it
            Cycle(t, T, 10, o.Seed, g => g.Range(0.35, 0.7), (age, life, R, i) =>
            {
                double a = R.Range(0, TAU), d = R.Range(16, 44), k = Px.Sin((age / life) * Math.PI);
                Sim.Sparkle(p, Px.Cos(a) * d, Px.Sin(a) * d, 1 + 2.4 * k * R.Range(0.5, 1), WHITE, C[5], 0.4 * k);
            });
        }
    }
}
