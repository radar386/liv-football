using System;
using System.Collections.Generic;
using UnityEngine;

// A club's home kit: colours as "#RRGGBB" strings (so they save as JSON and sync safely) and one pattern.
// DRAFT: mirrors the web game. Original colourways only, no crests, sponsors or manufacturer marks.
[Serializable]
public class ClubKit
{
    public string body = "#228844";
    public string sleeve = "#228844";
    public string shorts = "#FFFFFF";
    public string socks = "#228844";
    public string trim = "#FFFFFF";
    public string accent = "#FFFFFF";   // colour of the stripes, hoops, halves or sash
    public string pattern = "solid";    // solid, stripes, hoops, halves, sash
}

public static class KitCatalog
{
    public static readonly string[] Patterns = { "solid", "stripes", "hoops", "halves", "sash" };

    // body, sleeve, shorts, socks, trim, pattern, accent
    private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
    {
        { "liverpool",         new[] { "#C8102E", "#C8102E", "#C8102E", "#C8102E", "#F4E9D8", "solid", "#C8102E" } },
        { "arsenal",           new[] { "#D7141A", "#FFFFFF", "#FFFFFF", "#D7141A", "#FFFFFF", "solid", "#D7141A" } },
        { "chelsea",           new[] { "#1F4FB4", "#1F4FB4", "#1F4FB4", "#FFFFFF", "#FFFFFF", "solid", "#1F4FB4" } },
        { "manchester city",   new[] { "#8EC7EF", "#8EC7EF", "#FFFFFF", "#8EC7EF", "#FFFFFF", "solid", "#8EC7EF" } },
        { "manchester united", new[] { "#D81920", "#D81920", "#FFFFFF", "#141414", "#141414", "solid", "#D81920" } },
        { "barcelona",         new[] { "#A50044", "#A50044", "#16357A", "#A50044", "#F2C230", "stripes", "#16357A" } },
        { "real madrid",       new[] { "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#1B2A5C", "solid", "#FFFFFF" } },
        { "juventus",          new[] { "#F4F4F4", "#F4F4F4", "#141414", "#141414", "#141414", "stripes", "#141414" } },
        { "ac milan",          new[] { "#D2202B", "#D2202B", "#141414", "#D2202B", "#141414", "stripes", "#141414" } },
        { "bayern munich",     new[] { "#DC052D", "#DC052D", "#DC052D", "#DC052D", "#FFFFFF", "solid", "#DC052D" } },
        { "newcastle united",  new[] { "#F4F4F4", "#F4F4F4", "#141414", "#141414", "#141414", "stripes", "#141414" } },
        { "celtic",            new[] { "#FFFFFF", "#FFFFFF", "#FFFFFF", "#1E8A4B", "#1E8A4B", "hoops", "#1E8A4B" } },
        { "inter",             new[] { "#1B5FBF", "#1B5FBF", "#141414", "#141414", "#F2C230", "stripes", "#141414" } },
        { "borussia dortmund", new[] { "#FDE100", "#FDE100", "#141414", "#FDE100", "#141414", "solid", "#FDE100" } },
        { "sylfoid fc",        new[] { "#127C7A", "#127C7A", "#0E3F46", "#127C7A", "#F2B632", "sash", "#F2B632" } },
        { "sylford fc",        new[] { "#127C7A", "#127C7A", "#0E3F46", "#127C7A", "#F2B632", "sash", "#F2B632" } },
        { "umended fc",        new[] { "#5B2A86", "#5B2A86", "#2C1747", "#5B2A86", "#FFFFFF", "halves", "#F28C28" } },
    };

    // The kit the player chose, else the club's known colours, else colours made from the club name.
    public static ClubKit ForProfile(UserProfile p)
    {
        if (p != null && p.kit != null && !string.IsNullOrEmpty(p.kit.body)) return Sanitize(p.kit);
        return ForName(p == null ? "" : p.clubName, p == null ? "x" : p.username);
    }

    public static ClubKit ForName(string clubName, string seed)
    {
        string key = (clubName ?? "").Trim().ToLowerInvariant();
        string[] t;
        if (Table.TryGetValue(key, out t))
            return new ClubKit { body = t[0], sleeve = t[1], shorts = t[2], socks = t[3], trim = t[4], pattern = t[5], accent = t[6] };

        // Stable colours from the name, so a club always looks the same.
        uint h = 2166136261;
        foreach (char c in (seed ?? "x")) { h ^= c; h *= 16777619; }
        string main = HsvToHex((h % 360) / 360f, 0.62f, 0.55f);
        bool contrast = ((h >> 8) % 3) == 0;
        string second = contrast ? HsvToHex(((h % 360) + 180) % 360 / 360f, 0.7f, 0.85f) : "#F4F4F4";
        return new ClubKit { body = main, sleeve = main, shorts = contrast ? "#222222" : "#F4F4F4", socks = main, trim = second, accent = second, pattern = "solid" };
    }

    // Anything saved or received from another player is checked before it is used.
    public static ClubKit Sanitize(ClubKit k)
    {
        ClubKit s = new ClubKit();
        s.body = Hex(k.body, "#228844");
        s.sleeve = Hex(k.sleeve, s.body);
        s.shorts = Hex(k.shorts, "#FFFFFF");
        s.socks = Hex(k.socks, s.body);
        s.trim = Hex(k.trim, "#FFFFFF");
        s.accent = Hex(k.accent, s.trim);
        s.pattern = Array.IndexOf(Patterns, k.pattern) >= 0 ? k.pattern : "solid";
        return s;
    }

    public static bool IsHex(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 7 || s[0] != '#') return false;
        for (int i = 1; i < 7; i++)
        {
            char c = s[i];
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return false;
        }
        return true;
    }

    private static string Hex(string s, string fallback) { return IsHex(s) ? s.ToUpperInvariant() : fallback; }

    public static Color ToColor(string hex)
    {
        if (!IsHex(hex)) return new Color(0.5f, 0.5f, 0.5f, 1f);
        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    private static string HsvToHex(float h, float s, float v)
    {
        float r = 0, g = 0, b = 0;
        int i = (int)(h * 6f) % 6;
        float f = h * 6f - (int)(h * 6f), p = v * (1f - s), q = v * (1f - f * s), t = v * (1f - (1f - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return "#" + ((int)(r * 255f)).ToString("X2") + ((int)(g * 255f)).ToString("X2") + ((int)(b * 255f)).ToString("X2");
    }
}
