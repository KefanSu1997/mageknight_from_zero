# 魅惑一次折扣、回合资源预期与本地 MCP

魅惑招募减三必须独立于通用折扣，成功招募才消费；失败保持费用、供给、指挥槽和已有折扣状态。验收应使用原部队供给，并依次核对第一次和第二次招募，而不只断言一个折扣字段。

EndTurn 后未使用影响力失效。用例发现影响力4/0不符时，先核对规则和状态时点；同时断言 played:InfluencePool=4 与最终 InfluencePool=0，可证明出牌与回合结束两个阶段都正确。保留原失败证据，仅重跑这1例。622条当前范围断言符合仍有26例partial，不可升格为整卡通过。

Unity MCP v10 的 BridgeControlService 使用 EditorConfigurationCache。自定义菜单直接写 EditorPrefs 后必须 Refresh 缓存，否则启动选择与重载后选择可能不同。手动本地 Stdio 入口复用现有插件的 StartAsync，切换前由插件停掉另一通道；不需安装包、修改缓存文件或启用旧多智能体启动链。

本地TCP采用现有Unity MCP协议（WELCOME行、8字节大端长度、JSON type/params），客户端需有连接/读取超时和响应体大小上限，禁止旧脚本无限阻塞。HTTP实例列表为0不代表Unity离线，检查该Unity进程实际本地监听。最终仍要通过Unity MCP读取Console两次零error。

详细证据：vibe_coding/codex/worklog_2026-09-15_charm_scene_completion.md。
