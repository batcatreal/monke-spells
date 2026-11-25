using UnityEngine;

namespace GTAGWizard.Visuals
{
    internal static class ColorUtil
    {
        /// <summary>Parses "#RRGGBB" / "#RRGGBBAA" / a Unity colour name; falls back to fallbackHex, then white.</summary>
        public static Color Parse(string value, string fallbackHex)
        {
            Color parsed;
            if (!string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value.Trim(), out parsed)) return parsed;
            if (!string.IsNullOrEmpty(fallbackHex) && ColorUtility.TryParseHtmlString(fallbackHex, out parsed)) return parsed;
            return Color.white;
        }

        public static Color WithAlpha(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }

        public static Color Brighten(Color c, float amount)
        {
            return Color.Lerp(c, Color.white, Mathf.Clamp01(amount));
        }
    }

    internal static class Ease
    {
        public static float OutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); float inv = 1f - t; return 1f - inv * inv * inv; }
        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
    }

    internal static class GradientUtil
    {
        /// <summary>
        /// Builds a gradient from parallel arrays. colors[i].rgb is the colour at times[i] and
        /// colors[i].a is the alpha at times[i]. Times must ascend from 0 to 1; max 8 keys.
        /// </summary>
        public static Gradient Make(Color[] colors, float[] times)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(new Color(colors[i].r, colors[i].g, colors[i].b, 1f), times[i]);
                alphaKeys[i] = new GradientAlphaKey(colors[i].a, times[i]);
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }
    }
}
