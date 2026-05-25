using UnityEngine;

public static class TierColorTable
{
    // Exact Cube2048 color palette — 31 explicit entries by log2 index
    // Index = (int)Round(log2(tier)) - 1, starting at tier 2 = index 0
    static readonly Color[] PALETTE =
    {
        new Color(0.961f, 0.435f, 0.455f), // 0   tier 2     coral red     #F56F74
        new Color(0.310f, 0.639f, 1.000f), // 1   tier 4     bright blue   #4FA3FF
        new Color(0.263f, 0.898f, 0.561f), // 2   tier 8     mint green    #43E58F
        new Color(1.000f, 0.761f, 0.620f), // 3   tier 16    peach         #FFC29E
        new Color(1.000f, 0.608f, 0.322f), // 4   tier 32    orange        #FF9B52
        new Color(1.000f, 0.847f, 0.302f), // 5   tier 64    golden yellow #FFD84D
        new Color(1.000f, 0.953f, 0.851f), // 6   tier 128   cream white   #FFF3D9
        new Color(0.961f, 0.953f, 0.427f), // 7   tier 256   lemon yellow  #F5F36D
        new Color(0.208f, 0.898f, 0.898f), // 8   tier 512   aqua cyan     #35E5E5
        new Color(1.000f, 0.816f, 0.945f), // 9   tier 1K    soft pink     #FFD0F1
        new Color(1.000f, 0.953f, 0.420f), // 10  tier 2K    pale yellow   #FFF36B
        new Color(1.000f, 0.482f, 0.482f), // 11  tier 4K    coral salmon  #FF7B7B
        new Color(0.310f, 0.639f, 1.000f), // 12  tier 8K    bright blue   #4FA3FF
        new Color(0.275f, 0.941f, 0.482f), // 13  tier 16K   neon green    #46F07B
        new Color(1.000f, 0.816f, 0.690f), // 14  tier 32K   peach beige   #FFD0B0
        new Color(1.000f, 0.702f, 0.278f), // 15  tier 64K   orange gold   #FFB347
        new Color(0.847f, 0.647f, 1.000f), // 16  tier 128K  lavender      #D8A5FF
        new Color(0.259f, 0.910f, 1.000f), // 17  tier 256K  electric cyan #42E8FF
        new Color(0.659f, 1.000f, 0.353f), // 18  tier 512K  lime green    #A8FF5A
        new Color(1.000f, 0.780f, 0.910f), // 19  tier 1M    cotton candy  #FFC7E8
        new Color(1.000f, 0.969f, 0.600f), // 20  tier 2M    soft yellow   #FFF799
        new Color(1.000f, 0.573f, 0.573f), // 21  tier 4M    salmon pink   #FF9292
        new Color(1.000f, 0.420f, 0.420f), // 22  tier 8M    deep coral    #FF6B6B
        new Color(0.231f, 0.918f, 0.561f), // 23  tier 16M   emerald green #3BEA8F
        new Color(0.290f, 0.565f, 1.000f), // 24  tier 32M   royal blue    #4A90FF
        new Color(0.784f, 0.608f, 1.000f), // 25  tier 64M   lavender      #C89BFF
        new Color(1.000f, 0.667f, 0.357f), // 26  tier 128M  orange peach  #FFAA5B
        new Color(0.498f, 0.984f, 1.000f), // 27  tier 256M  ice cyan      #7FFBFF
        new Color(1.000f, 0.447f, 0.784f), // 28  tier 512M  hot pink      #FF72C8
        new Color(0.698f, 0.400f, 1.000f), // 29  tier 1B    electric purple #B266FF
        new Color(0.220f, 0.886f, 0.478f), // 30  tier 2B+   emerald green #38E27A
    };

    public static Color ForTier(long tier)
    {
        if (tier < 2) tier = 2;
        int idx = (int)Mathf.Round(Mathf.Log(tier, 2f)) - 1;
        // Cycle within palette for tiers beyond 1B
        return PALETTE[Mathf.Clamp(idx, 0, PALETTE.Length - 1) % PALETTE.Length];
    }
}
