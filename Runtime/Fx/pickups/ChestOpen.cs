// The collection pop (white squash, a ring snapping out, star sparks) and the glints follow Sprite Fusion's
// open-source pickup (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class ChestOpen : Effect
    {
        public static readonly Module Module = new Module("chest-open", "pixelfx/fx/pickups/chest-open", 176, 112, 88, 96, 4.6, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1e1206", "#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff" },
                ["wood"] = new[] { "#110a10", "#24131a", "#3a1f1f", "#552d24", "#73402c", "#925737", "#b07246" },
                ["coins"] = 12,
                ["seed"] = 3,
            },
            (o, e) => new ChestOpen(o, e))
        {
            Title = "Chest open",
            Desc = "A banded chest rattles with gold light leaking from its seam, the lid kicks open, rays fan up, and coins and gems arc out, bounce, glint and get collected.",
            Tags = new[] { "pickup", "chest", "treasure", "loot", "reward", "gold", "one-shot" },
            Bg = "ground",
        };

        // gems that come out with the gold: ruby, emerald, sapphire, amethyst (dark to bright)
        static readonly uint[][] GEMS =
        {
            Ramp("#2a0612", "#6e0f2a", "#b81c3c", "#f0405a", "#ff8a96", "#ffd2d6"),
            Ramp("#06200f", "#0f4a26", "#1a8040", "#34c060", "#8af0a0", "#d8ffe0"),
            Ramp("#0a1030", "#142a70", "#2050c0", "#3a8cf0", "#8ccaff", "#dcf0ff"),
            Ramp("#1a0a2a", "#3e1466", "#6a28a8", "#a050e0", "#d09cff", "#f0e0ff"),
        };

        const double T_OPEN = 0.8, T_COLLECT = 2.55, T_POOF = 3.3;
        static readonly double[][] RATTLES = { new[] { 0.1, 0.4 }, new[] { 0.32, 0.62 }, new[] { 0.5, 0.8 }, new[] { 0.64, 1 } };
        // the chest: 24 px wide, a 9 px front face, seen from a little above (tilt sin 0.34) so the open top shows
        const int HW = 12, FH = 9;
        const double CS = 0.94, SN = 0.34, HB = FH / CS, LA = 6, LB = 5.5;
        static readonly int[] STRAPS = { -9, 7 };
        static readonly uint[] IRON = Ramp("#0e0d16", "#1d1c28", "#2f2e3e", "#474658", "#6a6a7e", "#9898ac");
        static readonly uint DUST = Hex("#5e5068");

        sealed class LidRow
        {
            public double row, al, dep, lit;
            public bool outside, crest, seam;
        }

        // the lid as rows of screen pixels: which point of its curved profile each row shows, which side, how lit.
        // Points that swing below the rim (behind the back wall) are hidden.
        static List<LidRow> LidRows(double th)
        {
            var best = new Dictionary<double, LidRow>();
            var order = new List<double>();
            double ct = Px.Cos(th), st = Px.Sin(th);
            for (int i = 0; i <= 260; i++)
            {
                double al = i / 260.0 * Math.PI, dz = LA + LA * Px.Cos(al), dy = LB * Px.Sin(al);
                double z = -LA + dz * ct - dy * st, y = HB + dz * st + dy * ct;
                if (y < HB - 0.05) continue;
                double nz = Px.Cos(al) / LA, ny = Px.Sin(al) / LB;
                double nl = Hypot(nz, ny);
                nz /= nl;
                ny /= nl;
                double Nz = nz * ct - ny * st, Ny = nz * st + ny * ct;
                double v = y * CS + (LA - z) * SN, dep = z * CS + y * SN, row = Math.Floor(-v);
                bool had = best.TryGetValue(row, out LidRow cur);
                if (!had || dep > cur.dep)
                {
                    if (!had) order.Add(row);
                    best[row] = new LidRow { row = row, al = al, dep = dep, outside = Nz * CS + Ny * SN > 0, lit = Ny * 0.95 + Nz * 0.3 - 0.2 };
                }
            }
            List<LidRow> rows = order.Select(k => best[k]).OrderBy(r => r.row).ToList();
            // the lit crest: the outside row facing the light most squarely
            LidRow hi = null;
            foreach (LidRow r in rows)
                if (r.outside && (hi == null || r.lit > hi.lit))
                    hi = r;
            if (hi != null && hi.lit > 0.5) hi.crest = true;
            // stave seams: the row nearest each seam angle
            foreach (double s in new[] { Math.PI / 3, 2 * Math.PI / 3 })
            {
                LidRow k = null;
                foreach (LidRow r in rows)
                    if (k == null || Math.Abs(r.al - s) < Math.Abs(k.al - s))
                        k = r;
                if (k != null && Math.Abs(k.al - s) < 0.4) k.seam = true;
            }
            return rows;
        }

        static uint Tone(uint[] R, double l, int[] steps) => R[steps[l > 0.6 ? 0 : l > 0.25 ? 1 : l > -0.1 ? 2 : l > -0.45 ? 3 : 4]];

        static readonly int[] LIP_STEPS = { 6, 5, 5, 4, 3 }, STRAP_STEPS = { 5, 4, 3, 2, 1 }, WOOD_STEPS = { 5, 4, 3, 2, 1 };

        readonly ScratchLayer L = new ScratchLayer(40, 44), IL = new ScratchLayer(16, 16);

        static uint Heat(uint[] C, double k, double g) => C[(int)Clamp(Round(k + g * 2.2), 0, 7)];

        // the glowing hoard inside the open top: a mound of gold, lit from within
        void PaintHoard(uint[] C, uint[] W, double g)
        {
            for (int y = -13; y <= -10; y++)
            for (int x = -HW; x < HW; x++)
            {
                double h = Rand(x, y, 7);
                int k = h < 0.18 ? 5 : h < 0.55 ? 4 : 3;
                L.Set(x, y, y == -13 && Math.Abs(x + 0.5) > 9 ? W[2] : Heat(C, k - (y == -13 ? 1 : 0), g));
            }
            for (int x = -9; x < 9; x++)
            {
                double top = -13 - Round(2.2 * Px.Pow(Px.Cos((x + 0.5) / 10 * (Math.PI / 2)), 1.4));
                for (double y = top; y < -13; y++) L.Set(x, y, Heat(C, y == top ? 5 : 4 + (Rand(x, y, 3) < 0.3 ? 1 : 0), g));
            }
        }

        // the lid at angle th: wood staves, iron straps, a gold lip and hasp outside; dark wood lit by the hoard inside
        void PaintLid(uint[] C, uint[] W, double th, double g)
        {
            List<LidRow> rows = LidRows(th);
            double top = rows.Count > 0 ? rows[0].row : 0;
            foreach (LidRow r in rows)
            {
                double u = r.al;
                for (int x = -HW; x < HW; x++)
                {
                    bool strap = STRAPS.Any(s => x == s || x == s + 1), left = STRAPS.Any(s => x == s);
                    double side = (x + HW + 0.5) / (2 * HW);
                    bool corner = x == -HW || x == HW - 1;
                    uint c;
                    if (r.outside)
                    {
                        if (r.row == top && corner && th < 0.6) continue;
                        double l = r.lit + 0.2 - side * 0.35 - (r.row == top ? 0.45 : 0);
                        if (u < 0.3 || (u < 0.55 && x >= -2 && x <= 1)) c = Tone(C, l + (x >= -2 && x <= 1 ? 0.15 : 0.1), LIP_STEPS);
                        else if (corner) c = x < 0 ? IRON[4] : IRON[2];
                        else if (strap) c = Tone(IRON, l + (left ? 0.3 : -0.3), STRAP_STEPS);
                        else if (r.seam) c = W[0];
                        else
                        {
                            c = r.crest && side < 0.62 ? W[6] : Tone(W, l, WOOD_STEPS);
                            if (Rand(x >> 1, Round(u * 6), 11) < 0.2) c = W[Math.Max(0, Array.IndexOf(W, c) - 1)];
                        }
                    }
                    else
                    {
                        // the inside of the lid, lit by the hoard from below
                        double k = g * Px.Pow(u / Math.PI, 1.3);
                        if (u < 0.3) c = Heat(C, 1, g);
                        else if (r.seam) c = W[0];
                        else c = k > 0.6 ? Mix(W[4], C[5], 0.3) : k > 0.35 ? W[3] : k > 0.15 ? W[2] : W[1];
                        if (corner) c = W[0];
                    }
                    L.Set(x, r.row, c);
                }
            }
        }

        // the front face: a gold rim, two planks, iron straps and corners with rivets, a gold lock plate and keyhole
        void PaintFront(uint[] C, uint[] W, double g)
        {
            for (int y = -FH; y <= -1; y++)
            for (int x = -HW; x < HW; x++)
            {
                double side = (x + HW + 0.5) / (2 * HW);
                uint c;
                if (y == -FH) c = side < 0.3 ? C[6] : side < 0.7 ? C[5] : C[4];
                else if (y == -1) c = W[1];
                else if (y == -5) c = W[0];
                else
                {
                    bool top = y == -8 || y == -4, bot = y == -6 || y == -2;
                    c = W[top ? (side < 0.45 ? 4 : 3) : bot ? 2 : 3];
                    if (!top && Rand(x >> 1, y, 5) < 0.2) c = W[bot ? 1 : 2];
                }
                if (x == -HW) c = y == -FH ? C[5] : IRON[4];
                if (x == HW - 1) c = y == -FH ? C[3] : IRON[2];
                foreach (int s in STRAPS)
                {
                    if (x == s && y > -FH) c = y == -7 || y == -3 ? IRON[5] : IRON[4];
                    if (x == s + 1 && y > -FH) c = IRON[2];
                }
                L.Set(x, y, c);
            }
            for (int y = -FH; y <= -4; y++)
            for (int x = -2; x <= 1; x++)
                L.Set(x, y, x == 1 || y == -4 ? C[3] : x == -2 && y == -FH ? C[7] : C[5]);
            uint kh = g < 0.15 ? W[0] : g < 0.4 ? C[4] : g < 0.7 ? C[6] : WHITE;
            L.Set(-1, -7, kh);
            L.Set(0, -7, kh);
            L.Set(-1, -6, kh);
            L.Set(0, -6, Scale(kh, 0.85));
        }

        // the whole chest: past upright the lid stands behind the hoard, before that it covers it
        void PaintChest(uint[] C, uint[] W, double th, double g)
        {
            if (th > 1.2)
            {
                PaintLid(C, W, th, g);
                PaintHoard(C, W, g);
            }
            else
            {
                PaintHoard(C, W, g);
                PaintLid(C, W, th, g);
            }
            PaintFront(C, W, g);
        }

        sealed class Ray
        {
            public double a, len, w, d, f, ph;
        }

        sealed class Loot
        {
            public bool gem, up, rest, gone;
            public uint[] G;
            public double x, y, vx, vy, t0, r, ax, spin, vs, settle, ph, pop;
        }

        sealed class Sparkle
        {
            public double x, y, t0;
        }

        sealed class Bit
        {
            public double x, y, td;
            public uint c;
            public bool mote, done;
        }

        readonly uint[] C, W;
        readonly Rng R;
        readonly Particles ps, dust;
        readonly Lights lights;
        readonly List<Loot> loot = new List<Loot>();
        readonly List<Sparkle> rings = new List<Sparkle>();
        readonly List<Bit> bits = new List<Bit>();
        readonly Ray[] rays;
        double t;
        bool opened, poofed;

        ChestOpen(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            W = Fit(o.Ramp("wood"), 7);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[4], emitter: e);
            lights = new Lights(e);
            dust = new Particles(R, sparks: new[] { C[5], C[5], C[4], C[3] }, emitter: e);
            // the rays that fan out of the open chest
            rays = new Ray[9];
            for (int i = 0; i < rays.Length; i++)
            {
                double u = i / 8.0 * 2 - 1;
                var r = new Ray { a = u * 1.05 + R.Range(-0.06, 0.06) };
                r.len = R.Range(46, 64) * (1.2 - 0.4 * Math.Abs(u));
                r.w = R.Range(0.035, 0.08);
                r.d = R.Range(0, 0.06);
                r.f = R.Range(5, 10);
                r.ph = R.Range(0, TAU);
                rays[i] = r;
            }
            // the loot: coins and gems, launched in a fan out of the mouth
            double n = o.Num("coins");
            for (int i = 0; i < n + 4; i++)
            {
                bool gem = i >= n;
                double s = i % 2 != 0 ? 1 : -1, a = s * R.Range(0.1, 0.5), sp = R.Range(190, 270);
                var it = new Loot { gem = gem, G = gem ? GEMS[(int)((i - n) % GEMS.Length)] : null };
                it.x = R.Range(-7, 7);
                it.y = -12;
                it.vx = Px.Sin(a) * sp;
                it.vy = -Px.Cos(a) * sp;
                it.t0 = T_OPEN + 0.03 + R.Range(0, 0.2);
                it.r = gem ? 2.6 : 3;
                it.ax = R.Range(-0.6, 0.6);
                it.spin = R.Range(0, TAU);
                it.vs = R.Sign() * R.Range(14, 24);
                it.ph = R.Next();
                loot.Add(it);
            }
            // they get picked up from the outside in
            int k = 0;
            foreach (Loot it in loot.OrderByDescending(q => Math.Abs(q.vx)).ToList()) it.pop = T_COLLECT + k++ * 0.04 + R.Range(0, 0.02);
        }

        (double dx, double hop, double lid) Shake()
        {
            double dx = 0, hop = 0, lid = 0;
            foreach (double[] rt in RATTLES)
            {
                double t0 = rt[0], s = rt[1], u = t - t0;
                if (u < 0 || u > 0.18) continue;
                double e = 1 - u / 0.18;
                dx += Px.Sin(u * 90) * 1.6 * s * e;
                if (u < 0.06 && s > 0.5) hop = 1;
                lid = Math.Max(lid, Math.Max(0, Px.Sin(u * 38)) * 0.13 * s * e);
            }
            if (t >= T_OPEN && t < T_OPEN + 0.08) hop = 1;
            // the last strain: the lid creeps up off the seam just before it bursts open
            double strain = Smooth(T_OPEN - 0.14, T_OPEN, t);
            if (t < T_OPEN)
            {
                lid = Math.Max(lid, 0.07 * strain);
                dx += strain > 0.2 ? (Math.Floor(t * 60) % 2 != 0 ? 1 : -1) * 0.6 : 0;
            }
            return (Round(dx), hop, lid);
        }

        double LidAngle(double lid)
        {
            if (t < T_OPEN) return lid;
            double u = t - T_OPEN, w = 30, z = 0.55, wd = w * Math.Sqrt(1 - z * z);
            return 1.98 * (1 - Exp(-z * w * u) * (Px.Cos(wd * u) + z * w / wd * Px.Sin(wd * u)));
        }

        // the light inside: it builds through the rattles, flares as the lid goes, then settles and fades
        double Inner()
        {
            if (t < T_OPEN)
            {
                double k = 0.1 + 0.45 * Smooth(0, T_OPEN, t) + 0.5 * Smooth(T_OPEN - 0.14, T_OPEN, t);
                foreach (double[] rt in RATTLES)
                    if (t > rt[0])
                        k += 0.45 * rt[1] * Exp(-(t - rt[0]) * 9);
                return k;
            }
            return (0.62 + 0.3 * Exp(-(t - T_OPEN) * 5)) * (1 - Smooth(T_COLLECT - 0.3, T_POOF, t)) + 0.05 * Px.Sin(t * 9);
        }

        double RayK(Ray r)
        {
            double u = t - T_OPEN - r.d;
            if (u < 0) return 0;
            return Ease.OutCubic(Clamp(u / 0.1)) * (1 - Smooth(1.0, 2.1, u)) * (0.72 + 0.28 * Px.Sin(t * r.f + r.ph));
        }

        public override void Update(double dt)
        {
            t += dt;
            double g = Inner();
            foreach (double[] rt in RATTLES)
            {
                double t0 = rt[0], s = rt[1];
                if (s > 0.5 && t - dt < t0 + 0.05 && t >= t0 + 0.05)
                    for (int i = 0; i < 4; i++)
                    {
                        double sd = i % 2 != 0 ? 1 : -1;
                        ps.Spawn(P.Dust, sd * R.Range(9, 13), -1, sd * R.Range(20, 50), -R.Range(2, 8), R.Range(0.3, 0.5), DUST, R.Range(1.2, 2.2) * s);
                    }
            }
            // anticipation: motes of light squeeze out of the seam and the keyhole
            if (t < T_OPEN && R.Chance(g * 0.08))
            {
                double x = R.Range(-11, 11);
                ps.Spawn(P.Star, x, -10, x * 1.5, -R.Range(8, 30), R.Range(0.3, 0.6), R.Chance(0.5) ? C[6] : C[5], R.Range(0.4, 1.2));
            }
            if (!opened && t >= T_OPEN)
            {
                opened = true;
                lights.Add(0, -18, 64, C[5], 0.45, 0.7);
                lights.Add(0, -16, 22, WHITE, 0.35, 0.1);
                for (int i = 0; i < 7; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(120, 260);
                    ps.Spawn(P.Spark, R.Range(-8, 8), -13, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.12, 0.26));
                }
                for (int i = 0; i < 8; i++)
                {
                    double s = R.Sign();
                    ps.Spawn(P.Dust, s * R.Range(10, 14), -1, s * R.Range(30, 80), -R.Range(2, 10), R.Range(0.4, 0.7), DUST, R.Range(1.6, 3));
                }
            }
            // the hold: gold dust rising out of the hoard, twinkles in the light
            if (t > T_OPEN && t < T_COLLECT)
            {
                if (R.Chance(0.22)) ps.Spawn(P.Ember, R.Range(-9, 9), -13, R.Range(-12, 12), -R.Range(25, 60), R.Range(0.8, 1.5), C[5], R.Range(0.8, 1.6));
                if (R.Chance(0.12))
                {
                    double a = R.Range(-1, 1), d = R.Range(10, 40);
                    ps.Spawn(P.Star, Px.Sin(a) * d, -12 - Px.Cos(a) * d, 0, -R.Range(4, 12), R.Range(0.3, 0.7), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 2));
                }
            }
            // the loot flies, bounces and settles
            foreach (Loot it in loot)
            {
                if (it.gone || t < it.t0) continue;
                it.up = true;
                if (t >= it.pop + 0.13)
                {
                    it.gone = true;
                    double y = it.y - 6;
                    if (it.gem) rings.Add(new Sparkle { x = it.x, y = y, t0 = t });
                    lights.Add(it.x, y, 18, it.gem ? it.G[4] : C[5], 0.65, 0.25);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = R.Range(0, TAU), sp = R.Range(25, 55);
                        ps.Spawn(P.Star, it.x, y, Px.Cos(a) * sp - it.x * 0.25, Px.Sin(a) * sp - 20, R.Range(0.3, 0.55), i != 0 ? (it.gem ? it.G[5] : C[6]) : WHITE, R.Range(0.6, 1.5));
                    }
                    continue;
                }
                if (it.rest)
                {
                    it.settle += dt;
                    it.vx -= it.vx * 9 * dt;
                    it.x = Clamp(it.x + it.vx * dt, -74, 74);
                    continue;
                }
                it.vy += 950 * dt;
                it.x += it.vx * dt;
                // keep the loot on the canvas: the far ones skid back off its edge
                if (Math.Abs(it.x) > 74)
                {
                    it.x = Sign(it.x) * 74;
                    it.vx *= -0.3;
                }
                it.y += it.vy * dt;
                it.spin += it.vs * dt;
                double bottom = it.y + it.r;
                if (bottom >= -0.5 && it.vy > 0)
                {
                    it.y = -0.5 - it.r;
                    if (it.vy > 110)
                    {
                        ps.Spawn(P.Dust, it.x, -1, R.Range(-20, 20), -R.Range(4, 12), R.Range(0.25, 0.45), DUST, R.Range(1, 1.8));
                        if (it.vy > 220)
                        {
                            for (int i = 0; i < 2; i++) ps.Spawn(P.Spark, it.x, -1, R.Range(-90, 90), -R.Range(60, 140), R.Range(0.1, 0.22));
                            lights.Add(it.x, -2, 9, C[5], 0.5, 0.12);
                        }
                        it.vy = -it.vy * 0.42;
                        it.vx *= 0.5;
                        it.vs *= 0.55;
                    }
                    else
                    {
                        it.rest = true;
                        it.vy = 0;
                    }
                }
            }
            for (int i = rings.Count - 1; i >= 0; i--)
                if (t - rings[i].t0 > 0.3)
                    rings.RemoveAt(i);
            if (!poofed && t >= T_POOF)
            {
                // the empty chest dissolves from the lid down into rising gold dust
                poofed = true;
                lights.Add(0, -12, 40, C[5], 0.5, 0.6);
                L.Begin(-20, -32);
                PaintChest(C, W, LidAngle(0), 0.3);
                L.Outline(W[0]);
                Frame f = L.F;
                double top = 0;
                for (int i = 0; i < f.Color.Length; i++)
                    if (f.Color[i] != 0)
                        top = Math.Min(top, i / f.W + f.Oy);
                for (int i = 0; i < f.Color.Length; i++)
                {
                    uint c = f.Color[i];
                    if (c == 0) continue;
                    double x = i % f.W + f.Ox, y = i / f.W + f.Oy;
                    // in JS the frame's unsigned pixel never equals the signed W[0], so every pixel may shed a mote
                    bits.Add(new Bit { x = x, y = y, c = c, td = T_POOF + (y - top) / -top * 0.32 + Rand(x, y, 21) * 0.05, mote = Rand(x, y, 23) < 0.14 });
                }
            }
            foreach (Bit b in bits)
                if (!b.done && t >= b.td)
                {
                    b.done = true;
                    if (b.mote) dust.Spawn(P.Ember, b.x + 0.5, b.y + 0.5, R.Range(-28, 28), -R.Range(14, 50), R.Range(0.4, 0.8), C[5], R.Range(0.5, 1.1));
                }
            ps.Update(dt);
            dust.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            if (t < T_POOF + 0.2) BlobShadow(p, 0, 15, 1 - Smooth(T_POOF, T_POOF + 0.2, t));
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            double g = Inner();
            var sh = Shake();
            double th = LidAngle(sh.lid);
            // light on the floor and round the chest
            double open = t >= T_OPEN ? 1 : 0;
            Pool(p, 0, 0, 30 + 10 * g, 4, C[4], (0.1 + 0.18 * open) * g);
            p.Glow(0, -16, 24 + 8 * g, C[4], (0.1 + 0.2 * open) * g);
            if (t < T_POOF)
            {
                L.Begin(-20, -32);
                PaintChest(C, W, th, Math.Min(1.2, g));
                L.Outline(W[0]);
                L.Draw(p, sh.dx, -sh.hop);
            }
            else
                // in JS the bit's unsigned color never equals the signed W[0], so a bit about to go always flashes C[6]
                foreach (Bit b in bits)
                    if (!b.done)
                        p.Px(b.x, b.y, b.td - t < 0.04 ? C[6] : b.c);
            // light leaking from the seam before it opens
            if (t < T_OPEN)
            {
                p.GlowLine(-11, -9.5 - sh.hop, 11, -9.5 - sh.hop, 5, C[5], 0.22 * g);
                // light through the crack under the lid, growing as the hoard brightens
                if (g > 0.35 && sh.lid < 0.015)
                    p.Line(-10 + sh.dx, -10 - sh.hop, 9 + sh.dx, -10 - sh.hop, u => Math.Abs(u - 0.5) < 0.2 ? WHITE : C[6], u => (g - 0.35) * 2.2 * (1 - Math.Abs(u - 0.5) * 0.9));
                p.Glow(sh.dx, -7 - sh.hop, 6, C[6], 0.35 * g);
                // each bump lifts the lid off the seam and light shoots out of the crack in thin rays
                if (sh.lid > 0.015)
                    for (int i = 0; i < 8; i++)
                    {
                        double x = -10.5 + i * 3 + sh.dx + (Rand(i, 3) - 0.5) * 1.5, a = -Math.PI / 2 + x / 11 * 1.25, l = (7 + Rand(i, Math.Floor(t * 30), 1) * 12) * Math.Min(1, sh.lid / 0.05);
                        double y0 = -10 - sh.hop;
                        p.Line(x, y0, x + Px.Cos(a) * l, y0 + Px.Sin(a) * l, u => u < 0.25 ? WHITE : u < 0.6 ? C[6] : C[5], u => 1.15 - u);
                        p.GlowLine(x, y0, x + Px.Cos(a) * l * 0.7, y0 + Px.Sin(a) * l * 0.7, 3, C[5], 0.25);
                    }
            }
            // the rays: wedges of light fanning out of the open chest
            foreach (Ray r in rays)
            {
                double k = RayK(r);
                if (k <= 0.01) continue;
                double ox = Px.Sin(r.a) * 7, oy = -12, dx = Px.Sin(r.a), dy = -Px.Cos(r.a), len = r.len * (0.88 + 0.12 * Px.Sin(t * r.f * 0.7 + r.ph)), tw = Px.Tan(r.w);
                double x1 = ox + dx * len, y1 = oy + dy * len;
                p.Shade(Math.Min(ox, x1) - 8, Math.Min(oy, y1) - 8, Math.Max(ox, x1) + 8, Math.Max(oy, y1) + 8, (x, y, q) =>
                {
                    double rx = x - ox, ry = y - oy, d = rx * dx + ry * dy;
                    if (d < 0 || d > len) return 0;
                    double qq = Math.Abs(rx * dy - ry * dx), hw = 0.6 + d * tw, f = qq < hw ? 1 - 0.35 * (qq / hw) : 0.65 * (1 - (qq - hw) / 1.6);
                    if (f <= 0) return 0;
                    q.l = qq < hw * 0.45 ? C[6] : C[5];
                    q.k = k * 0.55 * f * Px.Pow(1 - d / len, 1.4) * Smooth(5, 14, d);
                    return 0;
                });
            }
            // the release: a flare out of the mouth
            if (t >= T_OPEN && t < T_OPEN + 0.2)
            {
                double a = (t - T_OPEN) / 0.2;
                Sim.Flare(p, 0, -14, 22 * Px.Pow(1 - a, 1.4), 1 - a, C);
            }
            // the loot
            foreach (Loot it in loot)
            {
                if (!it.up || it.gone) continue;
                double pu = t - it.pop, lift = pu > 0 ? 6 * Ease.OutCubic(Clamp(pu / 0.1)) : 0;
                bool wh = pu > 0.095;
                double y = it.y - lift, ax = it.ax, spin = it.spin;
                bool flat = false;
                if (it.rest && !it.gem)
                {
                    // a coin falls flat with a wobble, then lies there as a lens of gold
                    double w = Exp(-it.settle * 8) * Math.Abs(Px.Cos(it.settle * 22));
                    ax = 0;
                    spin = Px.Acos(Clamp(0.5 + 0.5 * w, 0, 1));
                    flat = w < 0.12;
                    y = flat ? -1.5 - lift : -it.r * Px.Cos(spin) - lift;
                }
                if (it.y > -10) BlobShadow(p, it.x, 3, 0.7 * (1 - Clamp(-it.y / 30)));
                if (it.gem) Gemlet(IL, p, it.x, y, it.r, it.rest ? 0.4 * Sign(it.vs) : it.spin, it.G, wh);
                else Coin(IL, p, flat ? Round(it.x) + 0.5 : it.x, y, it.r, ax, flat ? Px.Acos(0.5) : spin, C, wh, 1, !flat);
                p.Glow(it.x, y, 9, it.gem ? it.G[4] : C[5], it.rest ? 0.26 : 0.18);
                p.Glow(it.x, y, 3, WHITE, 0.12);
                // resting loot glints in turn
                if (it.rest && t < it.pop)
                {
                    double u = (t * 0.9 + it.ph) % 1;
                    if (u < 0.16) Glint(p, it.x - 1, y - 1.5, 2.6 * Px.Sin(u / 0.16 * Math.PI), it.gem ? it.G[5] : C[6], 0.5);
                }
            }
            foreach (Sparkle r in rings)
            {
                double k = (t - r.t0) / 0.3, e = Ease.OutCubic(k);
                p.Ring(r.x, r.y, 2 + 5 * e, 1, k < 0.3 ? WHITE : C[6], 1.15 - k);
                p.Glow(r.x, r.y, 12, C[5], 0.45 * (1 - k));
                if (k < 0.5) Glint(p, r.x, r.y, 3.4 * (1 - k / 0.5), C[6], 0.55);
            }
            ps.Draw(p, Layer.Front);
            dust.Draw(p);
            lights.Draw(p);
        }
    }
}
