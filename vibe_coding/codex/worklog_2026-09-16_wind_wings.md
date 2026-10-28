# 清风之翼 / 黑夜之翼：后台先行，场景定案

原始素材：`Assets/GameData/cards/magic_en_023.png`，原 JSON 和既有 WindWingsEffect。基础现在验证逐格已揭示路径、1—5 点移动费用与安全落点，并实际改变位置；强化按明确目标收取 0/1/3/6 点移动费用，拒绝重复、越界和奥术免疫目标，战斗结束只清理本效果新增的不攻击标记。

复用 CardTravelActions 的安全落点判断，未安装依赖或改变 Unity 包。场景显示真实路径、费用、手牌区和后续承伤；保留尚未覆盖的完整回合/场景条件，不把 partial 当整卡通过。

- 后台：`AutomationOutputs/RulesState/20260915_235711_75dc8c1b`，22 项本卡测试加 6 项共享安全判断回归，28/28，测试耗时 0.404 秒。
- 场景：`AutomationOutputs/AllOriginalCards/20260916_000355_b280d65c/magic`，13 例、47 次按钮操作，239 个效果断言和 158 个 UI 断言全部符合；9 partial、4 passed、0 failed。
- 已查看本地截图 `011_magic_023_03_execute.png`（移动 5 格与扣费）、`041_magic_023_10_execute.png`（战斗失效后实际承伤）。23 张截图仅本地保留，未上传。
- Unity 编译完成后与场景完成后分别 Console 两次 0 error。最终失败筛选移除本批 13 条日常检查，完整回归库保留。

累计定义 715 条，跨批次历史汇总为 172 passed / 532 partial / 11 failed。这不是全库重新认证；11 个历史失败含无效牌面记录 8 个，有效原卡已知失败剩正义之剑、星辰之弓、法术书。532 个 partial 的剩余规则仍须补齐。
