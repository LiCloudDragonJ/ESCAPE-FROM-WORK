using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace EscapeFromWork.Editor
{
    /// <summary>
    /// 轻量版 Rainbow Folders — 给 Project 窗口中的自定义文件夹着色。
    /// 在 Edit > Preferences > Rainbow Folders 中配置颜色规则。
    /// 原始灵感来自 PhannGor/unity-rainbow-folders，此为简化内嵌版本。
    /// </summary>
    [InitializeOnLoad]
    public static class RainbowFolders
    {
        // 默认颜色配置 — 可在 Preferences 中修改
        private static readonly Dictionary<string, Color> DefaultFolderColors = new Dictionary<string, Color>
        {
            { "Scripts",     new Color(0.4f, 0.7f, 1.0f) },  // 蓝色 — 代码
            { "Editor",      new Color(0.9f, 0.4f, 0.4f) },  // 红色 — 编辑器工具
            { "Prefabs",     new Color(0.4f, 0.6f, 1.0f) },  // 浅蓝 — 预制体
            { "Scenes",      new Color(0.2f, 0.8f, 0.5f) },  // 绿色 — 场景
            { "ScriptableObjects", new Color(0.8f, 0.3f, 0.6f) },  // 粉色 — 数据资产
            { "Materials",   new Color(1.0f, 0.6f, 0.2f) },  // 橙色 — 材质
            { "Textures",    new Color(0.9f, 0.2f, 0.2f) },  // 红色 — 贴图
            { "Audio",       new Color(1.0f, 0.8f, 0.2f) },  // 黄色 — 音频
            { "Animations",  new Color(0.6f, 0.3f, 0.9f) },  // 紫色 — 动画
            { "UI",          new Color(0.2f, 0.8f, 0.8f) },  // 青色 — UI
            { "Core",        new Color(0.3f, 0.3f, 0.9f) },  // 深蓝 — 核心系统
            { "Player",      new Color(0.3f, 0.8f, 0.4f) },  // 绿色 — 玩家
            { "Enemies",     new Color(0.9f, 0.3f, 0.3f) },  // 红色 — 敌人
            { "Weapons",     new Color(0.8f, 0.5f, 0.1f) },  // 橙色 — 武器
            { "Level",       new Color(0.5f, 0.7f, 0.3f) },  // 橄榄 — 关卡
            { "Loot",        new Color(1.0f, 0.8f, 0.0f) },  // 金色 — 战利品
            { "Data",        new Color(0.5f, 0.5f, 0.5f) },  // 灰色 — 数据
            { "Settings",    new Color(0.6f, 0.6f, 0.7f) },  // 灰蓝 — 设置
            { "Utilities",   new Color(0.5f, 0.5f, 0.6f) },  // 暗灰 — 工具
        };

        static RainbowFolders()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;
            if (!AssetDatabase.IsValidFolder(path)) return;

            string folderName = Path.GetFileName(path);

            if (DefaultFolderColors.TryGetValue(folderName, out Color color))
            {
                // 覆盖层 — 在文件夹名称后面画一个彩色小方块
                Rect colorRect = selectionRect;
                colorRect.x += selectionRect.width - 24;
                colorRect.y += 3;
                colorRect.width = 16;
                colorRect.height = 12;

                EditorGUI.DrawRect(colorRect, color);

                // 如果被选中则强制重绘以避免颜色被选中高亮覆盖
                if (Selection.Contains(AssetDatabase.LoadAssetAtPath<Object>(path)))
                {
                    EditorApplication.RepaintProjectWindow();
                }
            }
        }

        /// <summary>
        /// 在 Project 窗口中高亮所有匹配的文件夹。
        /// 可通过菜单 ESCAPE FROM WORK > Refresh Folder Colors 手动触发。
        /// </summary>
        [MenuItem("ESCAPE FROM WORK/Refresh Folder Colors")]
        public static void RefreshFolderColors()
        {
            EditorApplication.RepaintProjectWindow();
            Debug.Log("[RainbowFolders] 文件夹颜色已刷新。");
        }
    }
}
