using System;
using System.Collections;
using System.Collections.Generic;

namespace Pixelfx
{
    /// <summary>
    /// A pose: what differs from standing easy (Rig.STAND), in screen terms, px from the feet, y negative up. Numbers, names
    /// (weapon, offWeapon) and flags (two, toeB, armsFront...), keyed as in pixelfx's src/core/poses.js:
    ///   x, y       where the feet are, from the effect's origin
    ///   crouch     hips lowered; lean  the spine bent forward (+) or back (-); hx, hy  the head rolled, nodded
    ///   fx, fy, fz the near (right) hand; bx, by, bz  the far (left) hand; ffx, ffy  the near foot; fbx, fby  the far foot
    ///   turn       how far the chest faces us, in radians; yaw  the whole body turned about its feet
    ///   twist      extra chest turn; armsFront  the far hand comes round in front of the chest
    ///   weapon, sa what the near hand holds and its angle on screen (radians, 0 points forward); two  both hands on it
    ///   wl, wz     foreshorten the weapon towards (or, wz -1, away from) the viewer
    ///   pull, nocked  a bow drawn to the far hand, an arrow on the string
    ///   offWeapon, sb  a weapon in the far hand and its angle
    ///   toeF, toeB up on the toes; ghost  leave after-images
    /// Write one as new Pose(READY) { ["weapon"] = "sword", ["fx"] = 11 }: later keys win, as a JS spread does.
    /// A key set to null is there but undefined, as in JS.
    /// </summary>
    public sealed class Pose : IEnumerable<KeyValuePair<string, object>>
    {
        readonly Dictionary<string, object> v = new Dictionary<string, object>();

        public Pose() { }

        /// <summary>{ ...a, ...b, ... }</summary>
        public Pose(params Pose[] spread)
        {
            foreach (Pose p in spread)
                if (p != null)
                    foreach (var kv in p.v)
                        v[kv.Key] = kv.Value;
        }

        public object this[string key]
        {
            get => v.TryGetValue(key, out object o) ? o : null;
            set => v[key] = value switch
            {
                int i => (double)i,
                float f => (double)f,
                _ => value,
            };
        }

        public IEnumerable<string> Keys => v.Keys;
        public bool HasKey(string key) => v.ContainsKey(key);

        /// <summary>The number at key, or null if it isn't a number (undefined, a name, a flag).</summary>
        public double? N(string key) => v.TryGetValue(key, out object o) && o is double d ? d : (double?)null;
        public double Num(string key, double otherwise) => N(key) ?? otherwise;
        public string Str(string key) => v.TryGetValue(key, out object o) ? o as string : null;
        public bool Truthy(string key) => v.TryGetValue(key, out object o) && Options.Truthy(o);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => v.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => v.GetEnumerator();

        public override string ToString() => "{ " + string.Join(", ", System.Linq.Enumerable.Select(v, kv => $"{kv.Key}: {kv.Value}")) + " }";
    }

    /// <summary>A key on an act's pose track: at time T, this pose, arriving How ("smooth", "out", "in", "line", "snap").</summary>
    public sealed class PoseKey
    {
        public readonly double T;
        public readonly Pose Pose;
        public readonly string How;

        public PoseKey(double t, Pose pose, string how = null)
        {
            T = t;
            Pose = pose;
            How = how;
        }
    }

    /// <summary>
    /// A moment of contact: everything freezes Stop seconds, the picture shakes Shake seconds by Amp px, and the target (or only
    /// target At) is hit: pushed, flashed, shedding straw, taking a skin ("char", "ice", "shadow", "holy", "red", "shock", "poison",
    /// "flash"; null clears it), shivering. Unset fields keep the JS defaults.
    /// </summary>
    public sealed class HitCue
    {
        public double T;
        public double? Stop, Shake, Amp, Push, Straw, Flash, Shiver, Y;
        /// <summary>false: the target isn't touched.</summary>
        public bool Target = true;
        public int? At;
        string skin;
        public bool HasSkin { get; private set; }

        public string Skin
        {
            get => skin;
            set
            {
                skin = value;
                HasSkin = true;
            }
        }

        public HitCue Later(double dt)
        {
            var h = (HitCue)MemberwiseClone();
            h.T += dt;
            return h;
        }
    }

    /// <summary>Dust kicked up off the floor at time T: N specks from local X, thrown Dir (-1 left, 1 right, null either way), in color C.</summary>
    public sealed class DustCue
    {
        public double T, X;
        public int N = 6;
        public int? Dir;
        public string C;

        public DustCue Later(double dt, double dx = 0)
        {
            var d = (DustCue)MemberwiseClone();
            d.T += dt;
            d.X += dx;
            return d;
        }
    }

    /// <summary>
    /// An effect performed (pixelfx's `export const act`): the character's pose track, the moments of contact, the dust, the
    /// straw targets (their floor x, local px), the after-image color, and whether the effect paints the standing body (OnBody,
    /// carried along with the pose by Follow).
    /// </summary>
    public sealed partial class Act
    {
        public PoseKey[] Poses;
        public HitCue[] Hits;
        public DustCue[] Dust;
        public double[] Targets;
        public string Ghost;
        public bool OnBody;
        public double Follow = 1;
    }

    public static class Poses
    {
        /// <summary>The stance acts start from.</summary>
        public static readonly Pose READY = new Pose { ["crouch"] = 3, ["fx"] = 11, ["fy"] = -21, ["bx"] = 9, ["by"] = -21, ["ffx"] = 7, ["fbx"] = -7 };
    }
}
