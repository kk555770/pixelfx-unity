using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    // Adapted from Sprite Fusion's open-source pixel art VFX
    // (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
    public enum P
    {
        Spark,
        Mote,
        Streak,
        Flake,
        Vapour,
        Dust,
        Star,
        Ember,
        Smoke,
        Fire,
        Brand,
        Grit,
    }

    [Flags]
    public enum PF
    {
        None = 0,
        Smokes = 1,
        Big = 2,
        Back = 4,
    }

    public enum B
    {
        Shard,
        Chunk,
        Drip,
    }

    public static class Sim
    {
        public const double GRAVITY = 950;
        public static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        internal static readonly int[][] Diag = { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };
        internal static readonly uint[] Hot = Ramp("#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d");
        public static readonly uint[] FIRE_RAMP = Ramp("#ffffff", "#fff3a6", "#ffe26b", "#ffc23d", "#ff8a2a", "#f2561d", "#c9281f", "#8a1f17", "#4a1712");
        public static readonly uint[] SMOKE_RAMP = Ramp("#7b6f72", "#5d5660", "#433d48", "#2b2630", "#1a171e");

        internal static uint RampAt(uint[] r, double t) => r[Math.Min(r.Length - 1, Math.Max(0, (int)Math.Floor(t * r.Length)))];

        internal static void Born(Emitter e, out int x, out int y)
        {
            x = e?.X ?? 0;
            y = e?.Y ?? 0;
        }

        internal static void Shift(Painter p, Emitter e, int x, int y)
        {
            if (e != null) e.Shift(p, x, y);
        }

        public static void Sparkle(Painter p, double x, double y, double arm, uint c, uint tip, double k = 0.6)
        {
            int n = RoundInt(arm);
            if (n < 1) return;
            double X = Math.Floor(x), Y = Math.Floor(y);
            for (int d = 1; d <= n; d++)
            {
                uint cc = d > n * 0.6 ? tip : c;
                foreach (int[] v in Cross) p.Px(X + v[0] * d, Y + v[1] * d, cc);
            }
            if (n >= 3)
                foreach (int[] v in Diag)
                    p.Px(X + v[0], Y + v[1], c);
            if (n >= 6)
                foreach (int[] v in Diag)
                    p.Px(X + v[0] * 2, Y + v[1] * 2, tip);
            p.Px(X, Y, WHITE);
            p.Glow(x, y, 4 + n * 2, c, k);
        }

        public static void Sparkle(Painter p, double x, double y, double arm, uint c) => Sparkle(p, x, y, arm, c, c);

        public static void Flare(Painter p, double x, double y, double l, double k, uint[] r)
        {
            if (l < 1) return;
            int n = r.Length;
            for (int i = 0; i < 8; i++)
            {
                double an = i * Math.PI / 4, len = i % 4 == 0 ? l : i % 2 == 0 ? l * 0.6 : l * 0.3, ca = Math.Cos(an), sa = Math.Sin(an);
                for (double d = 0; d <= len; d += 0.5)
                {
                    double u = d / len;
                    p.Dot(x + ca * d, y + sa * d, u < 0.35 ? WHITE : u < 0.7 ? r[n - 2] : r[n - 3], u < 0.6 ? 1 : k * 1.5 * (1 - u) + 0.2);
                }
            }
        }

        public static void Band(Painter p, double cx, double cy, double r, double sy, double th, double k, uint[] ramp, Func<double, bool> keep = null, bool hot = false)
        {
            if (r <= 0 || k <= 0) return;
            int n = ramp.Length;
            for (int j = 0, m = (int)Math.Ceiling(r * TAU * 1.2); j < m; j++)
            {
                double ang = (double)j / m * TAU, ca = Math.Cos(ang), sa = Math.Sin(ang);
                if (keep != null && !keep(ang)) continue;
                for (double o = th; o >= 0; o -= 0.5)
                {
                    double u = o / th;
                    uint c = u < 0.15 ? (hot ? WHITE : ramp[n - 1]) : u < 0.4 ? ramp[n - 2] : u < 0.7 ? ramp[n - 3] : ramp[n - 4];
                    p.Dot(cx + ca * (r - o), cy + sa * (r - o) * sy, c, k * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }
    }

    public sealed class Particles
    {
        public sealed class Particle
        {
            public P k;
            public double x, y, vx, vy, life, max, size, seed, delay;
            public uint col;
            public PF flag;
            internal int bornX, bornY;
        }

        public readonly Rng R;
        public readonly List<Particle> List = new List<Particle>();
        public double T;
        readonly Func<double, double> ground;
        readonly uint[] sparks;
        readonly uint haze;
        readonly Action<double, double> onLand;
        readonly Emitter emitter;

        public Particles(Rng rng = null, double seed = 1, Func<double, double> ground = null, uint[] sparks = null, uint haze = 0, Action<double, double> onLand = null, Emitter emitter = null)
        {
            R = rng ?? new Rng(seed);
            this.ground = ground ?? (x => double.PositiveInfinity);
            this.sparks = sparks ?? Sim.Hot;
            this.haze = haze;
            this.onLand = onLand;
            this.emitter = emitter;
        }

        public int Count => List.Count;

        public void Spawn(P k, double x, double y, double vx, double vy, double life, uint col = WHITE, double size = 1, double delay = 0, PF flag = PF.None)
        {
            var q = new Particle { k = k, x = x, y = y, vx = vx, vy = vy, life = life, max = life, col = col, size = size, seed = R.Next() * 100, delay = delay, flag = flag };
            Sim.Born(emitter, out q.bornX, out q.bornY);
            List.Add(q);
        }

        public Particle Landing { get; private set; }

        internal void BornAt(int x, int y)
        {
            Particle q = List[List.Count - 1];
            q.bornX = x;
            q.bornY = y;
        }

        void SpawnFrom(Particle from, P k, double x, double y, double vx, double vy, double life, uint col, double size, double delay, PF flag)
        {
            Spawn(k, x, y, vx, vy, life, col, size, delay, flag);
            BornAt(from.bornX, from.bornY);
        }

        public void Update(double dt)
        {
            T += dt;
            Rng r = R;
            double t = T;
            for (int i = List.Count - 1; i >= 0; i--)
            {
                Particle p = List[i];
                if (p.delay > 0)
                {
                    p.delay -= dt;
                    continue;
                }
                if ((p.life -= dt) <= 0)
                {
                    if (p.k == P.Fire && (p.flag & PF.Smokes) != 0)
                    {
                        bool big = (p.flag & PF.Big) != 0;
                        SpawnFrom(p, P.Smoke, p.x, p.y, p.vx * 0.6, p.vy * 0.6 - 6, big ? r.Range(1.3, 2.3) : r.Range(0.8, 1.4), 0, p.size * (big ? 0.85 : 0.7), 0, big ? PF.Big : PF.None);
                    }
                    List.RemoveAt(i);
                    continue;
                }
                switch (p.k)
                {
                    case P.Fire:
                        p.vy -= 95 * dt;
                        p.vx += Math.Sin(t * 17 + p.seed) * 30 * dt;
                        p.vx -= p.vx * 5 * dt;
                        p.vy -= p.vy * 3.2 * dt;
                        break;
                    case P.Grit:
                    {
                        p.vy += Sim.GRAVITY * dt;
                        p.vx -= p.vx * 0.8 * dt;
                        double g = ground(p.x), ny = p.y + p.vy * dt;
                        p.x += p.vx * dt;
                        if (p.vy > 0 && p.y <= g && ny >= g)
                        {
                            p.y = g - 0.25;
                            if (p.vy > 130 && r.Chance(0.4))
                            {
                                p.vy = -p.vy * 0.25;
                                p.vx = p.vx * 0.5 + r.Range(-10, 10);
                            }
                            else
                            {
                                p.vx = p.vy = 0;
                                p.life = Math.Min(p.life, 0.5);
                            }
                        }
                        else p.y = ny;
                        continue;
                    }
                    case P.Brand:
                    {
                        p.vy += Sim.GRAVITY * 0.8 * dt;
                        p.vx -= p.vx * 0.9 * dt;
                        if (r.Chance(0.9 * Math.Min(1, dt * 60)))
                            SpawnFrom(p, P.Fire, p.x, p.y, p.vx * 0.1 + r.Range(-7, 7), p.vy * 0.1 - 10, r.Range(0.16, 0.32), 0, r.Range(0.9, 1.7) * (0.5 + p.life / p.max * 0.6), 0, r.Chance(0.4) ? PF.Smokes : PF.None);
                        double g = ground(p.x), ny = p.y + p.vy * dt;
                        p.x += p.vx * dt;
                        if (p.vy > 0 && p.y <= g && ny >= g)
                        {
                            Landing = p;
                            onLand?.Invoke(p.x, g);
                            Landing = null;
                            List.RemoveAt(i);
                        }
                        else p.y = ny;
                        continue;
                    }
                    case P.Spark:
                    {
                        p.vy += Sim.GRAVITY * 0.45 * dt;
                        p.vx -= p.vx * 2.2 * dt;
                        p.vy -= p.vy * 2.2 * dt;
                        double g = ground(p.x), ny = p.y + p.vy * dt;
                        p.x += p.vx * dt;
                        if (p.vy > 0 && p.y <= g && ny >= g)
                        {
                            p.vy = -p.vy * 0.45;
                            p.vx *= 0.7;
                            p.y = g - 0.25;
                        }
                        else p.y = ny;
                        continue;
                    }
                    case P.Streak:
                        p.vx += p.vx * 3 * dt;
                        p.vy += p.vy * 3 * dt;
                        break;
                    case P.Flake:
                        p.vx -= p.vx * 2.2 * dt;
                        p.vy -= p.vy * 2.2 * dt - 35 * dt;
                        p.x += Math.Sin(T * 2.5 + p.seed) * 7 * dt;
                        break;
                    case P.Ember:
                        p.vy += (-30 + Math.Sin(t * 3 + p.seed) * 20) * dt;
                        p.vx += Math.Cos(t * 2.3 + p.seed) * 45 * dt;
                        p.vx -= p.vx * 1.6 * dt;
                        p.vy -= p.vy * 1.6 * dt;
                        break;
                    case P.Smoke:
                    {
                        bool big = (p.flag & PF.Big) != 0;
                        p.vy -= (big ? 23 : 16) * dt;
                        p.vx += Math.Sin(t * 1.3 + p.seed) * 8 * dt;
                        p.vx -= p.vx * 1.6 * dt;
                        p.vy -= p.vy * 1.2 * dt;
                        break;
                    }
                    case P.Mote:
                        p.vx -= p.vx * 0.6 * dt;
                        p.vy -= p.vy * 0.6 * dt;
                        break;
                    default:
                    {
                        double drag = Soft(p.k) ? 4 : 1.6;
                        p.vx -= p.vx * drag * dt;
                        p.vy -= p.vy * drag * dt + 5 * dt;
                        break;
                    }
                }
                p.x += p.vx * dt;
                p.y += p.vy * dt;
            }
        }

        static bool Soft(P k) => k == P.Vapour || k == P.Dust || k == P.Smoke;

        public void Draw(Painter p, Layer layer = Layer.Both)
        {
            foreach (Particle q in List)
            {
                if (q.delay > 0) continue;
                if (layer != Layer.Both && (Soft(q.k) || (q.flag & PF.Back) != 0 ? Layer.Back : Layer.Front) != layer) continue;
                Sim.Shift(p, emitter, q.bornX, q.bornY);
                DrawOne(p, q);
            }
            p.Shift(0, 0);
        }

        void DrawOne(Painter p, Particle q)
        {
            double k = q.life / q.max, x = q.x, y = q.y, sp = Hypot(q.vx, q.vy);
            if (sp == 0 || double.IsNaN(sp)) sp = 1;
            double ux = q.vx / sp, uy = q.vy / sp;
            switch (q.k)
            {
                case P.Spark:
                {
                    uint[] s = sparks;
                    uint head = k > 0.6 ? s[0] : k > 0.3 ? s[1] : s[2];
                    double len = Math.Min(9, 1 + sp * 0.022);
                    p.Line(x - ux * len, y - uy * len, x - ux * len * 0.4, y - uy * len * 0.4, k > 0.5 ? head : s[s.Length - 1]);
                    p.Line(x - ux * len * 0.45, y - uy * len * 0.45, x, y, head);
                    p.Glow(x, y, 3, head, 0.45 * k);
                    break;
                }
                case P.Mote:
                {
                    double len = Math.Min(16, 0.5 + sp * 0.026) * q.size, a = k < 0.35 ? k / 0.35 : 1;
                    p.Line(x - ux * len, y - uy * len, x - ux * len * 0.35, y - uy * len * 0.35, Scale(q.col, 0.55), a);
                    p.Line(x - ux * len * 0.4, y - uy * len * 0.4, x, y, q.col, a);
                    p.Glow(x, y, 2.5 + q.size, q.col, 0.4 * k);
                    break;
                }
                case P.Streak:
                {
                    double l = Clamp(sp / 30, 1, 7);
                    for (double d = l; d >= 0; d -= 0.5) p.Dot(x - ux * d, y - uy * d, d < 1 ? WHITE : d < l * 0.5 ? q.col : Scale(q.col, 0.6), d < l * 0.5 ? 1 : 1.2 - d / l);
                    p.Glow(x, y, 4, q.col, 0.4);
                    break;
                }
                case P.Flake:
                {
                    double a = Math.Min(1, k * 3);
                    p.Dot(x, y, q.col, a);
                    if (q.size > 1.3)
                        foreach (int[] v in Sim.Cross)
                            p.Dot(x + v[0], y + v[1], Scale(q.col, 0.8), a * 0.8);
                    break;
                }
                case P.Vapour:
                {
                    double r = q.size * (0.6 + (1 - k) * 0.9);
                    p.Disc(x, y, r, q.col, Math.Pow(k, 1.3) * 0.45);
                    if (haze != 0) p.Glow(x, y, r * 1.8, haze, 0.16 * k);
                    break;
                }
                case P.Dust:
                    p.Disc(x, y, q.size * (0.6 + (1 - k) * 0.9), q.col, Math.Pow(k, 1.3) * 0.7);
                    break;
                case P.Smoke:
                {
                    double age = 1 - k;
                    bool big = (q.flag & PF.Big) != 0;
                    double r = q.size * (0.65 + Math.Min(1, age * 2.5) * 0.45 + age * (big ? 0.5 : 0.4));
                    double a = Math.Min(1, age * 12) * Math.Min(1, k * 2.4) * (big ? 0.7 : 0.58);
                    uint c = q.col != 0 ? Mix(q.col, Sim.RampAt(Sim.SMOKE_RAMP, age), age) : Sim.RampAt(Sim.SMOKE_RAMP, age * 1.05);
                    p.Disc(x, y, r, c, a * 0.45);
                    p.Disc(x + r * 0.05, y + r * 0.06, r * 0.74, c, a * 0.62);
                    if (age < 0.5) p.Disc(x - r * 0.22, y - r * 0.3, r * 0.5, Mix(c, WHITE, 0.12), a * 0.4 * (1 - age * 2));
                    break;
                }
                case P.Fire:
                {
                    uint[] f = Sim.FIRE_RAMP;
                    int n = f.Length;
                    double age = 1 - k, heat = Math.Min(0.999, Math.Pow(age, 0.62) + q.seed % 1 * 0.1);
                    int idx = Math.Min(n - 2, (int)Math.Floor(heat * (n - 1)));
                    double r = q.size * (0.55 + 0.45 * Math.Min(1, age * 7)) * (1 - age * 0.55);
                    uint c = f[idx];
                    if (idx >= n - 2) p.Disc(x, y, r, c, Math.Min(1, k * 3) * 0.85);
                    else
                    {
                        p.Circle(x, y, r, f[Math.Min(n - 1, idx + 2)]);
                        p.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, c);
                        if (r > 1.6) p.Circle(x, y - r * 0.9, r * 0.42, c);
                        if (idx <= 3 && r > 1.8) p.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, f[Math.Max(0, idx - 1)]);
                        if (idx < 6) p.Glow(x, y, r * 2.4 + 2, c, 0.18 * k);
                    }
                    break;
                }
                case P.Brand:
                    p.Circle(x, y, 1.6, Sim.Hot[3]);
                    p.Px(x, y, Math.Sin(T * 40 + q.seed) > 0 ? Sim.Hot[0] : Sim.Hot[1]);
                    p.Px(x + 1, y, Sim.Hot[1]);
                    p.Glow(x, y, 7, Sim.Hot[2], 0.45);
                    break;
                case P.Grit:
                    if (k > 0.25 || k * 4 > 0.5)
                    {
                        p.Px(x, y, q.col);
                        if (q.size >= 2)
                        {
                            p.Px(x + 1, y, q.col);
                            p.Px(x, y + 1, Scale(q.col, 0.8));
                            p.Px(x + 1, y + 1, Scale(q.col, 0.7));
                        }
                    }
                    break;
                case P.Ember:
                {
                    uint c = k > 0.6 ? sparks[0] : k > 0.3 ? sparks[1] : sparks[2];
                    if (k < 0.25 && Math.Sin(T * 40 + q.seed) < 0) break;
                    p.Px(x, y, c);
                    if (q.size > 1.5) p.Px(x, y + 1, sparks[sparks.Length - 1]);
                    p.Glow(x, y, 3 + q.size, c, 0.35 * k);
                    break;
                }
                default:
                {
                    double tw = 0.6 + 0.4 * Math.Sin(T * (8 + q.seed % 5) + q.seed);
                    if (q.size < 1)
                    {
                        if (tw * k > 0.25) p.Px(x, y, tw > 0.8 ? WHITE : q.col);
                        p.Glow(x, y, 3, q.col, 0.4 * k * tw);
                        break;
                    }
                    Sim.Sparkle(p, x, y, q.size * tw * Math.Min(1, k * 3), q.col, Scale(q.col, 0.75), 0.5 * k * tw);
                    break;
                }
            }
        }
    }

    public sealed class Blazes
    {
        sealed class Blaze
        {
            public double x, y, life, max, acc, power;
            public int bornX, bornY;
        }

        readonly Particles ps;
        readonly Emitter emitter;
        readonly List<Blaze> list = new List<Blaze>();

        public Blazes(Particles ps, Emitter emitter = null)
        {
            this.ps = ps;
            this.emitter = emitter;
        }

        public int Count => list.Count;

        public void Add(double x, double y, double life, double power)
        {
            var b = new Blaze { x = x, y = y, life = life, max = life, acc = ps.R.Next(), power = power };
            Sim.Born(emitter, out b.bornX, out b.bornY);
            if (ps.Landing != null)
            {
                b.bornX = ps.Landing.bornX;
                b.bornY = ps.Landing.bornY;
            }
            list.Add(b);
        }

        public void Update(double dt)
        {
            Rng r = ps.R;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Blaze b = list[i];
                if ((b.life -= dt) <= 0)
                {
                    list.RemoveAt(i);
                    continue;
                }
                double k = b.life / b.max, intensity = b.power * (k > 0.7 ? 1 : 0.35 + k / 0.7 * 0.65);
                for (b.acc += dt * (6 + 16 * intensity); b.acc > 1; b.acc--)
                {
                    double s = r.Range(1.2, 2.8) * intensity;
                    ps.Spawn(P.Fire, b.x + r.Range(-1.5, 1.5), b.y - s * 0.6, r.Range(-6, 6), r.Range(-30, -12), r.Range(0.28, 0.6), 0, s, 0, PF.Back | (r.Chance(0.3) ? PF.Smokes : PF.None));
                    ps.BornAt(b.bornX, b.bornY);
                    if (r.Chance(0.06))
                    {
                        ps.Spawn(P.Ember, b.x, b.y - 1.5, r.Range(-15, 15), r.Range(-45, -15), r.Range(0.6, 1.4), Sim.Hot[2], 1, 0, PF.Back);
                        ps.BornAt(b.bornX, b.bornY);
                    }
                }
            }
        }

        public void DrawBack(Painter p)
        {
            foreach (Blaze b in list)
            {
                Sim.Shift(p, emitter, b.bornX, b.bornY);
                double k = b.life / b.max;
                p.Px(b.x, b.y, k > 0.4 ? (Math.Sin(b.life * 31 + b.x) > 0 ? Sim.Hot[1] : Sim.Hot[2]) : Sim.Hot[3]);
                p.Glow(b.x, b.y, 6 + 5 * k * b.power, Sim.Hot[2], 0.3 * k * b.power);
            }
            p.Shift(0, 0);
        }
    }

    public sealed class Bits
    {
        public sealed class Bit
        {
            public B kind = B.Shard;
            public double x, y, vx, vy, a, va, s = 1, g = 1, drag, bounce, life = 1, max;
            public uint c = WHITE;
            public uint[] ramp;
            public bool rest;
            internal int bornX, bornY;
        }

        static readonly uint[] ChunkRamp = Ramp("#1c2030", "#3a4a66", "#6e86a8", "#a8c0dc", "#e8f2ff");

        public readonly List<Bit> List = new List<Bit>();
        readonly Func<double, double> ground;
        readonly Emitter emitter;

        public Bits(Func<double, double> ground = null, Emitter emitter = null)
        {
            this.ground = ground ?? (x => double.PositiveInfinity);
            this.emitter = emitter;
        }

        public int Count => List.Count;

        public void Add(Bit b)
        {
            b.max = b.life;
            Sim.Born(emitter, out b.bornX, out b.bornY);
            List.Add(b);
        }

        public void Update(double dt)
        {
            for (int i = List.Count - 1; i >= 0; i--)
            {
                Bit b = List[i];
                if ((b.life -= dt) <= 0)
                {
                    List.RemoveAt(i);
                    continue;
                }
                if (b.rest) continue;
                b.vy += Sim.GRAVITY * b.g * dt;
                b.vx -= b.vx * b.drag * dt;
                b.vy -= b.vy * b.drag * dt;
                b.x += b.vx * dt;
                b.y += b.vy * dt;
                b.a += b.va * dt;
                double floor = ground(b.x), sp = b.vy;
                if (b.g <= 0 || b.y < floor || sp <= 0) continue;
                b.y = floor - 0.01;
                if (b.kind == B.Drip) b.life = 0;
                else if (b.bounce > 0 && sp > 70)
                {
                    b.vy = -sp * b.bounce;
                    b.vx *= 0.7;
                    b.va *= 0.6;
                }
                else
                {
                    b.rest = true;
                    b.vx = b.vy = 0;
                }
            }
        }

        public void Draw(Painter p)
        {
            foreach (Bit b in List)
            {
                Sim.Shift(p, emitter, b.bornX, b.bornY);
                double a = Math.Min(1, b.life / 0.4), ca = Math.Cos(b.a), sa = Math.Sin(b.a);
                if (b.kind == B.Shard)
                {
                    for (double d = -b.s / 2; d <= b.s / 2; d += 0.5) p.Dot(b.x + ca * d, b.y + sa * d, d > b.s / 2 - 0.6 ? WHITE : b.c, a);
                }
                else if (b.kind == B.Drip)
                {
                    p.Dot(b.x, b.y, b.c, a);
                    p.Dot(b.x, b.y - 1, b.c, a * 0.5);
                }
                else
                {
                    uint[] r = b.ramp ?? ChunkRamp;
                    int n = r.Length;
                    double x = Round(b.x), y = Round(b.y);
                    int rr = (int)Math.Ceiling(b.s) + 1;
                    for (int py = -rr; py <= rr; py++)
                    for (int px = -rr; px <= rr; px++)
                    {
                        double u = (px + 0.5) * ca + (py + 0.5) * sa, v = -(px + 0.5) * sa + (py + 0.5) * ca;
                        double d = Math.Abs(u) / b.s + Math.Abs(v) / (b.s * 0.62);
                        if (d > 1.3) continue;
                        p.Dot(x + px, y + py, d > 1 ? r[0] : v < 0 ? (u < 0 ? r[n - 1] : r[n - 2]) : u < 0 ? r[n - 3] : r[Math.Max(1, n - 4)], a);
                    }
                }
            }
            p.Shift(0, 0);
        }
    }

    public sealed class Lights
    {
        sealed class Light
        {
            public double x, y, r, k, life, max;
            public uint color;
            public int bornX, bornY;
        }

        readonly List<Light> list = new List<Light>();
        readonly Emitter emitter;

        public Lights(Emitter emitter = null) => this.emitter = emitter;

        public void Add(double x, double y, double r, uint color, double k, double life)
        {
            var l = new Light { x = x, y = y, r = r, color = color, k = k, life = life, max = life };
            Sim.Born(emitter, out l.bornX, out l.bornY);
            list.Add(l);
        }

        public void Update(double dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if ((list[i].life -= dt) <= 0)
                    list.RemoveAt(i);
        }

        public void Draw(Painter p)
        {
            foreach (Light l in list)
            {
                Sim.Shift(p, emitter, l.bornX, l.bornY);
                double k = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.55 + 0.45 * k), l.color, l.k * k * k);
            }
            p.Shift(0, 0);
        }
    }
}
