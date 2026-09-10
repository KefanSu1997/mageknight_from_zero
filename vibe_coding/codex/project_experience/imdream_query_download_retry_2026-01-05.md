# Imdream 图像下载 EOF 重试经验

- 时间: 2026-01-05 01:27:19
- 场景: 使用 tools/imdream_query.ps1 下载 image_urls 时，Invoke-WebRequest 偶发 EOF
- 现象: 报错 "Received an unexpected EOF or 0 bytes from the transport stream."
- 处理: 直接重跑同一条 imdream_query.ps1 命令，通常即可成功下载
- 建议: 下载 image_urls 时加 --download-name；若失败先重试，再考虑更换网络或延长间隔
