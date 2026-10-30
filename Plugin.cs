using System;
using BepInEx;
using BepInEx.Logging;

namespace CustomAchievements
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(CatAPI.HarnessPlugin.PluginGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.yummi.dsp.customachievements";
        public const string PluginName = "Custom Achievements";
        public const string PluginVersion = "0.2.0";
        public const string GitHubUrl = "https://github.com/cat-yummi-dspmod/DSP-CustomAchievements";

        internal static ManualLogSource Log;

        bool panelFailed;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Update()
        {
            if (!Store.Loaded && VFPreload.done)
            {
                Store.Load();
            }
            Store.SaveIfDue();
            ShowUnlocks();
        }

        private void LateUpdate()
        {
            if (panelFailed || !Store.Loaded)
            {
                return;
            }
            try
            {
                PanelView.LateUpdate();
            }
            catch (Exception e)
            {
                panelFailed = true;
                Log.LogError("Achievement panel integration disabled: " + e);
                CatAPI.Harness.Report(CatAPI.NoticeLevel.Error, PluginName,
                    "自定义成就无法接入成就面板，成就仍会正常记录。详情见日志。",
                    "Custom achievements could not be added to the achievement panel; progress is still recorded. See the log.");
            }
        }

        private void OnApplicationQuit()
        {
            Store.Save();
        }

        private void OnDestroy()
        {
            Store.Save();
        }

        static void ShowUnlocks()
        {
            foreach (AchRecord rec in Store.TakeJustUnlocked())
            {
                try
                {
                    if (!UnlockPopup.Show(rec))
                    {
                        UIRealtimeTip.Popup(CatAPI.Lang.En("Achievement unlocked: ").ZhCN("成就解锁：") + rec.Name, true);
                    }
                }
                catch (Exception e)
                {
                    Log.LogWarning("Unlock popup failed: " + e.Message);
                }
            }
        }
    }
}
