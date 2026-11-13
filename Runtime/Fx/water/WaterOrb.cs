// The orb's build (a see-through body with a lit rim, an inner current drawn as streaks on a tilted plane, motes
// on tilted orbits split behind and in front, a pulsing light) follows Sprite Fusion's open-source energy orb
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class WaterOrb : Effect
    {
        public static readonly Module Module = new Module("water-orb", "pixelfx/fx/water/water-orb", 100, 108, 50, 100, 3, true, 30,
            new Options { ["palette"] = WATER, ["radius"] = 19, ["seed"] = 2 },
            (o, e) => new WaterOrb(o, e))
        {
            Title = "Water orb",
            Desc = "A sphere of water floats and wobbles: a current circles inside it, the floor shows upside down through it, it shines, light gathers on the floor under it, and drips fall and plink.",
            Tags = new[] { "water", "orb", "sphere", "magic", "float", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 3, SY = 0.3, G = 700, DRIP = 1.5, LET = 0.5;
        static readonly double SQRT2 = Math.Sqrt(2);

        static double CenterY(double ph) => -52 + Px.Sin(ph * TAU) * 2;

        // the skin wobbles: radius at angle a, and the belly sags into a drip before each one falls
        static double Skin(double a, double R, double ph)
        {
            double sag = Px.Pow(Math.Max(0, Px.Sin(a)), 6) * 2.2 * Smooth(0.1, LET, ((ph * D) % DRIP) / DRIP) * (((ph * D) % DRIP) / DRIP < LET ? 1 : 0);
            return R + Px.Sin(a * 3 + ph * TAU * 2) * 0.6 + Px.Sin(a * 5 - ph * TAU * 3) * 0.35 + sag;
        }

        struct Mote
        {
            public double x, y, z;
            public int i;
        }

        // motes of spray on tilted orbits round it: position, and whether it's in front
        static Mote[] Motes(double ph, double R)
        {
            var m = new Mote[7];
            for (int i = 0; i < 7; i++)
            {
                double a = (i / 7.0) * TAU + ph * TAU * (i % 2 != 0 ? 1 : -1), r = R + 5 + (i % 3) * 3, tilt = (i - 3) * 0.22, x = Px.Cos(a) * r, z = Px.Sin(a);
                m[i] = new Mote { x = x * Px.Cos(tilt), y = x * Px.Sin(tilt) + z * r * 0.28, z = z, i = i };
            }
            return m;
        }

        readonly Options o;

        WaterOrb(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, R = o.Num("radius"), cx = 0, cy = CenterY(ph);
            // light gathers on the floor under it, the orb's caustic
            p.Glow(cx, 0, 22, C[5], 0.32);
            p.Shade(cx - 9, -2, cx + 9, 2, (x, y, @out) =>
            {
                double ex = x - cx, ey = y * 2.5, d = ex * ex / 64 + ey * ey / 9;
                if (d > 1) return 0;
                @out.c = d < 0.25 ? WHITE : d < 0.6 ? C[6] : C[5];
                @out.a = 1.2 - d;
                return 0;
            });
            foreach (Mote m in Motes(ph, R))
                if (m.z < 0) p.Px(cx + m.x, cy + m.y, C[4]);
            p.Glow(cx, cy, R * 3.2, C[4], 0.3 + 0.05 * Px.Sin(ph * TAU * 2));
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, R = o.Num("radius"), cx = 0, cy = CenterY(ph), T = ph * TAU;
            // the body: see-through water, deeper toward the bottom; the floor seen upside down through its lower half
            p.Shade(cx - R - 4, cy - R - 4, cx + R + 4, cy + R + 6, (x, y, @out) =>
            {
                double dx = x - cx, dy = y - cy, d = Hypot(dx, dy), a = Atan2(dy, dx), r = Skin(a, R, ph);
                if (d > r) return 0;
                double q = d / r, lit = (-dx - dy) / SQRT2 / Math.Max(0.5, d);
                if (q > 0.9)
                {
                    @out.c = lit > 0.35 ? WHITE : lit > -0.2 ? C[5] : C[3];
                    return 0;
                }
                // a lens of water: dark on the side the light comes from, the light gathering low on the far side inside
                double gather = Math.Max(0, (dx + dy) / SQRT2 / r) * q;
                p.Blend(x, y, gather > 0.45 ? C[4] : gather > 0.25 ? C[3] : q > 0.7 ? C[2] : C[1], gather > 0.25 ? 0.7 : 0.35 + 0.3 * q);
                return 0;
            });
            // the current inside: streaks circling on a tilted plane, bright where they pass in front
            for (int k = 0; k < 3; k++)
            {
                double rr = R * (0.38 + 0.2 * k), tilt = -0.5 + k * 0.35, spin = T * (k % 2 != 0 ? -2 : 3) + k * 2;
                for (double s = 0; s < 1; s += 0.012)
                {
                    double a = spin + s * TAU, x = Px.Cos(a) * rr, z = Px.Sin(a), y = x * Px.Sin(tilt) * 0.6 + z * rr * 0.62, dash = (s * 3 + k * 0.3) % 1;
                    if (dash > 0.62) continue;
                    p.Px(cx + x * Px.Cos(tilt), cy + y, z > 0 ? (dash < 0.1 ? WHITE : dash < 0.4 ? C[6] : C[5]) : C[3]);
                }
            }
            // bubbles drift up inside it
            Cycle(t, D, 8, o.Seed, g => g.Range(1, 1.8), (age, life, r, _) =>
            {
                double k = age / life, x = r.Range(-0.5, 0.5) * R + Px.Sin(age * 4 + r.Range(0, TAU)) * 1.5, y = R * 0.6 - k * R * 1.2;
                if (Hypot(x, y) > R * 0.8) return;
                p.Px(cx + x, cy + y, k < 0.8 ? C[6] : WHITE);
                if (r.Next() < 0.35) p.Px(cx + x + 1, cy + y, C[5]);
            });
            // the shine: a bright crescent up on the left, a small refracted glint low on the right
            p.Arc(cx, cy, R * 0.74, 1.8, Math.PI * 1.12, Math.PI * 1.4, (u, v) => u > 0.25 && u < 0.75 && v > 0.4 ? WHITE : C[6]);
            p.Px(cx - R * 0.45, cy - R * 0.62, WHITE);
            p.Px(cx + R * 0.48, cy + R * 0.5, C[6]);
            p.Px(cx + R * 0.48 + 1, cy + R * 0.5, C[5]);
            // motes in front
            foreach (Mote m in Motes(ph, R))
                if (m.z >= 0)
                {
                    p.Px(cx + m.x, cy + m.y, WHITE);
                    p.Px(cx + m.x - (m.i % 2 != 0 ? -1 : 1), cy + m.y, C[5]);
                    p.Glow(cx + m.x, cy + m.y, 4, C[5], 0.35);
                }
            // drips: one swells on its belly and lets go every DRIP seconds, landing with a plink and a ring
            double dt = (t % DRIP) / DRIP, fall = (t % DRIP) - DRIP * LET, y0 = cy + R + 3, land = Math.Sqrt((2 * -y0) / G);
            if (fall < 0 && dt > 0.2)
            {
                double s = Smooth(0.2, LET, dt);
                p.Circle(cx, cy + R + 1 + s * 1.5, 0.8 + s * 1.2, C[5]);
                p.Px(cx - 0.5, cy + R + s * 1.5, C[6]);
            }
            else if (fall >= 0 && fall < land) Bead(p, cx, y0 + 0.5 * G * fall * fall, 0, G * fall, 2, C);
            else if (fall >= land)
            {
                double since = fall - land;
                if (since < 0.08) p.Px(cx, 0, WHITE);
                Ring(p, cx, 0, 1.5 + since * 18, SY, 1.1 * (1 - since / 0.4), C, n: 1, half: 1, seed: 3, hot: since < 0.05);
            }
            // where the lens brings the light to a point, low on the far side: a bright fleck, pulsing
            double fx = cx + R * 0.36, fy = cy + R * 0.4, pulse = 0.8 + 0.2 * Px.Sin(T * 4);
            p.Circle(fx, fy, 1.6, C[6]);
            p.Px(fx, fy, WHITE);
            p.Glow(fx, fy, 9, C[5], 0.45 * pulse);
            p.Glow(cx, cy, R * 1.4, C[5], 0.22);
            p.Glow(cx - R * 0.4, cy - R * 0.5, 6, WHITE, 0.25);
        }
    }
}
