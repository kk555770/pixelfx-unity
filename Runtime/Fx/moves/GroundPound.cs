namespace Pixelfx
{
    public static class GroundPound
    {
        const double LAND = 0.62, TX = 80;

        static readonly Pose COIL = new Pose(Poses.READY) { ["crouch"] = 8, ["lean"] = 4, ["fx"] = -6, ["fy"] = -14, ["bx"] = 3, ["by"] = -14, ["ffx"] = 8, ["fbx"] = -8 };
        static readonly Pose AIR = new Pose { ["turn"] = 0.8, ["crouch"] = -1, ["fx"] = -9, ["fy"] = -44, ["bx"] = 10, ["by"] = -44, ["ffx"] = 6, ["ffy"] = -8, ["fbx"] = -3, ["fby"] = -3, ["toeB"] = true };
        static readonly Pose TOP = new Pose(AIR) { ["y"] = -62, ["fx"] = -10, ["fy"] = -20, ["bx"] = 11, ["by"] = -20, ["ffx"] = 7, ["ffy"] = -10, ["fbx"] = -4, ["fby"] = -10 };
        static readonly Pose DOWN = new Pose { ["turn"] = 0.8, ["y"] = -8, ["crouch"] = 0, ["fx"] = -5, ["fy"] = -40, ["bx"] = 6, ["by"] = -40, ["ffx"] = 3, ["ffy"] = -3, ["fbx"] = -3, ["fby"] = -3, ["ghost"] = 1 };
        static readonly Pose HERO_ = new Pose { ["y"] = 0, ["crouch"] = 8, ["lean"] = 5, ["fx"] = 13, ["fy"] = -8, ["bx"] = -6, ["by"] = -23, ["ffx"] = 11, ["fbx"] = -10 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.5, Poses.READY),
                new PoseKey(-0.16, COIL),
                new PoseKey(0, new Pose(COIL) { ["crouch"] = 7 }),
                new PoseKey(0.03, new Pose(AIR) { ["y"] = -10, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.26, new Pose(TOP) { ["y"] = -60 }, "out"),
                new PoseKey(0.42, TOP),
                new PoseKey(LAND - 0.005, new Pose(DOWN) { ["y"] = -4 }, "in"),
                new PoseKey(LAND, HERO_, "snap"),
                new PoseKey(LAND + 0.8, new Pose(HERO_) { ["crouch"] = 6 }),
                new PoseKey(LAND + 1.3, new Pose(HERO_) { ["crouch"] = 5, ["lean"] = 3, ["fx"] = 13, ["fy"] = -15, ["bx"] = -5, ["by"] = -20 }),
                new PoseKey(LAND + 1.8, Poses.READY),
                new PoseKey(LAND + 3.2, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = LAND, Stop = 0.2, Shake = 0.5, Amp = 4, Target = false },
                new HitCue { T = LAND + 0.14, Stop = 0.04, Push = 3, Straw = 26, Y = -8 },
                new HitCue { T = LAND + 0.44, Push = -1.6, Straw = 8, Y = -8, Flash = 0 },
            },
            Dust = new[] { new DustCue { T = 0.03, X = 0, N = 8 } },
        };

        public static readonly Module Module = new Module("ground-pound", "pixelfx/fx/moves/ground-pound", 304, 204, 152, 168, LAND + 3.2, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(new Chain.Cue { Fx = GroundSlam.Module, T = LAND }),
            act: Act, actor: 2)
        {
            Title = "Ground pound",
            Desc = "He coils, leaps out of frame height, hangs, and comes down like a rock: the floor cracks, shock waves race out and throw the target about as they pass.",
            Tags = new[] { "move", "impact", "jump", "landing", "shockwave", "one-shot" },
            Bg = "ground",
        };
    }
}
