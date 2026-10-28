# RSIGame round 4 — 原敌人插图细节

- 对应原始 Editor call `0053_151fc672`，唯一修复项 I1：中央兽人骷髅战士插图软化。request.images 为空；原始提示引用 image_04，但本 call 没有附带图像。未重放游戏。
- 已实际查看 `Assets/GameData/cards/monster (32).jpg`：512×512 的原始印刷扫描本身存在软化。`resources/images/monster/monster (32).jpg` 与其 SHA256 同为 `F8E8E5B30B1B963133B020C493D3D23A260D5D719BBFF57C91003320F593D720`，并非更清楚的替代源。Patrol.asset 的 monster_032 绑定此素材；原 JSON 保留原名与规则。
- 上轮已关闭敌人图压缩、mipmap，并使用较大 UV 区域，本轮不重复修改导入参数或裁剪。
- `PveBattleController.DrawEnemyCardArtwork` 给大插图接入 `Resources/UI/EnemyIllustrationDetail.shader`，以原纹理 texel 为单位做受限 unsharp mask，提高现有细节的局部反差。颜色增量受限并限制到邻域范围，避免明显光晕；支持UI颜色、模板及裁剪。每个控制器复用一个材质，销毁时释放。shader不可用则继续显示原图。
- 原印刷token、原图文件及GUID、228高主插图、敌卡大小、名称、攻击/护甲/名望/能力、目标条、正式规则及1920×1080参考画布均保持。没有增绘或猜测角色细节，无法恢复源图里不存在的高频信息。
- 源码静态读回确认接入路径与资源路径匹配、GUID唯一；未运行Unity、导入、编译门禁或验收harness。主代理独占运行。待其确认shader导入/编译、加载候选程序集及一/二/三敌实际1920×1080画面，比较面部/装备轮廓、是否过锐化、目标选择及击败变色。源码处理不等同于运行视觉通过。
