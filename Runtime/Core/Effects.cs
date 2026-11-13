using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pixelfx
{
    public static class Effects
    {
        public static readonly IReadOnlyList<Module> All = typeof(Effects).Assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(Module))
            .Select(f => (Module)f.GetValue(null))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToArray();

        static readonly Dictionary<string, Module> ByName = All.ToDictionary(m => m.Name);

        public static Module Get(string name) =>
            ByName.TryGetValue(name, out Module module) ? module : throw new ArgumentException($"no library effect named {name}", nameof(name));
    }
}
