namespace AutoBow
{
    /// <summary>
    /// 全局配置常量。所有可调参数集中在此。
    /// 修改后重新编译即可生效。
    /// </summary>
    public static class AutoBowConfig
    {
        // ===== 武器 ID（必须与 auto_bow_items.xml 中一致）=====
        public const string AutoBowItemId = "auto_bow_machine_bow";
        public const string AutoArrowItemId = "auto_bow_machine_arrow";

        // ===== 无下坠 =====
        /// <summary>启用无下坠（机关弓发射的箭不受重力影响）</summary>
        public const bool EnableNoDrop = true;

        // ===== 全自动连发 =====
        /// <summary>启用全自动连发</summary>
        public const bool EnableAutoFire = true;

        /// <summary>射击间隔（秒）。0.15 = 每秒约 6-7 发（测试用，确认稳定后再调快）</summary>
        public const float FireInterval = 0.15f;

        /// <summary>箭矢初速度（米/秒）。设为 300 使下坠几乎不可见（接近无下坠）</summary>
        public const float MissileSpeed = 300f;

        // ===== 右键开镜瞄准 =====
        /// <summary>启用右键瞄准（按住右键缩小视野，模拟开镜放大）</summary>
        public const bool EnableAimZoom = true;

        /// <summary>瞄准时的 FOV 倍率。0.4 = 视野缩小到 40%（画面放大 2.5 倍）</summary>
        public const float AimFovMultiplier = 0.4f;

        /// <summary>正常时的 FOV 倍率（1.0 = 原始视野）</summary>
        public const float NormalFovMultiplier = 1.0f;

        // ===== 武器类型 =====
        // 改为轻弩(Crossbow/LightCrossbow)：左键射击/装填，不进入弓的拉弓瞄准模式。
        // 不再需要运行时取消原版瞄准。

        // ===== 爆炸效果 =====
        /// <summary>启用爆炸效果（弩箭命中时产生范围伤害+爆炸粒子）</summary>
        public const bool EnableExplosion = true;

        /// <summary>爆炸半径（米）。范围内的敌人会受到伤害。</summary>
        public const float ExplosionRadius = 3.0f;

        /// <summary>爆炸伤害（中心伤害，随距离衰减）。</summary>
        public const int ExplosionDamage = 100;

        // ===== 调试 =====
        /// <summary>主调试开关：输出关键状态信息（装备/卸下、瞄准切换、射击成功/失败）</summary>
        public const bool DebugLog = false;

        /// <summary>详细调试：输出每帧的输入检测、武器识别细节、心跳等。排查问题时开启，正常使用可关闭。</summary>
        public const bool VerboseLog = false;

        /// <summary>心跳日志间隔（秒）。装备机关弓时每隔此时间输出一次状态。设为 0 关闭心跳。</summary>
        public const float HeartbeatInterval = 0f;
    }
}
