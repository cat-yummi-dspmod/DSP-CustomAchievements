using System;
using System.Collections.Generic;
using System.IO;

namespace CustomAchievements
{
    internal sealed class ModRecord
    {
        internal string Id;
        internal AchText Name;
        internal bool Installed;
        internal readonly List<AchRecord> Achievements = new List<AchRecord>();

        internal AchRecord Find(string key)
        {
            foreach (AchRecord a in Achievements)
            {
                if (a.Key == key)
                {
                    return a;
                }
            }
            return null;
        }
    }

    internal sealed class AchRecord
    {
        internal ModRecord Mod;
        internal string Key;
        internal AchText Name;
        internal AchText Description;
        internal long Target = 1;
        internal long Progress;
        internal bool Unlocked;
        internal long UnlockedUtcTicks;
        internal bool Registered;
        internal bool AllowSandbox;
        internal string IconRef;
        internal byte[] IconPng;

        internal string FullId => Mod.Id + "#" + Key;
    }

    /// <summary>
    /// 所有成就记录。只写自己的文件，不碰原版 Achievement 目录里的加密文件。
    /// All achievement records. Writes only its own file and never touches the vanilla encrypted achievement file.
    /// 注册过的 mod 卸载后，它的记录仍留在文件里，只是不再推进。
    /// Records of a mod that was uninstalled stay in the file; they just stop progressing.
    /// </summary>
    internal static class Store
    {
        const int Magic = 0x68634143;
        const int FormatVersion = 2;
        const double SaveDelaySeconds = 2;

        internal static readonly object Sync = new object();
        static readonly List<ModRecord> mods = new List<ModRecord>();
        static readonly Queue<AchRecord> justUnlocked = new Queue<AchRecord>();

        static string path;
        static bool dirty;
        static DateTime lastChangeUtc;

        internal static bool Loaded { get; private set; }

        /// <summary>任何记录变化都会加一，界面据此判断是否重绘。 / Increments on every change; the UI redraws when it moves.</summary>
        internal static int Version { get; private set; }

        internal static ModRecord GetOrAddMod(string id, AchText name)
        {
            lock (Sync)
            {
                ModRecord mod = FindMod(id);
                if (mod == null)
                {
                    mod = new ModRecord { Id = id };
                    mods.Add(mod);
                }
                mod.Name = name;
                mod.Installed = true;
                MarkDirty();
                return mod;
            }
        }

        internal static AchRecord GetOrAddAchievement(ModRecord mod, string key, AchText name, AchText description, long target, AchIcon icon, bool allowSandbox)
        {
            lock (Sync)
            {
                AchRecord rec = mod.Find(key);
                if (rec == null)
                {
                    rec = new AchRecord { Mod = mod, Key = key };
                    mod.Achievements.Add(rec);
                }
                rec.Name = name;
                rec.Description = description;
                rec.Target = target;
                rec.AllowSandbox = allowSandbox;
                rec.IconRef = icon?.Ref;
                rec.IconPng = icon?.Png;
                rec.Registered = true;
                MarkDirty();
                Plugin.Log?.LogInfo("Registered achievement " + rec.FullId);
                return rec;
            }
        }

        internal static bool CanProgress(AchRecord rec)
        {
            if (!Loaded || rec.Unlocked || !rec.Registered)
            {
                return false;
            }
            GameData data = GameMain.data;
            return rec.AllowSandbox || data == null || !data.gameDesc.isSandboxMode;
        }

        internal static void SetProgress(AchRecord rec, long value)
        {
            lock (Sync)
            {
                if (!CanProgress(rec))
                {
                    return;
                }
                if (value < 0)
                {
                    value = 0;
                }
                if (value >= rec.Target)
                {
                    UnlockLocked(rec);
                    return;
                }
                if (value != rec.Progress)
                {
                    rec.Progress = value;
                    MarkDirty();
                }
            }
        }

        internal static void AddProgress(AchRecord rec, long delta)
        {
            lock (Sync)
            {
                SetProgress(rec, rec.Progress + delta);
            }
        }

        internal static void Unlock(AchRecord rec)
        {
            lock (Sync)
            {
                if (CanProgress(rec))
                {
                    UnlockLocked(rec);
                }
            }
        }

        static void UnlockLocked(AchRecord rec)
        {
            rec.Progress = rec.Target;
            rec.Unlocked = true;
            rec.UnlockedUtcTicks = DateTime.UtcNow.Ticks;
            justUnlocked.Enqueue(rec);
            MarkDirty();
            Plugin.Log?.LogInfo("Achievement unlocked: " + rec.FullId);
        }

        internal static List<AchRecord> TakeJustUnlocked()
        {
            lock (Sync)
            {
                var list = new List<AchRecord>(justUnlocked);
                justUnlocked.Clear();
                return list;
            }
        }

        /// <summary>给界面用的快照：已安装的 mod 在前。
        /// Snapshot for the UI: installed mods first.</summary>
        internal static List<ModRecord> SnapshotMods()
        {
            lock (Sync)
            {
                var list = new List<ModRecord>(mods);
                list.Sort((a, b) =>
                {
                    if (a.Installed != b.Installed)
                    {
                        return a.Installed ? -1 : 1;
                    }
                    return string.Compare(a.Name.ToString(), b.Name.ToString(), StringComparison.CurrentCultureIgnoreCase);
                });
                return list;
            }
        }

        static ModRecord FindMod(string id)
        {
            foreach (ModRecord m in mods)
            {
                if (m.Id == id)
                {
                    return m;
                }
            }
            return null;
        }

        static void MarkDirty()
        {
            dirty = true;
            lastChangeUtc = DateTime.UtcNow;
            Version++;
        }

        static string IconDir => Path.Combine(Path.GetDirectoryName(path), "icons");

        /// <summary>hash 只来自 AchIcon 计算的 SHA1 十六进制串或本文件读出的值，先校验再拼路径。
        /// The hash is the SHA1 hex string from AchIcon or read from our own file; it is validated before building a path.</summary>
        internal static byte[] ReadIcon(string hash)
        {
            if (!Loaded || !IsHash(hash))
            {
                return null;
            }
            string file = Path.Combine(IconDir, hash + ".png");
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }

        static bool IsHash(string s)
        {
            if (s == null || s.Length != 40)
            {
                return false;
            }
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }
            return true;
        }

        static void WriteIcons()
        {
            foreach (ModRecord mod in mods)
            {
                foreach (AchRecord a in mod.Achievements)
                {
                    if (a.IconPng == null || a.IconRef == null || !a.IconRef.StartsWith("png:", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string hash = a.IconRef.Substring(4);
                    if (!IsHash(hash))
                    {
                        continue;
                    }
                    string file = Path.Combine(IconDir, hash + ".png");
                    if (!File.Exists(file))
                    {
                        Directory.CreateDirectory(IconDir);
                        File.WriteAllBytes(file, a.IconPng);
                    }
                }
            }
        }

        // ---------------- 文件 / File ----------------

        static string ResolvePath()
        {
            string vanilla = GameConfig.achievementFolder.TrimEnd('/', '\\');
            string root = Path.GetDirectoryName(vanilla);
            string account = "local";
            try
            {
                if (!AccountData.me.isNull)
                {
                    account = AccountData.me.platform + "_" + AccountData.me.userId;
                }
            }
            catch
            {
                // 拿不到账号时用 local。/ Fall back to "local" when the account is unavailable.
            }
            return Path.Combine(Path.Combine(root, "CustomAchievements"), account + ".dat");
        }

        internal static void Load()
        {
            lock (Sync)
            {
                if (Loaded)
                {
                    return;
                }
                path = ResolvePath();
                if (File.Exists(path))
                {
                    try
                    {
                        using (var r = new BinaryReader(File.OpenRead(path)))
                        {
                            Read(r);
                        }
                    }
                    catch (Exception e)
                    {
                        string backup = path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        Plugin.Log?.LogError("Achievement file is unreadable, kept a copy at " + backup + "\n" + e);
                        try
                        {
                            File.Copy(path, backup, true);
                        }
                        catch (Exception copyError)
                        {
                            Plugin.Log?.LogError("Backup failed: " + copyError.Message);
                        }
                    }
                }
                Loaded = true;
                dirty = true;
                Plugin.Log?.LogInfo("Achievement file: " + path);
                foreach (ModRecord mod in mods)
                {
                    int unlocked = 0;
                    foreach (AchRecord a in mod.Achievements)
                    {
                        if (a.Unlocked)
                        {
                            unlocked++;
                        }
                    }
                    string state = mod.Installed ? "installed" : "not installed, frozen";
                    Plugin.Log?.LogInfo($"  {mod.Id} ({mod.Name.English}, {state}): {unlocked}/{mod.Achievements.Count} unlocked");
                }
            }
        }

        internal static void SaveIfDue()
        {
            if (dirty && Loaded && (DateTime.UtcNow - lastChangeUtc).TotalSeconds >= SaveDelaySeconds)
            {
                Save();
            }
        }

        internal static void Save()
        {
            lock (Sync)
            {
                if (!Loaded || !dirty)
                {
                    return;
                }
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    WriteIcons();
                    string temp = path + ".tmp";
                    using (var w = new BinaryWriter(File.Create(temp)))
                    {
                        Write(w);
                    }
                    if (File.Exists(path))
                    {
                        File.Copy(path, path + ".bak", true);
                        File.Delete(path);
                    }
                    File.Move(temp, path);
                    dirty = false;
                    Plugin.Log?.LogDebug("Achievement file saved");
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError("Saving achievements failed: " + e);
                    lastChangeUtc = DateTime.UtcNow;
                }
            }
        }

        static void Write(BinaryWriter w)
        {
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(mods.Count);
            foreach (ModRecord mod in mods)
            {
                w.Write(mod.Id);
                mod.Name.Write(w);
                w.Write(mod.Achievements.Count);
                foreach (AchRecord a in mod.Achievements)
                {
                    w.Write(a.Key);
                    a.Name.Write(w);
                    a.Description.Write(w);
                    w.Write(a.Target);
                    w.Write(a.Progress);
                    w.Write(a.Unlocked);
                    w.Write(a.UnlockedUtcTicks);
                    w.Write(a.IconRef ?? "");
                }
            }
        }

        /// <summary>已注册的条目只取进度；没注册的（mod 已卸载）整条留作只读记录。
        /// Registered entries take only the stored progress; unregistered ones (mod uninstalled) are kept whole as read-only records.</summary>
        static void Read(BinaryReader r)
        {
            if (r.ReadInt32() != Magic)
            {
                throw new InvalidDataException("not a custom achievement file");
            }
            int version = r.ReadInt32();
            if (version > FormatVersion)
            {
                throw new InvalidDataException("file version " + version + " is newer than this mod");
            }
            int modCount = r.ReadInt32();
            for (int i = 0; i < modCount; i++)
            {
                string id = r.ReadString();
                AchText name = AchText.Read(r);
                ModRecord mod = FindMod(id);
                if (mod == null)
                {
                    mod = new ModRecord { Id = id, Name = name, Installed = false };
                    mods.Add(mod);
                }
                int achCount = r.ReadInt32();
                for (int j = 0; j < achCount; j++)
                {
                    string key = r.ReadString();
                    AchText achName = AchText.Read(r);
                    AchText desc = AchText.Read(r);
                    long target = r.ReadInt64();
                    long progress = r.ReadInt64();
                    bool unlocked = r.ReadBoolean();
                    long unlockedUtc = r.ReadInt64();
                    string iconRef = version >= 2 ? r.ReadString() : "";

                    AchRecord rec = mod.Find(key);
                    if (rec == null)
                    {
                        rec = new AchRecord { Mod = mod, Key = key, Name = achName, Description = desc, Target = target, IconRef = iconRef };
                        mod.Achievements.Add(rec);
                    }
                    rec.Progress = progress;
                    rec.Unlocked = unlocked;
                    rec.UnlockedUtcTicks = unlockedUtc;
                    if (!rec.Unlocked && rec.Progress >= rec.Target)
                    {
                        // 目标值调低后，已记录的最好成绩可能已达标 / A lowered target may already be met by the recorded best
                        if (rec.Registered)
                        {
                            UnlockLocked(rec);
                        }
                        else
                        {
                            rec.Progress = rec.Target;
                        }
                    }
                }
            }
        }
    }
}
