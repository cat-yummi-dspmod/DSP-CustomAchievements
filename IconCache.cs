using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CustomAchievements
{
    /// <summary>把图标引用解析成 Sprite。只在主线程调用。
    /// Resolves icon references to Sprites. Main thread only.</summary>
    internal static class IconCache
    {
        const string DefaultIconResource = "CustomAchievements.res.default_icon.png";

        static readonly Dictionary<string, Sprite> pngSprites = new Dictionary<string, Sprite>();
        static Sprite fallback;

        internal static Sprite Get(string reference, byte[] png)
        {
            Sprite sprite = null;
            try
            {
                sprite = Resolve(reference, png);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("Icon " + reference + " failed: " + e.Message);
            }
            return sprite != null ? sprite : Fallback();
        }

        static Sprite Resolve(string reference, byte[] png)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }
            int colon = reference.IndexOf(':');
            if (colon < 0)
            {
                return null;
            }
            string kind = reference.Substring(0, colon);
            string value = reference.Substring(colon + 1);
            switch (kind)
            {
                case "item":
                    return int.TryParse(value, out int itemId) ? LDB.items.Select(itemId)?.iconSprite : null;
                case "ach":
                    return int.TryParse(value, out int achId) ? LDB.achievements.Select(achId)?.iconSprite : null;
                case "res":
                    return Resources.Load<Sprite>(value);
                case "png":
                    return PngSprite(value, png);
                default:
                    return null;
            }
        }

        static Sprite PngSprite(string hash, byte[] png)
        {
            if (pngSprites.TryGetValue(hash, out Sprite cached))
            {
                return cached;
            }
            byte[] bytes = png ?? Store.ReadIcon(hash);
            Sprite sprite = bytes == null ? null : ToSprite(bytes);
            pngSprites[hash] = sprite;
            return sprite;
        }

        static Sprite Fallback()
        {
            if (fallback == null)
            {
                using (Stream stream = typeof(IconCache).Assembly.GetManifestResourceStream(DefaultIconResource))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    fallback = ToSprite(memory.ToArray());
                }
            }
            return fallback;
        }

        /// <summary>图标显示尺寸远小于原图，没有 mipmap 缩小时会严重锯齿，所以解码后重建一张带 mipmap 的贴图。
        /// Icons are shown far smaller than their source size and alias badly without mipmaps, so the decoded image is copied into a mipmapped texture.</summary>
        static Sprite ToSprite(byte[] png)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(png))
                {
                    return null;
                }
                var texture = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4,
                };
                texture.SetPixels32(BleedTransparent(decoded.GetPixels32()));
                texture.Apply(true, true);
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            finally
            {
                UnityEngine.Object.Destroy(decoded);
            }
        }

        /// <summary>PNG 编码器常把全透明像素存成黑色，缩放和 mipmap 会把黑色混进线条边缘。改成图标的平均颜色。
        /// PNG encoders often store fully transparent pixels as black, which scaling and mipmaps blend into line edges. They get the icon's average colour instead.</summary>
        static Color32[] BleedTransparent(Color32[] pixels)
        {
            long r = 0, g = 0, b = 0, weight = 0;
            foreach (Color32 p in pixels)
            {
                r += p.r * p.a;
                g += p.g * p.a;
                b += p.b * p.a;
                weight += p.a;
            }
            if (weight == 0)
            {
                return pixels;
            }
            var fill = new Color32((byte)(r / weight), (byte)(g / weight), (byte)(b / weight), 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0)
                {
                    pixels[i] = fill;
                }
            }
            return pixels;
        }
    }
}
