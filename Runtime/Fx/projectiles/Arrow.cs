using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class Arrow : Effect
    {
        public static readonly Module Module = new Module("arrow", "pixelfx/fx/projectiles/arrow", 112, 32, 92, 16, 0.6, true, 30,
            new Options
            {
                ["palette"] = new[] { "#7a1f2c", "#b8323c", "#e8e2d6", "#ffffff" },
                ["wood"] = new[] { "#3a2414", "#6e4422", "#a8723c", "#d8a868" },
                ["steel"] = new[] { "#2e3648", "#5a6680", "#9aa6ba", "#dfe7f2" },
                ["wind"] = "#b8c8e8",
                ["seed"] = 3,
            },
            (o, e) => new Arrow(o))
        {
            Title = "Arrow",
            Desc = "An arrow in flight: a steel broadhead catching the light, a wooden shaft, fletching fluttering in the wind, streaks of air peeling off its head and swirling off its feathers.",
            Tags = new[] { "projectile", "arrow", "bow", "medieval", "directional", "loop" },
            Bg = "sky",
        };

        const double D = 0.6;
        const int OX = 92;

        // the arrow, nose right, tip at (0, 0): fletching (three poses for the flutter), shaft, binding, broadhead
        static readonly string[] HEAD = { "..LL....", "..sLLL..", "bbsSSSSW", "..dDDD..", "..DD...." };
        static readonly string[][] FLETCH =
        {
            new[] { "AAa.........", ".aAAa.......", "..aaAAa.....", "nkkkkkkkkkkk", "..cCCc......", ".cCCc.......", "CCc........." },
            new[] { ".AAa........", "..aAAa......", "..aaAAAa....", "nkkkkkkkkkkk", "..cCCCc.....", "..cCCc......", ".CCc........" },
            new[] { "AAAa........", "aAAa........", ".aaAAa......", "nkkkkkkkkkkk", "..ccCc......", "cCCc........", "CCc........." },
        };

        readonly Options o;

        Arrow(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] PL = Fit(o.Ramp("palette"), 4), W = Fit(o.Ramp("wood"), 4), ST = Fit(o.Ramp("steel"), 4);
            uint wind = o.Color("wind");
            double S = o.Seed;
            int fr = (int)Math.Floor(t * 30 + 1e-6);
            double ph = t / D, left = -OX + 2, LEN = 36;

            // streaks of air peeling off the head and the fletching, stretching back and thinning
            Beat(t, D, 9, S, r => r.Range(0.14, 0.26), (age, life, r, i) =>
            {
                double k = age / life, top = r.Chance(0.5) ? -1 : 1, from = r.Chance(0.6) ? -3 : -LEN + 6;
                double x0 = from - age * r.Range(160, 240), len = r.Range(8, 16) * (0.5 + k), y = 0.5 + top * (r.Range(3, 5) + k * r.Range(1, 2.5));
                if (x0 - len < left) return;
                uint c = k < 0.4 ? wind : Mix(wind, Hex("#2a2540"), (k - 0.4) * 1.2);
                p.Line(x0, y, x0 - len, y, u => u < 0.25 && k < 0.4 ? Mix(wind, WHITE, 0.5) : c, u => u < 0.65 ? 1 : (1 - u) / 0.35);
            });
            // little eddies spinning off the feathers
            Beat(t, D, 6, S + 3, r => r.Range(0.15, 0.25), (age, life, r, i) =>
            {
                double k = age / life, cx = -LEN - 2 - age * r.Range(100, 150), cy = 0.5 + r.Sign() * r.Range(2, 4), a = age * 30 + r.Range(0, TAU), rr = 1.2 + k * 1.5;
                if (cx < left + 4) return;
                for (int j = 0; j < 4; j++) p.Dot(cx + Px.Cos(a + j * 0.5) * rr, cy + Px.Sin(a + j * 0.5) * rr, wind, (1 - k) * (1 - j * 0.2));
            });

            // the shaft with a lit top line
            p.Line(-LEN + 11, 0, -7, 0, W[2]);
            for (double x = -LEN + 14; x < -8; x += 6) p.Px(x, 0, W[3]);
            // the fletching flutters through its poses, faster than the frame rate can quite catch
            string[] pose = FLETCH[(int)Math.Floor(Rand(fr, S) * 3)];
            p.Sprite(pose, new Dictionary<char, uint> { ['a'] = PL[2], ['A'] = PL[3], ['c'] = PL[0], ['C'] = PL[1], ['k'] = W[1], ['n'] = W[0] }, -LEN, -3);
            // the binding and the broadhead: a lit upper bevel, a shaded lower one, a white point
            p.Sprite(HEAD, new Dictionary<char, uint> { ['L'] = ST[3], ['s'] = ST[2], ['S'] = ST[2], ['d'] = ST[0], ['D'] = ST[1], ['b'] = W[0], ['W'] = WHITE }, -7, -2);
            // a glint runs down the blade now and then
            double g = (ph * 2) % 1;
            if (g < 0.25) Sim.Sparkle(p, -5 + g * 16, -1, 1 + Round(2 * Px.Sin((g / 0.25) * Math.PI)), WHITE, ST[3], 0.25);
        }
    }
}
