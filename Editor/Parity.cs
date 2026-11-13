using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Pixelfx
{
    public static class Parity
    {
        [Serializable]
        public sealed class Case
        {
            public string id;
            public int w;
            public int h;
            public string[] frames;
        }

        [Serializable]
        sealed class Reference
        {
            public Case[] cases;
        }

        public static readonly string DumpFolder = Path.Combine(Path.GetTempPath(), "pixelfx-parity");

        public static Dictionary<string, Case> Load(string path) =>
            JsonUtility.FromJson<Reference>(File.ReadAllText(path)).cases.ToDictionary(c => c.id);

        static string Hash(SHA256 sha, byte[] rgba) => string.Concat(sha.ComputeHash(rgba).Take(8).Select(b => b.ToString("x2")));

        public static string Mismatch(Case js, Module module, Options options, int margin)
        {
            int w = module.W + margin * 2, h = module.H + margin * 2;
            if ((w, h) != (js.w, js.h)) return $"{js.id} is {w}x{h}, the js original is {js.w}x{js.h}";
            using SHA256 sha = SHA256.Create();
            int i = 0;
            foreach (byte[] rgba in Canvas.Frames(module, options, margin))
            {
                if (i < js.frames.Length && Hash(sha, rgba) != js.frames[i])
                    return $"{js.id} frame {i} differs from the js original; this port's frame is {Dump($"{js.id}-{i}.unity.png", w, h, rgba)}";
                i++;
            }
            return i == js.frames.Length ? null : $"{js.id} has {i} frames, the js original has {js.frames.Length}";
        }

        static string Dump(string name, int w, int h, byte[] rgba)
        {
            var rowsFromTheBottom = new byte[rgba.Length];
            for (int y = 0; y < h; y++) Array.Copy(rgba, y * w * 4, rowsFromTheBottom, (h - 1 - y) * w * 4, w * 4);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixelData(rowsFromTheBottom, 0);
            Directory.CreateDirectory(DumpFolder);
            string path = Path.Combine(DumpFolder, name);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }
    }
}
