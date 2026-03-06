using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// FragmentProgressUI.cs
///
/// Manages the visual fragment progress display inside the ObjectiveUI panel.
/// Shows the dashed outline and reveals each fragment image as pieces are collected.
///
/// Fragment images are set to full alpha immediately when collected (not faded)
/// because the panel itself is hidden at that point — the fade-in of the panel
/// handles the reveal effect. Once the panel fades in, all collected fragments
/// are already fully visible.
/// </summary>
public class FragmentProgressUI : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector References
    // -----------------------------------------------------------------------

    [Header("Outline (hidden until first fragment collected)")]
    [Tooltip("The dashed outline image shown as the background of the puzzle.")]
    public Image outlineImage;

    [Header("Fragment Images (index 0 = piece 1)")]
    [Tooltip("Assign each fragment image in order. They start invisible and appear when collected.")]
    public Image[] fragmentImages;

    [Header("Counter Text")]
    [Tooltip("Shows e.g. '3/5'. Leave null to hide.")]
    public TextMeshProUGUI counterText;

    [Header("Settings")]
    [Tooltip("Target alpha of the outline image once visible (0-1).")]
    [Range(0f, 1f)]
    public float outlineAlpha = 0.7f;

    // -----------------------------------------------------------------------
    // Private State
    // -----------------------------------------------------------------------

    /// <summary>Tracks which pieces have already been revealed.</summary>
    private bool[] revealed;

    /// <summary>Total number of pieces.</summary>
    private int totalPieces = 5;

    /// <summary>Whether the outline and counter have been made visible yet.</summary>
    private bool hasShownOutline = false;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Awake()
    {
        revealed = new bool[fragmentImages != null ? fragmentImages.Length : 5];

        // Keep outline fully hidden at start
        SetAlpha(outlineImage, 0f);

        // Hide all fragment images at startup
        if (fragmentImages != null)
            foreach (Image img in fragmentImages)
                SetAlpha(img, 0f);

        // Keep counter hidden at start
        if (counterText != null)
        {
            Color c = counterText.color;
            c.a = 0f;
            counterText.color = c;
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Call this when the player collects a fragment.
    /// Sets the fragment image to full alpha immediately (panel is hidden anyway)
    /// so when the panel fades in, the fragment is already there.
    /// </summary>
    public void RevealFragment(int pieceIndex, int total)
    {
        totalPieces = total;

        if (fragmentImages == null || pieceIndex < 0 || pieceIndex >= fragmentImages.Length)
        {
            Debug.LogWarning($"[FragmentProgressUI] pieceIndex {pieceIndex} out of range!");
            return;
        }

        if (revealed[pieceIndex]) return;
        revealed[pieceIndex] = true;

        // Make outline and counter visible the first time
        ShowOutlineAndCounter();

        // Count collected
        int collectedCount = 0;
        foreach (bool r in revealed)
            if (r) collectedCount++;

        UpdateCounter(collectedCount, total);

        // Set to full alpha immediately — the panel's own fade handles the reveal
        // No point running a fade coroutine on a panel that isn't visible yet
        Image img = fragmentImages[pieceIndex];
        SetAlpha(img, 1f);
    }

    /// <summary>
    /// Sync the UI with all currently collected pieces.
    /// Used when the panel becomes visible after being hidden (catch-up).
    /// </summary>
    public void SyncProgress(System.Collections.Generic.List<int> collectedIndices, int total)
    {
        totalPieces = total;

        if (collectedIndices == null || collectedIndices.Count == 0) return;

        // Show outline and counter now that something has been collected
        ShowOutlineAndCounter();

        foreach (int idx in collectedIndices)
        {
            if (idx < 0 || idx >= fragmentImages.Length) continue;
            if (revealed[idx]) continue;

            revealed[idx] = true;

            // Set immediately — no fade needed during sync
            SetAlpha(fragmentImages[idx], 1f);
        }

        UpdateCounter(collectedIndices.Count, total);
    }

    /// <summary>
    /// Reset everything to hidden. Call on New Game.
    /// </summary>
    public void ResetAll()
    {
        hasShownOutline = false;

        if (revealed != null)
            for (int i = 0; i < revealed.Length; i++)
                revealed[i] = false;

        SetAlpha(outlineImage, 0f);

        if (fragmentImages != null)
            foreach (Image img in fragmentImages)
                SetAlpha(img, 0f);

        if (counterText != null)
        {
            Color c = counterText.color;
            c.a = 0f;
            counterText.color = c;
        }
    }

    // -----------------------------------------------------------------------
    // Private Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Makes the outline and counter visible the first time a fragment is collected.
    /// Only runs once — subsequent calls do nothing.
    /// </summary>
    private void ShowOutlineAndCounter()
    {
        if (hasShownOutline) return;
        hasShownOutline = true;

        SetAlpha(outlineImage, outlineAlpha);

        if (counterText != null)
        {
            Color c = counterText.color;
            c.a = 1f;
            counterText.color = c;
        }
    }

    /// <summary>Update the X/Y counter text.</summary>
    private void UpdateCounter(int collected, int total)
    {
        if (counterText == null) return;
        counterText.text = collected >= total
            ? "<color=yellow>Complete!</color>"
            : $"{collected}/{total}";
    }

    /// <summary>Set an Image's alpha directly.</summary>
    private void SetAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}