using System;
using System.Collections.Generic;
using System.IO;

namespace CustomAchievements
{
    /// <summary>
    /// 英语必填，其他语言可选。取文本的规则与 CatAPI.Lang 相同：语言完全对上 → 同一主语言 → 英语。
    /// English is required, other languages are optional. Lookup follows CatAPI.Lang: exact language, then same primary language, then English.
    /// AchText.En("Well Fought").ZhCN("漂亮的一仗")
    /// </summary>
    public sealed class AchText
    {
        readonly string en;
        readonly Dictionary<int, string> text = new Dictionary<int, string>();

        AchText(string en)
        {
            this.en = en ?? "";
        }

        public static AchText En(string en)
        {
            if (string.IsNullOrEmpty(en))
            {
                throw new ArgumentException("English text is required / 必须提供英文", nameof(en));
            }
            return new AchText(en);
        }

        internal static AchText Empty() => new AchText("");

        public static implicit operator AchText(string en)
        {
            return En(en);
        }

        public AchText ZhCN(string value) => Set(CatAPI.Lang.ZhCN, value);
        public AchText FrFR(string value) => Set(CatAPI.Lang.FrFR, value);
        public AchText DeDE(string value) => Set(CatAPI.Lang.DeDE, value);
        public AchText EsES(string value) => Set(CatAPI.Lang.EsES, value);
        public AchText JaJP(string value) => Set(CatAPI.Lang.JaJP, value);
        public AchText KoKR(string value) => Set(CatAPI.Lang.KoKR, value);

        public AchText Set(int lcid, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                text[lcid] = value;
            }
            return this;
        }

        public string English => en;

        public override string ToString()
        {
            int lcid = CatAPI.Lang.Lcid;
            if (text.TryGetValue(lcid, out string exact))
            {
                return exact;
            }
            int primary = lcid & 0x3FF;
            foreach (KeyValuePair<int, string> pair in text)
            {
                if ((pair.Key & 0x3FF) == primary)
                {
                    return pair.Value;
                }
            }
            return en;
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(en);
            w.Write(text.Count);
            foreach (KeyValuePair<int, string> pair in text)
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
            }
        }

        internal static AchText Read(BinaryReader r)
        {
            var result = new AchText(r.ReadString());
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int lcid = r.ReadInt32();
                result.Set(lcid, r.ReadString());
            }
            return result;
        }
    }
}
