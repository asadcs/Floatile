using UnityEngine;

public static class TierColorTable
{
    // Powers of 2 from 2 to 2048 mapped to pastel/vivid colors
    static readonly Color[] COLORS =
    {
        new Color(0.690f, 0.831f, 0.945f), // 2     #B0D4F1 powder blue
        new Color(0.420f, 0.749f, 0.420f), // 4     #6BBF6B sage green
        new Color(0.302f, 0.722f, 0.722f), // 8     #4DB8B8 teal
        new Color(0.878f, 0.361f, 0.361f), // 16    #E05C5C rose
        new Color(0.941f, 0.573f, 0.227f), // 32    #F0923A tangerine
        new Color(0.961f, 0.816f, 0.227f), // 64    #F5D03A gold
        new Color(0.608f, 0.369f, 0.800f), // 128   #9B5ECC purple
        new Color(0.910f, 0.243f, 0.549f), // 256   #E83E8C hot pink
        new Color(1.000f, 0.082f, 0.576f), // 512   #FF1493 deep pink
        new Color(0.173f, 0.290f, 0.561f), // 1024  #2C4A8F navy
        new Color(0.102f, 0.184f, 0.435f), // 2048  #1A2F6F dark navy
    };

    // 11 tier levels: index 0 = tier 2, index 10 = tier 2048
    // Beyond 2048: cycle through COLORS with 30% brightness boost
    public static Color ForTier(long tier)
    {
        int n = TierIndex(tier);
        if (n < COLORS.Length)
            return COLORS[n];

        // Cycle with brightness boost
        Color baseColor = COLORS[n % COLORS.Length];
        return new Color(
            Mathf.Clamp01(baseColor.r * 1.30f),
            Mathf.Clamp01(baseColor.g * 1.30f),
            Mathf.Clamp01(baseColor.b * 1.30f),
            1f);
    }

    // Returns the index in the tier sequence (0 = tier 2)
    static int TierIndex(long tier)
    {
        if (tier <= 2)    return 0;
        if (tier <= 4)    return 1;
        if (tier <= 8)    return 2;
        if (tier <= 16)   return 3;
        if (tier <= 32)   return 4;
        if (tier <= 64)   return 5;
        if (tier <= 128)  return 6;
        if (tier <= 256)  return 7;
        if (tier <= 512)  return 8;
        if (tier <= 1024) return 9;
        if (tier <= 2048) return 10;
        // Beyond 2048: map by log2 mod COLORS.Length
        return (int)(Mathf.Log(tier, 2f)) % COLORS.Length;
    }
}
