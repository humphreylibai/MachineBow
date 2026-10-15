using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AutoBow
{
    /// <summary>
    /// 机关连弩核心逻辑。
    ///
    /// 关键 API（已从 v1.5.5 游戏 DLL 确认）：
    /// - MissionWeapon 是 struct（值类型），ConsumeAmmo 修改副本不写回 agent
    ///   → 必须用 agent.SetWeaponAmountInSlot(slot, amount, false) 修改弹药
    /// - agent.OnWeaponAmmoConsume(slot, totalAmmo) 是 internal，需反射调用，触发 UI 更新
    /// - agent.GetCurrentActionType(0) 返回 ActionCodeType，ReadyRanged=15 是拉弓蓄力
    /// - agent.SetAttackState(0) 重置攻击状态，阻止拉弓
    /// - Mission.OnAgentShootMissile 是 internal，需反射调用，走游戏内部射击代码路径
    /// </summary>
    public class AutoBowMissionBehavior : MissionBehavior
    {
        // ===== 运行状态 =====
        private float _fireTimer = 0f;
        private bool _isAiming = false;
        private bool _wasEquipped = false;
        private bool _wasLeftDown = false;
        private bool _wasRightDown = false;
        private float _heartbeatTimer = 0f;
        private int _shotCount = 0;

        // ===== 反射缓存 =====
        private MethodInfo _onAgentShootMissileMethod = null;
        private MethodInfo _onWeaponAmmoConsumeMethod = null;
        private bool _behaviorInitialized = false;

        // ===== 音效缓存 =====
        // 弓弦/弩弦释放音效：event:/mission/combat/missile/foley/bowrelease
        // 改为轻弩后，弩弦释放音效是 crossbowrelease，但 bowrelease 也能用
        private int _bowReleaseSoundId = -1;
        private bool _soundResolved = false;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnPreMissionTick(float dt)
        {
            base.OnPreMissionTick(dt);
            // 改为轻弩后不需要取消原版瞄准——轻弩的左键是射击/装填，不进入弓的拉弓瞄准模式。
            // 此方法保留为空，用于未来可能的输入预处理。
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            try
            {
                EnsureInitialized();
                ResolveBowReleaseSound();
                HandleAutoFire(dt);
                HandleAimZoom();
                HandleHeartbeat(dt);
            }
            catch (System.Exception ex)
            {
                LogErr("OnMissionTick 异常: " + ex);
            }
        }

        /// <summary>
        /// 当导弹命中敌人时触发爆炸：生成爆炸粒子 + 范围伤害。
        /// 只对机关弩箭触发的命中生效。
        /// </summary>
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent,
            in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in collisionData);
            try
            {
                if (affectorWeapon.IsEmpty) return;
                string weaponId = GetStringId(affectorWeapon.Item);
                if (weaponId != AutoBowConfig.AutoArrowItemId) return;

                Vec3 hitPos = collisionData.CollisionGlobalPosition;
                if (hitPos.LengthSquared < 0.01f && affectedAgent != null)
                    hitPos = affectedAgent.Position;

                TriggerExplosion(affectorAgent, hitPos, affectedAgent);
            }
            catch (System.Exception ex)
            {
                LogErr("OnAgentHit 异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 在指定位置触发爆炸：生成爆炸粒子 + 对范围内敌人造成伤害。
        /// </summary>
        private void TriggerExplosion(Agent shooter, Vec3 position, Agent primaryVictim)
        {
            if (!AutoBowConfig.EnableExplosion) return;
            try
            {
                float radius = AutoBowConfig.ExplosionRadius;
                int damage = AutoBowConfig.ExplosionDamage;

                // 1. 生成爆炸粒子效果
                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = position;
                Mission.AddParticleSystemBurstByName("psys_game_burning_projectile_default_coll", frame, false);
                Mission.AddParticleSystemBurstByName("psys_game_ballistamissile_metal_coll", frame, false);

                // 2. 对范围内的所有敌人造成伤害（排除射击者和已被主弹命中的敌人）
                foreach (Agent agent in Mission.AllAgents)
                {
                    if (agent == null || !agent.IsActive()) continue;
                    if (agent == shooter) continue; // 不伤害自己
                    if (agent == primaryVictim) continue; // 主弹已造成伤害

                    float dist = (agent.Position - position).Length;
                    if (dist > radius) continue;

                    // 距离衰减：越远伤害越低
                    float falloff = 1f - (dist / radius);
                    int actualDamage = (int)(damage * falloff);
                    if (actualDamage <= 0) continue;

                    // 不伤害友军（同队伍）
                    if (shooter.Team != null && agent.Team != null && shooter.Team == agent.Team) continue;

                    // 构造 Blow 施加伤害
                    Blow blow = new Blow(shooter.Index);
                    blow.InflictedDamage = actualDamage;
                    blow.DamageType = DamageTypes.Pierce;
                    blow.BaseMagnitude = actualDamage;
                    blow.AttackType = AgentAttackType.Standard;
                    blow.BlowFlag = BlowFlags.NoSound;
                    blow.GlobalPosition = agent.Position;
                    blow.BoneIndex = -1;
                    blow.DamageCalculated = true;

                    // AttackCollisionData 是不可变 struct，所有属性只读，用 default 即可。
                    // 伤害信息由 Blow 自身携带。
                    AttackCollisionData acd = default(AttackCollisionData);

                    agent.RegisterBlow(blow, ref acd);
                    LogV("爆炸伤害: " + agent.Name + " 距离=" + dist.ToString("F1") + " 伤害=" + actualDamage);
                }

                Log("爆炸触发 位置=" + position + " 半径=" + radius + " 伤害=" + damage);
            }
            catch (System.Exception ex)
            {
                LogErr("TriggerExplosion 异常: " + ex.Message);
            }
        }

        private void EnsureInitialized()
        {
            if (_behaviorInitialized) return;
            _behaviorInitialized = true;
            Log("AutoBowMissionBehavior 已激活。Mission=" + (Mission != null ? "存在" : "null"));

            _onAgentShootMissileMethod = typeof(Mission).GetMethod(
                "OnAgentShootMissile",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Log(_onAgentShootMissileMethod != null
                ? "反射: 已解析 OnAgentShootMissile"
                : "反射: 未找到 OnAgentShootMissile");

            _onWeaponAmmoConsumeMethod = typeof(Agent).GetMethod(
                "OnWeaponAmmoConsume",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Log(_onWeaponAmmoConsumeMethod != null
                ? "反射: 已解析 OnWeaponAmmoConsume"
                : "反射: 未找到 OnWeaponAmmoConsume");

            LogV("配置: AutoFire=" + AutoBowConfig.EnableAutoFire +
                 " Interval=" + AutoBowConfig.FireInterval +
                 " Speed=" + AutoBowConfig.MissileSpeed +
                 " (轻弩模式，无需取消瞄准)");
        }

        // ============ 全自动连发 ============
        private void HandleAutoFire(float dt)
        {
            if (!AutoBowConfig.EnableAutoFire) return;

            Agent mainAgent = Mission.MainAgent;
            if (mainAgent == null) { if (_wasEquipped) _wasEquipped = false; return; }
            if (!mainAgent.IsActive()) { LogV("主角非活动状态"); return; }

            bool equipped = IsAutoBowEquipped(mainAgent);

            if (equipped && !_wasEquipped)
            {
                Log("机关连弩已握持！按住左键连发。");
                ReportWeaponInfo(mainAgent);
            }
            else if (!equipped && _wasEquipped)
            {
                Log("机关连弩已收起/切换");
            }
            _wasEquipped = equipped;

            if (!equipped) return;

            // 每帧检查：如果弩处于"装填中"状态（OnAgentShootMissile 射击后会进入此状态），
            // 立即恢复到"已装填"状态。这样玩家按下左键时弩已经准备好，准星正常显示。
            KeepBowReloaded(mainAgent);

            bool leftDown = IsAttackKeyDown();
            if (leftDown && !_wasLeftDown)
            {
                LogV("左键按下 — 开始连射");
                _shotCount = 0;
                // 单击立即射一发，不等 FireInterval
                FireMissile(mainAgent);
                _fireTimer = 0f;
            }
            else if (!leftDown && _wasLeftDown)
            {
                LogV("左键松开 — 本次共发射 " + _shotCount + " 发");
            }
            _wasLeftDown = leftDown;

            if (!leftDown) { _fireTimer = 0f; return; }

            _fireTimer += dt;
            if (_fireTimer >= AutoBowConfig.FireInterval)
            {
                _fireTimer = 0f;
                FireMissile(mainAgent);
            }
        }

        // CancelVanillaBowDraw 已移除：改为轻弩后，左键是射击/装填，不进入弓的拉弓瞄准模式。
        // 不再需要在运行时清除 MovementFlags 或调用 SetAttackState。

        private void ReportWeaponInfo(Agent agent)
        {
            try
            {
                MissionWeapon w = agent.WieldedWeapon;
                string itemId = "(empty)";
                if (!w.IsEmpty && w.Item != null)
                    itemId = GetStringId(w.Item) ?? "(no id)";
                LogV("握持武器: id=" + itemId + " 目标ID=" + AutoBowConfig.AutoBowItemId +
                     " 匹配=" + (itemId == AutoBowConfig.AutoBowItemId));

                for (int i = 0; i <= 4; i++)
                {
                    try
                    {
                        var slot = agent.Equipment[(EquipmentIndex)i];
                        if (!slot.IsEmpty)
                        {
                            string sid = GetStringId(slot.Item) ?? "?";
                            LogV("  槽位[" + i + "]: id=" + sid +
                                 " amount=" + slot.Amount +
                                 " ammo=" + slot.Ammo +
                                 " maxAmmo=" + slot.MaxAmmo);
                        }
                    }
                    catch { }
                }
            }
            catch (System.Exception ex) { LogErrV("ReportWeaponInfo 异常: " + ex.Message); }
        }

        // ============ 射击 ============
        private void FireMissile(Agent agent)
        {
            try
            {
                MissionWeapon weapon = agent.WieldedWeapon;
                if (weapon.IsEmpty)
                {
                    LogWarn("射击取消: 武器为空");
                    return;
                }

                // 确认握持的是机关弓
                if (!IsWeaponAutoBow(weapon))
                {
                    LogWarn("射击取消: 握持的不是机关弓 id=" + GetStringId(weapon.Item));
                    return;
                }

                // 检查弹药：弩槽 ammo_limit=500，一次装满500发。
                // 原版射击自动扣弩槽 ammo（500→499→...→0），正好消耗500发。
                // 用完后引擎自动从箭袋装填一次（装填动画此时是合理的，因为真的需要换箭袋）。
                // 总弹药 = 弩槽ammo + 箭袋amount。
                int totalAmmo = weapon.Ammo + GetTotalAmmoAmount(agent);
                if (totalAmmo <= 0)
                {
                    LogWarn("射击取消: 弹药已空（弩槽=" + weapon.Ammo + " 箭袋=" + GetTotalAmmoAmount(agent) + "）");
                    return;
                }

                Vec3 lookDir = agent.LookDirection;
                if (lookDir.LengthSquared < 0.01f)
                {
                    LogWarn("视线方向接近零");
                    lookDir = new Vec3(0f, 1f, 0f);
                }
                else
                {
                    lookDir = lookDir.NormalizedCopy();
                }

                // 用 agent.GetEyeGlobalPosition() 获取精确的眼睛位置（步战/骑马都准确）。
                Vec3 pos = agent.GetEyeGlobalPosition() + lookDir * 1.2f;

                float speed = AutoBowConfig.MissileSpeed;
                Vec3 velocity = new Vec3(lookDir.X * speed, lookDir.Y * speed, lookDir.Z * speed);

                Mat3 orientation = Mat3.Identity;
                orientation.f = lookDir;

                EquipmentIndex weaponIndex = FindWeaponSlot(agent, weapon);
                LogV("射击: 槽位=" + weaponIndex + " 弹药=" + weapon.Ammo +
                     " 速度=" + speed + " 发射点=" + pos + " 骑马=" + agent.HasMount);

                // 走原版射击流程：OnAgentShootMissile 播放射击动作、生成弹道、播放音效、扣弩槽 ammo。
                // 弩槽 ammo_limit=500，射击后 ammo=499 > 0，前499发都不触发装填动画。
                // 第500发后 ammo=0，引擎自动从箭袋装填一次（合理，真的需要换箭袋）。
                LogV("射击前: ReloadPhase=" + weapon.ReloadPhase + "/" + weapon.ReloadPhaseCount +
                     " IsReloading=" + weapon.IsReloading + " Ammo=" + weapon.Ammo);
                bool success = false;
                if (_onAgentShootMissileMethod != null)
                {
                    try
                    {
                        _onAgentShootMissileMethod.Invoke(Mission, new object[]
                        {
                            agent, weaponIndex, pos, velocity, orientation,
                            true, true, -1
                        });
                        success = true;
                    }
                    catch (System.Exception ex)
                    {
                        string inner = ex.InnerException != null
                            ? ex.InnerException.GetType().Name + ": " + ex.InnerException.Message
                            : ex.Message;
                        LogErr("OnAgentShootMissile 失败: " + inner);
                        LogErrV("完整异常: " + ex);
                    }
                }

                if (success)
                {
                    _shotCount++;
                    MissionWeapon wAfter = agent.Equipment[weaponIndex];
                    LogV("射击后: ReloadPhase=" + wAfter.ReloadPhase + "/" + wAfter.ReloadPhaseCount +
                         " IsReloading=" + wAfter.IsReloading + " Ammo=" + wAfter.Ammo);
                    // 手动扣弩槽 ammo
                    ConsumeBowAmmo(agent, weaponIndex);
                    // 尝试恢复"已装填"状态：强制 ReloadPhase 到满，防止引擎认为需要装填
                    // OnAgentShootMissile 可能在 native 把 reload phase 设为 0，导致准星消失
                    try
                    {
                        agent.SetWeaponReloadPhaseAsClient(weaponIndex, (short)wAfter.ReloadPhaseCount);
                        LogV("SetWeaponReloadPhaseAsClient(" + weaponIndex + ", " + wAfter.ReloadPhaseCount + ") 已调用");
                    }
                    catch (System.Exception ex)
                    {
                        LogV("SetWeaponReloadPhaseAsClient 失败: " + ex.Message);
                    }
                    // 播放弩弦释放音效
                    PlayBowReleaseSound(agent, pos);
                    Log("射击成功 #" + _shotCount + " 弩槽=" + agent.Equipment[weaponIndex].Ammo +
                        " 箭袋=" + GetTotalAmmoAmount(agent));
                }
            }
            catch (System.Exception ex)
            {
                LogErr("FireMissile 异常: " + ex);
            }
        }

        // RefillBowFromQuiver / KeepBowFull 已移除：
        // 弩槽 ammo_limit=500，一次装满500发，我们手动扣（500→499→...→0）。
        // 用完后引擎自动从箭袋装填一次（装填动画此时合理，真的需要换箭袋）。

        /// <summary>
        /// 手动扣弩槽 ammo。
        /// OnAgentShootMissile 只生成导弹，不扣弹药（弹药扣减在引擎输入层，我们绕过了）。
        /// 
        /// 关键：SetWeaponAmountInSlot 只设置 Amount，不触发弹药消耗逻辑。
        /// 必须调用 OnWeaponAmmoConsume（internal，反射）让引擎真正处理弹药消耗 + UI 更新。
        /// 否则引擎认为弹药没变，下一帧又覆盖回原值。
        /// </summary>
        private void ConsumeBowAmmo(Agent agent, EquipmentIndex bowSlot)
        {
            try
            {
                short ammo = agent.Equipment[bowSlot].Ammo;
                if (ammo <= 0) return; // 已空，等引擎装填
                short newAmmo = (short)(ammo - 1);
                // 1. 设置新弹药数
                agent.SetWeaponAmountInSlot(bowSlot, newAmmo, false);
                // 2. 触发引擎的弹药消耗逻辑（UI 更新 + 状态同步）
                if (_onWeaponAmmoConsumeMethod != null)
                {
                    try
                    {
                        _onWeaponAmmoConsumeMethod.Invoke(agent, new object[] { bowSlot, newAmmo });
                    }
                    catch (System.Exception ex)
                    {
                        LogV("OnWeaponAmmoConsume 失败: " + ex.Message);
                    }
                }
                LogV("弩槽弹药 " + ammo + " -> " + newAmmo);
            }
            catch (System.Exception ex)
            {
                LogErrV("ConsumeBowAmmo 异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 每帧检查弩的装填状态。
        /// OnAgentShootMissile 射击后，引擎会把弩设为"装填中"（ReloadPhase=0, IsReloading=True），
        /// 导致准星消失。SetWeaponReloadPhaseAsClient 可以恢复，但需要到下一帧才生效。
        /// 所以每帧检查：如果弩在装填中且 ammo > 0（不需要真正装填），立即恢复到"已装填"状态。
        /// 这样玩家下次按下左键时弩已经准备好，准星正常显示。
        /// </summary>
        private void KeepBowReloaded(Agent agent)
        {
            try
            {
                MissionWeapon weapon = agent.WieldedWeapon;
                if (weapon.IsEmpty || !IsWeaponAutoBow(weapon)) return;
                // 只在装填中且还有弹药时恢复（ammo=0 时让引擎正常装填）
                if (weapon.IsReloading && weapon.Ammo > 0)
                {
                    EquipmentIndex slot = FindWeaponSlot(agent, weapon);
                    agent.SetWeaponReloadPhaseAsClient(slot, weapon.ReloadPhaseCount);
                }
            }
            catch { }
        }

        /// <summary>
        /// 查找机关箭（箭袋）所在的装备槽位。
        /// 机关弓配的箭是 auto_bow_machine_arrow（在 AutoBowConfig.AutoArrowItemId 中定义）。
        /// 支持多个箭袋：优先返回 Amount > 0 的箭袋，用完后自动切换到下一个。
        /// </summary>
        private EquipmentIndex FindAmmoSlot(Agent agent)
        {
            try
            {
                // 优先找有弹药（Amount > 0）的箭袋
                for (int i = 0; i <= 4; i++)
                {
                    var slotWeapon = agent.Equipment[(EquipmentIndex)i];
                    if (!slotWeapon.IsEmpty)
                    {
                        string sid = GetStringId(slotWeapon.Item);
                        if (sid == AutoBowConfig.AutoArrowItemId && slotWeapon.Amount > 0)
                            return (EquipmentIndex)i;
                    }
                }
                // 如果没有有弹药的箭袋，返回任意一个机关箭槽（用于报告空弹药）
                for (int i = 0; i <= 4; i++)
                {
                    var slotWeapon = agent.Equipment[(EquipmentIndex)i];
                    if (!slotWeapon.IsEmpty)
                    {
                        string sid = GetStringId(slotWeapon.Item);
                        if (sid == AutoBowConfig.AutoArrowItemId)
                            return (EquipmentIndex)i;
                    }
                }
                return EquipmentIndex.None;
            }
            catch { return EquipmentIndex.None; }
        }

        /// <summary>
        /// 统计所有机关箭袋的剩余弹药总量。
        /// 用于检查是否有弹药可射、多箭袋支持。
        /// </summary>
        private int GetTotalAmmoAmount(Agent agent)
        {
            try
            {
                int total = 0;
                for (int i = 0; i <= 4; i++)
                {
                    var slotWeapon = agent.Equipment[(EquipmentIndex)i];
                    if (!slotWeapon.IsEmpty)
                    {
                        string sid = GetStringId(slotWeapon.Item);
                        if (sid == AutoBowConfig.AutoArrowItemId)
                            total += slotWeapon.Amount;
                    }
                }
                return total;
            }
            catch { return 0; }
        }

        // ============ 弩弦释放音效 ============
        // 我们绕过了原版装填动画，引擎不会自动播放弩弦音效。
        // 这里手动用 Mission.MakeSound 播放 crossbowrelease 音效。
        // 改为轻弩后使用 crossbowrelease，bowrelease 作为备用。
        private const string BowReleaseSoundEvent = "event:/mission/combat/missile/foley/crossbowrelease";

        private void ResolveBowReleaseSound()
        {
            if (_soundResolved) return;
            try
            {
                int id = TaleWorlds.Engine.SoundEvent.GetEventIdFromString(BowReleaseSoundEvent);
                if (id > 0)
                {
                    _bowReleaseSoundId = id;
                    _soundResolved = true;
                    Log("弩弦音效已解析 id=" + id);
                }
            }
            catch (System.Exception ex)
            {
                // 音效系统尚未就绪，下一帧重试
                LogV("弩弦音效解析失败（重试中）: " + ex.Message);
            }
        }

        private void PlayBowReleaseSound(Agent agent, Vec3 position)
        {
            if (!_soundResolved || _bowReleaseSoundId <= 0) return;
            try
            {
                // MakeSound(soundIndex, position, soundCanBePredicted, isReliable, relatedAgent1, relatedAgent2)
                Mission.MakeSound(_bowReleaseSoundId, position, false, true, agent.Index, agent.Index);
            }
            catch (System.Exception ex)
            {
                LogErrV("PlayBowReleaseSound 异常: " + ex.Message);
            }
        }

        private bool TryAddCustomMissile(Agent agent, MissionWeapon weapon,
            Vec3 position, Vec3 direction, Mat3 orientation, float speed)
        {
            try
            {
                int baseSpeed = weapon.GetModifiedMissileSpeedForCurrentUsage();
                if (baseSpeed <= 0) baseSpeed = (int)speed;
                Mission.AddCustomMissile(
                    agent, weapon, position, direction, orientation,
                    (float)baseSpeed, speed, true, null, -1);
                return true;
            }
            catch (System.Exception ex)
            {
                LogErr("AddCustomMissile 失败: " + (ex.InnerException?.Message ?? ex.Message));
                return false;
            }
        }

        private EquipmentIndex FindWeaponSlot(Agent agent, MissionWeapon weapon)
        {
            try
            {
                for (int i = 0; i <= 4; i++)
                {
                    var slotWeapon = agent.Equipment[i];
                    if (!slotWeapon.IsEmpty && IsSameWeapon(slotWeapon, weapon))
                        return (EquipmentIndex)i;
                }
                return EquipmentIndex.Weapon0;
            }
            catch { return EquipmentIndex.Weapon0; }
        }

        private bool IsSameWeapon(MissionWeapon a, MissionWeapon b)
        {
            try
            {
                if (a.IsEmpty || b.IsEmpty) return false;
                return a.Item == b.Item;
            }
            catch { return false; }
        }

        // ============ 右键开镜瞄准 ============
        private void HandleAimZoom()
        {
            if (!AutoBowConfig.EnableAimZoom) return;

            Agent mainAgent = Mission.MainAgent;
            if (mainAgent == null || !mainAgent.IsActive()) return;

            bool equipped = IsAutoBowEquipped(mainAgent);

            if (!equipped)
            {
                if (_isAiming)
                {
                    Mission.SetCustomCameraFovMultiplier(AutoBowConfig.NormalFovMultiplier);
                    _isAiming = false;
                    Log("退出瞄准模式（未装备机关弓）");
                }
                return;
            }

            bool rightDown = IsRightMouseButtonDown();
            _wasRightDown = rightDown;

            if (rightDown && !_isAiming)
            {
                Mission.SetCustomCameraFovMultiplier(AutoBowConfig.AimFovMultiplier);
                _isAiming = true;
                Log("进入瞄准模式 FOV=" + AutoBowConfig.AimFovMultiplier);
            }
            else if (!rightDown && _isAiming)
            {
                Mission.SetCustomCameraFovMultiplier(AutoBowConfig.NormalFovMultiplier);
                _isAiming = false;
                Log("退出瞄准模式 FOV=" + AutoBowConfig.NormalFovMultiplier);
            }
        }

        // ============ 心跳日志 ============
        private void HandleHeartbeat(float dt)
        {
            if (AutoBowConfig.HeartbeatInterval <= 0f) return;
            if (!_wasEquipped) return;

            _heartbeatTimer += dt;
            if (_heartbeatTimer >= AutoBowConfig.HeartbeatInterval)
            {
                _heartbeatTimer = 0f;
                Agent mainAgent = Mission.MainAgent;
                if (mainAgent == null) return;
                MissionWeapon w = mainAgent.WieldedWeapon;
                int totalAmmo = GetTotalAmmoAmount(mainAgent);
                // 输出动作类型、瞄准计时器、攻击方向、总弹药，诊断拉弓和多箭袋
                string actionType = "?";
                try { actionType = mainAgent.GetCurrentActionType(0).ToString(); } catch { }
                float aimTimer = 0f;
                try { aimTimer = mainAgent.GetAimingTimer(); } catch { }
                var flags = mainAgent.MovementFlags;
                LogV("心跳: 总弹药=" + totalAmmo + " 累计=" + _shotCount +
                     " 动作=" + actionType + " 瞄准计时=" + aimTimer +
                     " 移动标志=" + flags);
            }
        }

        // ============ 输入检测 ============
        private bool IsAttackKeyDown()
        {
            try { return Input.IsKeyDown(InputKey.LeftMouseButton); }
            catch { return false; }
        }

        private bool IsRightMouseButtonDown()
        {
            try { return Input.IsKeyDown(InputKey.RightMouseButton); }
            catch { return false; }
        }

        // ============ 武器识别 ============
        /// <summary>
        /// 检查玩家当前握持的武器是否是机关弓。
        /// 只检查 WieldedWeapon（当前握持的），不检查所有槽位。
        /// 这样玩家握持斧头时不会误触发连射。
        /// </summary>
        private bool IsAutoBowEquipped(Agent agent)
        {
            try
            {
                return IsWeaponAutoBow(agent.WieldedWeapon);
            }
            catch (System.Exception ex)
            {
                LogErrV("IsAutoBowEquipped 异常: " + ex.Message);
                return false;
            }
        }

        private bool IsWeaponAutoBow(MissionWeapon weapon)
        {
            try
            {
                if (weapon.IsEmpty) return false;
                var item = weapon.Item;
                if (item == null) return false;
                return GetStringId(item) == AutoBowConfig.AutoBowItemId;
            }
            catch { return false; }
        }

        private string GetStringId(object obj)
        {
            try
            {
                var type = obj.GetType();
                var prop = type.GetProperty("StringId");
                if (prop != null) return prop.GetValue(obj) as string;
                prop = type.GetProperty("Id");
                if (prop != null) return prop.GetValue(obj)?.ToString();
            }
            catch { }
            return null;
        }

        // ============ 日志辅助 ============
        private void Log(string msg)
        {
            AutoBowLogger.Info(msg);
            if (AutoBowConfig.DebugLog)
                InformationManager.DisplayMessage(new InformationMessage("[MachineBow] " + msg));
        }

        private void LogWarn(string msg)
        {
            AutoBowLogger.Warn(msg);
            if (AutoBowConfig.DebugLog)
                InformationManager.DisplayMessage(new InformationMessage("[MachineBow][警告] " + msg));
        }

        private void LogErr(string msg)
        {
            AutoBowLogger.Error(msg);
            if (AutoBowConfig.DebugLog)
                InformationManager.DisplayMessage(new InformationMessage("[MachineBow][错误] " + msg));
        }

        private void LogV(string msg)
        {
            AutoBowLogger.Verbose(msg);
            if (AutoBowConfig.VerboseLog)
                InformationManager.DisplayMessage(new InformationMessage("[MachineBow][V] " + msg));
        }

        private void LogErrV(string msg)
        {
            AutoBowLogger.Error(msg);
            if (AutoBowConfig.VerboseLog)
                InformationManager.DisplayMessage(new InformationMessage("[MachineBow][错误][V] " + msg));
        }
    }
}
