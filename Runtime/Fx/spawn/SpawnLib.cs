// Shared by the spawn and death effects: the actor's own body as a list of pixels to break up, burn, freeze or
// copy, and the cells just outside it for outlines. A port of pixelfx's src/fx/spawn/_lib.js.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class SpawnLib
    {
        /// <summary>One pixel of the body, local space; key and gone are the effects' own bookkeeping.</summary>
        internal sealed class Cell
        {
            public int x, y, mx, my;
            public uint c;
            public bool edge;
            public double key;
            public bool gone;
        }

        /// <summary>A cell just outside the body.</summary>
        internal sealed class RimCell
        {
            public int x, y, mx, my;
        }

        internal sealed class Body
        {
            public readonly Cell[] list;
            public readonly int w, h, x, y;
            readonly Mask m;
            readonly Cell[] cells;

            internal Body(Cell[] list, Cell[] cells, Mask m, int x, int y)
            {
                this.list = list;
                this.cells = cells;
                this.m = m;
                w = m.W;
                h = m.H;
                this.x = x;
                this.y = y;
            }

            public bool On(int mx, int my) => On(m, mx, my);
            public Cell Cell(int mx, int my) => On(mx, my) ? cells[my * m.W + mx] : null;

            internal static bool On(Mask m, int mx, int my) => mx >= 0 && my >= 0 && mx < m.W && my < m.H && m.Data[my * m.W + mx] != 0;
        }

        /// <summary>
        /// The actor's body, local space with the feet at (0, 0). c is the character's own color (actor.Pixels); an actor
        /// without pixels is shaded from the ramp C (lit at the top, darker toward the feet). edge marks pixels on the rim.
        /// </summary>
        public static Body BodyOf(Actor actor, uint[] C)
        {
            Mask m = actor.Mask;
            uint[] px = actor.Pixels;
            var list = new List<Cell>();
            var cells = new Cell[m.W * m.H];
            for (int my = 0; my < m.H; my++)
                for (int mx = 0; mx < m.W; mx++)
                {
                    if (!Body.On(m, mx, my)) continue;
                    uint own = px != null && my * m.W + mx < px.Length ? px[my * m.W + mx] : 0;
                    uint c = own != 0 ? own : C[(int)Math.Max(1, Math.Min(C.Length - 2, Round((C.Length - 2) * (0.85 - ((double)my / m.H) * 0.5))))];
                    var b = new Cell
                    {
                        x = actor.X + mx, y = actor.Y + my, mx = mx, my = my, c = c,
                        edge = !Body.On(m, mx - 1, my) || !Body.On(m, mx + 1, my) || !Body.On(m, mx, my - 1) || !Body.On(m, mx, my + 1),
                    };
                    list.Add(b);
                    cells[my * m.W + mx] = b;
                }
            return new Body(list.ToArray(), cells, m, actor.X, actor.Y);
        }

        /// <summary>Cells just outside the body (4-neighbours of its rim), local space.</summary>
        public static RimCell[] RimOf(Body body)
        {
            var o = new List<RimCell>();
            for (int my = -1; my <= body.h; my++)
                for (int mx = -1; mx <= body.w; mx++)
                    if (!body.On(mx, my) && (body.On(mx - 1, my) || body.On(mx + 1, my) || body.On(mx, my - 1) || body.On(mx, my + 1)))
                        o.Add(new RimCell { x = body.x + mx, y = body.y + my, mx = mx, my = my });
            return o.ToArray();
        }
    }
}
