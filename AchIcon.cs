using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CustomAchievements
{
    /// <summary>
    /// 成就图标。官方图标是透明底白色线稿，推荐同样风格。PNG 图标会复制进成就目录，mod 卸载后仍能显示。
    /// Achievement icon. Vanilla icons are white line art on a transparent background; the same style is recommended. PNG icons are copied into the achievement folder so they still show after the mod is uninstalled.
    /// </summary>
    public sealed class AchIcon
    {
        internal readonly string Ref;
        internal readonly byte[] Png;

        AchIcon(string reference, byte[] png)
        {
            Ref = reference;
            Png = png;
        }

        /// <summary>物品图标，例如 5111 护卫舰。 / An item's icon, e.g. 5111 Corvette.</summary>
        public static AchIcon Item(int itemId) => new AchIcon("item:" + itemId, null);

        /// <summary>某个官方成就的图标。 / The icon of a vanilla achievement.</summary>
        public static AchIcon VanillaAchievement(int achievementId) => new AchIcon("ach:" + achievementId, null);

        /// <summary>游戏 Resources 里的 Sprite 路径。 / A Sprite path inside the game's Resources.</summary>
        public static AchIcon Resource(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Resource path is required", nameof(path));
            }
            return new AchIcon("res:" + path, null);
        }

        public static AchIcon FromPng(byte[] png)
        {
            if (png == null || png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
            {
                throw new ArgumentException("Not a PNG image", nameof(png));
            }
            using (SHA1 sha = SHA1.Create())
            {
                var hex = new StringBuilder();
                foreach (byte b in sha.ComputeHash(png))
                {
                    hex.Append(b.ToString("x2"));
                }
                return new AchIcon("png:" + hex, (byte[])png.Clone());
            }
        }

        /// <summary>从程序集的嵌入资源读取 PNG，resourceName 为完整资源名。
        /// Reads a PNG embedded in an assembly; resourceName is the full manifest resource name.</summary>
        public static AchIcon FromEmbeddedPng(Assembly assembly, string resourceName)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new ArgumentException("Embedded resource not found: " + resourceName, nameof(resourceName));
                }
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return FromPng(memory.ToArray());
                }
            }
        }
    }
}
