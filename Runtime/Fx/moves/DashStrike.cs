namespace Pixelfx
{
    public static class DashStrike
    {
        const double CUT = 0.16, HIT = CUT + 0.06, AT = 116, TX = 176;

        static readonly Pose GUARD = new Pose(Poses.READY) { ["weapon"] = "sword", ["fx"] = 12, ["fy"] = -21, ["sa"] = -0.85 };
        static readonly Pose LOW = new Pose(GUARD) { ["crouch"] = 6, ["lean"] = 6, ["fx"] = -3, ["fy"] = -17, ["sa"] = 3.05, ["bx"] = 9, ["by"] = -23, ["ffx"] = 11, ["fbx"] = -10 };
        static readonly Pose COCK = new Pose(GUARD) { ["x"] = AT - 4, ["crouch"] = 3, ["lean"] = -2, ["fx"] = -7, ["fy"] = -35, ["sa"] = -2.65, ["bx"] = 9, ["by"] = -25, ["armsFront"] = true, ["ffx"] = 9, ["fbx"] = -7 };
        static readonly Pose CUT_ = new Pose(GUARD) { ["x"] = AT, ["crouch"] = 2, ["lean"] = 3, ["fx"] = 16, ["fy"] = -37, ["sa"] = -1.45, ["bx"] = -7, ["by"] = -24, ["ffx"] = 11, ["fbx"] = -10 };
        static readonly Pose DOWN = new Pose(GUARD) { ["x"] = AT + 2, ["crouch"] = 5, ["lean"] = 5, ["fx"] = 20, ["fy"] = -21, ["sa"] = 0.62, ["bx"] = -9, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true };
        static readonly Pose HOP = new Pose(GUARD) { ["y"] = -14, ["crouch"] = 0, ["lean"] = -3, ["fx"] = 13, ["fy"] = -31, ["sa"] = -0.9, ["bx"] = -8, ["by"] = -28, ["ffx"] = 6, ["ffy"] = -8, ["fbx"] = -6, ["fby"] = -6, ["ghost"] = 1 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.5, new Pose(GUARD) { ["x"] = 0 }),
                new PoseKey(-0.16, new Pose(LOW) { ["x"] = -2 }),
                new PoseKey(-0.01, new Pose(LOW) { ["x"] = -3, ["crouch"] = 6 }),
                new PoseKey(0, new Pose(LOW) { ["x"] = 8, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.14, new Pose(LOW) { ["x"] = AT - 6, ["ghost"] = 1 }, "line"),
                new PoseKey(0.145, COCK, "snap"),
                new PoseKey(CUT, CUT_, "snap"),
                new PoseKey(HIT, DOWN, "snap"),
                new PoseKey(0.75, new Pose(DOWN) { ["crouch"] = 4, ["lean"] = 4 }),
                new PoseKey(1.25, new Pose(GUARD) { ["x"] = AT + 2 }),
                new PoseKey(1.32, new Pose(GUARD) { ["x"] = AT + 2, ["crouch"] = 5, ["lean"] = -2 }, "snap"),
                new PoseKey(1.4, new Pose(HOP) { ["x"] = AT - 28 }, "snap"),
                new PoseKey(1.6, new Pose(HOP) { ["x"] = 22, ["y"] = -18 }, "line"),
                new PoseKey(1.74, new Pose(GUARD) { ["x"] = 0, ["y"] = 0, ["crouch"] = 5 }, "line"),
                new PoseKey(2.2, new Pose(GUARD) { ["x"] = 0 }),
            },
            Hits = new[] { new HitCue { T = HIT, Stop = 0.17, Shake = 0.26, Amp = 3, Push = 3.4, Straw = 32 } },
            Dust = new[] { new DustCue { T = 0, X = 0, N = 8, Dir = -1 }, new DustCue { T = 0.14, X = AT - 6, N = 7 }, new DustCue { T = 1.74, X = 0, N = 6 } },
        };

        public static readonly Module Module = new Module("dash-strike", "pixelfx/fx/moves/dash-strike", 372, 156, 66, 136, 2.3, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = SwordSlash.Module, T = CUT, X = t => AT },
                new Chain.Cue { Fx = CritHit.Module, T = HIT, X = t => TX - 10, Options = new Options { ["height"] = 28 } }),
            act: Act, actor: 2)
        {
            Title = "Dash strike",
            Desc = "A crouch, then he's across the floor in a blur of after-images and already cutting: a critical hit, a beat, and he hops back to where he started.",
            Tags = new[] { "move", "melee", "sword", "dash", "crit", "one-shot" },
            Bg = "ground",
        };
    }
}
