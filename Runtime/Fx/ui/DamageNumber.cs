using System;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class DamageNumber : Effect
    {
        const double DURATION = 1.05;

        public static readonly Module Module = new Module("damage-number", "pixelfx/fx/ui/damage-number", 76, 68, 30, 52, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0508", "#4a0b12", "#8c1420", "#d42a2e", "#ff5a4a", "#ff9a86", "#ffd8cc", "#ffffff" },
                ["text"] = "-27",
                ["seed"] = 2,
            },
            (o, e) => new DamageNumber(o, e))
        {
            Title = "Damage number",
            Desc = "A red -27 punches out of a hit spark, white for a frame, arcs up, lands with a squash and a little hop, then fades as it lifts away.",
            Tags = new[] { "ui", "damage", "number", "combat", "text", "hit", "one-shot" },
            Bg = "dark",
        };

        const double LAND = -8, FADE = 0.84;

        readonly uint[] C;
        readonly Rng R;
        readonly Word word;
        readonly Particles ps;
        readonly Lights lights;
        readonly double[][] spikes;
        // the number is a little body: thrown up and out, it lands, hops once and settles
        double nx = 0, ny = LAND, nvx = 34, nvy = -175, nsq = 0;
        int hops = 0;
        double t;
        bool hit;

        DamageNumber(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            word = Spell(Convert.ToString(o["text"], System.Globalization.CultureInfo.InvariantCulture));
            ps = new Particles(R, sparks: new[] { C[7], C[6], C[4], C[2] }, emitter: e);
            lights = new Lights(e);
            spikes = new double[9][];
            for (int i = 0; i < 9; i++)
            {
                double a = ((double)i / 9) * TAU + R.Range(-0.2, 0.2);
                spikes[i] = new[] { a, R.Range(8, 14) };
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!hit)
            {
                hit = true;
                lights.Add(0, -4, 30, C[5], 0.7, 0.25);
                lights.Add(0, -4, 14, WHITE, 0.6, 0.08);
                for (int i = 0; i < 9; i++)
                {
                    double an = R.Range(-Math.PI * 0.95, -0.05) + (i % 2 != 0 ? Math.PI * 0.08 : 0), sp = R.Range(90, 200);
                    ps.Spawn(P.Spark, 0, -4, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.1, 0.25));
                }
            }
            if (hops < 2)
            {
                nvy += 900 * dt;
                nvx -= nvx * 2 * dt;
                nx += nvx * dt;
                ny += nvy * dt;
                if (ny >= LAND && nvy > 0)
                {
                    ny = LAND;
                    nsq = 1;
                    hops++;
                    nvy = hops < 2 ? -nvy * 0.36 : 0;
                    if (hops == 1)
                        foreach (int s in new[] { -1, 1 })
                            ps.Spawn(P.Star, nx + s * 14, LAND + R.Range(-2, 1), s * R.Range(15, 35), -R.Range(5, 20), R.Range(0.2, 0.35), C[6], R.Range(0.5, 1.2));
                }
            }
            nsq = Math.Max(0, nsq - dt / 0.09);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            // the hit: two frames of a spiky white-hot star, then a glint and a ring where the blow landed
            if (t < 0.07)
            {
                bool big = t < 0.035;
                for (int i = 0; i < spikes.Length; i++)
                {
                    double an = spikes[i][0], L = spikes[i][1];
                    double l = L * (big ? 1 : 1.3), ca = Px.Cos(an), sa = Px.Sin(an);
                    if (big)
                    {
                        // a fat spike: a red rim either side of a white-hot core that narrows to the tip
                        p.Line(-sa, -4 + ca, ca * l * 0.7 - sa, -4 + sa * l * 0.7 + ca, C[4]);
                        p.Line(sa, -4 - ca, ca * l * 0.7 + sa, -4 + sa * l * 0.7 - ca, C[4]);
                    }
                    p.Line(ca * (big ? 0 : l * 0.45), -4 + sa * (big ? 0 : l * 0.45), ca * l, -4 + sa * l, big ? WHITE : i % 2 != 0 ? C[6] : C[5]);
                }
                if (big) p.Circle(0, -4, 3.2, WHITE);
            }
            else if (t < 0.16) Sim.Sparkle(p, 0, -4, 5 * (1 - (t - 0.07) / 0.09), WHITE, C[5], 0.6);
            double rk = t / 0.22;
            if (rk < 1) p.Ring(0, -4, 3 + 10 * Ease.OutCubic(rk), Math.Max(1, 2 * (1 - rk)), rk < 0.4 ? WHITE : C[5], 1.2 - rk);
            ps.Draw(p);
            // the number: punches out big and white, springs to size, squashes as it lands, then lifts and fades
            double outK = Seg(t, FADE, DURATION - 0.04);
            if (outK < 1)
            {
                double s = 2 + 1.4 * Exp(-t * 22) * Px.Cos(t * 30), sx = s, sy = s;
                if (nsq > 0)
                {
                    sx = s * (1 + 0.18 * nsq);
                    sy = s * (1 - 0.2 * nsq);
                }
                else if (hops < 2 && Math.Abs(nvy) > 120)
                {
                    sx = s * 0.92;
                    sy = s * 1.1;
                }
                double W = word.W * sx, H = 7 * sy, x0 = nx - W / 2, y0 = ny - H - 6 * Ease.InQuad(outK);
                double flash = t < 0.035 ? 1 : t < 0.07 ? 0.4 : 0;
                var items = word.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = x0 + L.X * sx, Y = y0, Sx = sx, Sy = sy, Flash = flash, A = 1 - outK }).ToList();
                DrawWord(p, items, new Style { Fill = new[] { C[3], C[3], C[4], C[5] }, Edge = C[6], Under = C[2], Outline = C[0], Shadow = Scale(C[0], 0.5) });
                p.Glow(nx, y0 + H / 2, 18, C[4], 0.3 * (1 - outK) + (t < 0.1 ? 0.4 * (1 - t / 0.1) : 0));
            }
            lights.Draw(p);
        }
    }
}
