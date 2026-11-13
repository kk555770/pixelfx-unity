// The column recipe (ramp bands out from a white core, drifting strands, dithered edges, light every few rows)
// is adapted from Sprite Fusion's open-source holy light (https://www.spritefusion.com/pixel-art-vfx/holy-light),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class LootBeam : Effect
    {
        const double DURATION = 2.4, H = 150;
        static readonly double[] RINGS = { 0.08, 0.58 };
        static readonly uint[] STEEL = Ramp("#151726", "#2c3149", "#4f5775", "#8a93b3", "#d6dcef", "#ffffff");
        static readonly uint[] GOLD = Ramp("#3a2008", "#7a4a10", "#b8801c", "#e8b432", "#ffe27a", "#fff6cc");
        static readonly uint[] LEATHER = Ramp("#1e0f0e", "#3e2016", "#633620");
        static readonly uint INK = Hex("#0d0b14");

        public static readonly Module Module = new Module("loot-beam", "pixelfx/fx/pickups/loot-beam", 96, 176, 48, 162, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#140a24", "#2c1250", "#4b1d86", "#7330c0", "#a050f0", "#c890ff", "#ecd4ff", "#ffffff" },
                ["seed"] = 4,
            },
            (o, e) => new LootBeam(o, e))
        {
            Title = "Loot beam",
            Desc = "A dropped sword in a tall pillar of rarity light: a white core thinning to the sky, rings pulsing across the floor, motes streaming up, a glint racing up the shaft.",
            Tags = new[] { "pickup", "loot", "beam", "rarity", "drop", "item", "loop" },
            Bg = "ground",
        };

        readonly ScratchLayer L = new ScratchLayer(40, 24);
        readonly Options o;

        LootBeam(Options o, Emitter e) => this.o = o;

        // the time since the last ring left the foot, in loop phase, for ring k
        static double Since(double ph, int k) => ((ph - RINGS[k]) % 1 + 1) % 1;

        // the sword lying where it fell, 22 px from pommel to point: it rests on its guard and its tip, so the hilt
        // lifts off the floor. Built column by column on a stepped axis so its lines stay crisp at 1x.
        // The beam rises from the guard; the blade reaches out of the light to the right.
        const double OX = 4;

        static double Axis(double x) => Math.Floor(-4 + (x + 4) / 7 + 0.5);

        void Sword(Painter p, uint[] C, double ph)
        {
            uint EdgeLit(int x) => Math.Abs(x + OX - 0.5) < 6 ? C[6] : Mix(STEEL[4], C[6], 0.45);
            void Set(double x, double y, uint c) => L.Set(x + OX, y, c);
            L.Begin(-16, -12);
            for (int x = -11; x <= 10; x++)
            {
                double a = Axis(x);
                if (x <= -9)
                {
                    // the pommel: a stone that holds the beam's colour
                    int m = x + 10;
                    if (m == 0)
                    {
                        Set(x, a - 1, C[6]);
                        Set(x, a, WHITE);
                        Set(x, a + 1, C[4]);
                    }
                    else Set(x, a, m < 0 ? C[5] : C[3]);
                }
                else if (x <= -5)
                {
                    // the grip, wrapped in leather
                    Set(x, a, LEATHER[x % 2 != 0 ? 2 : 1]);
                    Set(x, a + 1, LEATHER[x % 2 != 0 ? 1 : 0]);
                }
                else if (x <= -3)
                {
                    // the guard, gold catching the beam on its lit face, a small stone at its heart
                    for (double y = a - 3; y <= a + 3; y++)
                    {
                        bool end = y == a - 3 || y == a + 3;
                        Set(x, y, y == a ? (x == -4 ? C[6] : C[4]) : x == -4 ? (end ? GOLD[3] : Mix(GOLD[4], C[6], 0.3)) : end ? GOLD[0] : GOLD[1]);
                    }
                }
                else
                {
                    // the blade: a lit edge on top, steel, a dark spine below, tapering to the point
                    // backlit by the beam: a dark body with the beam's colour running along its upper edge
                    int tip = x - 8;
                    if (tip < 1) Set(x, a - 1, EdgeLit(x));
                    if (tip < 2) Set(x, a, tip == 1 ? EdgeLit(x) : Mix(STEEL[3], C[4], 0.2));
                    Set(x, a + 1, tip == 2 ? STEEL[2] : STEEL[0]);
                }
            }
            L.Outline(INK).Draw(p);
            // a glint slides down the blade's edge once a loop
            double g = (ph - 0.72) / 0.16;
            if (g > 0 && g < 1)
            {
                double x = -1 + 9 * Ease.InOutSine(g);
                Glint(p, x + OX, Axis(Round(x)) - 1, 2.6 * Px.Sin(g * Math.PI), WHITE, 0.5);
            }
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double T = DURATION, ph = t / T;
            // the floor: a pool of rarity light, a bright spot right under the item
            Pool(p, 0.5, 0, 34, 5, C[4], 0.14);
            // light thrown on the floor in front, kept off the item itself
            p.Shade(-30, -0.5, 31, 6, (x, y, q) =>
            {
                double dx = (x - 0.5) / 30, dy = y / 6, d = dx * dx + dy * dy;
                if (d >= 1) return 0;
                q.l = C[5];
                q.k = 0.4 * Px.Pow(1 - Math.Sqrt(d), 2);
                return 0;
            });
            for (int x = -16; x <= 16; x++)
            {
                double q = Math.Abs(x) / 16.5, a = 1.35 - q * 1.25;
                p.Dot(x, 0, q < 0.3 ? WHITE : q < 0.6 ? C[6] : C[5], a);
                if (q < 0.75) p.Dot(x, 1, q < 0.3 ? C[6] : q < 0.55 ? C[5] : C[4], a * 0.8);
                if (q < 0.4) p.Dot(x, 2, C[4], a * 0.5);
            }
            // the far half of each ring, behind the item and the beam
            for (int k = 0; k < RINGS.Length; k++) Ring(p, C, Since(ph, k) * T, true);
            Column(p, C, t, T);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double T = DURATION, ph = t / T;
            // a soft shadow where it lies, dark in the middle of the lit floor
            BlobShadow(p, OX - 0.5, 12.5, 0.95);
            Sword(p, C, ph);
            for (int k = 0; k < RINGS.Length; k++) Ring(p, C, Since(ph, k) * T, false);
            Motes(p, C, t, T, o.Seed);
        }

        // a ring of light that pulses out across the floor from the item's foot
        static void Ring(Painter p, uint[] C, double a, bool back)
        {
            const double L1 = 1.1;
            double k = a / L1;
            if (k >= 1) return;
            double r = 3 + 24 * Ease.OutCubic(k), th = Math.Max(1, 3.2 * (1 - k));
            Sim.Band(p, 0.5, 0, r, 0.2, th, Math.Min(1, 1.25 * (1 - k)), new[] { C[2], C[4], C[5], C[6] }, g => (Px.Sin(g) < 0) == back, k < 0.12);
            if (!back)
                foreach (int s in new[] { -1, 1 })
                    p.Glow(0.5 + s * r, -1, 8, C[5], 0.35 * (1 - k));
        }

        // the column: bands of the ramp out from a white core, strands drifting through it, pulses climbing it, the
        // whole thing thinning and dithering away towards the sky; it swells as each ring leaves its foot
        static void Column(Painter p, uint[] C, double t, double T)
        {
            double ph = t / T;
            double beat = 0;
            for (int k = 0; k < RINGS.Length; k++)
            {
                double s = Since(ph, k) * T;
                beat = Math.Max(beat, Math.Max(Exp(-s * 7) * (s < 0.5 ? 1 : 0), Exp(-((T - s) * 30))));
            }
            // the glint racing up the shaft once a loop
            double gq = (ph - 0.22) / 0.42, gh = gq > 0 && gq < 1 ? 4 + (H - 4) * Ease.InQuad(gq) : -100;
            double shimmer = Math.Floor(ph * 24);
            for (int y = -1; y >= -H; y--)
            {
                double h = -y - 0.5, u = h / H, fade = 1 - Smooth(0.25, 1, u);
                // it narrows with height until only the core is left, and the core breaks into dashes near the top
                double w = (4.2 * (1 - 0.45 * u) + 3.2 * Exp(-h / 7)) * (1 + 0.2 * beat);
                // dashes of uneven length, flickering, as the column gives out
                double seg = Math.Floor(h / 3 + Rand(Math.Floor(h / 9), 5) * 2), flick = Math.Floor(ph * 12);
                if (u > 0.66 && Rand(seg, flick, 7) > 1.75 - u * 1.65) continue;
                bool thread = u > 0.58 && Rand(seg, flick, 9) < (u - 0.58) * 3;
                double climb = ((h / 30 - ph * 3) % 1 + 1) % 1 < 0.1 ? 1 : 0, trail = gh - h > 0 && gh - h < 18 ? 1 - (gh - h) / 18 : 0;
                double X = thread ? 0 : Math.Ceiling(w * 1.6);
                for (double x = -X; x <= X; x++)
                {
                    double q = Math.Abs(x) / w;
                    double cw = 0.2 + 0.18 * (1 - Smooth(0, 0.5, u));
                    double lvl = q < cw ? 0 : q < 0.47 ? 1 : q < 0.72 ? 2 : q < 1 ? 3 : 4;
                    for (int s = 0; s < 3; s++)
                    {
                        double sx = (s - 1) * w * 0.55 + Px.Sin(h * 0.09 + ph * TAU * (s + 1) + s * 2) * 0.9;
                        if (Math.Abs(x - sx) < 0.5) lvl = Math.Max(0, lvl - 1);
                    }
                    if (Rand(y, shimmer, 3) < 0.05 && lvl > 0) lvl--;
                    if (q < 0.72) lvl = Math.Max(0, lvl - climb - (trail > 0.3 ? 1 : 0));
                    // the core and inner bands are solid light; only the fringe is dithered, and it goes first
                    double a = lvl <= 2 ? 1 : (1.55 - q) / 0.75 * fade * 1.2;
                    if (a <= 0) continue;
                    p.Dot(x, y, thread ? (u > 0.85 ? C[6] : WHITE) : lvl == 0 ? (u > 0.8 ? C[6] : WHITE) : C[7 - (int)lvl], a);
                }
                // light all along it: a close glow, and a wide haze that fades with the column
                double near = Smooth(8, 20, h);
                if ((y & 3) == 0) p.Glow(0.5, y, w * 2.6 + 6, C[5], 0.36 * near * (0.3 + 0.7 * fade) * (1 + 0.4 * beat));
                if ((y & 15) == 0) p.Glow(0.5, y, 34, C[4], 0.2 * near * (0.25 + 0.75 * fade));
            }
            if (gh > 0)
            {
                Glint(p, 0, -gh, 3.5 * (1 - gq * 0.5), C[6], 0.6);
                p.Glow(0.5, -gh, 14, C[5], 0.45);
            }
            // the foot of the beam burns brightest: a small flare where it rises out of the item
            Sim.Flare(p, 0.5, -5, 8 + 3 * beat + Px.Sin(ph * TAU * 4) * 0.8, 0.6 + 0.4 * beat, C);
            p.Glow(0.5, -16, 16, C[4], 0.12 + 0.25 * beat);
        }

        // motes streaming up round the beam, a few twinkling
        static void Motes(Painter p, uint[] C, double t, double T, double seed)
        {
            Cycle(t, T, 30, seed, g => g.Range(1.0, 2.2), (age, life, r, i) =>
            {
                bool inner = r.Chance(0.3);
                double side = r.Sign(), x0 = inner ? r.Range(-3, 3) : side * r.Range(4, 15), vy = r.Range(22, 52) * (inner ? 1.5 : 1);
                double x = x0 + Px.Sin(age * r.Range(1.5, 3.5) + r.Range(0, TAU)) * 1.6, y = -r.Range(1, 10) - vy * age;
                double k = Math.Min(1, Math.Min(age / 0.25, (life - age) / 0.5));
                if (k <= 0 || y < -H) return;
                if (r.Chance(0.22))
                {
                    double tw = 0.5 + 0.5 * Px.Sin(age * r.Range(9, 14) + r.Range(0, TAU));
                    if (tw * k > 0.3) Glint(p, x, y, tw > 0.75 ? 2 : 1, C[6], 0.4 * k);
                    return;
                }
                p.Dot(x, y, inner ? WHITE : C[6], k * 1.5);
                p.Dot(x, y + 1, inner ? C[6] : C[4], k);
                p.Dot(x, y + 2, C[3], k * 0.6);
                p.Glow(x + 0.5, y + 0.5, 3.5, C[5], 0.35 * k);
            });
        }
    }
}
