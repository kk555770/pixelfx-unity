// The coin's spin (squashed toward its edge), glints and burst of light, and the soft smoke of the poof, follow
// Sprite Fusion's open-source pickup (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class CoinDrop : Effect
    {
        public static readonly Module Module = new Module("coin-drop", "pixelfx/fx/spawn/coin-drop", 160, 88, 80, 78, 3, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1606", "#5c3208", "#9a5a0c", "#d48c14", "#f5b82a", "#ffe066", "#fff6c4", "#ffffff" },
                ["smoke"] = new[] { "#3a3450", "#6c6688", "#a8a2c0", "#dcd8ea" },
                ["coins"] = 7,
                ["seed"] = 23,
            },
            (o, e) => new CoinDrop(o, e))
        {
            Title = "Coin drop",
            Desc = "An enemy goes up in a soft poof and gold coins spin out of it, bounce with a glint on each hit, settle on the floor, twinkle, and wink out one by one.",
            Tags = new[] { "spawn", "death", "loot", "coins", "gold", "drop", "pickup", "ground", "one-shot" },
            Bg = "ground",
        };

        const double CY = -16;
        // an original coin: a rim, a lit face, a raised mark in the middle
        static readonly string[] COIN = { "..oooo..", ".ohhlmo.", "ohlmmmdo", "ohmdhmdo", "olmdhmdo", "olmmmmdo", ".oddddo.", "..oooo.." };
        // lying flat on the floor, seen from the side
        static readonly string[] FLAT = { ".lhhhl.", "olmmmdo", ".odddo." };

        sealed class Ball
        {
            public double x, y, vx, vy, r, life, max;
        }

        sealed class Coin
        {
            public double x, y, vx, vy, ph, w, o, glint, born, restAt;
            public int hits;
            public bool rest, gone;
        }

        readonly uint[] C, SM;
        readonly Rng R;
        readonly Dictionary<char, uint> PAL;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Coin> coins = new List<Coin>();
        readonly List<Ball> balls = new List<Ball>();
        double t;

        CoinDrop(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            SM = Fit(o.Ramp("smoke"), 4);
            R = new Rng(o.Seed);
            PAL = new Dictionary<char, uint> { ['o'] = C[1], ['h'] = C[6], ['l'] = C[5], ['m'] = C[4], ['d'] = C[3] };
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
            // the poof the enemy goes up in
            for (int i = 0; i < 10; i++)
            {
                double a = i / 10.0 * TAU + R.Range(-0.25, 0.25), sp = R.Range(60, 100);
                double life = R.Range(0.55, 0.85);
                balls.Add(new Ball { x = Px.Cos(a) * 2, y = CY + Px.Sin(a) * 2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp * 0.7 - 10, r = R.Range(4, 6), life = life, max = life });
            }
            // the coins: thrown up and out, each spinning at its own rate
            double n = o.Num("coins");
            for (int i = 0; i < n; i++)
            {
                double u = n > 1 ? i / (n - 1) - 0.5 : 0, a = -Math.PI / 2 + u * 1.6 + R.Range(-0.1, 0.1), sp = R.Range(230, 300);
                coins.Add(new Coin
                {
                    x = R.Range(-3, 3), y = CY, vx = Px.Cos(a) * sp * 0.4, vy = Px.Sin(a) * sp, ph = R.Range(0, TAU), w = R.Range(9, 15) * R.Sign(),
                    rest = false, hits = 0, o = 2.15 + i * 0.07 + R.Range(0, 0.1), glint = R.Range(1.2, 1.9), born = R.Range(0.04, 0.12),
                });
            }
            uint[] pick = { WHITE, C[6], C[5] };
            for (int i = 0; i < 8; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(50, 120);
                ps.Spawn(P.Star, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp - 20, R.Range(0.3, 0.6), R.Pick(pick), R.Range(0.8, 1.8));
            }
            lights.Add(0, CY, 44, C[4], 0.7, 0.4);
            lights.Add(0, CY, 20, WHITE, 0.7, 0.1);
        }

        // a coin turning in the air: its face squashed toward the edge, the rim showing when it's side-on
        void DrawCoin(Painter p, double x, double y, double ph)
        {
            double c = Px.Cos(ph), sx = Math.Abs(c);
            if (sx < 0.28)
            {
                for (int dy = -3; dy <= 3; dy++) p.Px(x, y + dy, Math.Abs(dy) == 3 ? C[1] : dy < 0 ? C[6] : C[5]);
                if (sx > 0.12)
                    for (int dy = -2; dy <= 2; dy++)
                        p.Px(x + (c > 0 ? 1 : -1), y + dy, C[3]);
                return;
            }
            double half = 4 * sx;
            for (double px = Math.Floor(x - half); px < Math.Ceiling(x + half); px++)
            {
                double col = Math.Floor((px + 0.5 - x) / sx + 4);
                if (col < 0 || col > 7) continue;
                if (c < 0) col = 7 - col;
                for (int row = 0; row < 8; row++)
                {
                    char ch = COIN[row][(int)col];
                    if (ch != '.') p.Px(px, Round(y - 4 + row), PAL[ch]);
                }
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball b = balls[i];
                if ((b.life -= dt) <= 0)
                {
                    balls.RemoveAt(i);
                    continue;
                }
                b.vx -= b.vx * 7 * dt;
                b.vy -= b.vy * 7 * dt + 26 * dt;
                b.x += b.vx * dt;
                b.y += b.vy * dt;
            }
            uint[] pick = { WHITE, C[6], C[5] };
            foreach (Coin c in coins)
            {
                // a coin winks out with a little burst of gold
                if (!c.gone && t >= c.o)
                {
                    c.gone = true;
                    for (int j = 0; j < 5; j++)
                    {
                        double a = R.Range(-Math.PI, 0), sp = R.Range(25, 60);
                        ps.Spawn(P.Star, c.x, -3, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.3, 0.55), R.Pick(pick), R.Range(0.7, 1.4));
                    }
                    lights.Add(c.x, -3, 14, C[5], 0.6, 0.2);
                }
                if (t < c.born || t > c.o || c.rest) continue;
                c.vy += 820 * dt;
                c.vx -= c.vx * 0.4 * dt;
                c.x += c.vx * dt;
                c.y += c.vy * dt;
                c.ph += c.w * dt;
                if (c.y >= -1.5 && c.vy > 0)
                {
                    c.y = -1.5;
                    c.hits++;
                    // a tink of light and a puff of dust at each hit
                    ps.Spawn(P.Star, c.x, -3, 0, -8, 0.25, WHITE, c.hits == 1 ? 2.2 : 1.4);
                    for (int j = 0; j < 2; j++) ps.Spawn(P.Dust, c.x + R.Range(-1, 1), -0.5, R.Sign() * R.Range(8, 24), -R.Range(2, 8), R.Range(0.25, 0.45), SM[1], R.Range(1, 1.8));
                    lights.Add(c.x, -3, 12, C[5], 0.5, 0.15);
                    if (c.vy > 110)
                    {
                        c.vy *= -0.45;
                        c.vx *= 0.7;
                        c.w = c.w * 0.8 + R.Range(-2, 2);
                    }
                    else
                    {
                        c.rest = true;
                        c.vx = c.vy = 0;
                        c.restAt = t;
                    }
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // a warm gleam on the floor where the coins lie
            foreach (Coin c in coins)
                if (c.rest && t < c.o)
                    p.Glow(c.x, -1, 7, C[4], 0.18);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            // the poof: soft, top-lit balls, a pop of light in the middle
            for (int layer = 0; layer < 3; layer++)
                foreach (Ball b in balls)
                {
                    double age = b.max - b.life, k = b.life / b.max, r = b.r * (0.6 + 0.6 * Ease.OutCubic(Clamp(age / 0.2))) * (k < 0.35 ? k / 0.35 : 1);
                    if (r < 0.6) continue;
                    if (layer == 0) p.Disc(b.x + r * 0.15, b.y + r * 0.2, r, SM[1], 0.8);
                    else if (layer == 1) p.Disc(b.x - r * 0.06, b.y - r * 0.08, r * 0.86, b.y > CY + 2 ? SM[1] : SM[2], 0.85);
                    else if (b.y < CY + 2) p.Disc(b.x - r * 0.28, b.y - r * 0.34, r * 0.4, SM[3], 0.85);
                }
            if (t < 0.07)
            {
                double k = t / 0.07;
                p.Circle(0, CY, 8 + 2 * k, C[6]);
                p.Circle(0, CY, 6 + 2 * k, WHITE);
                Sim.Flare(p, 0, CY, 16 * (1 - k * 0.5), 1 - k, C);
            }
            foreach (Coin c in coins)
            {
                if (t < c.born || t > c.o + 0.12) continue;
                if (t > c.o)
                {
                    // a coin winks out with a last flash and a ring
                    double k = (t - c.o) / 0.12;
                    Sim.Sparkle(p, c.x, -2, Round(5 * (1 - k)), WHITE, C[5], 0.5);
                    p.Ring(c.x, -3, 2 + 5 * Ease.OutCubic(k), 1, C[6], 1.1 - k);
                    continue;
                }
                if (c.rest)
                {
                    // flat on the floor; now and then a glint runs over it
                    p.Sprite(FLAT, PAL, Round(c.x) - 3, -3);
                    p.Glow(c.x, -2, 6, C[5], 0.25);
                    double g = ((t - c.restAt + c.glint) % 0.95) / 0.95;
                    if (g < 0.2) Sim.Sparkle(p, c.x - 1, -3, Round(3 * Px.Sin((g / 0.2) * Math.PI)), WHITE, C[5], 0.4);
                }
                else
                {
                    DrawCoin(p, c.x, c.y, c.ph);
                    p.Glow(c.x, c.y, 8, C[4], 0.3);
                    // a glint catches the face as it turns toward us
                    if (Math.Abs(Px.Cos(c.ph)) > 0.96) p.Px(c.x - 1, c.y - 2, WHITE);
                }
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
