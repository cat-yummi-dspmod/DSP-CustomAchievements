using System;

namespace CustomAchievements
{
    /// <summary>
    /// 其他 mod 的入口。先用 GitHub 地址登记自己，再登记成就：
    /// Entry point for other mods. Register yourself with your GitHub URL, then your achievements:
    /// <code>
    /// var mod = AchievementRegistry.RegisterMod("https://github.com/owner/repo", AchText.En("My Mod").ZhCN("我的 mod"));
    /// var a = mod.Register("first-blood", AchText.En("First Blood"), AchText.En("Destroy one Dark Fog unit."));
    /// a.Unlock();
    /// </code>
    /// 成就的完整 ID 是 "github.com/owner/repo#key"，GitHub 地址与 key 都不区分大小写。
    /// The full achievement ID is "github.com/owner/repo#key"; both the URL and the key are case-insensitive.
    /// </summary>
    public static class AchievementRegistry
    {
        /// <param name="githubUrl">仓库地址，例如 https://github.com/owner/repo / Repository URL, e.g. https://github.com/owner/repo</param>
        /// <param name="name">mod 名称，英语必填 / Mod name; English is required</param>
        public static AchievementMod RegisterMod(string githubUrl, AchText name)
        {
            if (!GitHubId.TryNormalize(githubUrl, out string id))
            {
                throw new ArgumentException("Expected a GitHub repository URL like https://github.com/owner/repo, got: " + githubUrl, nameof(githubUrl));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            return new AchievementMod(Store.GetOrAddMod(id, name));
        }
    }

    public sealed class AchievementMod
    {
        readonly ModRecord record;

        internal AchievementMod(ModRecord record)
        {
            this.record = record;
        }

        /// <summary>"github.com/owner/repo"</summary>
        public string Id => record.Id;

        /// <param name="key">本 mod 内唯一，只能用字母、数字、- _ . / Unique within this mod; letters, digits, - _ . only</param>
        /// <param name="target">进度目标；1 表示一次性成就 / Progress target; 1 means a one-shot achievement</param>
        /// <param name="icon">不填则用默认图标 / Default icon when omitted</param>
        /// <param name="allowSandbox">沙盒模式下是否也能推进 / Whether it can progress in sandbox mode</param>
        public Achievement Register(string key, AchText name, AchText description, long target = 1, AchIcon icon = null, bool allowSandbox = false)
        {
            if (!GitHubId.IsKey(key))
            {
                throw new ArgumentException("Achievement key must use letters, digits, '-', '_' or '.', got: " + key, nameof(key));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (target < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(target), "target must be at least 1");
            }
            AchRecord rec = Store.GetOrAddAchievement(record, key.ToLowerInvariant(), name, description ?? AchText.Empty(), target, icon, allowSandbox);
            return new Achievement(rec);
        }
    }

    /// <summary>可在任意线程调用。成就文件尚未读取（主菜单之前）或已解锁时调用无效。
    /// Safe to call from any thread. Calls do nothing before the achievement file is loaded (before the main menu) or once unlocked.</summary>
    public sealed class Achievement
    {
        readonly AchRecord rec;

        internal Achievement(AchRecord rec)
        {
            this.rec = rec;
        }

        /// <summary>"github.com/owner/repo#key"</summary>
        public string Id => rec.FullId;

        public long Target => rec.Target;

        public long Progress
        {
            get
            {
                lock (Store.Sync)
                {
                    return rec.Progress;
                }
            }
        }

        public bool Unlocked
        {
            get
            {
                lock (Store.Sync)
                {
                    return rec.Unlocked;
                }
            }
        }

        /// <summary>现在调用 SetProgress / Unlock 是否会生效。
        /// Whether SetProgress / Unlock would take effect right now.</summary>
        public bool CanProgress
        {
            get
            {
                lock (Store.Sync)
                {
                    return Store.CanProgress(rec);
                }
            }
        }

        public void Unlock() => Store.Unlock(rec);

        /// <summary>达到 Target 时自动解锁。 / Unlocks automatically once Target is reached.</summary>
        public void SetProgress(long value) => Store.SetProgress(rec, value);

        public void AddProgress(long delta) => Store.AddProgress(rec, delta);
    }
}
