using System;

// The three rating scales used by LIV Football Mobile:
//  - Unity rating (this project): 55 to 115. Packs, shops and the catalog use it.
//  - Web rating (liv.html "ovr"):  50 to 97.  Unity 55 -> 55, Unity 115 -> 97.
//  - eFootball rating (Featured and Epic packs): 60 to 99+.
public static class RatingScale
{
    public static int ToWeb(int unityRating)
    {
        return (int)Math.Round(55 + (unityRating - 55) * 0.70);
    }

    public static int FromWeb(int webRating)
    {
        return (int)Math.Round(55 + (webRating - 55) / 0.70);
    }

    // eFootball scale on the Unity rating: 115 -> 99, 85 -> 80, 55 -> 60.
    public static int ToEFootball(int unityRating)
    {
        return (int)Math.Round(60 + (unityRating - 55) * 0.65);
    }

    public static string EFootballTier(int efRating)
    {
        return efRating >= 90 ? "Epic" : efRating >= 80 ? "Featured" : "Standard";
    }
}
