using UnityEngine;

// Guarantees hero tile color regardless of TileVisual defaults
public sealed class HeroColor : MonoBehaviour
{
    private void Awake()
    {
        // Use tier-2 color so same-number tiles look identical; Sprint 1 replaces this with TierColorTable.
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.996f, 0.847f, 0.878f); // #FED8E0 Cotton Pink (tier 2)
    }
}
