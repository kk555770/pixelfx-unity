// The faceted shading (a lit ridge, a bright face toward the light, a dark one away, a bright rim) follows
// Sprite Fusion's open-source ice crystals (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class DiamondWipe : Effect
    {
        public static readonly Module Module = Screen("diamond-wipe", "Diamond wipe",
            "A wave of faceted diamonds swells across the screen, rims bright and ridges lit, cooling to black as they lock; then they rise as gems again and shrink away with glints.",
            new[] { "diamond", "lattice", "geometric", "wipe" }, 2.0,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 1 },
            (o, e) => new DiamondWipe(o));

        const double S = 32, HD = S / 2 + 1, GROW = 0.24, IN0 = 0.04, IN1 = 0.66, OUT0 = 1.05, OUT1 = 1.56;

        sealed class Diamond
        {
            public double cx, cy, tin, tout, spin;
            public bool glint;
        }

        // the lattice: rows of diamonds S apart, every other row shifted half a step, so they tile when full
        static readonly Dictionary<double, List<Diamond>> cache = new Dictionary<double, List<Diamond>>();

        static List<Diamond> Lattice(double seed)
        {
            if (cache.TryGetValue(seed, out List<Diamond> hit)) return hit;
            var o = new List<Diamond>();
            for (int j = -1; j <= Math.Ceiling(H / (S / 2)) + 1; j++)
                for (int i = -1; i <= Math.Ceiling(W / S) + 1; i++)
                {
                    double cx = i * S + ((j & 1) != 0 ? S / 2 : 0), cy = (j * S) / 2;
                    if (cx + HD < 0 || cy + HD < 0 || cx - HD > W || cy - HD > H) continue;
                    // the wave runs left to right on a slant, each diamond a little early or late
                    double u = Clamp((cx + 0.45 * cy) / (W + 0.45 * H)), jr = Rand(i, j, seed);
                    o.Add(new Diamond
                    {
                        cx = cx,
                        cy = cy,
                        tin = IN0 + (IN1 - IN0) * Clamp(u + (jr - 0.5) * 0.08),
                        tout = OUT0 + (OUT1 - OUT0) * Clamp(u + (Rand(i, j, seed + 1) - 0.5) * 0.08),
                        glint = jr < 0.3,
                        spin = Rand(i, j, seed + 2) * 3,
                    });
                }
            cache[seed] = o;
            return o;
        }

        readonly Options o;

        DiamondWipe(Options o) => this.o = o;

        public override void Draw(Painter p0, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            uint ink = o.Color("color");
            Painter p = ClipTo(p0);
            List<Diamond> L = Lattice(o.Seed);
            var glints = new List<(Diamond d, double u, double hd)>();
            // facets, toward the light first: top left, top right, bottom left, bottom right; and how each cools
            // (faces stay in the shadow end of the ramp: the rims and ridges carry the light)
            uint[][] FACE = { new[] { C[2], C[1], C[0] }, new[] { C[1], C[0], C[0] }, new[] { C[1], C[0], C[0] }, new[] { C[0], C[0], ink } };
            foreach (Diamond d in L)
            {
                double ki = (t - d.tin) / GROW, ko = (t - d.tout) / GROW;
                // the last glint as it winks out
                if (d.glint && ko > 0.7 && ko < 1.5) glints.Add((d, (ko - 0.7) / 0.8, 0));
                if (ki <= 0 || ko >= 1) continue;
                // its size: swelling in with an overshoot; out, a breath bigger and then shrinking to nothing
                double hd = HD * (ko > 0 ? 1 - Ease.InBack(ko, 2) : Ease.OutBack(Math.Min(1, ki), 2.2));
                // its heat: bright while it swells, cooling once it's locked in; lighting up again as it goes
                double heat = ko > 0 ? Smooth(0, 0.35, ko) : 1 - Smooth(0.45, 1.25, ki);
                if (hd < 0.5) continue;
                bool settled = heat <= 0;
                double y0 = Math.Max(0, Math.Floor(d.cy - hd)), y1 = Math.Min(H - 1, Math.Ceiling(d.cy + hd));
                for (double y = y0; y <= y1; y++)
                {
                    double dy = y + 0.5 - d.cy, w = hd - Math.Abs(dy);
                    if (w <= 0) continue;
                    double x0 = Math.Max(0, Math.Ceiling(d.cx - w - 0.5)), x1 = Math.Min(W - 1, Math.Floor(d.cx + w - 0.5));
                    if (x1 < x0) continue;
                    if (settled)
                    {
                        Rect(p0, x0, y, x1 - x0 + 1, 1, ink);
                        continue;
                    }
                    int st = heat > 0.66 ? 0 : heat > 0.33 ? 1 : 2;
                    for (double x = x0; x <= x1; x++)
                    {
                        double dx = x + 0.5 - d.cx, e = hd - Math.Abs(dx) - Math.Abs(dy); // e: px in from the rim
                        int q = (dy < 0 ? 0 : 2) + (dx < 0 ? 0 : 1);
                        bool vr = dx > -1 && dx <= 0, hr = dy > -1 && dy <= 0; // the centre column and row
                        uint c;
                        if (e < 1)
                        {
                            // the rim, lit from the top left: white there, cooling round to the far side
                            uint r = new[] { heat > 0.5 ? WHITE : C[6], heat > 0.5 ? C[6] : C[5], heat > 0.5 ? C[5] : C[4], heat > 0.5 ? C[4] : C[3] }[q];
                            c = heat > 0.15 ? r : ink;
                            if (heat > 0.15) p0.Light(x, y, C[5], 0.4 * heat * (q == 0 ? 1 : 0.6));
                        }
                        else if (e < 2 && q == 0 && heat > 0.4) c = C[5];
                        else if ((vr || hr) && heat > 0.3)
                        {
                            // ridges, with a white tip at the top point and a sheen running down them
                            bool tip = vr && dy < -hd + 4, sheen = Math.Abs(((x + y - t * 260) % 90 + 90) % 90 - 45) < 3;
                            c = tip || (sheen && heat > 0.5) ? WHITE : vr && dy < 0 ? (heat > 0.6 ? C[6] : C[5]) : heat > 0.6 ? C[3] : C[2];
                        }
                        else c = heat > 0.15 ? FACE[q][st] : ink;
                        p0.Px(x, y, c);
                    }
                }
                if (!settled) p.Glow(d.cx, d.cy, hd + 6, C[4], 0.22 * heat);
                // a glint on its top point as it locks in
                if (ki > 0.6 && ki < 1.8 && d.glint) glints.Add((d, (ki - 0.6) / 1.2, hd));
            }
            foreach (var (d, u, hd) in glints)
            {
                if (u <= 0 || u >= 1) continue;
                double y = hd != 0 ? d.cy - hd : d.cy;
                Glint(p, d.cx, y, 9 * Px.Pow(Px.Sin(Math.PI * u), 0.7), d.spin + u * 1.5, C[6], C[5]);
            }
        }
    }
}
