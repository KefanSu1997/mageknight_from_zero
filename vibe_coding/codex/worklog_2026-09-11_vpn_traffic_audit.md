# VPN 流量核查与后续存档约束

用户反馈 VPN 流量耗尽。本轮暂停测试和远端推送，仅读取本地记录与进程，无联网调查、无网络设置修改。

已记录的四批 Git LFS 上传：首次存档 346 MB；元素触发批次 275 MB；攻城/分次格挡批次 316 MB；英雄技能批次 206 MB。小计约 1,143 MB。这只是已找到的上传载荷小计，不是 VPN 账单总量；其他批次、失败重试、协议开销和模型对话流量未核算。后续推送明确通过命令级 http.proxy=http://127.0.0.1:7890 使用现有代理。

来源：worklog_2026-09-10_archive_and_card_verification.md、worklog_2026-09-10_card_elemental_triggers.md、worklog_2026-09-10_card_siege_split_blocks.md、worklog_2026-09-10_hero_skill_activation.md；项目外 archive_20260910 下两份 LFS progress log。

检查时未发现 git、git-lfs、ssh 或 Unity 进程。未终止 VPN 进程。无法仅凭这些记录认定全部 VPN 配额由上述上传耗尽。

后续约束：暂停远端推送；截图和完整验收证据保留本地，继续本地验证不自动触发图片上传。恢复远端存档前先计算待传 LFS 对象大小并向用户说明；优先代码和精简文字报告，不再按每批完整截图自动上传。不能用普通 git push 绕过此约束，因为未推送历史中的 LFS 对象也可能被自动上传。不得删除已存在的测试证据或重写历史以掩盖流量。