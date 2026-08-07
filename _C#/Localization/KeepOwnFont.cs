using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Marks a label that <see cref="LanguageFonts"/> should leave alone — an arrow, a score, an
    /// icon glyph, anything whose typeface is a choice rather than a language.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeepOwnFont : MonoBehaviour
    {
    }
}
