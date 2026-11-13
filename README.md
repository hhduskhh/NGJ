# 寂响遗迹 / Echoes Below

Unity 2D 俯视角像素风肉鸽射击原型。以可视声波惊醒有限敌人，用附近清醒敌人的数量换取有上限的伤害加成；探索单层连通地牢，击杀升级，自选时机挑战守关者。

## 运行

1. 用 Unity Hub 添加本目录，使用 **2022.3.62f3c1** 打开。
2. 打开 `Assets/Echoes/Scenes/Prototype.unity`，点击 Play。
3. 若场景尚未生成，执行编辑器菜单 **Echoes > Create or Update Prototype Scene**。
4. 如需 Windows 程序，执行 **Echoes > Build Windows Prototype**，输出到 `Builds/Windows/EchoesBelow.exe`。

本次已生成 Windows 试玩版，可直接运行上述 EXE。请保留同目录的数据文件夹和 DLL。验证结果见 `Docs/008-自动验证与原型交付.md`：7 项规则测试、3 项运行集成测试通过，Windows 构建及独立程序启动检查通过。

| 操作 | 按键 |
| --- | --- |
| 移动 | WASD |
| 瞄准 / 射击 | 鼠标 / 按住左键 |
| 投掷响铃诱饵 | Q，朝鼠标方向 |
| 激活守关者 | 靠近右上房间祭坛按 E |
| 选择升级 | 点击卡片或 1 / 2 / 3 |
| 暂停 / 静音 | Esc / M |

起点没有敌人。沿开着的门探索；普通房间不会锁门。金色声波是枪声，青色声波是诱饵；外侧细圈为最大影响边界。玩家青色细环为围攻加成半径。灰色横线为沉睡，金色标记为调查或警戒，红色竖线为追击。关闭音效仍可识别全部必要信息。

## 审查与调参

- `Assets/Echoes/Settings/DefaultGameConfig.asset`：集中配置战斗和成长数值。
- `Assets/Echoes/Scripts/Core/`：整局流程、纯规则、升级定义。
- `Assets/Echoes/Scripts/Combat/`：玩家、敌人状态、子弹、声波、诱饵。
- `Assets/Echoes/Scripts/World/`：连通地图、寻路、占位美术、相机。
- `Assets/Echoes/Scripts/UI/`：HUD、三选一、暂停、结算。
- `Docs/001-…` 起：逐项功能记录，包含编辑器设置与验证方法。

## 测试

在 **Window > General > Test Runner** 运行 EditMode 的 `Echoes.Tests` 和 PlayMode 的 `Echoes.PlayModeTests`。集成测试检查七个房间连通、有限敌人、噪声惊醒与调查、永久警戒、击杀经验、三选一暂停、主动风险升级、守关者激活和非清场胜利。另一项运行测试确认时间实际推进并尝试保存探索、战斗和升级截图到 `TestResults/`。

## 当前范围

七个固定房间、五个战斗房共四十只普通怪、一只守关者、一把枪、一个响铃诱饵、六种可重复升级。正式美术、音乐、随机地图及更多内容尚未接入。5–8 分钟为设计目标，需完整人工试玩后调参。中文字体使用 Noto Sans SC，字体目录内包含 OFL 许可证。
