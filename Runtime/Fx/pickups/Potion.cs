// The bubbles (lit and shaded rims, a highlight, a dotted pop) follow Sprite Fusion's open-source bubbles
// (https://www.spritefusion.com/pixel-art-vfx/bubbles), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class Potion : Effect
    {
        const double DURATION = 2.4, RB = 7, HOVER = 17, NECK = 2, LIP = -12, CORK_TOP = -16;
        static readonly uint INK = Hex("#120d1a"), GLASS = Hex("#2a2236");
        static readonly uint[] CORK = Ramp("#2a160c", "#4e2c16", "#7a4a26", "#a8703a", "#d09a5a");

        public static readonly Module Module = new Module("potion", "pixelfx/fx/pickups/potion", 56, 72, 28, 64, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0516", "#3d0a33", "#701560", "#b0228f", "#f53cd1", "#ff86e6", "#ffd2f6", "#ffffff" },
                ["seed"] = 5,
            },
            (o, e) => new Potion(o, e))
        {
            Title = "Potion",
            Desc = "A round glass flask of glowing brew rocks in the air: the brew sloshes behind the rock, bubbles rise and pop, light glows through the glass, wisps curl off the cork.",
            Tags = new[] { "pickup", "potion", "flask", "brew", "magic", "loop" },
            Bg = "ground",
        };

        readonly ScratchLayer L = new ScratchLayer(40, 44);
        readonly Options o;

        Potion(Options o, Emitter e) => this.o = o;

        // the flask hovers and rocks; the brew's surface stays level in the world and lags behind the rock
        static (double y, double tilt) Rock(double ph) => (-HOVER - 1.4 * Px.Sin(ph * TAU), 0.13 * Px.Sin(ph * TAU + 0.4));

        static double Surface(double x, double ph) => -0.5 + 0.17 * Px.Sin(ph * TAU - 0.9) * x + 0.45 * Px.Sin(x * 0.85 + ph * TAU * 3);

        void Flask(Painter p, uint[] C, double t, double ph, double cy, double tilt, double seed)
        {
            double ct = Px.Cos(tilt), st = Px.Sin(tilt);
            L.Begin(-20, Math.Floor(cy) - 22);
            L.Fill(-20, Math.Floor(cy) - 22, 19, Math.Floor(cy) + 12, (px, py) =>
            {
                double wx = px, wy = py - cy, fx = wx * ct + wy * st, fy = -wx * st + wy * ct, d = Hypot(fx, fy);
                // the cork, the lip, the neck and the bulb, in the flask's own frame
                if (fy < LIP - 0.5 && fy >= CORK_TOP && Math.Abs(fx) <= NECK + 0.2) return CORK[fy < CORK_TOP + 1 ? 4 : fx < -0.6 ? 3 : fx > 1 ? 1 : 2];
                if (fy >= LIP - 0.5 && fy < LIP + 1 && Math.Abs(fx) <= NECK + 1.4) return fx < 0 ? Mix(WHITE, C[6], 0.4) : Mix(GLASS, C[5], 0.45);
                bool inNeck = fy >= LIP + 1 && fy < -RB + 2 && Math.Abs(fx) <= NECK + 0.6;
                if (d > RB && !inNeck) return 0;
                bool wall = inNeck ? Math.Abs(fx) > NECK - 0.5 : d > RB - 1;
                // the brew: below its sloshing surface, glowing from within, a bright skin on top
                double s = Surface(wx, ph), depth = wy - s;
                if (!wall && !inNeck && depth > 0)
                {
                    // dark at the bottom and the edges, a lit heart, a bright skin with a sheen on its left
                    double core = 1 - Hypot(wx * 0.85, wy - 1.8) / RB;
                    if (depth < 0.9) return wx < -1.5 && wx > -4.5 ? WHITE : C[6];
                    int k = core > 0.62 ? 5 : core > 0.38 ? 4 : core > 0.18 ? 3 : 2;
                    if (depth < 1.9 && k < 5) k++;
                    if (wx > 1.5 && wy > 2) k--;
                    return C[Math.Max(2, Math.Min(6, k))];
                }
                if (wall)
                {
                    // the glass: catching light on its left, showing the brew through its lower half
                    double lit = (-fx * 0.75 - fy * 0.66) / Math.Max(1, d);
                    if (inNeck) return fx < 0 ? Mix(GLASS, C[6], 0.55) : Mix(GLASS, C[5], 0.3);
                    if (lit > 0.82) return Mix(WHITE, C[6], 0.3);
                    if (lit > 0.3) return Mix(GLASS, C[6], 0.5);
                    return depth > 0 ? C[3] : Mix(GLASS, C[5], 0.28);
                }
                // empty glass above the brew
                return GLASS;
            });
            L.Outline(INK).Draw(p);
            // a curved streak of light on the glass, and a pin-point
            for (double a = 3.45; a < 4.35; a += 0.04)
            {
                double lx = Px.Cos(a + tilt) * (RB - 2.2), ly = Px.Sin(a + tilt) * (RB - 2.2);
                p.Px(lx, cy + ly, a > 3.7 && a < 4.1 ? WHITE : Mix(WHITE, C[6], 0.5));
            }
            p.Px(Round(2 * Px.Cos(tilt) + 3 * Px.Sin(tilt)), Round(cy - 3), WHITE);
            // bubbles rising through the brew and popping at its surface
            Cycle(t, DURATION, 10, seed, g => g.Range(0.7, 1.3), (age, life, r, i) =>
            {
                double x0 = r.Range(-4, 4), r0 = r.Range(0.6, 1.7), vy = r.Range(7, 13);
                double x = x0 + Px.Sin(age * r.Range(5, 9) + r.Range(0, TAU)) * 0.7, y = cy + 5.5 - vy * age, ph2 = (t + age) / DURATION;
                double top = cy + Surface(x, ph2);
                if (y > top + 0.8)
                {
                    double rr = r0 * (0.7 + 0.5 * (age / life));
                    if (Hypot(x, y - cy) > RB - 1.5) return;
                    if (rr < 0.95) p.Px(x, y, C[6]);
                    else
                    {
                        p.Px(x, y, C[3]);
                        p.Px(x + 1, y, C[6]);
                        p.Px(x, y - 1, WHITE);
                        p.Px(x + 1, y - 1, C[6]);
                        if (rr > 1.4)
                        {
                            p.Px(x - 1, y, C[5]);
                            p.Px(x, y + 1, C[4]);
                        }
                    }
                    p.Glow(x + 0.5, y, 3, C[6], 0.15);
                }
                else
                {
                    // the pop: a little arc of light and two flecks thrown up
                    double k = Clamp((top + 0.8 - y) / 3);
                    if (k >= 1) return;
                    for (double a = Math.PI; a < TAU; a += 0.6) p.Dot(x + Px.Cos(a) * (1 + k * 2), top - 0.5 + Px.Sin(a) * (1 + k * 2) * 0.5, k < 0.5 ? WHITE : C[6], 1.2 - k);
                    p.Dot(x - 1, top - 1.5 - k * 3, C[6], 1 - k);
                    p.Dot(x + 1.5, top - 2 - k * 4, WHITE, 1 - k);
                }
            });
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION;
            var (y, _) = Rock(ph);
            BlobShadow(p, 0, 7.5, 0.8 - (-y - HOVER + 1.4) * 0.06);
            // light thrown on the floor through the brew
            Pool(p, 0, 0.5, 18, 3, C[5], 0.3);
            // wisps curling up off the cork: thin trails of light that sway wider as they rise and fade
            Cycle(t, DURATION, 7, o.Seed + 1, g => g.Range(1.2, 1.8), (age, life, r, n) =>
            {
                double side = r.Sign(), f = r.Range(3, 4.5), ph0 = r.Range(0, TAU), vy = r.Range(11, 16), x0 = r.Range(-1, 1);
                for (int i = 0; i < 9; i++)
                {
                    double a = age - i * 0.04;
                    if (a < 0) break;
                    double x = x0 + Px.Sin(a * f + ph0) * (0.8 + a * 3.2) * side, yy = y + CORK_TOP - 1 - a * vy;
                    double k = (1 - a / life) * Math.Min(1, a / 0.15) * (1 - i / 9.0);
                    p.Dot(x, yy, i == 0 ? WHITE : i < 3 ? C[6] : C[5], k * 1.4);
                    if (i == 0) p.Glow(x, yy, 4, C[5], 0.3 * k);
                }
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION;
            var (y, tilt) = Rock(ph);
            // the brew glows through the glass
            p.Glow(0, y + 1.5, 26, C[4], 0.22);
            Flask(p, C, t, ph, y, tilt, o.Seed);
            p.Glow(0, y + 2, 10, C[5], 0.38 + 0.08 * Px.Sin(ph * TAU * 2));
            // a glint on the glass now and then
            double g = (ph + 0.2) % 0.5 / 0.5;
            if (g < 0.16) Glint(p, -4, y - 4, 2.6 * Px.Sin(g / 0.16 * Math.PI), WHITE, 0.4);
        }
    }
}
