# Changelog

## 0.2.0

- Mod achievements now show inside the vanilla achievement panel via a toggle button, as vanilla-style cards. The Alt+K window is removed.
- Unlocks use the vanilla "Achievement unlocked" popup card; clicking it opens the mod page and scrolls to the achievement.
- New `icon` parameter on `Register` (`AchIcon`). Save format v2; v1 files are still read.
- MOD 成就改为在原版成就面板内显示（左上角切换按钮，官方卡片样式），移除 Alt+K 窗口。
- 解锁时弹出与官方相同的"获得成就"卡片，点击后打开 MOD 成就页并定位到该成就。
- `Register` 新增 `icon` 参数（`AchIcon`）。存档格式升级到 v2，仍可读取 v1。

## 0.1.0

- First version: mods register achievements by GitHub URL; progress is stored in its own file; uninstalled mods' achievements stay visible.
- 首个版本：mod 用 GitHub 地址登记成就；进度存在独立文件；卸载的 mod 的成就仍然显示。
