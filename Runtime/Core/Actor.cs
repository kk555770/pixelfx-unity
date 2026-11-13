using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>
    /// A stock character to try effects on: status effects wrap its silhouette, hits land on it, auras sit behind it. Previews
    /// draw it; effects only read its mask through options["actor"]. A port of pixelfx's src/core/actor.js.
    /// In local space with its feet at (0, 0): X, Y is the top-left of the mask.
    /// </summary>
    public sealed class Actor
    {
        public static readonly string[] HERO =
        {
            "....kkkkk...",
            "..kkhhhhhk..",
            ".khhhhhhhhk.",
            ".khhhhhhhhhk",
            ".khhssssshhk",
            ".khsessesshk",
            ".khsessesshk",
            "..ksssssssk.",
            "...kkSSSkk..",
            "..kttttttk..",
            ".kttTttTttk.",
            ".ksttTTttsk.",
            ".kstttttttk.",
            "..kbbbbbbk..",
            "..kppkkppk..",
            "..kppk.kppk.",
            "..kppk.kppk.",
            "..koook.kook",
            "..kkkkk.kkkk",
        };

        public static readonly IReadOnlyDictionary<char, string> HERO_PAL = new Dictionary<char, string>
        {
            ['k'] = "#17121f", ['h'] = "#7a4128", ['s'] = "#f3c49d", ['S'] = "#c98b6c", ['e'] = "#17121f",
            ['t'] = "#5f6f94", ['T'] = "#46536f", ['b'] = "#8a5a32", ['p'] = "#38334a", ['o'] = "#6a4030",
            // the 2x character's extra detail
            ['H'] = "#a35d34", ['w'] = "#ffffff", ['m'] = "#a3564a", ['g'] = "#e8c060", ['L'] = "#7f90b6", ['O'] = "#8f5a3f", ['P'] = "#4b4560",
            // the scarf
            ['c'] = "#f53cd1", ['C'] = "#ff8fe6", ['D'] = "#a31f8a",
        };

        /// <summary>The dummy: a neutral shell that lets any effect's colors do the talking, near and far limbs, dark bands at the joints, a small mark.</summary>
        public static readonly IReadOnlyDictionary<char, string> DUMMY_PAL = new Dictionary<char, string>
        {
            ['k'] = "#0e0c14", ['A'] = "#e4e6ec", ['B'] = "#b6b9c3", ['C'] = "#868995", ['D'] = "#5c5e6a", ['E'] = "#6e707c", ['F'] = "#4b4d58", ['G'] = "#34353f", ['J'] = "#2a2b33", ['m'] = "#f53cd1",
        };

        /// <summary>The same character at twice the size (24 x 38), standing easy: the rest pose of the rig, baked.</summary>
        public static readonly string[] HERO2 =
        {
            "...........kkk..........",
            "..........kAABk.........",
            ".........kAAABJk........",
            ".........kABBJJk........",
            ".........kBBBJCDk.......",
            ".........kBCJDDDk.......",
            ".........kkJJJDk........",
            ".......kkkkABBJCkk......",
            "......kAABCJBCCCkDk.....",
            "......kABBCJCCCmDkDk....",
            "......kBBCDDJCDDDkDk....",
            "......kABCDJDDDDDkk.....",
            ".....kAABDkDDDDJkk......",
            ".....kABCDkDDJJGkk......",
            ".....kABCkJJJGGGkk......",
            "....kJJCDkFFFGGGk.......",
            "....kAJJDkJJJGGGk.......",
            "....kJCJJkABBJGGk.......",
            ".....kJCCkBBCDJJJk......",
            ".....kCCCJBBCDJJDk......",
            ".....kCCCDJCCDkJDDk.....",
            "......kCDDJBCDkDDDk.....",
            ".......kDJJJCDkDDDkk....",
            ".......kJBCJCDkDDDkk....",
            ".......kBDJBCDkCDDkk....",
            "........kJJBCDkCDDk.....",
            "........kBBBCDkCCDDk....",
            "........kBBBCDkCCDDk....",
            ".........kBBCDkJDDDk....",
            "........kBJJJJkCJJJk....",
            "........kBBCCDkCCDDk....",
            "........kBBCDDkCDDk.....",
            "........kBBCDkCCDDk.....",
            "........kBCCDkCCDDk.....",
            "........kBCDDkCDDDk.....",
            ".......kJBCJkkkDJkkkk...",
            ".......kAJJAAAAkCCCCCk..",
            "........kkkkkkkkkkkkk...",
        };

        static Mask MaskOf(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var data = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rows[y][x] != '.') data[y * w + x] = 1;
            return new Mask(w, h, data);
        }

        // the character's own colors, row by row (0 where empty), for effects that recolor it: frozen, poisoned, stone
        static uint[] PixelsOf(string[] rows, IReadOnlyDictionary<char, string> pal)
        {
            int w = rows[0].Length;
            var data = new uint[w * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < w; x++)
                    data[y * w + x] = rows[y][x] == '.' ? 0 : Hex(pal[rows[y][x]]);
            return data;
        }

        static readonly Mask Mask1 = MaskOf(HERO), Mask2 = MaskOf(HERO2);
        static readonly uint[] Pixels1 = PixelsOf(HERO, HERO_PAL), Pixels2 = PixelsOf(HERO2, DUMMY_PAL);

        public readonly Mask Mask;
        public readonly uint[] Pixels;
        public readonly int X, Y, W, H, K;
        /// <summary>Top of the head and the middle of the chest, local px: [x, y].</summary>
        public readonly int[] Head, Chest;
        /// <summary>The eyes, in mask cells (null at scale 1).</summary>
        public readonly int[][] Eyes;

        Actor(int k)
        {
            K = k;
            Mask = k == 2 ? Mask2 : Mask1;
            Pixels = k == 2 ? Pixels2 : Pixels1;
            W = Mask.W;
            H = Mask.H;
            X = -(int)Math.Floor(Mask.W / 2.0);
            Y = -Mask.H;
            Head = new[] { 0, Y };
            Chest = new[] { 0, k == 2 ? -26 : Y + 11 };
            Eyes = k == 2 ? new[] { new[] { 13, 3 }, new[] { 15, 3 } } : null;
        }

        /// <summary>The stock actor at scale k (1: 12 x 19 px, 2: 24 x 38 px), in local space with its feet at (0, 0).</summary>
        public static Actor Stock(int k = 1) => new Actor(k == 2 ? 2 : 1);

        /// <summary>Draw the stock actor with its feet at the painter's origin, at alpha a (dithered).</summary>
        public static void Draw(Painter p, Actor actor = null, double a = 1)
        {
            actor ??= Stock();
            if (a > 0) p.Sprite(actor.K == 2 ? HERO2 : HERO, actor.K == 2 ? DUMMY_PAL : HERO_PAL, actor.X, actor.Y, a: a);
        }
    }
}
