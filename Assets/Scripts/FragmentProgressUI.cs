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
/// IMPORTANT: This script's GameObject starts DISABLED, which means Awake() never
/// runs until the panel is first activated. All initialization is therefore done in
/// EnsureInitialized(), which is called defensively at the top of every public method.
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

    /// <summary>Whether EnsureInitialized has already run.</summary>
    private bool isInitialized = false;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        // OnEnable fires even when the GameObject is first activated,
        // so this catches the case where Awake never ran due to starting disabled.
        EnsureInitialized();
    }

    // -----------------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------------

    /// <summary>
    /// Safe initialization that can be called multiple times — only runs once.
    /// Called from both Awake and OnEnable to handle the disabled-at-start case.
    /// </summary>
    private void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        // Initialize the revealed tracker
        int count = (fragmentImages != null) ? fragmentImages.Length : 5;
        revealed = new bool[count];

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
    /// Sets the fragment image to full alpha immediately so it is ready
    /// when the panel fades in after the paper UI closes.
    /// </summary>
    public void RevealFragment(int pieceIndex, int total)
    {
        // Always initialize first in case this is called before OnEnable
        EnsureInitialized();

        totalPieces = total;

        // Guard: index out of range
        if (fragmentImages == null || pieceIndex < 0 || pieceIndex >= fragmentImages.Length)
        {
            Debug.LogWarning($"[FragmentProgressUI] pieceIndex {pieceIndex} is out of range! " +
                             $"fragmentImages length = {(fragmentImages != null ? fragmentImages.Length : 0)}");
            return;
        }

        // Guard: revealed array size mismatch (safety net)
        if (revealed == null || revealed.Length != fragmentImages.Length)
            revealed = new bool[fragmentImages.Length];

        // Guard: already revealed
        if (revealed[pieceIndex]) return;
        revealed[pieceIndex] = true;

        // Show outline and counter the first time
        ShowOutlineAndCounter();

        // Count collected
        int collectedCount = 0;
        foreach (bool r in revealed)
            if (r) collectedCount++;

        UpdateCounter(collectedCount, total);

        // Set to full alpha immediately — the panel's own fade handles the visual reveal
        SetAlpha(fragmentImages[pieceIndex], 1f);
    }

    /// <summary>
    /// Sync the UI with all currently collected pieces.
    /// Used as a catch-up when the panel becomes visible after being hidden.
    /// </summary>
    public void SyncProgress(System.Collections.Generic.List<int> collectedIndices, int total)
    {
        EnsureInitialized();

        totalPieces = total;

        if (collectedIndices == null || collectedIndices.Count == 0) return;

        // Show outline and counter
        ShowOutlineAndCounter();

        // Safety net for array size mismatch
        if (revealed == null || (fragmentImages != null && revealed.Length != fragmentImages.Length))
            revealed = new bool[fragmentImages != null ? fragmentImages.Length : 5];

        foreach (int idx in collectedIndices)
        {
            if (fragmentImages == null || idx < 0 || idx >= fragmentImages.Length) continue;
            if (revealed[idx]) continue;

            revealed[idx] = true;
            SetAlpha(fragmentImages[idx], 1f);
        }

        UpdateCounter(collectedIndices.Count, total);
    }

    /// <summary>
    /// Reset everything to hidden. Call on New Game.
    /// </summary>
    public void ResetAll()
    {
        isInitialized = false;
        hasShownOutline = false;
        EnsureInitialized();
    }

    // -----------------------------------------------------------------------
    // Private Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Makes outline and counter visible the first time a fragment is collected.
    /// Only runs once.
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

    /// <summary>Safely set an Image's alpha.</summary>
    private void SetAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}