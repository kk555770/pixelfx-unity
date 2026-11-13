using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    // An act: an effect performed. The character winds up, lets go and recovers, a target takes the hit, and everything freezes
    // for a moment on contact. Effects describe it as data (Module.Act), so a game with its own character can read the same timing.
    // The freeze, the shake, the springy target and poses held on 12 fps steps are how Hollow Crown plays its skills
    // (https://github.com/rehan-remade/hollow-crown), MIT License, Copyright (c) 2026 rehan-remade. A port of pixelfx's src/core/act.js.
    public sealed partial class Act
    {
        static double Lerp(double a, double b, double u) => a + (b - a) * u;

        static bool IsUndefined(Pose p, string k) => p[k] == null;

        /// <summary>Between two poses: numbers move; names and flags stay as the earlier pose has them.</summary>
        public static Pose LerpPose(Pose a, Pose b, double u)
        {
            var o = new Pose(Rig.STAND, a);
            var B = new Pose(Rig.STAND, b);
            foreach (string k in B.Keys.ToArray())
            {
                double? v = B.N(k), ok = o.N(k);
                if (v is double vv && ok is double oo) o[k] = Lerp(oo, vv, u);
                else if (IsUndefined(o, k) && v is double v2) o[k] = v2;
            }
            // what the later pose doesn't mention eases back to its default
            foreach (string k in new[] { "hx", "hy", "lean", "crouch" })
                if (IsUndefined(B, k) && o.N(k) is double ov) o[k] = Lerp(ov, 0, u);
            if (IsUndefined(B, "wl") && o.N("wl") is double wl) o["wl"] = Lerp(wl, 1, u);
            return o;
        }

        static double EaseOf(string how, double u) => how switch
        {
            "out" => 1 - (1 - u) * (1 - u),
            "in" => u * u,
            "line" => u,
            _ => u * u * (3 - 2 * u),
        };

        /// <summary>
        /// The pose at time t on a track of keys: How says how the pose arrives: smoothly by default, "out" fast then settling, "in"
        /// slowly then fast, "snap" holding the last pose and jumping on the key. Limbs move in whole 12 fps steps counted from each key,
        /// so they read as held drawings; the feet's position over the floor (x, y) stays smooth.
        /// </summary>
        public static Pose PoseAt(PoseKey[] track, double t)
        {
            if (t <= track[0].T) return track[0].Pose;
            for (int i = 0; i < track.Length - 1; i++)
            {
                double t0 = track[i].T, t1 = track[i + 1].T;
                Pose a = track[i].Pose, b = track[i + 1].Pose;
                string how = track[i + 1].How;
                if (t >= t1) continue;
                if (how == "snap") return a;
                double stepped = t0 + Math.Floor((t - t0) * 12 + 1e-6) / 12;
                Pose o = LerpPose(a, b, EaseOf(how, (stepped - t0) / (t1 - t0)));
                double u = EaseOf(how, (t - t0) / (t1 - t0));
                o["x"] = Lerp(a.N("x") ?? 0, b.N("x") ?? 0, u);
                o["y"] = Lerp(a.N("y") ?? 0, b.N("y") ?? 0, u);
                return o;
            }
            return track[track.Length - 1].Pose;
        }

        static readonly string[] HeldKeys = { "fx", "fy", "bx", "by", "crouch", "lean", "x", "y" };

        /// <summary>Is the body between keys that hardly differ, or past the last?</summary>
        internal static bool IsHeld(PoseKey[] track, double t)
        {
            if (t >= track[track.Length - 1].T || t < track[0].T) return true;
            for (int i = 0; i < track.Length - 1; i++)
            {
                if (t >= track[i + 1].T) continue;
                Pose a = new Pose(Rig.STAND, track[i].Pose), b = new Pose(Rig.STAND, track[i + 1].Pose);
                return track[i + 1].T - track[i].T > 0.4 && HeldKeys.All(k => Math.Abs((a.N(k) ?? 0) - (b.N(k) ?? 0)) < 1.5);
            }
            return true;
        }

        /// <summary>The straw targets, by their floor x (66 when unset).</summary>
        internal double[] TargetXs => Targets ?? new double[0];

        /// <summary>When an act's poses begin and end, in effect time (a loop ends when it's told to).</summary>
        public static (double from, double to) Span(Module mod)
        {
            Act a = mod.Act;
            double D = mod.Duration;
            PoseKey[] k = a?.Poses;
            double first = k != null && k.Length > 0 ? k[0].T : 0, last = k != null && k.Length > 0 ? k[k.Length - 1].T : 0;
            return (Math.Min(0, first), mod.Loop ? double.PositiveInfinity : Math.Max(D, last));
        }

        /// <summary>
        /// An act as plain data, for a game with its own character: how long the wind-up and recovery are, when to freeze and shake,
        /// what a target should do, and the poses themselves. Times are in the effect's own seconds: start the effect at 0.
        /// </summary>
        public static Timing TimingOf(Module mod)
        {
            Act a = mod.Act;
            if (a == null) return null;
            var (from, to) = Span(mod);
            return new Timing
            {
                Windup = -from,
                Recovery = mod.Loop ? 0 : Math.Max(0, to - mod.Duration),
                OnBody = a.OnBody,
                Targets = a.TargetXs.ToArray(),
                Hits = (a.Hits ?? new HitCue[0]).Select(h => new Timing.Hit
                {
                    T = h.T,
                    Freeze = h.Stop ?? 0,
                    Shake = h.Shake ?? 0,
                    Amp = h.Amp ?? 0,
                    Push = !h.Target ? 0 : h.Push ?? 1.2,
                    Skin = h.Skin,
                    HasSkin = h.HasSkin,
                    At = h.At,
                }).ToArray(),
                Poses = (a.Poses ?? new PoseKey[0]).Select(k => new PoseKey(k.T, k.Pose, k.How ?? "smooth")).ToArray(),
            };
        }

        /// <summary>The box an act needs in local space, so nothing gets cut off, sampled over its poses.</summary>
        public static (int x0, int y0, int x1, int y1) Bounds(Module mod)
        {
            Act a = mod.Act;
            int x0 = -mod.OX, y0 = -mod.OY, x1 = mod.W - mod.OX, y1 = mod.H - mod.OY;
            void See(Pose p)
            {
                Rig.Pixels r = Rig.Posed(p);
                int X = RoundInt(p.N("x") ?? 0), Y = RoundInt(p.N("y") ?? 0);
                x0 = Math.Min(x0, r.X0 + X - 1);
                x1 = Math.Max(x1, r.X1 + X + 1);
                y0 = Math.Min(y0, r.Y0 + Y - 1);
                y1 = Math.Max(y1, r.Y1 + Y + 1);
            }
            PoseKey[] k = a?.Poses;
            if (k != null)
            {
                for (double t = k[0].T; t <= k[k.Length - 1].T; t += 1.0 / 12) See(PoseAt(k, t));
                foreach (PoseKey key in k) See(key.Pose);
            }
            else See(Rig.STAND);
            foreach (double tx in a?.TargetXs ?? new double[0])
            {
                x0 = (int)Math.Min(x0, tx - 30);
                x1 = (int)Math.Max(x1, tx + 36);
                y0 = Math.Min(y0, -56);
            }
            return (x0, y0, x1, y1);
        }

        /// <summary>An act as plain data (the JS actTiming).</summary>
        public sealed class Timing
        {
            public sealed class Hit
            {
                public double T, Freeze, Shake, Amp, Push;
                public string Skin;
                public bool HasSkin;
                public int? At;
            }

            public double Windup, Recovery;
            public bool OnBody;
            public double[] Targets;
            public Hit[] Hits;
            public PoseKey[] Poses;
        }
    }

    /// <summary>A straw target on a spring: it sways, flashes, takes a skin, shivers and sheds straw. X: the floor under it, local space.</summary>
    public sealed class Target
    {
        const int TW = 38, TH = 52;
        static readonly uint INK = Hex("#0b0a1a"), WOOD = Hex("#724c2e"), WOOD_L = Hex("#a0703a"), WOOD_D = Hex("#4e331f"), STRAW_C = Hex("#d2aa55"), STRAW_L = Hex("#f0d98a"),
            BURLAP_D = Hex("#86643a"), BURLAP = Hex("#b38b57"), BURLAP_L = Hex("#c9a26c"), RED = Hex("#9a2f3c"), RED_L = Hex("#c4434f"), WHITE_C = Hex("#ffffff");

        static readonly uint[] DUMMY = BuildDummy();

        static uint[] BuildDummy()
        {
            var D = new uint[TW * TH];
            void Px(int x, int y, uint c)
            {
                if (x >= 0 && y >= 0 && x < TW && y < TH) D[y * TW + x] = c;
            }
            void R(int x, int y, int w, int h, uint c)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Px(x + i, y + j, c);
            }
            // the post and the crossbar
            R(15, 38, 8, 14, INK); R(16, 34, 6, 18, WOOD); R(16, 34, 2, 18, WOOD_L); R(20, 34, 2, 18, WOOD_D);
            R(1, 18, 36, 1, INK); R(1, 23, 36, 1, INK); R(1, 19, 36, 4, WOOD); R(1, 19, 36, 1, WOOD_L); R(1, 22, 36, 1, WOOD_D);
            // straw bursting out of the ends of the arms
            foreach (var (bx, s) in new[] { (0, -1), (33, 1) })
            {
                R(bx, 17, 5, 8, STRAW_C);
                foreach (var (dx, dy, c) in new[] { (2, 16, STRAW_C), (3, 15, STRAW_L), (1, 17, STRAW_L), (2, 25, STRAW_C), (1, 26, STRAW_L), (3, 25, BURLAP_D), (2, 19, STRAW_L), (3, 21, STRAW_L), (1, 22, BURLAP_D), (2, 23, STRAW_L) })
                    Px(bx + dx, dy, c);
                Px(s < 0 ? bx - 1 : bx + 5, 19, STRAW_C);
                Px(s < 0 ? bx - 1 : bx + 5, 22, STRAW_L);
            }
            void Sack(int x, int y, int w, int h)
            {
                R(x + 1, y, w - 2, 1, INK); R(x + 1, y + h - 1, w - 2, 1, INK);
                R(x, y + 1, 1, h - 2, INK); R(x + w - 1, y + 1, 1, h - 2, INK);
                R(x + 1, y + 1, w - 2, h - 2, BURLAP);
                R(x + w - 4, y + 1, 3, h - 2, BURLAP_D);
                R(x + 1, y + h - 3, w - 2, 2, BURLAP_D);
                R(x + 1, y + 1, 1, h - 4, BURLAP_L); R(x + 1, y + 1, w - 6, 1, BURLAP_L);
                // the weave
                for (int j = y + 2; j < y + h - 2; j += 3)
                    for (int i = x + 3 + (j % 2); i < x + w - 4; i += 4)
                        Px(i, j, BURLAP_D);
            }
            Sack(9, 17, 20, 24);
            // the rings on its chest
            for (int y = 19; y < 36; y++)
                for (int x = 11; x < 27; x++)
                {
                    double d = Hypot(x - 18.5, y - 27);
                    if ((d > 4 && d < 5.4) || d < 1.9) Px(x, y, x + y < 45 ? RED_L : RED);
                }
            // the rope belt, and straw coming out under it
            for (int x = 10; x < 28; x++)
            {
                Px(x, 36, (x & 1) != 0 ? WOOD_L : STRAW_C);
                Px(x, 37, (x & 1) != 0 ? WOOD_D : WOOD);
            }
            foreach (var (x, y, c) in new[] { (11, 41, STRAW_C), (12, 42, STRAW_L), (14, 41, STRAW_L), (15, 42, STRAW_C), (23, 41, STRAW_C), (24, 42, STRAW_L), (26, 41, STRAW_C), (13, 43, STRAW_C), (25, 43, STRAW_C) })
                Px(x, y, c);
            Sack(10, 1, 18, 16);
            // X for eyes and a stitched mouth
            foreach (int ex in new[] { 14, 23 })
                for (int d = -2; d <= 2; d++)
                {
                    Px(ex + d, 7 + d, INK);
                    Px(ex - d, 7 + d, INK);
                }
            for (int x = 14; x < 24; x++) Px(x, 12, (x & 1) != 0 ? INK : BURLAP_D);
            foreach (int x in new[] { 15, 18, 21 })
            {
                Px(x, 11, INK);
                Px(x, 13, INK);
            }
            // the rope at its neck, and straw for hair
            for (int x = 12; x < 26; x++)
            {
                Px(x, 16, (x & 1) != 0 ? WOOD_L : STRAW_C);
                Px(x, 17, (x & 1) != 0 ? WOOD : WOOD_D);
            }
            foreach (var (x, y, c) in new[] { (13, 0, STRAW_C), (15, 0, STRAW_L), (18, 0, STRAW_C), (21, 0, STRAW_L), (24, 0, STRAW_C), (9, 4, STRAW_C), (28, 6, STRAW_C), (8, 7, STRAW_L), (29, 3, STRAW_L) })
                Px(x, y, c);
            return D;
        }

        /// <summary>What a hit can leave on the target: a color and how much of it.</summary>
        public static readonly IReadOnlyDictionary<string, (uint c, double k)> SKINS = new Dictionary<string, (uint, double)>
        {
            ["flash"] = (WHITE_C, 1), ["char"] = (Hex("#1a0c0e"), 0.72), ["ice"] = (Hex("#a8e8ff"), 0.72), ["shadow"] = (Hex("#3a1a70"), 0.5),
            ["holy"] = (Hex("#fff4c0"), 0.5), ["red"] = (Hex("#ff2040"), 0.45), ["shock"] = (Hex("#8fd6ff"), 0.6), ["poison"] = (Hex("#58c43a"), 0.5),
        };

        static readonly uint[] STRAW = { STRAW_L, STRAW_C, STRAW_C, BURLAP_D, BURLAP_D };

        sealed class Straw
        {
            public double x, y, vx, vy, age, life;
            public bool big;
        }

        public readonly double X;
        public double Theta, Omega, FlashT, Shiver, Clock;
        public string Skin;
        readonly Rng R;
        List<Straw> straw = new List<Straw>();

        public Target(double x, double seed = 5)
        {
            X = x;
            R = new Rng(seed);
        }

        public void Hit(HitCue h)
        {
            double push = h.Push ?? 1.2, count = h.Straw ?? 8, flash = h.Flash ?? 0.05, shiver = h.Shiver ?? 0, y = h.Y ?? -26;
            Omega += push;
            FlashT = Math.Max(FlashT, flash);
            if (h.HasSkin) Skin = h.Skin;
            if (shiver != 0 && !double.IsNaN(shiver)) Shiver = shiver;
            for (int k = 0; k < count; k++)
            {
                bool back = R.Next() < 0.3;
                var s = new Straw();
                s.x = X + R.Range(-3, 3);
                s.y = y + R.Range(-3, 3);
                s.vx = back ? R.Range(-60, -5) : R.Range(10, 130);
                s.vy = R.Range(-120, -5);
                s.life = R.Range(1.2, 2.2);
                s.big = R.Next() < 0.5;
                straw.Add(s);
            }
        }

        public void Step(double dt)
        {
            Clock += dt;
            Omega += (-60 * Theta - 3.4 * Omega) * dt;
            Theta = Clamp(Theta + Omega * dt, -0.4, 0.42);
            if (FlashT > 0) FlashT -= dt;
            if (Shiver > 0) Shiver -= dt;
            foreach (Straw s in straw)
            {
                s.age += dt;
                s.vy += 210 * dt;
                s.x += s.vx * dt;
                s.y += s.vy * dt;
                if (s.y >= 0)
                {
                    s.y = 0;
                    s.vx *= 0.2;
                    s.vy = 0;
                }
            }
            straw = straw.Where(s => s.age < s.life).ToList();
        }

        static void Set(uint[] C, int W, int H, double px, double py, uint c)
        {
            // a JS typed array ignores writes at fractional or outside indices
            if (px >= 0 && py >= 0 && px < W && py < H && px == Math.Floor(px) && py == Math.Floor(py)) C[(int)py * W + (int)px] = c;
        }

        /// <summary>Paint it into a color buffer W x H, the floor under it at (X, Y) in that buffer, mirrored if flip.</summary>
        public void Draw(uint[] C, int W, int H, double X, double Y, bool flip)
        {
            double tan = Px.Tan(Theta);
            int sv = Shiver > 0 ? ((ToInt32(Math.Floor(Clock * 30)) & 1) != 0 ? 1 : -1) : 0;
            string name = FlashT > 0 ? "flash" : Skin;
            bool skinned = name != null && SKINS.ContainsKey(name);
            var sk = skinned ? SKINS[name] : default;
            double bas = X + (flip ? -this.X - 1 : this.X);
            for (int r = 0; r < TH; r++)
            {
                double o = Round((TH - 1 - r) * tan * 0.8) + sv;
                for (int x = 0; x < TW; x++)
                {
                    uint c = DUMMY[r * TW + x];
                    if (c == 0) continue;
                    double px = bas + (flip ? -(x - 19 + o) - 1 : x - 19 + o), py = Y - TH + r;
                    Set(C, W, H, px, py, skinned ? Mix(c, sk.c, sk.k) : c);
                }
            }
        }

        public void DrawStraw(uint[] C, int W, int H, double X, double Y, bool flip)
        {
            foreach (Straw s in straw)
            {
                uint c = STRAW[(int)Math.Min(4, Math.Floor((s.age / s.life) * 5))];
                double lx = Round(s.x), ly = Round(s.y) - 1;
                for (int i = 0; i < (s.big ? 2 : 1); i++)
                    Set(C, W, H, X + (flip ? -(lx + i) - 1 : lx + i), Y + ly, c);
            }
        }
    }

    /// <summary>
    /// An effect performed (the JS createAct): the effect, the character and the target together. T runs from the first pose (before
    /// the effect's own t = 0) to the last. Update() holds everything still during a freeze; Draw() paints the effect, the character and
    /// the target. Options x, y place the feet. Without an act, it just plays.
    /// </summary>
    public sealed class Performance
    {
        const double GHOST_LIFE = 0.16;
        static readonly uint[] DUST = Ramp("#efe7dc", "#c9bfb8", "#8f8693", "#5a5468");
        static readonly byte[] REST_MASK = new byte[24 * 38];
        static readonly uint[] REST_PX = new uint[24 * 38];
        // every cell of the standing sprite, mapped to the nearest cell that's on the body
        static readonly short[] NEAREST = new short[24 * 38];

        static Performance()
        {
            for (int r = 0; r < 38; r++)
                for (int c = 0; c < 24; c++)
                {
                    char ch = Actor.HERO2[r][c];
                    if (ch == '.') continue;
                    REST_MASK[r * 24 + c] = 1;
                    REST_PX[r * 24 + c] = Hex(Actor.DUMMY_PAL[ch]);
                }
            for (int r = 0; r < 38; r++)
                for (int c = 0; c < 24; c++)
                {
                    int best = -1;
                    double bd = 1e9;
                    for (int y = Math.Max(0, r - 7); y < Math.Min(38, r + 8); y++)
                        for (int x = Math.Max(0, c - 7); x < Math.Min(24, c + 8); x++)
                        {
                            int d = (x - c) * (x - c) + (y - r) * (y - r);
                            if (REST_MASK[y * 24 + x] != 0 && d < bd)
                            {
                                bd = d;
                                best = y * 24 + x;
                            }
                        }
                    NEAREST[r * 24 + c] = (short)best;
                }
        }

        static double Lum(uint c) => (0.3 * (c & 255) + 0.59 * ((c >> 8) & 255) + 0.11 * ((c >> 16) & 255)) / 255;

        // an effect's color over the standing body, moved onto a pixel of the pose: its color, at this pixel's own light and shade
        static uint Carry(uint v, uint rest, uint own)
        {
            double lv = Lum(v);
            if (lv > 0.9 || v >> 24 < 255) return v;
            double k = Math.Max(0.45, Math.Min(1.5, Lum(own) / Math.Max(0.08, Lum(rest))));
            uint Ch(int n) => (uint)Math.Min(255, Round(((v >> n) & 255) * k));
            return 0xff000000u | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
        }

        sealed class Speck
        {
            public double x, y, vx, vy, age, life;
            public bool big;
            public uint c;
        }

        sealed class Ghost
        {
            public Pose q;
            public double age;
        }

        public readonly Fx Fx;
        public readonly Module Module;
        public readonly Act Act;
        public readonly Target[] Targets;
        /// <summary>Called on every hit as it lands.</summary>
        public event Action<HitCue> OnHit;

        readonly double D, X, Y;
        readonly bool flip, loop;
        readonly PoseKey[] track;
        readonly double spanTo;
        readonly HitCue[] hits;
        readonly DustCue[] dust;
        readonly Rng R;
        readonly List<Speck> specks = new List<Speck>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly uint gc;
        double t, stop, shakeT, shake0, amp, gAcc;
        bool stopped;
        int nh, nd, loopN;
        Pose last;
        Frame scratchA, scratchB;

        public Performance(Module mod, Options o = null)
        {
            o ??= new Options();
            Module = mod;
            Act = mod.Act ?? new Act();
            D = mod.Duration;
            loop = mod.Loop;
            flip = o.Bool("flip");
            double seed = o.Has("seed") ? o.Num("seed") : mod.Defaults != null && mod.Defaults.Has("seed") ? mod.Defaults.Num("seed") : 1;
            Fx = new Fx(mod, o);
            track = Act.Poses ?? new[] { new PoseKey(0, new Pose(Rig.STAND)) };
            var span = Act.Span(mod);
            t = span.from;
            spanTo = span.to;
            X = o.Num("x", 0);
            Y = o.Num("y", 0);
            Targets = Act.TargetXs.Select((x, i) => new Target(x, seed + 4 + i)).ToArray();
            hits = (Act.Hits ?? new HitCue[0]).OrderBy(h => h.T).ToArray();
            dust = (Act.Dust ?? new DustCue[0]).ToArray();
            R = new Rng(seed + 9);
            // after-images take the effect's own color unless the act names one
            object ramp = o["palette"] ?? mod.Defaults?["palette"];
            string[] list = ramp is uint[] u ? Array.ConvertAll(u, ToHex) : ramp as string[];
            gc = Hex(Act.Ghost ?? (list != null && list.Length > 0 ? list[(int)Math.Floor(list.Length * 0.7)] : null) ?? "#8fb4ff");
            PoseNow();
        }

        /// <summary>The act's clock: negative during the wind-up, before the effect's own t = 0.</summary>
        public double T => t;

        /// <summary>How far the whole picture is thrown right now, in px.</summary>
        public (int x, int y) Shake
        {
            get
            {
                if (shakeT <= 0) return (0, 0);
                double k = amp * (shakeT / shake0), f = Math.Floor(shakeT * 60);
                return (RoundInt(Px.Sin(f * 2.4) * k), RoundInt(Px.Cos(f * 3.7) * k * 0.7));
            }
        }

        public bool Frozen => stop > 0;

        /// <summary>The pose he's in right now.</summary>
        public Pose Pose => last;

        Pose PoseNow()
        {
            double tt = t;
            if (loop && t >= D) tt = t % D;
            Pose q = Act.PoseAt(track, tt);
            // nothing is asked of him: he breathes
            if (!loop && IsHeldAt(tt))
            {
                int up = ToInt32(Math.Floor(tt * 3)) & 1;
                q = new Pose(Rig.STAND, q)
                {
                    ["crouch"] = (q.N("crouch") ?? 0) + up,
                    ["fy"] = (q.N("fy") ?? Rig.STAND.N("fy").Value) + up,
                    ["by"] = (q.N("by") ?? Rig.STAND.N("by").Value) + up,
                };
            }
            last = q;
            return q;
        }

        bool IsHeldAt(double tt) => Act.IsHeld(track, tt);

        public void Update(double dt)
        {
            if (shakeT > 0) shakeT -= dt;
            if (stop > 0)
            {
                stop -= dt;
                return;
            }
            double t0 = t;
            t += dt;
            if (t >= 0) Fx.Update(t0 < 0 ? t : dt);
            if (loop && t >= D)
            {
                int n = (int)Math.Floor(t / D);
                if (n != loopN)
                {
                    loopN = n;
                    nh = 0;
                    nd = 0;
                }
            }
            double tt = loop && t >= D ? t % D : t;
            for (; nh < hits.Length && hits[nh].T <= tt; nh++)
            {
                HitCue h = hits[nh];
                if (h.Stop is double s && s != 0 && !double.IsNaN(s)) stop = Math.Max(stop, s);
                if (h.Shake is double sh && sh != 0 && !double.IsNaN(sh))
                {
                    shakeT = shake0 = sh;
                    amp = h.Amp ?? 2;
                }
                if (h.Target)
                    for (int i = 0; i < Targets.Length; i++)
                        if (h.At == null || h.At == i)
                            Targets[i].Hit(h);
                OnHit?.Invoke(h);
            }
            for (; nd < dust.Length && dust[nd].T <= tt; nd++)
            {
                DustCue d = dust[nd];
                for (int i = 0; i < d.N; i++)
                {
                    double dir = d.Dir ?? (R.Next() < 0.5 ? -1 : 1);
                    var k = new Speck();
                    k.x = d.X + R.Range(-3, 3);
                    k.y = -1 - R.Range(0, 2);
                    k.vx = dir * R.Range(22, 90);
                    k.vy = R.Range(-34, -6);
                    k.life = R.Range(0.16, 0.4);
                    k.big = R.Next() < 0.45;
                    k.c = string.IsNullOrEmpty(d.C) ? 0 : Hex(d.C);
                    specks.Add(k);
                }
            }
            foreach (Speck k in specks)
            {
                k.age += dt;
                k.vx *= 1 - 5 * dt;
                k.vy += 110 * dt;
                k.x += k.vx * dt;
                k.y = Math.Min(-1, k.y + k.vy * dt);
            }
            for (int i = specks.Count - 1; i >= 0; i--)
                if (specks[i].age >= specks[i].life)
                    specks.RemoveAt(i);
            Pose q = PoseNow();
            foreach (Target g in Targets) g.Step(dt);
            // after-images while a key asks for them
            gAcc += dt;
            if (q.Truthy("ghost") && gAcc >= 1.0 / 30)
            {
                gAcc = 0;
                ghosts.Add(new Ghost { q = q });
                if (ghosts.Count > 7) ghosts.RemoveAt(0);
            }
            foreach (Ghost g in ghosts) g.age += dt;
            while (ghosts.Count > 0 && ghosts[0].age > GHOST_LIFE) ghosts.RemoveAt(0);
        }

        static void Put(uint[] C, int W, int H, int px, int py, uint c)
        {
            if (px >= 0 && py >= 0 && px < W && py < H) C[py * W + px] = c;
        }

        /// <summary>Paint it into frame. body: false leaves the character out, target: false the target.</summary>
        public void Draw(Frame frame, bool body = true, bool target = true)
        {
            int W = frame.W, H = frame.H;
            Pose q = last ?? PoseNow();
            bool active = t >= 0;
            Target[] tgt = target ? Targets : new Target[0];
            int FX = (int)X - frame.Ox, FY = (int)Y - frame.Oy;
            void DrawTargets()
            {
                foreach (Target g in tgt) g.Draw(frame.Color, W, H, FX, FY, flip);
            }
            void DrawStraw()
            {
                foreach (Target g in tgt) g.DrawStraw(frame.Color, W, H, FX, FY, flip);
            }
            void DrawDust()
            {
                foreach (Speck k in specks)
                {
                    uint c = k.c != 0 ? k.c : DUST[(int)Math.Min(3, Math.Floor((k.age / k.life) * 4))];
                    int lx = RoundInt(k.x), py = FY + RoundInt(k.y);
                    for (int i = 0; i < (k.big ? 2 : 1); i++)
                        Put(frame.Color, W, H, flip ? FX - lx - i - 1 : FX + lx + i, py, c);
                }
            }
            void FxDraw(Frame f, Layer layer) => Fx.Draw(Painter.For(f), layer);
            if (!body)
            {
                if (active) FxDraw(frame, Layer.Back);
                DrawTargets();
                if (active) FxDraw(frame, Layer.Front);
                DrawStraw();
                return;
            }
            int rx = RoundInt(q.N("x") ?? 0), ry = RoundInt(q.N("y") ?? 0), BX = FX + (flip ? -rx : rx), BY = FY + ry;
            Rig.Pixels r = Rig.Posed(q);
            void Paint(int i, uint color)
            {
                int px = flip ? BX - r.Xs[i] - 1 : BX + r.Xs[i], py = BY + r.Ys[i];
                if (px < 0 || py < 0 || px >= W || py >= H) return;
                frame.Color[py * W + px] = color;
                // he stays crisp in front of his own after-images
                if (ghosts.Count > 0) frame.Light[py * W + px] = 0;
            }
            void DrawGhosts()
            {
                foreach (Ghost g in ghosts)
                {
                    // after-images are light, not paint: they brighten what's behind them and leave nothing muddy
                    Rig.Pixels gr = Rig.Posed(g.q);
                    int gx = FX + (flip ? -RoundInt(g.q.N("x") ?? 0) : RoundInt(g.q.N("x") ?? 0)), gy = FY + RoundInt(g.q.N("y") ?? 0);
                    double k = 0.5 * (1 - g.age / GHOST_LIFE);
                    double r0 = (gc & 255) * k, g0 = ((gc >> 8) & 255) * k, b0 = ((gc >> 16) & 255) * k;
                    for (int i = 0; i < gr.N; i++)
                    {
                        int px = flip ? gx - gr.Xs[i] - 1 : gx + gr.Xs[i], py = gy + gr.Ys[i];
                        if (px < 0 || py < 0 || px >= W || py >= H) continue;
                        int j = py * W + px;
                        uint d = frame.Light[j];
                        frame.Light[j] = 0xff000000u | ((uint)ToInt32(Math.Min(255, ((d >> 16) & 255) + b0)) << 16) | ((uint)ToInt32(Math.Min(255, ((d >> 8) & 255) + g0)) << 8) | (uint)ToInt32(Math.Min(255, (d & 255) + r0));
                    }
                }
            }
            void Shadow()
            {
                int sy = FY, cx = FX + (flip ? -rx : rx);
                int hw = 10 + (ToInt32(Math.Abs((q.N("ffx") ?? double.NaN) - (q.N("fbx") ?? double.NaN))) >> 1) - Math.Min(8, Math.Abs(ry) >> 1);
                if (sy < 0 || sy >= H) return;
                for (int dy = 0; dy < 2; dy++)
                    for (int x = -hw + dy * 2; x <= hw - dy * 2; x++)
                    {
                        int px = cx + x, i = (sy + dy) * W + px;
                        if (px < 0 || px >= W || sy + dy >= H) continue;
                        frame.Color[i] = (frame.Color[i] & 0xffffff) | (Math.Max(frame.Color[i] >> 24, 110u) << 24);
                    }
            }
            if (!Act.OnBody)
            {
                // the effect doesn't care about the body: back, body, front
                if (active) FxDraw(frame, Layer.Back);
                DrawTargets();
                Shadow();
                DrawGhosts();
                for (int i = 0; i < r.N; i++) Paint(i, r.Color[i]);
                if (active) FxDraw(frame, Layer.Front);
                DrawStraw();
                DrawDust();
                return;
            }
            // the effect paints the standing body: draw it there, then carry each body pixel to where the pose put it
            if (scratchA == null || scratchA.W != frame.W || scratchA.H != frame.H)
            {
                scratchA = new Frame(frame.W, frame.H, frame.Ox, frame.Oy);
                scratchB = new Frame(frame.W, frame.H, frame.Ox, frame.Oy);
            }
            Frame A = scratchA, B = scratchB;
            A.Clear();
            B.Clear();
            if (active) FxDraw(A, Layer.Back);
            Array.Copy(A.Color, B.Color, A.Color.Length);
            Array.Copy(A.Light, B.Light, A.Light.Length);
            double tt = loop ? (t < 0 ? 0 : t % D) : Clamp(t, 0, D), alpha = active ? Module.ActorAlphaAt(tt, Fx.Options) : 1;
            Actor.Draw(Painter.For(B).Place(FX, FY, flip), Fx.Options.Actor ?? Actor.Stock(2), alpha);
            if (active) FxDraw(B, Layer.Front);
            double follow = Act.Follow, lean = (q.N("lean") ?? 0) * follow, cr = (q.N("crouch") ?? 0) * follow;
            // what's up on the body goes with it; what's on the floor stays on the floor
            (int, int) ShiftOf(int ly)
            {
                double k = Clamp((-11 - ly) / 7.0, 0, 1), f = Clamp((-1 - ly) / 6.0, 0, 1);
                return (RoundInt(lean * k + rx * f), RoundInt(cr * k + ry * f));
            }
            bool Rest(int sx, int sy)
            {
                int c = (flip ? FX - sx - 1 : sx - FX) + 12, r0 = sy - FY + 38;
                return c >= 0 && r0 >= 0 && c < 24 && r0 < 38 && REST_MASK[r0 * 24 + c] == 1;
            }
            uint[] C = frame.Color, Ls = frame.Light;
            void Copy(bool light, int sx, int sy, uint s)
            {
                var (dx, dy) = ShiftOf(sy - FY);
                int px = sx + (flip ? -dx : dx), py = sy + dy;
                if (px < 0 || py < 0 || px >= W || py >= H) return;
                int i = py * W + px;
                if (light)
                {
                    uint d = Ls[i];
                    Ls[i] = 0xff000000u | (Math.Min(255u, ((d >> 16) & 255) + ((s >> 16) & 255)) << 16) | (Math.Min(255u, ((d >> 8) & 255) + ((s >> 8) & 255)) << 8) | Math.Min(255u, (d & 255) + (s & 255));
                }
                else if (s >> 24 == 255 || C[i] == 0) C[i] = s;
                else
                {
                    double k = (s >> 24) / 255.0;
                    uint d = C[i];
                    uint Ch(int n) => (uint)ToInt32(Round(((s >> n) & 255) * k + ((d >> n) & 255) * (1 - k)));
                    C[i] = (Math.Max(s >> 24, d >> 24) << 24) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
                }
            }
            DrawTargets();
            Shadow();
            for (int sy = 0; sy < H; sy++)
                for (int sx = 0; sx < W; sx++)
                {
                    uint c = A.Color[sy * W + sx];
                    if (c != 0) Copy(false, sx, sy, c);
                }
            DrawGhosts();
            int RestIndex(int s)
            {
                int c = s % 24, r0 = s / 24, sx = flip ? FX - (c - 12) - 1 : FX + c - 12, sy = FY + r0 - 38;
                return sx >= 0 && sy >= 0 && sx < W && sy < H ? sy * W + sx : -1;
            }
            for (int i = 0; i < r.N; i++)
            {
                int s = r.Src[i] >= 0 ? NEAREST[r.Src[i]] : -1;
                if (s < 0)
                {
                    if (alpha > 0.5) Paint(i, r.Color[i]);
                    continue;
                }
                int j = RestIndex(s);
                uint v = j >= 0 ? B.Color[j] : REST_PX[s];
                if (v == 0) continue;
                // the effect left this bit of body alone, or changed it: carry the change, on top of this pixel's own shading
                Paint(i, v == REST_PX[s] ? r.Color[i] : Carry(v, REST_PX[s], r.Color[i]));
            }
            for (int sy = 0; sy < H; sy++)
                for (int sx = 0; sx < W; sx++)
                {
                    int j = sy * W + sx;
                    if (B.Color[j] != 0 && B.Color[j] != A.Color[j] && !Rest(sx, sy)) Copy(false, sx, sy, B.Color[j]);
                    if ((B.Light[j] & 0xffffff) != 0) Copy(true, sx, sy, B.Light[j]);
                }
            DrawStraw();
            DrawDust();
        }

        /// <summary>Let a loop end: it fades out and Done goes true.</summary>
        public void Stop()
        {
            Fx.Stop();
            stopped = true;
        }

        /// <summary>One-shots end when the last pose is over.</summary>
        public bool Done => stopped ? Fx.Done : t >= spanTo && stop <= 0;
    }
}
