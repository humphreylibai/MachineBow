using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AutoBow
{
    /// <summary>
    /// Mod 入口类。
    /// Bannerlord 启动时通过 SubModule.xml 加载此类型。
    ///
    /// 注意：此 Mod 不依赖 Harmony。
    /// 所有功能（全自动连发、无下坠）都通过 AutoBowMissionBehavior
    /// 的每帧逻辑实现，不需要 Harmony patch。
    /// 这样 AutoBow.dll 不会引用 0Harmony.dll，
    /// 避免 Bannerlord 启动器的 DLL 验证问题。
    /// </summary>
    public class AutoBowSubModule : MBSubModuleBase
    {
        public const string ModId = "MachineBow";
        public const string ModName = "MachineBow";
        public const string ModVersion = "1.0.0";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // 尽早初始化文件日志（这是 Mod 最早的入口点）
            AutoBowLogger.Init();
            AutoBowLogger.Info("=== OnSubModuleLoad ===");
            AutoBowLogger.Info("Mod=" + ModName + " v" + ModVersion);
            AutoBowLogger.Info("日志文件: " + (AutoBowLogger.LogPath ?? "(初始化失败)"));

            if (AutoBowConfig.DebugLog)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{ModName}] v{ModVersion} 已加载 — 机关连弩就绪！日志: " + (AutoBowLogger.LogPath ?? "初始化失败"),
                    Colors.Yellow));
            }
        }

        /// <summary>
        /// 在每个 Mission 初始化时添加自定义 MissionBehavior。
        /// 这是 Bannerlord Mod 注册每帧逻辑的标准方式。
        /// 全自动连发和无下坠功能都通过此 Behavior 实现。
        /// </summary>
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            AutoBowLogger.Info("OnMissionBehaviorInitialize: 注册 AutoBowMissionBehavior, Mission=" + (mission != null ? mission.ToString() : "null"));
            mission.AddMissionBehavior(new AutoBowMissionBehavior());
        }
    }
}
