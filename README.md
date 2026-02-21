# OriginalB

一个基于 Unity 的休闲解谜原型项目：

- 主题：小猫躲进纸箱，玩家通过搬运同色纸箱完成三消，找到小猫后通关。
- 当前重点：货架自动生成、均匀分散布局、避免重叠、运行时一键重生。

## 环境要求

- Unity（建议使用项目当前 `ProjectSettings/ProjectVersion.txt` 对应版本）
- 平台：Windows / macOS 均可

## 运行方式

1. 用 Unity Hub 打开本项目根目录。
2. 打开场景：`Assets/Scenes/MainScene.unity`。
3. 点击 Play 运行。

## 已实现功能

### 游戏核心（GameManager）

- 关卡数据结构与运行时状态
- 纸箱移动与同色规则校验
- 三消判定（3个及以上连续同色消除）
- 小猫发现通关 / 无可移动失败
- 道具系统（显示货架、撤销、猫提示）
- 每日次数与积分存储（PlayerPrefs）

### 货架系统（ShelfSpawnManager）

- 独立管理类，职责与 GameManager 分离
- 自动创建根节点：`ShelfRoot/RuntimeShelves/LegacyShelves`
- 货架围绕屏幕中心随机分布
- 支持上下、左右分别设置边距：
  - `viewportHorizontalPadding`
  - `viewportVerticalPadding`
- 防重叠策略：
  - 基于货架占位尺寸判定
  - 最小间距 `minShelfDistance`
  - 网格回退放置，无法安全放置时跳过并告警
- 运行时按钮：`重新产生货架`
  - 点击后销毁旧运行时货架并重新生成
  - 支持每次刷新随机位置

## 主要脚本

- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/ShelfSpawnManager.cs`

## 小游戏迁移与验收

- 真机前检查清单：`Docs/小游戏真机前检查清单.md`
- 最小回归用例表：`Docs/小游戏最小回归用例表.md`

## 可调参数建议

- 若货架仍过密：增大 `viewportHorizontalPadding` / `viewportVerticalPadding`，或减小 `previewShelfCount`
- 若希望更疏：增大 `minShelfDistance`
- 若希望每次位置固定：关闭 `randomizeOnEachRefresh`

## Git 说明

仓库已连接远程：

- `origin`: [https://github.com/zgyunyunyunyun/OriginalB.git](https://github.com/zgyunyunyunyun/OriginalB.git)

后续提交流程：

```bash
git add .
git commit -m "your message"
git push
```
