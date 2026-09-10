# feat(adventure): archive Unity 6 scenes and canonical Mage Knight cards

## Why

历史美术/UI分支保留着未完成的本地合并，Unity升级、四个规则场景和近期原卡接入也没有完整远端存档。需要建立可恢复的代码、美术与验收基线。

## What

- 完成现有无冲突合并，保存当前Unity 6000.6.0f1工程状态。
- 归档抽牌桌面、战斗/探索/招募/旅程场景、可复用角色地点配置与卡牌目标驱动交互。
- 使用既有行进、耐力、承诺、决心、狂怒的CardSO、卡面和ActionSystem，保留基础选项与强化费用。
- 归档工作记录、迁移经验及现有场景验收证据，排除运行轨迹和本地缓存。

## How to test

使用Unity Hub打开项目，不使用batchmode，不安装或卸载Packages。

- `Tools/Mage Knight/Adventure/Run All EditMode Tests`：91项通过。
- `Tools/Mage Knight/Adventure/Run All Four`：114次按钮操作、477个断言、118张截图通过。
- Unity Console连续两次errors读取为0，记录位于AutomationOutputs/OfficialCards/20260910。

## Risks

这是历史成果的大范围存档，包含Unity迁移及较多原有美术文件。5张已接入行动牌以外的全库规则完整性不在此存档验收结论内；后续独立场景审计会保留失败和待联验项。恢复项目需要Git LFS。

## Related issues

无独立issue；对应用户要求“合并push远端存档”。
