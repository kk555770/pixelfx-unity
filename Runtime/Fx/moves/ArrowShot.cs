namespace Pixelfx
{
    public static class ArrowShot
    {
        const double LOOSE = 0.56, FLY = 0.24, HIT = LOOSE + FLY, TX = 180, SPEED = (TX - 30) / FLY;

        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "bow", ["sa"] = -0.4, ["fx"] = 12, ["fy"] = -20, ["bx"] = 6, ["by"] = -17 };
        static readonly Pose AIM = new Pose(REST_) { ["crouch"] = 3, ["lean"] = 1, ["fx"] = 18, ["fy"] = -28, ["sa"] = 0, ["bx"] = 10, ["by"] = -27, ["nocked"] = true, ["ffx"] = 11, ["fbx"] = -9 };
        static readonly Pose DRAWN = new Pose(AIM) { ["crouch"] = 4, ["lean"] = -2, ["fx"] = 18, ["bx"] = -3, ["by"] = -29, ["pull"] = true };
        static readonly Pose LOOSED = new Pose(AIM) { ["crouch"] = 3, ["lean"] = 2, ["fx"] = 19, ["fy"] = -28, ["bx"] = -9, ["by"] = -32, ["nocked"] = false, ["pull"] = false };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.44, REST_),
                new PoseKey(-0.14, AIM),
                new PoseKey(0.16, DRAWN),
                new PoseKey(LOOSE, new Pose(DRAWN) { ["crouch"] = 4 }),
                new PoseKey(LOOSE + 0.002, LOOSED, "snap"),
                new PoseKey(1.1, new Pose(LOOSED) { ["bx"] = -6, ["by"] = -25 }),
                new PoseKey(1.6, REST_),
                new PoseKey(1.9, REST_),
            },
            Hits = new[] { new HitCue { T = HIT, Stop = 0.07, Shake = 0.12, Amp = 2, Push = 2.2, Straw = 14, Y = -28 } },
        };

        public static readonly Module Module = new Module("arrow-shot", "pixelfx/fx/moves/arrow-shot", 300, 74, 38, 66, 1.9, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = Arrow.Module, T = LOOSE, Until = HIT, X = t => 30 + (t - LOOSE) * SPEED, Y = t => -28 },
                new Chain.Cue { Fx = HitSpark.Module, T = HIT, X = t => TX - 10, Options = new Options { ["height"] = 30 } }),
            act: Act, actor: 2)
        {
            Title = "Arrow shot",
            Desc = "He raises the bow, draws to his cheek, holds his breath and looses: the string snaps forward, the arrow crosses the floor and thuds home.",
            Tags = new[] { "move", "bow", "arrow", "projectile", "ranged", "one-shot" },
            Bg = "ground",
        };
    }
}
