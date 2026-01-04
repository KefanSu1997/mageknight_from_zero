# 工作记录：Imdream PS1 文生图/图生图流程

- 时间: 2026-01-05 01:27:59
- 目标: 跑通 ps1 文生图/图生图标准流程并更新 AGENTS.md
- 执行过程:
  - 文生图提交: tools/imdream_submit.ps1 -> 任务 ID 3267885436696303878
  - 文生图下载: tools/imdream_query.ps1 --download-name t2i_cat -> AutomationOutputs/Imdream/t2i_cat_0.png
  - 上传参考图: tools/imdream_upload_ref.ps1 -> tmpfiles URL
  - 图生图提交: tools/imdream_submit.ps1 -ImageUrls <url> -> 任务 ID 16769887199142355142
  - 图生图下载: tools/imdream_query.ps1 --download-name i2i_cat（首次 EOF，重试成功）
- 修改内容:
  - 更新 `AGENTS.md`：将即梦流程改为 .ps1 版命令与说明
  - 新增经验记录：`vibe_coding/codex/project_experience/imdream_query_download_retry_2026-01-05.md`
- 结果: 文生图与图生图流程均跑通
