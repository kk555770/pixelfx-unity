// The coin's pop (a white disc, then a ring and stars) and the trails of flying coins adapt Sprite Fusion's
// open-source pickup (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class CoinCollect : Effect
    {
        const double DURATION = 2.45;

        public static readonly Module Module = new Module("coin-collect", "pixelfx/fx/ui/coin-collect", 176, 116, 60, 102, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1405", "#5c3008", "#9a5a0e", "#d48e18", "#f5b92c", "#ffde5e", "#fff4bc", "#ffffff" },
                ["count"] = 10,
                ["from"] = 12,
                ["seed"] = 4,
            },
            (o, e) => new CoinCollect(o, e))
        {
            Title = "Coin collect",
            Desc = "A coin bursts into a fountain of spinning coins that hang, then zip one by one into a HUD counter that pops and ticks up, glints and folds away.",
            Tags = new[] { "ui", "coins", "pickup", "reward", "hud", "counter", "one-shot" },
            Bg = "ground",
        };

        const double POP = 0.3, FLY0 = 0.58, GAP = 0.075, FLY = 0.28, FOLD = 2.0, CY = -16;
        // the HUD: a plate at the top right with a coin icon and the count
        const double HX = 50, HY = -88, HW = 60, HH = 22, IX = HX + 12, IY = HY + 11;
        static readonly Plate INK = new Plate { Outline = Hex("#0d0a14"), Rim = Hex("#4a4060"), Top = Hex("#2a2338"), Body = Hex("#1e1929"), Low = Hex("#141019") };
        const double END = DURATION - 0.05;

        // a coin r px across, turned so its face is w wide (0..1): a dark edge, a rim lit on its upper left, a face lit
        // from the top left with an embossed bar, white when it flashes
        static void DrawCoin(Painter p, uint[] C, double x, double y, double r, double w, bool white = false)
        {
            double rx = Math.Max(0.7, r * w), ry = r;
            p.Shade(x - rx - 1, y - ry - 1, x + rx + 1, y + ry + 1, (px, py, o) =>
            {
                double u = (px - x) / rx, v = (py - y) / ry, d = Hypot(u, v);
                if (d > 1) return 0;
                double e = (1 - d) * Math.Min(rx * 1.6, ry);
                if (white) return e < 1 ? C[6] : WHITE;
                if (e < 1) return C[1];
                if (rx < 2) return v < -0.2 ? C[6] : v < 0.4 ? C[4] : C[3];
                if (e < 2) return u + v < -0.2 ? C[6] : C[3];
                if (Math.Abs(px - x) < 0.55 && Math.Abs(v) < 0.5) return C[3];
                if (px - x >= 0.55 && px - x < 1.55 && Math.Abs(v) < 0.5) return C[7];
                double l = -(u * 0.6 + v * 0.8);
                return l > 0.4 ? C[7] : l > 0.05 ? C[6] : l > -0.35 ? C[5] : C[4];
            });
        }

        sealed class Coin
        {
            public double x, y, vx, vy, ph, spin, t0, fx, fy, bob;
            public int state;
            public readonly List<double[]> trail = new List<double[]>();
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Coin[] coins;
        double t, bump, flash;
        bool popped;
        int count;

        CoinCollect(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            int N = (int)Clamp(Round(o.Num("count")), 1, 12);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
            coins = new Coin[N];
            for (int i = 0; i < N; i++)
            {
                double s = ((double)i / Math.Max(1, N - 1)) * 2 - 1;
                var c = new Coin { x = 0, y = CY };
                c.vx = s * R.Range(55, 90) + R.Range(-12, 12);
                c.vy = -R.Range(170, 285);
                c.ph = R.Range(0, TAU);
                c.spin = R.Range(9, 14) * R.Sign();
                c.state = 0;
                c.t0 = FLY0 + i * GAP;
                c.bob = R.Range(0, TAU);
                coins[i] = c;
            }
            count = RoundInt(o.Num("from"));
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!popped && t >= POP)
            {
                popped = true;
                lights.Add(0, CY, 46, WHITE, 0.8, 0.14);
                lights.Add(0, CY, 48, C[5], 0.6, 0.5);
                for (int i = 0; i < 14; i++)
                {
                    double an = ((double)i / 14) * TAU + R.Range(-0.2, 0.2), sp = R.Range(80, 170);
                    ps.Spawn(P.Spark, Px.Cos(an) * 3, CY + Px.Sin(an) * 3, Px.Cos(an) * sp, Px.Sin(an) * sp - 30, R.Range(0.15, 0.35));
                }
                for (int i = 0; i < 4; i++) ps.Spawn(P.Dust, R.Range(-6, 6), -1, R.Sign() * R.Range(15, 40), -R.Range(2, 8), R.Range(0.3, 0.5), Hex("#5a4f6e"), R.Range(1.5, 2.5));
                // a ring of stars flung out of the burst
                for (int i = 0; i < 10; i++)
                {
                    double an = ((double)i / 10) * TAU + R.Range(-0.2, 0.2), sp = R.Range(70, 130), down = Px.Sin(an) > 0 ? 0.25 : 1;
                    ps.Spawn(P.Star, Px.Cos(an) * 6, CY + Px.Sin(an) * 6, Px.Cos(an) * sp, Px.Sin(an) * sp * down, R.Range(0.35, 0.6), R.Pick(new[] { C[7], C[6], C[5] }), i % 3 != 0 ? 0.6 : R.Range(1.2, 2));
                }
            }
            foreach (Coin c in coins)
            {
                if (!popped || c.state == 3) continue;
                c.ph += c.spin * dt;
                if (c.state < 2)
                {
                    // the fountain: thrown up, slowed by the air, then they hang and bob
                    if (c.state == 0)
                    {
                        c.vy += 640 * dt;
                        c.vx -= c.vx * 2.2 * dt;
                        if (c.vy > 0) c.state = 1;
                    }
                    else
                    {
                        c.vy -= c.vy * 7 * dt;
                        c.vx -= c.vx * 4 * dt;
                    }
                    c.x += c.vx * dt;
                    c.y += c.vy * dt;
                    if (t >= c.t0)
                    {
                        c.state = 2;
                        c.fx = c.x;
                        c.fy = c.y + Px.Sin(t * 6 + c.bob) * 1.5;
                    }
                }
                if (c.state == 2)
                {
                    // the flight: an arc up and over into the HUD's coin, quickening as it goes
                    double u = Clamp((t - c.t0) / FLY), e = u * u, mx = c.fx + (IX - c.fx) * 0.35, my = Math.Min(c.fy, IY) - 26;
                    c.x = (1 - e) * (1 - e) * c.fx + 2 * (1 - e) * e * mx + e * e * IX;
                    c.y = (1 - e) * (1 - e) * c.fy + 2 * (1 - e) * e * my + e * e * IY;
                    c.trail.Insert(0, new[] { c.x, c.y });
                    if (c.trail.Count > 7) c.trail.RemoveAt(c.trail.Count - 1);
                    // it sheds sparkles as it goes
                    if (R.Chance(0.25)) ps.Spawn(P.Star, c.x + R.Range(-2, 2), c.y + R.Range(-2, 2), R.Range(-8, 8), R.Range(-4, 10), R.Range(0.2, 0.4), R.Pick(new[] { C[7], C[6] }), 0.6);
                    if (u >= 1)
                    {
                        c.state = 3;
                        count++;
                        bump = 1;
                        flash = 1;
                        lights.Add(IX, IY, 26, C[5], 0.6, 0.25);
                        for (int i = 0; i < 3; i++)
                        {
                            double an = R.Range(0, TAU), sp = R.Range(18, 40);
                            ps.Spawn(P.Star, IX + Px.Cos(an) * 5, IY + Px.Sin(an) * 5, Px.Cos(an) * sp, Px.Sin(an) * sp, Math.Min(R.Range(0.25, 0.45), END - t), R.Pick(new[] { C[7], C[6] }), i != 0 ? 0.6 : R.Range(1.2, 1.8));
                        }
                    }
                }
            }
            bump = Math.Max(0, bump - dt / 0.14);
            flash = Math.Max(0, flash - dt / 0.07);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // a ring on the floor where the coin bursts
            double k = (t - POP) / 0.45;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0, 4 + 34 * Ease.OutCubic(k), 0.22, Math.Max(1, 3 * (1 - k)), 1.15 - k, C, g => Px.Sin(g) < 0);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            double k = (t - POP) / 0.45;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0, 4 + 34 * Ease.OutCubic(k), 0.22, Math.Max(1, 3 * (1 - k)), 1.15 - k, C, g => Px.Sin(g) >= 0);
            // the big coin: bobbing and turning above its shadow, then it bursts in a white disc
            if (t < POP + 0.02)
            {
                double y = CY + Px.Sin(t * 9) * 1.5, w = Math.Abs(Px.Cos(t * 7));
                p.Ellipse(0, 0.5, 6 - 0.5 * Px.Sin(t * 9), 1.3, Hex("#0d0a14"), 0.6);
                // two motes circle it, passing behind and in front
                for (int i = 0; i < 2; i++)
                {
                    double an = t * 7 + i * Math.PI, ox = Px.Cos(an) * 11, oy = Px.Sin(an) * 3;
                    if (Px.Sin(an) < 0) p.Px(ox, y + oy, C[4]);
                }
                DrawCoin(p, C, 0, y, 7, Math.Max(0.12, w), t > POP - 0.04);
                for (int i = 0; i < 2; i++)
                {
                    double an = t * 7 + i * Math.PI, ox = Px.Cos(an) * 11, oy = Px.Sin(an) * 3;
                    if (Px.Sin(an) >= 0)
                    {
                        p.Px(ox, y + oy, C[7]);
                        p.Glow(ox, y + oy, 4, C[5], 0.4);
                    }
                }
                p.Glow(0, y, 20, C[5], 0.45 + 0.4 * Seg(t, POP - 0.15, POP));
                if (t > 0.06 && t < 0.2) Sim.Sparkle(p, -3, y - 3, 3.5 * Px.Sin(((t - 0.06) / 0.14) * Math.PI), C[7], C[6], 0.4);
            }
            double pk = (t - POP) / 0.1;
            if (pk > 0 && pk < 1) p.Circle(0, CY, 13 * (1 - pk) + 1, pk < 0.5 ? WHITE : C[6]);
            double fk = (t - POP) / 0.2;
            if (fk > 0 && fk < 1) Sim.Flare(p, 0, CY, 26 * Px.Pow(1 - fk, 1.5), 1 - fk, C);
            double rk = (t - POP - 0.03) / 0.35;
            if (rk > 0 && rk < 1) p.Ring(0, CY, 6 + 22 * Ease.OutCubic(rk), Math.Max(1, 2 * (1 - rk)), rk < 0.3 ? WHITE : C[6], 1.2 - rk);
            // the coins and their trails
            foreach (Coin c in coins)
            {
                if (!popped || c.state == 3) continue;
                for (int i = 1; i < c.trail.Count; i++)
                    p.Line(c.trail[i - 1][0], c.trail[i - 1][1], c.trail[i][0], c.trail[i][1], i < 3 ? C[6] : C[4], 1.2 - i / 7.0);
                double hover = c.state == 1 ? Px.Sin(t * 6 + c.bob) * 1.5 : 0, w = Math.Abs(Px.Cos(c.ph));
                DrawCoin(p, C, c.x, c.y + hover, 5, Math.Max(0.15, w));
                p.Glow(c.x, c.y + hover, 10, C[5], c.state == 2 ? 0.5 : 0.32);
                // light catches each hanging coin now and then
                double gl = (t * 1.7 + c.bob) % 1;
                if (c.state == 1 && gl < 0.12 && w > 0.5) Sim.Sparkle(p, c.x - 1, c.y + hover - 2, 3 * Px.Sin((gl / 0.12) * Math.PI), C[7], C[6], 0.4);
            }
            // the HUD: unfolds, counts up with a pop on every coin, glints, folds away
            double open = Ease.OutBack(Seg(t, 0.06, 0.26), 2.2) * (1 - Ease.InBack(Seg(t, FOLD, FOLD + 0.2), 2));
            if (open > 0.04)
            {
                double h = HH * open, y0 = HY + (HH - h) / 2, sh = Seg(t, 1.5, 1.76);
                Panel(p, HX, y0, HW, h, INK, Math.Min(4, h / 2), 1, sh > 0 && sh < 1 ? HX - 6 + (HW + 16) * sh : (double?)null);
                if (open > 0.7)
                {
                    DrawCoin(p, C, IX, IY, 5.5 * (1 + 0.3 * bump), 1, flash > 0.5);
                    p.Glow(IX, IY, 14, C[5], 0.25 + 0.4 * bump);
                    if (bump > 0) p.Ring(IX, IY, 7 + 8 * (1 - bump), 1, bump > 0.6 ? WHITE : C[6], bump * 1.3);
                    double s = 2 * (1 + 0.22 * bump);
                    Word word = Spell("x" + count);
                    double W = word.W * s, H = 7 * s, x0 = IX + 9, ty = IY - H / 2;
                    double fl = flash > 0.5 ? 1 : 0;
                    var items = word.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = x0 + L.X * s, Y = ty, Sx = s, Sy = s, Flash = fl }).ToList();
                    DrawWord(p, items, new Style { Fill = new[] { C[4], C[5], C[6], C[7] }, Edge = WHITE, Under = C[3], Outline = INK.Outline, Shadow = 0 });
                    if (bump > 0) p.Glow(x0 + W / 2, IY, 16, C[5], 0.3 * bump);
                }
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
