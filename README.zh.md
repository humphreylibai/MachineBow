# MachineBow 机关连弩

English | [中文说明](README.zh.md)

《骑马与砍杀 2：霸主》（v1.5.5）模组，添加一把"机关连弩"——全自动连发的十字弩，配以高速穿透弩箭、右键开镜瞄准、以及箭矢爆炸效果。

## 功能特性

- **全自动连发**：按住鼠标左键持续射击；单击左键立即射出一发。
- **高速无下坠**：弩箭初速 300 m/s，几乎看不到弹道下坠。
- **右键开镜瞄准**：按住右键缩小视野（FOV 0.4 倍），模拟开镜放大。
- **穿透弩箭**：可穿透所有单位、坐骑和盾牌（`MultiplePenetration` + `CanPenetrateShield` + `burning_ballista` 物理材质）。
- **燃烧轨迹 + 命中爆炸**：弩箭带有火焰拖尾，命中后触发范围伤害爆炸（半径与伤害可配置）。
- **大容量弹药**：每堆 500 发弩箭，重量极轻（0.01）。
- **作弊模式开启**：便于在测试中获取武器。
- **无 Harmony 依赖**：纯 `MissionBehavior` + 反射实现。

## 安装

### 方式 A：下载 Release（推荐）

1. 打开 [Releases 页面](https://github.com/humphreylibai/MachineBow/releases/latest)。
2. 下载最新版的 `MachineBow.zip`。
3. 将 zip 解压到你的 `Mount & Blade II Bannerlord/Modules/` 目录。zip 内顶层就是 `MachineBow/` 文件夹，解压后应得到 `Modules/MachineBow/SubModule.xml`、`Modules/MachineBow/ModuleData/...`、`Modules/MachineBow/bin/Win64_Shipping_Client/MachineBow.dll`。
4. 启动游戏，在启动器中勾选 **MachineBow**。
5. （作弊模式）用物品栏作弊菜单获取"机关连弩"和"机关弩箭"。

### 方式 B：从源码编译

1. 克隆本仓库。
2. 在 `src/AutoBow/` 目录下执行 `dotnet build -c Release`（见下方 [编译](#编译) 章节）。
3. 将生成的 `MachineBow` 文件夹（包含 `SubModule.xml`、`ModuleData/`、`bin/Win64_Shipping_Client/MachineBow.dll`）复制到 `Mount & Blade II Bannerlord/Modules/`。
4. 启动游戏，在启动器中勾选 **MachineBow**。
5. （作弊模式）用物品栏作弊菜单获取"机关连弩"和"机关弩箭"。

### 方式 C：复制已有安装

如果你已经有模组文件：

1. 将 `MachineBow` 文件夹复制到 `Mount & Blade II Bannerlord/Modules/`。
2. 启动游戏，在启动器中勾选本模组。
3. （作弊模式）用物品栏作弊菜单获取"机关连弩"和"机关弩箭"。

## 使用方法

- **鼠标左键（按住）**：全自动连发。
- **鼠标左键（单击）**：单发射击。
- **鼠标右键（按住）**：开镜瞄准放大。
- 弩箭可穿透敌人/坐骑/盾牌，命中后爆炸。

## 配置

编辑 `src/AutoBow/AutoBowConfig.cs`：

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `EnableAutoFire` | true | 全自动连发 |
| `FireInterval` | 0.15 秒 | 射击间隔（约每秒 6-7 发） |
| `MissileSpeed` | 300 | 弩箭初速（米/秒） |
| `EnableAimZoom` | true | 右键开镜 |
| `AimFovMultiplier` | 0.4 | 开镜时视野倍率 |
| `EnableExplosion` | true | 命中爆炸 |
| `ExplosionRadius` | 3.0 | 爆炸半径（米） |
| `ExplosionDamage` | 100 | 爆炸中心伤害（随距离衰减） |
| `DebugLog` | false | 关键状态日志 |
| `VerboseLog` | false | 每帧详细日志 |

修改后重新编译即可生效。

## 编译

```
cd src/AutoBow
"C:\Program Files\dotnet\dotnet.exe" build -c Release
```

DLL 会自动复制到 `bin/Win64_Shipping_Client/`。编译前请先关闭霸主启动器（它会锁定 DLL 文件）。

## 文件结构

| 文件 | 说明 |
|------|------|
| `ModuleData/auto_bow_items.xml` | 武器（轻型十字弩）+ 穿透燃烧弩箭定义 |
| `src/AutoBow/AutoBowMissionBehavior.cs` | 核心每帧逻辑：连发、开镜、命中爆炸 |
| `src/AutoBow/AutoBowSubModule.cs` | 入口，初始化日志 + MissionBehavior |
| `src/AutoBow/AutoBowConfig.cs` | 可调常量 |
| `src/AutoBow/AutoBowLogger.cs` | 文件日志 |

## 环境要求

- 《骑马与砍杀 2：霸主》v1.5.5

## 许可证

MIT
