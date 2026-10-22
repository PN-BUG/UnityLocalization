using System;
using TMPro;
using UnityEngine;

/// <summary>Optional formatting after translation; input adapters remain outside this package.</summary>
public static class LocalizedTextPresentation
{
    /// <summary>Arguments are TMP target (null for legacy Text), translated text, and optional color.</summary>
    public static Func<TMP_Text, string, string, string> Formatter { get; set; }

    public static event Action Changed;

    public static string Format(TMP_Text target, string text, string color = null)
    {
        return Formatter != null ? Formatter(target, text, color) : text;
    }

    public static void NotifyChanged() => Changed?.Invoke();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Formatter = null;
        Changed = null;
    }
}
