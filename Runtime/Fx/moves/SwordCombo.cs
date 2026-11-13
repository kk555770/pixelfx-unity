using System.Linq;

namespace Pixelfx
{
    public static class SwordCombo
    {
        const double A = 0, B = 0.5, C = 1.04, STEP = 10, FINISH = C + 0.335;

        static PoseKey[] Until(PoseKey[] keys, double t) => keys.Where(k => k.T <= t).ToArray();

        static readonly Pose STAND_ = new Pose(Poses.READY) { ["weapon"] = "sword", ["fx"] = 11, ["fy"] = -15, ["sa"] = 0.38 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 118 },
            Poses = Chain.ShiftKeys(Until(SwordSlash.Act.Poses, 0.3), A)
                .Concat(Chain.ShiftKeys(Until(DoubleSlash.Act.Poses, 0.27).Where(k => k.T >= -0.14), B, STEP))
                .Concat(Chain.ShiftKeys(HeavyCleave.Act.Poses.Where(k => k.T >= -0.3), C, STEP * 2, new Pose { ["weapon"] = "sword" }))
                .Concat(new[] { new PoseKey(FINISH + 1.6, new Pose(STAND_) { ["x"] = STEP * 2 - 4 }) })
                .ToArray(),
            Hits = Chain.ShiftHits(SwordSlash.Act.Hits, A).Concat(Chain.ShiftHits(DoubleSlash.Act.Hits, B)).Concat(Chain.ShiftHits(HeavyCleave.Act.Hits, C)).ToArray(),
            Dust = Chain.ShiftDust(SwordSlash.Act.Dust, A).Concat(Chain.ShiftDust(HeavyCleave.Act.Dust, C, STEP * 2)).ToArray(),
        };

        public static readonly Module Module = new Module("sword-combo", "pixelfx/fx/moves/sword-combo", 264, 150, 72, 130, FINISH + 1.6, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = SwordSlash.Module, T = A },
                new Chain.Cue { Fx = DoubleSlash.Module, T = B, X = t => STEP },
                new Chain.Cue { Fx = HeavyCleave.Module, T = C, X = t => STEP * 2 }),
            act: Act, actor: 2)
        {
            Title = "Sword combo",
            Desc = "Three strokes walking forward: a slash, a crossing double cut, and a two-handed cleave into the floor that throws the target about.",
            Tags = new[] { "move", "melee", "sword", "combo", "one-shot" },
            Bg = "ground",
        };
    }
}
