using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public static class PickupsLib
    {
        public static void Glint(Painter p, double x, double y, double arm, uint c, double k = 0.4) => Glint(p, x, y, arm, c, k, Scale(c, 0.8));

        public static void Glint(Painter p, double x, double y, double arm, uint c, double k, uint tip)
        {
            double n = Round(arm), X = Math.Floor(x), Y = Math.Floor(y);
            if (n < 0) return;
            for (int d = 1; d <= n; d++)
            {
                uint cc = d == n && n > 1 ? tip : c;
                p.Px(X + d, Y, cc);
                p.Px(X - d, Y, cc);
                p.Px(X, Y + d, cc);
                p.Px(X, Y - d, cc);
            }
            p.Px(X, Y, n > 0 ? WHITE : c);
            p.Glow(X + 0.5, Y + 0.5, 3 + n * 2.5, c, k);
        }
    }
}
