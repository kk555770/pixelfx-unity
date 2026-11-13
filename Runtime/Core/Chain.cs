using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixelfx
{
    /// <summary>
    /// Moves: several effects played as one. A cue says which effect, when, where and for how long; a projectile is just an effect
    /// whose x is a function of time. It's an ordinary effect from the outside: it plays, recolors and exports. A port of pixelfx's
    /// src/core/chain.js.
    /// </summary>
    public static class Chain
    {
        /// <summary>
        /// One effect in a move: Fx starts at T and is cut off at Until; X and Y place it (local space, as functions of the move's time);
        /// Flip mirrors it; Options are any of that effect's own options (its seed is the move's seed plus the cue's index).
        /// </summary>
        public sealed class Cue
        {
            public Module Fx;
            public double T;
            public double Until = double.PositiveInfinity;
            public Func<double, double> X = t => 0, Y = t => 0;
            public bool Flip;
            public Options Options;
        }

        sealed class Live
        {
            public Cue Cue;
            public Options Opts;
            public Fx Inst;
            public bool Over;
        }

        sealed class Move : Effect
        {
            readonly List<Live> list;
            double t;

            public Move(Cue[] cues, Options o)
            {
                double seed = o.Num("seed", 1);
                list = cues.Select((c, i) =>
                {
                    Options opts = c.Options?.Copy() ?? new Options();
                    opts["seed"] = seed + i;
                    return new Live { Cue = c, Opts = opts };
                }).ToList();
            }

            public override void Update(double dt)
            {
                t += dt;
                foreach (Live c in list)
                {
                    if (c.Over || t < c.Cue.T) continue;
                    if (c.Inst == null) c.Inst = new Fx(c.Cue.Fx, c.Opts);
                    else c.Inst.Update(dt);
                    if (t >= c.Cue.Until || (c.Inst.Done && !c.Cue.Fx.Loop)) c.Over = true;
                }
            }

            void Each(Painter p, Layer layer)
            {
                Painter.State s = p.Save();
                double tx = p.OriginX, ty = p.OriginY, sign = p.Mirror;
                foreach (Live c in list)
                {
                    if (c.Inst == null || c.Over) continue;
                    c.Inst.X = tx + sign * c.Cue.X(t);
                    c.Inst.Y = ty + c.Cue.Y(t);
                    c.Inst.Flip = sign < 0 != c.Cue.Flip;
                    c.Inst.Draw(p.Reset(p.Frame), layer);
                }
                // each one places the painter for itself: put it back for whoever draws next
                p.Restore(s);
            }

            public override void DrawBack(Painter p, double tt) => Each(p, Layer.Back);
            public override void Draw(Painter p, double tt) => Each(p, Layer.Front);
        }

        /// <summary>The create function of a move made of these cues.</summary>
        public static Func<Options, Emitter, Effect> Of(params Cue[] cues) => (o, e) => new Move(cues, o);

        /// <summary>Reuse an act's keys in a longer move: later by dt, moved along by dx, with anything else laid over each pose.</summary>
        public static IEnumerable<PoseKey> ShiftKeys(IEnumerable<PoseKey> keys, double dt, double dx = 0, Pose over = null) =>
            keys.Select(k => new PoseKey(k.T + dt, new Pose(k.Pose, over) { ["x"] = (k.Pose.N("x") ?? 0) + dx }, k.How));

        /// <summary>The same for hits.</summary>
        public static IEnumerable<HitCue> ShiftHits(IEnumerable<HitCue> hits, double dt) => hits.Select(h => h.Later(dt));

        /// <summary>The same for dust, moved along by dx.</summary>
        public static IEnumerable<DustCue> ShiftDust(IEnumerable<DustCue> dust, double dt, double dx = 0) => dust.Select(d => d.Later(dt, dx));
    }
}
