using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>Preview backdrops: what the tools and the gallery draw behind an effect. Sprite sheets stay transparent. A port of pixelfx's src/core/scene.js.</summary>
    public static class Scene
    {
        static readonly uint SKY = Hex("#15131d"), SKY_TOP = Hex("#0d0b14"), SKY_LOW = Hex("#221d33");
        static readonly uint FLOOR_EDGE = Hex("#4a4263"), FLOOR = Hex("#2b2639"), FLOOR_DARK = Hex("#211d2d"), MORTAR = Hex("#1a1724"), STAR = Hex("#cfc8ff");
        static readonly int[] BAYER = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public static readonly string[] BACKDROPS = { "dark", "ground", "sky", "light", "black" };

        /// <summary>
        /// A backdrop of w x h colors. kind: "dark" | "ground" (a stone floor from groundY down) | "sky" (night gradient with stars)
        /// | "light" | "black". groundY defaults to h.
        /// </summary>
        public static uint[] Backdrop(string kind, int w, int h, int? groundY = null)
        {
            int gY = groundY ?? h;
            var o = new uint[w * h];
            if (kind == "light" || kind == "black")
            {
                uint fill = Hex(kind == "light" ? "#ebe8f0" : "#000000");
                for (int i = 0; i < o.Length; i++) o[i] = fill;
                return o;
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    uint c = SKY;
                    if (kind == "sky")
                    {
                        double t = (double)y / System.Math.Max(1, h - 1);
                        int step = ToInt32(t * 5 + BAYER[((y & 3) << 2) | (x & 3)] / 16.0);
                        c = Mix(SKY_TOP, SKY_LOW, System.Math.Min(1, step / 4.0));
                        double r = Rand(x, y, 77);
                        if (r > 0.994) c = Mix(c, STAR, 0.35 + (r - 0.994) * 90);
                    }
                    if (kind == "ground" && y >= gY)
                    {
                        int gy = y - gY;
                        if (gy == 0) c = FLOOR_EDGE;
                        else
                        {
                            int row = (gy - 1) / 5, off = row % 2 != 0 ? 6 : 0;
                            bool mortar = (gy - 1) % 5 == 4 || (x + off) % 12 == 0;
                            c = mortar ? MORTAR : gy < 3 ? FLOOR : FLOOR_DARK;
                        }
                    }
                    o[y * w + x] = c;
                }
            return o;
        }
    }
}
