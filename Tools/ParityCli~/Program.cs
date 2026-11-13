using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Pixelfx;

// Every ported effect against the JS original's frame hashes (Tests/Editor/Parity.json), and with --acts every act against
// Tests/Editor/ActParity.json. Arguments narrow it to effect names or categories. On a mismatch the first differing frame is
// written as a PNG to %TEMP%/pixelfx-parity; nothing else is written.
static class Program
{
    // the package: the first folder up from here (or from the program) that holds Tests/Editor/Parity.json
    static readonly string Root = Find(Directory.GetCurrentDirectory()) ?? Find(AppContext.BaseDirectory) ?? throw new DirectoryNotFoundException("run this from inside the pixelfx-unity package");

    static string Find(string from)
    {
        for (var d = new DirectoryInfo(from); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Tests", "Editor", "Parity.json"))) return d.FullName;
        return null;
    }
    static readonly string Dump = Path.Combine(Path.GetTempPath(), "pixelfx-parity");

    static int Main(string[] args)
    {
        bool acts = args.Contains("--acts");
        string[] wanted = args.Where(a => !a.StartsWith("--")).ToArray();
        bool Wanted(Module m) => wanted.Length == 0 || wanted.Contains(m.Name) || wanted.Contains(ParityRun.Category(m));
        return acts ? Acts(Wanted) : Frames(Wanted);
    }

    static JsonElement Load(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "Tests", "Editor", name))).RootElement;

    static int Frames(Func<Module, bool> wanted)
    {
        var js = Load("Parity.json").GetProperty("cases").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString());
        var tally = new SortedDictionary<string, (int pass, int fail)>();
        int failed = 0;
        foreach (Module m in Effects.All.Where(wanted))
        {
            string cat = ParityRun.Category(m);
            tally.TryGetValue(cat, out var t);
            string why;
            if (!js.TryGetValue(m.Name, out JsonElement c)) why = $"{m.Name} has no js reference";
            else
            {
                var frames = c.GetProperty("frames").EnumerateArray().Select(f => f.GetString()).ToList();
                (int frame, byte[] rgba, string why)? bad;
                try
                {
                    bad = ParityRun.EffectMismatch(m, c.GetProperty("w").GetInt32(), c.GetProperty("h").GetInt32(), frames);
                }
                catch (Exception e)
                {
                    bad = (-1, null, $"{m.Name} threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
                why = bad?.why;
                if (bad is { rgba: { } } b) why += " -> " + Png($"{m.Name}-{b.frame}.cs.png", m.W, m.H, b.rgba);
            }
            if (why == null) t.pass++;
            else
            {
                t.fail++;
                failed++;
                Console.WriteLine("FAIL " + why);
            }
            tally[cat] = t;
        }
        foreach (var kv in tally) Console.WriteLine($"{kv.Key,-12} {kv.Value.pass} pass, {kv.Value.fail} fail");
        var ported = new HashSet<string>(Effects.All.Select(m => m.Name));
        var missing = js.Keys.Where(k => !ported.Contains(k)).ToList();
        Console.WriteLine($"total {tally.Values.Sum(v => v.pass)} pass, {failed} fail; {missing.Count} of {js.Count} js effects not ported");
        return failed == 0 ? 0 : 1;
    }

    static int Acts(Func<Module, bool> wanted)
    {
        var cases = Load("ActParity.json").GetProperty("cases").EnumerateArray().ToList();
        var byName = Effects.All.ToDictionary(m => m.Name);
        int pass = 0, failed = 0, skipped = 0;
        foreach (JsonElement c in cases)
        {
            string id = c.GetProperty("id").GetString();
            if (!byName.TryGetValue(id, out Module m))
            {
                skipped++;
                continue;
            }
            if (!wanted(m)) continue;
            var frames = c.GetProperty("frames").EnumerateArray().Select(f => f.GetString()).ToList();
            (int frame, byte[] rgba, string why)? bad;
            try
            {
                bad = ParityRun.ActMismatch(m, c.GetProperty("timing").GetString(), c.GetProperty("w").GetInt32(), c.GetProperty("h").GetInt32(), frames);
            }
            catch (Exception e)
            {
                bad = (-1, null, $"{id} act threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
            if (bad == null)
            {
                pass++;
                continue;
            }
            failed++;
            string why = bad.Value.why;
            if (bad.Value.rgba != null) why += " -> " + Png($"{id}-act-{bad.Value.frame}.cs.png", c.GetProperty("w").GetInt32(), c.GetProperty("h").GetInt32(), bad.Value.rgba);
            Console.WriteLine("FAIL " + why);
        }
        Console.WriteLine($"acts: {pass} pass, {failed} fail, {skipped} not ported yet");
        return failed == 0 ? 0 : 1;
    }

    // a minimal PNG: RGBA, no filtering, zlib-compressed
    static string Png(string name, int w, int h, byte[] rgba)
    {
        Directory.CreateDirectory(Dump);
        string path = Path.Combine(Dump, name);
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * w * 4, w * 4);
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) raw.WriteTo(zs);
        using var f = File.Create(path);
        f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] data)
        {
            byte[] t = System.Text.Encoding.ASCII.GetBytes(type);
            f.Write(BE((uint)data.Length));
            f.Write(t);
            f.Write(data);
            f.Write(BE(Crc(t.Concat(data).ToArray())));
        }
        var ihdr = new List<byte>();
        ihdr.AddRange(BE((uint)w));
        ihdr.AddRange(BE((uint)h));
        ihdr.AddRange(new byte[] { 8, 6, 0, 0, 0 });
        Chunk("IHDR", ihdr.ToArray());
        Chunk("IDAT", z.ToArray());
        Chunk("IEND", new byte[0]);
        return path;
    }

    static byte[] BE(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    static uint Crc(byte[] data)
    {
        uint c = 0xffffffff;
        foreach (byte b in data)
        {
            c ^= b;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xffffffff;
    }
}
