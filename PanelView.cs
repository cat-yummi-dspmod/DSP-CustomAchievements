using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CustomAchievements
{
    /// <summary>
    /// 在官方成就面板左上角加一个切换按钮。MOD 模式下隐藏官方的三个分区，换成同样布局的 MOD 分区，卡片用官方预制体。
    /// 卡片的内容由这里直接填写，不调用官方 Refresh（它按 ID 查官方数据表，MOD 成就没有 ID）。
    /// Adds a toggle at the top-left of the vanilla achievement panel. In mod mode the three vanilla sections are hidden and replaced by mod sections with the same layout, using the vanilla card prefab.
    /// Cards are filled here directly instead of through the vanilla Refresh, which looks achievements up by ID in the vanilla table.
    /// </summary>
    internal static class PanelView
    {
        const int Columns = 4;
        const int EntryWidth = 270;
        const int EntryHeight = 310;
        const int EntryLeft = 8;
        const int EntryTop = 41;
        const int ContainerBottom = 8;
        const int ContainerGap = 16;
        const float RebuildInterval = 0.5f;

        struct Row
        {
            public string FullId;
            public string Name;
            public string Description;
            public string Source;
            public long Target;
            public long Progress;
            public bool Unlocked;
            public long UnlockedUtcTicks;
            public string IconRef;
            public byte[] IconPng;
        }

        static UIAchievementPanel panel;
        static RectTransform unlockedBox;
        static RectTransform progressBox;
        static RectTransform lockedBox;
        static Text toggleText;
        static readonly List<UIAchievementEntry> entries = new List<UIAchievementEntry>();
        static readonly Dictionary<string, UIAchievementEntry> entriesById = new Dictionary<string, UIAchievementEntry>();

        static bool showMods;
        static string pendingReveal;
        static int builtVersion = -1;
        static int builtLcid = -1;
        static float lastRebuildTime = -100f;
        static float contentHeight;
        static int total;
        static int unlockedCount;

        internal static void LateUpdate()
        {
            UIAchievementPanel current = FindPanel();
            if (current == null)
            {
                return;
            }
            if (current != panel || unlockedBox == null || toggleText == null)
            {
                Attach(current);
            }
            if (!panel.gameObject.activeInHierarchy)
            {
                return;
            }
            if (pendingReveal != null)
            {
                RevealNow();
            }
            else if (showMods && panel.highlightingEntries.Count > 0)
            {
                SetMode(false);
            }
            if (showMods)
            {
                bool stale = Store.Version != builtVersion || CatAPI.Lang.Lcid != builtLcid;
                if (stale && Time.unscaledTime - lastRebuildTime >= RebuildInterval)
                {
                    Rebuild();
                }
                Enforce();
            }
        }

        /// <summary>与官方 HighlightAchievement 相同的打开方式，下一次 LateUpdate 时切到 MOD 页并滚动到该成就。
        /// Opens the panel the same way as the vanilla HighlightAchievement; the next LateUpdate switches to the mod page and scrolls to the achievement.</summary>
        internal static void Reveal(string fullId)
        {
            UIGame ui = UIRoot.instance.uiGame;
            if (!ui.statWindow.active)
            {
                ui.ShutAllFunctionWindow();
                ui.ShutPlayerInventory();
                ui.OpenProductionWindow();
            }
            ui.statWindow.OnTabButtonClick(5);
            pendingReveal = fullId;
        }

        static void RevealNow()
        {
            string fullId = pendingReveal;
            pendingReveal = null;
            if (!showMods)
            {
                SetMode(true);
            }
            else if (Store.Version != builtVersion)
            {
                Rebuild();
            }
            if (fullId != null && entriesById.TryGetValue(fullId, out UIAchievementEntry card) && card != null)
            {
                var box = (RectTransform)card.selfRect.parent;
                float y = -(box.anchoredPosition.y + card.selfRect.anchoredPosition.y) - 30f;
                panel.scrollViewConent.anchoredPosition = new Vector2(panel.scrollViewConent.anchoredPosition.x, Mathf.Max(0f, y));
            }
        }

        static UIAchievementPanel FindPanel()
        {
            UIRoot root = UIRoot.instance;
            if (root == null || root.uiGame == null || root.uiGame.statWindow == null)
            {
                return null;
            }
            return root.uiGame.statWindow.achievementPanelUI;
        }

        static void Attach(UIAchievementPanel target)
        {
            panel = target;
            entries.Clear();
            entriesById.Clear();
            showMods = false;
            builtVersion = -1;
            unlockedBox = CloneContainer(panel.unlockedContainerRect);
            progressBox = CloneContainer(panel.inProgressContainerRect);
            lockedBox = CloneContainer(panel.lockedContainerRect);
            CreateToggle();
            UpdateToggleText();
        }

        /// <summary>复制官方分区（保留"已达成"等标题），去掉复制过来的官方卡片。
        /// Copies a vanilla section (keeping its "Unlocked"-style header) and drops the vanilla cards that came along.</summary>
        static RectTransform CloneContainer(RectTransform source)
        {
            GameObject copy = Object.Instantiate(source.gameObject, source.parent, false);
            copy.name = source.name + "_Mod";
            foreach (UIAchievementEntry card in copy.GetComponentsInChildren<UIAchievementEntry>(true))
            {
                card.gameObject.SetActive(false);
                Object.Destroy(card.gameObject);
            }
            copy.SetActive(false);
            return (RectTransform)copy.transform;
        }

        static void CreateToggle()
        {
            var panelRect = (RectTransform)panel.transform;
            GameObject go = Object.Instantiate(panel.enableAchBtn.gameObject, panelRect, false);
            go.name = "ModAchievementToggle";
            foreach (Localizer localizer in go.GetComponentsInChildren<Localizer>(true))
            {
                Object.Destroy(localizer);
            }
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SetMode(!showMods));
            toggleText = go.GetComponentInChildren<Text>(true);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(160f, 32f);

            // 放在"总成就进度"标签左侧；左上角那行提示文字会和按钮重叠
            // Placed left of the "overall progress" label; the tip text at the top-left would overlap the button
            RectTransform bar = panel.overallProgressImg.rectTransform;
            Vector3 barCenter = panelRect.InverseTransformPoint(bar.TransformPoint(bar.rect.center));
            float barLeft = panelRect.InverseTransformPoint(bar.TransformPoint(new Vector3(bar.rect.xMin, 0f, 0f))).x;
            float right = ProgressLabelLeft(panelRect, barCenter.y, barLeft) ?? barLeft - 110f;
            rect.localPosition = new Vector3(right - 16f - rect.sizeDelta.x / 2f, barCenter.y, 0f);
            go.SetActive(true);
        }

        /// <summary>官方面板没有引用这个标签，按位置找：与进度条同一行、在其左边最近的文字。返回文字可见部分的左缘。
        /// The vanilla panel has no field for this label, so it is found by position: the nearest text left of the bar on the same row. Returns the left edge of the visible text.</summary>
        static float? ProgressLabelLeft(RectTransform panelRect, float rowY, float barLeft)
        {
            float? best = null;
            float bestRight = float.MinValue;
            foreach (Text text in panelRect.GetComponentsInChildren<Text>(true))
            {
                if (text == toggleText || text == panel.topTipText || text == panel.overallProgressText || text == panel.detailText
                    || string.IsNullOrEmpty(text.text) || text.GetComponentInParent<UIAchievementEntry>() != null
                    || text.transform.IsChildOf(panel.enableDetectionTip.transform))
                {
                    continue;
                }
                RectTransform r = text.rectTransform;
                Vector3 min = panelRect.InverseTransformPoint(r.TransformPoint(new Vector3(r.rect.xMin, r.rect.yMin, 0f)));
                Vector3 max = panelRect.InverseTransformPoint(r.TransformPoint(new Vector3(r.rect.xMax, r.rect.yMax, 0f)));
                if (rowY < min.y - 4f || rowY > max.y + 4f || (min.x + max.x) / 2f >= barLeft || max.x <= bestRight)
                {
                    continue;
                }
                float scale = (max.x - min.x) / Mathf.Max(r.rect.width, 1f);
                float visible = Mathf.Min(text.preferredWidth * scale, max.x - min.x);
                switch (text.alignment)
                {
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight:
                        best = max.x - visible;
                        break;
                    case TextAnchor.UpperCenter:
                    case TextAnchor.MiddleCenter:
                    case TextAnchor.LowerCenter:
                        best = (min.x + max.x - visible) / 2f;
                        break;
                    default:
                        best = min.x;
                        break;
                }
                bestRight = max.x;
            }
            return best;
        }

        static void UpdateToggleText()
        {
            if (toggleText != null)
            {
                toggleText.text = showMods
                    ? CatAPI.Lang.En("Official Achievements").ZhCN("官方成就")
                    : CatAPI.Lang.En("Mod Achievements").ZhCN("MOD成就");
            }
        }

        static void SetMode(bool mods)
        {
            showMods = mods;
            Vector2 scroll = panel.scrollViewConent.anchoredPosition;
            panel.scrollViewConent.anchoredPosition = new Vector2(scroll.x, 0f);
            if (mods)
            {
                Rebuild();
                Enforce();
            }
            else
            {
                unlockedBox.gameObject.SetActive(false);
                progressBox.gameObject.SetActive(false);
                lockedBox.gameObject.SetActive(false);
                panel.SetLayout();
                panel.SetOverallProgress();
                AccessTools.Method(typeof(UIAchievementPanel), "_OnOpen").Invoke(panel, null);
            }
            UpdateToggleText();
        }

        /// <summary>官方代码随时可能重新显示自己的分区（例如官方成就解锁时），每帧盖回去。
        /// Vanilla code may show its own sections again at any time (e.g. when a vanilla achievement unlocks), so this re-applies every frame.</summary>
        static void Enforce()
        {
            panel.unlockedContainerRect.gameObject.SetActive(false);
            panel.inProgressContainerRect.gameObject.SetActive(false);
            panel.lockedContainerRect.gameObject.SetActive(false);
            panel.topTipText.gameObject.SetActive(false);
            panel.enableDetectionTip.SetActive(false);
            panel.scrollViewConent.sizeDelta = new Vector2(0f, contentHeight);

            float ratio = total == 0 ? 0f : (float)unlockedCount / total;
            panel.overallProgressImg.fillAmount = ratio;
            panel.progressPointRect.anchoredPosition = new Vector2(panel.overallProgressImg.rectTransform.rect.width * ratio, 0f);
            panel.overallProgressText.text = unlockedCount + " / " + total;
        }

        static void Rebuild()
        {
            lastRebuildTime = Time.unscaledTime;
            builtVersion = Store.Version;
            builtLcid = CatAPI.Lang.Lcid;

            foreach (UIAchievementEntry card in entries)
            {
                if (card != null)
                {
                    card._Destroy();
                }
            }
            entries.Clear();
            entriesById.Clear();

            var unlocked = new List<Row>();
            var inProgress = new List<Row>();
            var locked = new List<Row>();
            foreach (ModRecord mod in Store.SnapshotMods())
            {
                lock (Store.Sync)
                {
                    string source = CatAPI.Lang.En("From: ").ZhCN("源自：") + mod.Name;
                    if (!mod.Installed)
                    {
                        source += CatAPI.Lang.En(" (not installed)").ZhCN("（未安装）");
                    }
                    foreach (AchRecord a in mod.Achievements)
                    {
                        var row = new Row
                        {
                            FullId = a.FullId,
                            Name = a.Name.ToString(),
                            Description = a.Description.ToString(),
                            Source = source,
                            Target = a.Target,
                            Progress = a.Progress,
                            Unlocked = a.Unlocked,
                            UnlockedUtcTicks = a.UnlockedUtcTicks,
                            IconRef = a.IconRef,
                            IconPng = a.IconPng,
                        };
                        if (row.Unlocked)
                        {
                            unlocked.Add(row);
                        }
                        else if (row.Target > 1 && row.Progress > 0)
                        {
                            inProgress.Add(row);
                        }
                        else
                        {
                            locked.Add(row);
                        }
                    }
                }
            }
            unlocked.Sort((a, b) => a.UnlockedUtcTicks.CompareTo(b.UnlockedUtcTicks));
            inProgress.Sort((a, b) => ((double)b.Progress / b.Target).CompareTo((double)a.Progress / a.Target));

            total = unlocked.Count + inProgress.Count + locked.Count;
            unlockedCount = unlocked.Count;

            Fill(unlockedBox, unlocked);
            Fill(progressBox, inProgress);
            Fill(lockedBox, locked);
            Layout(unlocked.Count, inProgress.Count, locked.Count);
        }

        static void Fill(RectTransform box, List<Row> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                UIAchievementEntry card = Object.Instantiate(panel.entryPrefab, box);
                card._Create();
                card._Init(null);
                card._Open();
                card.selfRect.anchoredPosition = new Vector2(i % Columns * EntryWidth + EntryLeft, -(i / Columns * EntryHeight + EntryTop));
                Show(card, rows[i]);
                entries.Add(card);
                entriesById[rows[i].FullId] = card;
            }
        }

        /// <summary>位置、透明度与官方 UIAchievementEntry.Refresh 的三种状态一致。
        /// Positions and alpha match the three states of the vanilla UIAchievementEntry.Refresh.</summary>
        static void Show(UIAchievementEntry card, Row row)
        {
            card.icon.sprite = IconCache.Get(row.IconRef, row.IconPng);
            card.titleText.text = row.Name;
            card.descText.supportRichText = true;
            card.descText.text = row.Description + "\n<color=#ffffff80>" + row.Source + "</color>";
            card.propertyTipRect.gameObject.SetActive(false);

            float alpha;
            if (row.Unlocked)
            {
                card.iconRect.anchoredPosition = new Vector2(0f, -35f);
                card.titleRect.anchoredPosition = new Vector2(0f, -130f);
                card.descRect.anchoredPosition = new Vector2(0f, -165f);
                card.progressRect.gameObject.SetActive(false);
                card.lockedRect.gameObject.SetActive(false);
                card.unlockedBelt.gameObject.SetActive(true);
                card.unlockedBeltText.gameObject.SetActive(true);
                alpha = 1f;
            }
            else if (row.Target > 1 && row.Progress > 0)
            {
                card.iconRect.anchoredPosition = new Vector2(0f, -28f);
                card.titleRect.anchoredPosition = new Vector2(0f, -108f);
                card.descRect.anchoredPosition = new Vector2(0f, -130f);
                card.progressRect.gameObject.SetActive(true);
                card.lockedRect.gameObject.SetActive(false);
                card.unlockedBelt.gameObject.SetActive(false);
                card.unlockedBeltText.gameObject.SetActive(false);
                card.progress = (double)row.Progress / row.Target;
                card.progressBar.fillAmount = (float)card.progress;
                card.progressPointRect.anchoredPosition = new Vector2(card.progressBar.rectTransform.rect.width * (float)card.progress, 0f);
                card.progressText.text = row.Progress + " / " + row.Target;
                alpha = -1f;
            }
            else
            {
                card.iconRect.anchoredPosition = new Vector2(0f, -35f);
                card.titleRect.anchoredPosition = new Vector2(0f, -116f);
                card.descRect.anchoredPosition = new Vector2(0f, -140f);
                card.progressRect.gameObject.SetActive(false);
                card.lockedRect.gameObject.SetActive(true);
                card.unlockedBelt.gameObject.SetActive(false);
                card.unlockedBeltText.gameObject.SetActive(false);
                alpha = -1f;
            }
            float iconAlpha = alpha > 0f ? 1f : 3f / 85f;
            float textAlpha = alpha > 0f ? 1f : 0.2509804f;
            card.icon.color = WithAlpha(card.icon.color, iconAlpha);
            card.titleText.color = WithAlpha(card.titleText.color, textAlpha);
            card.descText.color = WithAlpha(card.descText.color, textAlpha);
        }

        static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        /// <summary>与官方 SetLayout 相同的分区排布。 / Same section layout as the vanilla SetLayout.</summary>
        static void Layout(int unlocked, int inProgress, int locked)
        {
            float y = 0f;
            float height = 0f;
            y = Place(unlockedBox, unlocked, y, ref height);
            y = Place(progressBox, inProgress, y, ref height);
            Place(lockedBox, locked, y, ref height);
            contentHeight = height;
        }

        static float Place(RectTransform box, int count, float y, ref float height)
        {
            if (count == 0)
            {
                box.sizeDelta = Vector2.zero;
                box.gameObject.SetActive(false);
                return y;
            }
            if (height > 0f)
            {
                y -= ContainerGap;
                height += ContainerGap;
            }
            float h = Mathf.CeilToInt((float)count / Columns) * EntryHeight + EntryTop + ContainerBottom;
            box.anchoredPosition = new Vector2(0f, y);
            box.sizeDelta = new Vector2(0f, h);
            box.gameObject.SetActive(true);
            height += h;
            return y - h;
        }
    }
}
