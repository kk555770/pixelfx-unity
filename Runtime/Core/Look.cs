using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixelfx
{
    public enum Turns
    {
        None,
        Aim,
        Heading,
    }

    public readonly struct Hit
    {
        public readonly double At, Freeze, Shake;
        public readonly int Amp;

        public Hit(double at, double freeze, double shake, int amp)
        {
            At = at;
            Freeze = freeze;
            Shake = shake;
            Amp = amp;
        }
    }

    public sealed class Look
    {
        public static readonly string[] AimHeadings = { "e", "ne", "n", "nw", "w", "sw", "s", "se" };
        static readonly string[] SlashHeadings = { "e", "se", "s", "sw", "w", "nw", "n", "ne" };

        public readonly string Name;
        public readonly Module Module;
        public readonly Options Options;
        public readonly Turns Turns;
        public readonly int Margin;
        public readonly Hit[] Hits;
        public readonly bool Lying;

        public Look(string name, Module module, Options options = null, Turns turns = Turns.None, int margin = 0, Hit[] hits = null, bool lying = false)
        {
            Name = name;
            Module = module;
            Options = options ?? new Options();
            Turns = turns;
            Margin = margin;
            Hits = hits ?? new Hit[0];
            Lying = lying;
        }

        public IReadOnlyList<string> Headings => Turns == Turns.Aim ? AimHeadings : Turns == Turns.Heading ? SlashHeadings : new string[0];

        public Options Turned(string heading)
        {
            var o = new Options();
            foreach (var kv in Options.All) o[kv.Key] = kv.Value;
            if (Turns == Turns.None) return o;
            int turn = Array.IndexOf((string[])Headings, heading);
            if (turn < 0) throw new ArgumentException($"{Name} has no heading {heading}", nameof(heading));
            if (Turns == Turns.Aim) o["aim"] = -(double)turn * Math.PI / 4;
            else o["heading"] = turn * Math.PI / 4;
            return o;
        }

        public IEnumerable<(string id, Options options)> Cases =>
            Turns == Turns.None ? new[] { (Name, Turned(null)) } : Headings.Select(h => ($"{Name}-{h}", Turned(h)));
    }
}
