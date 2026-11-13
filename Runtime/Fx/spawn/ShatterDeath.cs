// The faceted ice shell, the cracks, and the shards, mist and glints of the burst are adapted from Sprite
// Fusion's open-source ice (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class ShatterDeath : Effect
    {
        static readonly Pose FEAR = new Pose { ["turn"] = 0.9, ["lean"] = -2, ["crouch"] = 2, ["fx"] = -9, ["fy"] = -31, ["bx"] = 9, ["by"] = -31, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.2, Poses.READY),
                new PoseKey(0.08, FEAR, "snap"),
                new PoseKey(0.3, new Pose(FEAR) { ["lean"] = -3, ["fy"] = -34 }),
                new PoseKey(0.5, FEAR),
                new PoseKey(3.3, FEAR),
            },
            Hits = new[]
            {
                new HitCue { T = 0.5, Stop = 0.02, Shake = 0.06, Amp = 1 },
                new HitCue { T = 1.4, Stop = 0.09, Shake = 0.26, Amp = 3 },
            },
        };

        const double DURATION = 3.3, LOCK = 0.5, CRACK = 1.05, BURST = 1.4;

        public static readonly Module Module = new Module("shatter-death", "pixelfx/fx/spawn/shatter-death", 172, 96, 84, 84, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0e1636", "#1a2f66", "#2557a3", "#3488d6", "#5fc0f0", "#a2e6ff", "#dcf8ff", "#ffffff" },
                ["seed"] = 14,
            },
            (o, e) => new ShatterDeath(o, e), act: Act, actor: 2, actorAlpha: (t, o) => t < BURST ? 1 : 0)
        {
            Title = "Shatter death",
            Desc = "Frost races up the body and seals it in faceted ice, a glint sweeps it, cracks spread from the chest, then it bursts into its own facets that tumble, bounce and melt.",
            Tags = new[] { "spawn", "death", "ice", "freeze", "shatter", "shards", "one-shot" },
            Bg = "ground",
        };

        sealed class Spike
        {
            public double x, b, h, w, lean, at;
        }

        sealed class Piece
        {
            public int id, n;
            public double x, y, vx, vy, a, va, r, life, max;
            public Dictionary<(int, int), uint> map;
            public bool rest;
        }

        sealed class Rime
        {
            public double x, w, t0;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly SpawnLib.Body body;
        readonly int W, H, X0, Y0;
        readonly int[] ice;
        readonly uint[] col, glass;
        readonly bool[] solid;
        readonly List<Spike> spikes = new List<Spike>();
        readonly double IX, IY;
        readonly List<List<double[]>> cracks = new List<List<double[]>>();
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Rime> rime = new List<Rime>();
        double t;
        bool locked, cracked, burst;

        ShatterDeath(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            body = SpawnLib.BodyOf(o.Actor, C);
            W = body.w + 2;
            H = body.h + 1;
            X0 = body.x - 1;
            Y0 = body.y - 1;
            // the ice: the body grown by a pixel (not below its feet), cut into facets round a few seeds
            ice = new int[W * H];
            for (int i = 0; i < ice.Length; i++) ice[i] = -1;
            col = new uint[W * H];
            glass = new uint[W * H];
            solid = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int mx = x - 1, my = y - 1;
                    if (body.On(mx, my) || (my < body.h - 1 && (body.On(mx - 1, my) || body.On(mx + 1, my) || body.On(mx, my - 1) || body.On(mx, my + 1)))) ice[y * W + x] = 0;
                }
            var seeds = new List<double[]>();
            for (int i = 0; i < 22; i++)
            {
                double x, y;
                int tries = 0;
                do
                {
                    x = R.Range(0, W);
                    y = R.Range(0, H);
                } while (ice[(int)Math.Floor(y) * W + (int)Math.Floor(x)] < 0 && ++tries < 40);
                seeds.Add(new[] { x, y });
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (ice[i] < 0) continue;
                    double d1 = 1e9, d2 = 1e9;
                    int k = 0;
                    for (int j = 0; j < seeds.Count; j++)
                    {
                        double d = Hypot(x + 0.5 - seeds[j][0], (y + 0.5 - seeds[j][1]) * 0.85);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                            k = j;
                        }
                        else if (d < d2) d2 = d;
                    }
                    ice[i] = k;
                    // each facet catches the light by where it faces: up and left bright, down and right deep
                    double sx = seeds[k][0], sy = seeds[k][1];
                    double lit = Clamp(0.55 + 0.35 * (Rand(k, 41) - 0.5) - 0.5 * ((sx - W / 2.0) / W) - 0.55 * ((sy - H / 2.0) / H));
                    uint own = body.Cell(x - 1, y - 1)?.c ?? 0;
                    // under the ice: the body's own colours, gone cold; over it: a see-through facet catching the light
                    col[i] = own != 0 ? Mix(own, C[4], 0.36) : C[3];
                    glass[i] = C[3 + (int)Math.Min(2, Math.Floor(lit * 3))];
                    uint sc = 0;
                    if (d2 - d1 < 0.42 && own == 0) sc = lit > 0.5 ? C[6] : C[5]; // the seams between facets, out in the shell
                    else if (d2 - d1 < 0.42) col[i] = Mix(col[i], C[6], 0.45); // and faint where they cross the body
                    if (!At(x - 1, y) || !At(x, y - 1)) sc = C[6]; // lit outer rim, top and left
                    if (!At(x + 1, y) || (!At(x, y + 1) && y < H - 1)) sc = C[3]; // shaded rim, bottom and right
                    if (sc != 0)
                    {
                        col[i] = sc;
                        solid[i] = true;
                    }
                }
            // rime spikes where the ice pushes out: on the shoulders, the head and the feet
            for (int i = 0; i < 7; i++)
            {
                int x = R.Int(1, W - 2), y0 = R.Int(2, H - 3);
                int y = y0;
                while (y > 0 && At(x, y - 1)) y--;
                if (At(x, y)) spikes.Add(new Spike { x = X0 + x + 0.5, b = Y0 + y + 1, h = R.Range(3, 6), w = R.Range(2.5, 3.5), lean = R.Range(-2.5, 2.5), at = LOCK + R.Range(-0.02, 0.08) });
            }
            foreach (int sd in new[] { -1, 1 })
                for (int i = 0; i < 2; i++)
                {
                    double h = i != 0 ? R.Range(5, 7) : R.Range(8, 11);
                    spikes.Add(new Spike { x = sd * (i != 0 ? R.Range(11, 13) : R.Range(7, 9)), b = 0, h = h, w = i != 0 ? 3.5 : 4.5, lean = sd * h * R.Range(0.25, 0.45), at = R.Range(0.08, 0.2) });
                }
            // cracks from the chest
            IX = Round(W * 0.48);
            IY = Round(H * 0.42);
            for (int i = 0; i < 7; i++) Walk(IX, IY, (double)i / 7 * TAU + R.Range(-0.3, 0.3), 20);

            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(x => 0, e);
            lights = new Lights(e);
        }

        bool At(double x, double y) => x >= 0 && y >= 0 && x < W && y < H && ice[(int)y * W + (int)x] >= 0;

        void Walk(double x, double y, double a, double len)
        {
            var pts = new List<double[]> { new[] { x, y } };
            for (int s = 0; s < len; s++)
            {
                a += R.Range(-0.5, 0.5);
                x += Px.Cos(a) * R.Range(1.4, 2.4);
                y += Px.Sin(a) * R.Range(1.4, 2.4);
                if (!At(Math.Floor(x), Math.Floor(y))) break;
                pts.Add(new[] { x, y });
                if (pts.Count > 3 && cracks.Count < 18 && R.Chance(0.15)) Walk(x, y, a + R.Sign() * R.Range(0.6, 1.1), len / 2);
            }
            cracks.Add(pts);
        }

        // the frost line, mask row
        double Level(double t) => H * (1 - Ease.OutCubic(Clamp(t / LOCK)));

        // the burst: every facet flies as one piece of ice, carrying its own pixels
        void Shatter()
        {
            var cells = new Dictionary<int, List<(int x, int y, uint c)>>();
            var keys = new List<int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int k = ice[y * W + x];
                    if (k < 0) continue;
                    if (!cells.ContainsKey(k))
                    {
                        cells[k] = new List<(int, int, uint)>();
                        keys.Add(k);
                    }
                    int i = y * W + x;
                    cells[k].Add((x, y, solid[i] ? col[i] : Mix(col[i], glass[i], 0.3)));
                }
            foreach (int key in keys)
            {
                var px = cells[key];
                double cx = 0, cy = 0;
                foreach (var q in px)
                {
                    cx += q.x + 0.5;
                    cy += q.y + 0.5;
                }
                cx /= px.Count;
                cy /= px.Count;
                var map = new Dictionary<(int, int), uint>();
                double r = double.NegativeInfinity;
                foreach (var q in px) r = Math.Max(r, Hypot(q.x + 0.5 - cx, q.y + 0.5 - cy));
                r += 1;
                foreach (var q in px) map[(RoundInt(q.x + 0.5 - cx), RoundInt(q.y + 0.5 - cy))] = q.c;
                double dx = cx - IX, dy = cy - IY, d = Hypot(dx, dy);
                if (d == 0 || double.IsNaN(d)) d = 1;
                double sp = R.Range(55, 115);
                double life = R.Range(1.3, 1.85);
                var piece = new Piece { id = pieces.Count + 1, x = X0 + cx, y = Y0 + cy, vx = (dx / d) * sp + R.Range(-18, 18), vy = (dy / d) * sp * 0.6 - R.Range(110, 220), a = 0, va = R.Range(-9, 9), r = r, map = map, life = life, max = life, rest = false, n = px.Count };
                pieces.Add(piece);
            }
            uint[] shard = { C[7], C[6], C[5] };
            for (int i = 0; i < 64; i++)
            {
                double an = -Math.PI / 2 + R.Range(-1.45, 1.45), sp = R.Range(60, 180);
                bits.Add(new Bits.Bit
                {
                    kind = B.Shard, x = X0 + IX + R.Range(-6, 6), y = Y0 + IY + R.Range(-8, 8), vx = Px.Cos(an) * sp, vy = Px.Sin(an) * sp, g = 0.85, drag = 0.6, bounce = 0.3,
                    life = R.Range(0.9, 1.6), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1.5, 3.2), c = R.Pick(shard),
                });
            }
            for (int i = 0; i < 14; i++) ps.Spawn(P.Vapour, R.Range(-10, 10), -R.Range(1, 20), R.Sign() * R.Range(20, 80), -R.Range(4, 20), R.Range(0.6, 1.1), R.Chance(0.5) ? C[6] : C[5], R.Range(2.5, 4.5));
            for (int i = 0; i < 16; i++) ps.Spawn(P.Star, R.Range(-16, 16), -R.Range(4, 38), R.Range(-40, 40), -R.Range(10, 60), R.Range(0.4, 1), R.Pick(new[] { WHITE, C[6] }), R.Range(1, 2.4));
            lights.Add(0, Y0 + IY, 46, WHITE, 0.75, 0.14);
            lights.Add(0, Y0 + IY, 70, C[4], 0.6, 0.5);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the cold climbs: rime sparkles at the frost line, vapour rolls off the feet
            if (t < LOCK)
            {
                double y = Y0 + Level(t);
                if (R.Chance(0.7)) ps.Spawn(P.Star, R.Range(-10, 10), y, R.Range(-12, 12), -R.Range(0, 18), R.Range(0.25, 0.5), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 1.8));
                if (R.Chance(0.25)) ps.Spawn(P.Vapour, R.Range(-8, 8), -1, R.Sign() * R.Range(15, 50), -R.Range(2, 10), R.Range(0.6, 1), C[6], R.Range(2, 3.5));
            }
            if (!locked && t >= LOCK)
            {
                locked = true;
                lights.Add(0, -20, 54, C[4], 0.6, 0.3);
                for (int i = 0; i < 6; i++) ps.Spawn(P.Star, R.Range(-12, 12), -R.Range(4, 36), 0, -4, R.Range(0.35, 0.7), R.Pick(new[] { WHITE, C[6] }), R.Range(1.2, 2.4));
            }
            if (locked && t < CRACK && R.Chance(0.08)) ps.Spawn(P.Star, R.Range(-11, 11), -R.Range(2, 36), 0, -3, R.Range(0.3, 0.6), R.Pick(new[] { WHITE, C[6] }), R.Range(0.8, 2));
            if (!cracked && t >= CRACK)
            {
                cracked = true;
                for (int i = 0; i < 5; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(60, 150);
                    bits.Add(new Bits.Bit
                    {
                        kind = B.Shard, x = X0 + IX, y = Y0 + IY, vx = Px.Cos(an) * sp, vy = Px.Sin(an) * sp - 60, g = 0.8, drag = 0.6, bounce = 0.3,
                        life = R.Range(0.5, 0.9), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1, 2), c = C[6],
                    });
                }
                lights.Add(0, Y0 + IY, 24, WHITE, 0.7, 0.1);
            }
            if (!burst && t >= BURST)
            {
                burst = true;
                Shatter();
            }
            // the pieces: tumble, bounce, skid to a stop, melt
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece q = pieces[i];
                if ((q.life -= dt) <= 0)
                {
                    pieces.RemoveAt(i);
                    continue;
                }
                if (q.rest)
                {
                    if (t < DURATION - 0.9 && R.Chance(0.02)) ps.Spawn(P.Vapour, q.x, q.y - 1, R.Range(-4, 4), -R.Range(4, 10), R.Range(0.5, 0.8), C[5], R.Range(1.5, 2.5));
                    continue;
                }
                q.vy += 900 * dt;
                q.vx -= q.vx * 0.5 * dt;
                q.x += q.vx * dt;
                q.y += q.vy * dt;
                q.a += q.va * dt;
                double g = -q.r * 0.55;
                if (q.y > g && q.vy > 0)
                {
                    q.y = g;
                    if (q.vy > 90)
                    {
                        rime.Add(new Rime { x = q.x, w = 2 + q.r * 0.6, t0 = t });
                        q.vy *= -0.32;
                        q.vx *= 0.65;
                        q.va = q.va * 0.5 + R.Range(-4, 4);
                        if (q.n > 8) ps.Spawn(P.Star, q.x, q.y, 0, -6, 0.3, WHITE, 1.2);
                        for (int j = 0; j < 2; j++) ps.Spawn(P.Dust, q.x + R.Range(-2, 2), -0.5, R.Sign() * R.Range(10, 30), -R.Range(2, 8), R.Range(0.3, 0.5), C[5], R.Range(1.2, 2));
                    }
                    else if (Math.Abs(q.vx) > 12)
                    {
                        q.vy = 0;
                        q.vx *= 0.9;
                        q.va *= 0.85;
                    }
                    else
                    {
                        q.rest = true;
                        q.vx = q.vy = 0;
                    }
                }
            }
            bits.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // frost on the floor under the feet, spreading as the ice locks, gone as the pieces melt
            double k = Smooth(0.1, LOCK, t) * (1 - Smooth(BURST + 0.6, BURST + 1.6, t)), reach = 6 + 12 * Smooth(0, LOCK + 0.3, t);
            if (k > 0)
                for (double x = Math.Floor(-reach); x <= Math.Ceiling(reach); x++)
                {
                    double h = Rand(x, 17), e = 1 - Math.Abs(x) / (reach + 1);
                    p.Dot(x, 0, h < 0.3 ? WHITE : C[6], k * Math.Min(1, e * 2));
                    if (h < 0.5) p.Dot(x, 1, C[5], k * e);
                    if (h < 0.35 && e > 0.3) p.Dot(x, -1, C[6], k * e);
                }
            if (k > 0) p.Glow(0, -2, 26, C[4], 0.25 * k);
            // rime left where the pieces hit the floor
            foreach (Rime r in rime)
            {
                double a = Clamp((t - r.t0) / 0.08) * (1 - Smooth(DURATION - 1, DURATION - 0.15, t));
                for (double x = Math.Floor(r.x - r.w); x <= r.x + r.w; x++)
                {
                    double h = Rand(x, 23);
                    if (h < 0.7) p.Dot(x, 0, h < 0.25 ? WHITE : C[6], a);
                    if (h < 0.3) p.Dot(x, 1, C[5], a * 0.7);
                }
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            if (!burst)
            {
                // the ice body: everything below the frost line, a white rime edge at the line itself
                double lv = Level(t), shake = t > CRACK && t < BURST ? (Math.Floor(t * 50) % 2 != 0 ? 1 : -1) * (t > BURST - 0.15 ? 1 : 0.5) : 0;
                double glint = t > LOCK ? ((t - LOCK) * 0.9) % 1.6 : -1;
                for (int y = 0; y < H; y++)
                {
                    if (y < Math.Floor(lv)) continue;
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;
                        if (ice[i] < 0) continue;
                        // JS reads col from a Uint32Array (unsigned) and every palette color is signed, so a color read
                        // from col never equals one from the palette, and a palette color never equals col[i]
                        bool fromCol = !(y == Math.Floor(lv) && t < LOCK) && !(t >= LOCK && t < LOCK + 0.05);
                        uint c = y == Math.Floor(lv) && t < LOCK ? (Rand(x, 5) < 0.6 ? WHITE : C[6]) : t >= LOCK && t < LOCK + 0.05 ? WHITE : col[i];
                        // a glint runs diagonally across the ice
                        double g = Math.Abs((x + (H - y) * 0.6) / (W + H * 0.6) - glint);
                        bool on = glint >= 0 && glint <= 1;
                        if (on && g < 0.035)
                        {
                            c = WHITE;
                            fromCol = false;
                        }
                        else if (on && g < 0.07 && (fromCol || c != C[3]))
                        {
                            c = C[6];
                            fromCol = false;
                        }
                        p.Px(X0 + x + shake, Y0 + y, c);
                        if (!solid[i] && fromCol) p.Blend(X0 + x + shake, Y0 + y, glass[i], 0.3);
                    }
                }
                // the dark outline that holds the ice together against the floor and the dark
                for (double y = Math.Max(0, Math.Floor(lv) - 1); y <= H; y++)
                    for (int x = -1; x <= W; x++)
                        if (!At(x, y) && (At(x - 1, y) || At(x + 1, y) || (At(x, y + 1) && y + 1 >= lv) || At(x, y - 1)) && y < H)
                            p.Px(X0 + x + shake, Y0 + y, C[0]);
                foreach (Spike s in spikes)
                {
                    double g = Ease.OutBack(Clamp((t - s.at) / 0.12), 2.4);
                    if (g > 0) Sim.Crystal(p, s.x + shake, s.b, s.h * g, s.w, s.lean * g, C, white: t < s.at + 0.04);
                }
                if (cracked)
                {
                    double k = Clamp((t - CRACK) / 0.25);
                    foreach (List<double[]> pts in cracks)
                        for (int i = 0, n = (int)Math.Floor(pts.Count * k); i < n; i++)
                        {
                            double x = pts[i][0], y = pts[i][1];
                            bool fresh = i > n - 3 && t - CRACK < 0.3;
                            p.Px(X0 + x + shake, Y0 + y, fresh ? WHITE : C[7]);
                            if (!fresh) p.Px(X0 + x + shake, Y0 + y + 1, C[1]);
                        }
                }
                // cold light from the ice, a flash as it locks
                p.Glow(0, -18, 30, C[4], 0.18 * Smooth(0, LOCK, t));
                if (t > LOCK && t < LOCK + 0.18) Sim.Sparkle(p, -4, Y0 + 8, Round(8 * (1 - (t - LOCK) / 0.18)), WHITE, C[5], 0.6);
            }
            else if (t < BURST + 0.08)
            {
                // the burst frame
                double k = (t - BURST) / 0.08;
                p.Circle(X0 + IX, Y0 + IY, 10 * (1 - k) + 3, WHITE);
            }
            // the pieces: each drawn back from its own little map, so they turn without holes
            foreach (Piece q in pieces)
            {
                double ca = Px.Cos(q.a), sa = Px.Sin(q.a), fade = Math.Min(1, q.life / 0.5);
                int r = (int)Math.Ceiling(q.r);
                for (int py = -r; py <= r; py++)
                    for (int px = -r; px <= r; px++)
                    {
                        int ux = RoundInt(px * ca + py * sa), uy = RoundInt(-px * sa + py * ca);
                        // a melting piece loses pixels at random rather than dithering away
                        if (q.map.TryGetValue((ux, uy), out uint c) && c != 0 && Rand(ux, uy, q.id) < fade)
                            p.Px(Math.Floor(q.x) + px, Math.Floor(q.y) + py, q.max - q.life < 0.05 ? WHITE : c);
                    }
                if (q.n > 6) p.Glow(q.x, q.y, 6, C[5], 0.2 * fade);
            }
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
