# 即梦 API 脚本调用经验

## 背景
- 需求：在 CLI 中批量生成美术占位图，需通过火山视觉服务的 Jimeng 模型。
- 限制：接口要求 V4 签名算法（Action/Version/Region 固定），返回异步任务，需要二次查询并保存 base64 数据。

## 签名要点
1. **参数拼装**：公共参数 `Action=CVSync2AsyncSubmitTask` 与 `Version=2022-08-31` 放在 URL query，主体 JSON 至少包含 `req_key`、`prompt`，按需追加 `image_urls`、`width/height`、`size`、`scale`、`force_single`、`min_ratio`、`max_ratio`。
2. **签名字段**：Canonical Request 必须按顺序加入 `content-type`, `host`, `x-content-sha256`, `x-date`，值使用全小写；`Content-Type` 推荐 `application/json; charset=utf-8`。
3. **Secret 不解码**：实测火山后台发放的 `IMDREAM_SECRET_KEY` 已是原始字符串（看似 Base64），直接使用即可；若确需解码，可在环境变量中显式设 `IMDREAM_SECRET_KEY_DECODE_BASE64=1`。
4. **帮助脚本**：`tools/imdream_sign_helper.py` 复用官方 V4 算法，支持可选 Base64 解码、Session Token、调试 canonical 字符串。

## 调用流程
1. `tools/generate_imdream_image.sh`：
   - 校验 `curl/jq` 依赖，自动加载 `.env`。
   - 支持 `--ref <url>`（最多 10 次）、`--width/--height`、`--size`、`--scale`、`--force-single`、`--min-ratio`、`--max-ratio` 等参数；也可通过 `IMDREAM_IMAGE_REFS=URL1,URL2` 环境变量批量注入参考图。
   - 通过签名脚本获取 `Authorization` 等头，提交任务，输出 `task_id`。
2. `tools/imdream_query.sh`：
   - 同样签名后查询结果，默认写入 `AutomationOutputs/Imdream/`。
   - 当 `data.binary_data_base64` 返回图片时自动尝试保存，多图按序号命名。
3. 若状态为 `in_queue`/`running`，循环调用查询脚本直到 `status` 变为 `success` 或其他终态。

## 实践提示
- `binary_data_base64` 以 `/9j/` 开头通常为 JPEG，可用 `base64 --decode` 直接落盘。
- 场景提示词包含中文时需保证 shell & JSON UTF-8；`jq -nc --arg prompt` 会自动处理。
- 若接口增加 Session Token（临时 AK/SK），在 `.env` 添加 `IMDREAM_SESSION_TOKEN` 即可，由签名脚本自动带入。
- 参考图总数不得超过 10 张；若提示词同时需要固定长宽比，可同时指定 `--width/--height` 或 `--size`，避免模型自动判定导致接口报错。

## 常见错误
- **SignatureDoesNotMatch**：多因 service 设错（应为 `cv`）、Content-Type 不匹配或额外手动 Base64 解码 Secret。
- **ServiceNotFound**：Action/Service/Version 拼写错误，或错误地区。
- **在队列中**：生成任务尚未完成，需间隔数秒继续查询。
