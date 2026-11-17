using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vertical power gauge: green (0) -> orange (50) -> red (100), with a moving marker line.
/// </summary>
public class PowerGauge : MonoBehaviour
{
    [Tooltip("Everything that is shown or hidden together.")]
    public GameObject visuals;

    [Tooltip("The bar that gets the colour gradient.")]
    public RawImage fill;

    [Tooltip("The marker line. Its anchors are moved along the bar (child of the fill).")]
    public RectTransform marker;

    [Tooltip("Optional number showing the current value.")]
    public Text valueText;

    public Gradient colors = DefaultGradient();

    void Awake()
    {
        if (fill == null)
            return;
        const int height = 128;
        var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int y = 0; y < height; y++)
            tex.SetPixel(0, y, colors.Evaluate(y / (height - 1f)));
        tex.Apply();
        fill.texture = tex;
        fill.color = Color.white;
    }

    /// <summary>Value 0..1 (0 at the bottom, 1 at the top).</summary>
    public void SetValue(float value)
    {
        value = Mathf.Clamp01(value);
        if (marker != null)
        {
            marker.anchorMin = new Vector2(marker.anchorMin.x, value);
            marker.anchorMax = new Vector2(marker.anchorMax.x, value);
            marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, 0f);
        }
        if (valueText != null)
            valueText.text = Mathf.RoundToInt(value * 100f).ToString();
    }

    public void SetVisible(bool visible)
    {
        if (visuals != null)
            visuals.SetActive(visible);
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.15f, 0.75f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.85f, 0.1f, 0.1f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
