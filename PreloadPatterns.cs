using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SharpDX;

namespace ModernBar
{
    internal sealed class CompiledPreload
    {
        public string Group;
        public string Display;
        public Regex Regex;
        public Color Color;
    }

    internal sealed class PreloadFilePattern
    {
        public string Path = "";
        public string Display = "";
        public string ColorHex = "#FFFFFFFF";
        public bool Enable = true;
    }

    internal sealed class PreloadFileGroup
    {
        public string Name = "New Group";
        public bool Enable = true;
        public List<PreloadFilePattern> Patterns = new();
    }

    internal static class PreloadFileStore
    {
        internal static string LastSearch = "";

        internal static string ResolvePath(string pluginDir, string asmName)
        {
            try
            {
                var dir = pluginDir ?? "";
                var runtime = Path.Combine(dir, "Data", "preloads.json");
                if (File.Exists(runtime))
                {
                    LastSearch = $"live='{runtime}' (runtime copy)";
                    return runtime;
                }
                var inSource = Path.GetFullPath(Path.Combine(dir, "..", "..", "Source", asmName, "Data", "preloads.json"));
                LastSearch = $"dir='{dir}' runtimeMissing source='{inSource}' sourceExists={File.Exists(inSource)}";
                if (File.Exists(inSource))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(runtime));
                    File.Copy(inSource, runtime);
                    LastSearch += " seeded=true";
                    return runtime;
                }
            }
            catch (Exception ex)
            {
                LastSearch = "resolve threw: " + ex.Message;
            }
            return null;
        }

        internal static List<PreloadFileGroup> Load(string path)
        {
            var groups = new List<PreloadFileGroup>();
            var root = JsonConvert.DeserializeObject<PreloadDocFile>(File.ReadAllText(path));
            if (root?.Groups == null) return groups;
            foreach (var g in root.Groups)
            {
                if (g == null) continue;
                var fg = new PreloadFileGroup { Name = g.Name ?? "New Group", Enable = g.Enable };
                if (g.Patterns != null)
                {
                    foreach (var p in g.Patterns)
                    {
                        if (p == null) continue;
                        fg.Patterns.Add(new PreloadFilePattern
                        {
                            Path = p.Path ?? "",
                            Display = p.Display ?? "",
                            ColorHex = $"#{Clamp(p.Color?.R ?? 255):X2}{Clamp(p.Color?.G ?? 255):X2}{Clamp(p.Color?.B ?? 255):X2}{Clamp(p.Color?.A ?? 255):X2}",
                            Enable = p.Enable,
                        });
                    }
                }
                groups.Add(fg);
            }
            return groups;
        }

        internal static void Save(string path, List<PreloadFileGroup> groups)
        {
            var root = new PreloadDocFile();
            foreach (var g in groups)
            {
                var dg = new PreloadDocGroup { Name = g.Name, Enable = g.Enable };
                foreach (var p in g.Patterns)
                {
                    ParseHexToParts(p.ColorHex, out var r, out var gg, out var b, out var a);
                    dg.Patterns.Add(new PreloadDocPattern
                    {
                        Path = p.Path,
                        Display = p.Display,
                        Enable = p.Enable,
                        Color = new PreloadDocColor { R = r, G = gg, B = b, A = a },
                    });
                }
                root.Groups.Add(dg);
            }
            root.Version = 1;
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            }
            catch { }
            File.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented));
        }

        private static int Clamp(int v) => Math.Max(0, Math.Min(255, v));

        internal static void ParseHexToParts(string hex, out int r, out int g, out int b, out int a)
        {
            r = 255; g = 255; b = 255; a = 255;
            try
            {
                var h = (hex ?? "").Trim().TrimStart('#');
                if (h.Length == 6) h += "FF";
                if (h.Length != 8) return;
                r = Convert.ToInt32(h.Substring(0, 2), 16);
                g = Convert.ToInt32(h.Substring(2, 2), 16);
                b = Convert.ToInt32(h.Substring(4, 2), 16);
                a = Convert.ToInt32(h.Substring(6, 2), 16);
            }
            catch { }
        }

        internal static Color ParseHex(string hex, Color fallback)
        {
            ParseHexToParts(hex, out var r, out var g, out var b, out var a);
            try { return new Color((byte)r, (byte)g, (byte)b, (byte)a); }
            catch { return fallback; }
        }

        private sealed class PreloadDocFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("groups")] public List<PreloadDocGroup> Groups = new();
        }

        private sealed class PreloadDocGroup
        {
            [JsonProperty("name")] public string Name = "";
            [JsonProperty("enable")] public bool Enable = true;
            [JsonProperty("patterns")] public List<PreloadDocPattern> Patterns = new();
        }

        private sealed class PreloadDocPattern
        {
            [JsonProperty("path")] public string Path = "";
            [JsonProperty("display")] public string Display = "";
            [JsonProperty("color")] public PreloadDocColor Color = new();
            [JsonProperty("enable")] public bool Enable = true;
        }

        private sealed class PreloadDocColor
        {
            [JsonProperty("r")] public int R = 255;
            [JsonProperty("g")] public int G = 255;
            [JsonProperty("b")] public int B = 255;
            [JsonProperty("a")] public int A = 255;
        }
    }
}
