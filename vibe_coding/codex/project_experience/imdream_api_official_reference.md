# Imdream API 官方文档整理（引用版）

## 接口简介

即梦 4.0 提供文生图、图像编辑与多图组合能力，支持单次输入最多 10 张参考图并进行复合编辑；模型会根据 prompt 进行深度推理，自动适配最优比例与尺寸，最多输出 15 张内容关联图像，支持 4K 超高清输出。

## 接入说明

- SDK 使用说明：参考官方 SDK 文档
- HTTP 方式接入：参考官方 HTTP 请求示例

## 输入图要求

- 格式：仅支持 JPEG、PNG，建议 JPEG
- 文件大小：单张最大 15MB
- 分辨率：最大 4096 × 4096
- 宽高比（宽/高）：[1/3, 3]
- 数量：最多 10 张
- 必须为公网可访问 URL（无需登录/跳转）

## 输出图说明

- 输出图以列表形式返回
- 最大输出数量 = 15 - 输入图片数量
- `image_urls` 链接有效期通常为 24 小时

## 其他说明

- 输出分辨率越大、输出数量越多、输入图数量越多，延迟越高
- 单次调用可能输出多张图片，按输出张数计费
- 默认由 prompt 意图判断输出数量；如对延迟/价格敏感，建议 `force_single`
- 组图建议 prompt 控制在 9 张及以内
- 组图建议显式指定分辨率或 `width/height`，避免分辨率不一致导致报错

## 参数说明（关键字段）

### prompt
- 最长不超过 800 字符
- 支持在 prompt 中直接指定比例
- 除引号外不建议输入特殊符号（如 `$`）

### size（面积）
- 默认值：4194304（2048×2048）
- 取值范围：[1024×1024, 4096×4096]
- 仅传 `size` 时模型智能判断比例
- `size` 与 `width/height` 同时传时优先 `width/height`

### width / height
- 需同时传入才生效
- 宽高乘积在 [1024×1024, 4096×4096]
- 宽高比需在 [min_ratio, max_ratio] 内

### scale
- 默认 0.5
- 取值范围 [0, 1]
- 值越大文本影响越强、参考图影响越弱（精度支持小数点后两位）

### force_single
- 默认 false
- 是否强制生成单图

### min_ratio / max_ratio
- min_ratio 默认 1/3，范围 [1/16, 16)
- max_ratio 默认 3，范围 [1/16, 16)

### seed
- 默认 -1（随机）
- 相同正整数且参数一致时结果高度一致

## 尺寸与比例规则

1. 仅传 `size`（文生图）：模型根据 prompt 意图智能判断宽高比  
2. 图生图：模型结合 prompt 与参考图尺寸智能判断宽高比  

### 推荐宽高
- 1K：1024×1024（1:1）
- 2K：2048×2048（1:1），2304×1728（4:3），2496×1664（3:2），2560×1440（16:9），3024×1296（21:9）
- 4K：4096×4096（1:1），4694×3520（4:3），4992×3328（3:2），5404×3040（16:9），6198×2656（21:9）

## 请求说明

### 接口地址与方法
- 接口地址：`https://visual.volcengineapi.com`
- 请求方式：`POST`
- Content-Type：`application/json`

### 提交任务（Submit）
Query 参数（拼接到 URL）：
- `Action=CVSync2AsyncSubmitTask`
- `Version=2022-08-31`

Header 参数（签名）：
- Region 固定 `cn-north-1`，Service 固定 `cv`
- 需包含 `Host`、`Content-Type`、`X-Date`、`X-Content-Sha256`、`Authorization`

Body 参数：
- `req_key`（必选）：`jimeng_t2i_v40`
- `image_urls`（可选）：0~10 张
- `prompt`（必选）
- `scale` / `force_single` / `min_ratio` / `max_ratio` / `seed`

提交返回关注字段：
- `code`：10000 表示成功
- `data.task_id`
- `request_id`、`time_elapsed`

### 查询任务（GetResult）
Query 参数：
- `Action=CVSync2AsyncGetResult`
- `Version=2022-08-31`

Body 参数：
- `req_key`（必选）：`jimeng_t2i_v40`
- `task_id`（必选）
- `req_json`（可选）：JSON 字符串，示例  
  `{"return_url":true}`  
  可配置 `logo_info` / `aigc_meta`

查询返回关注字段：
- `data.status`：`in_queue` / `generating` / `done` / `not_found` / `expired`
- `data.image_urls` / `data.binary_data_base64`
- 注意：先判断 `code=10000` 再判断 `data.status`

## 错误码（业务）

- 50411：Pre Img Risk Not Pass（输入图审核未过）
- 50511：Post Img Risk Not Pass（输出图审核未过，可重试）
- 50412：Text Risk Not Pass（输入文本审核未过）
- 50512/50413：Post Text Risk Not Pass（输出/敏感词审核未过）
- 50518：Pre Img Risk Not Pass: Copyright
- 50519：Post Img Risk Not Pass: Copyright（可重试）
- 50520：Risk Internal Error（审核服务异常）
- 50521：Antidirt Internal Error（版权词服务异常）
- 50522：Image Copyright Internal Error（版权图服务异常）
- 50429：Request Has Reached API Limit（QPS 超限，可重试）
- 50430：Request Has Reached API Concurrent Limit（并发超限，可重试）
- 50500：Internal Error（内部错误）
- 50501：Internal RPC Error（内部算法错误）

## Python HTTP 示例（官方示例）

```python
import json
import sys
import datetime
import hashlib
import hmac
import requests

method = 'POST'
host = 'visual.volcengineapi.com'
region = 'cn-north-1'
endpoint = 'https://visual.volcengineapi.com'
service = 'cv'

def sign(key, msg):
    return hmac.new(key, msg.encode('utf-8'), hashlib.sha256).digest()

def getSignatureKey(key, dateStamp, regionName, serviceName):
    kDate = sign(key.encode('utf-8'), dateStamp)
    kRegion = sign(kDate, regionName)
    kService = sign(kRegion, serviceName)
    kSigning = sign(kService, 'request')
    return kSigning

def formatQuery(parameters):
    request_parameters_init = ''
    for key in sorted(parameters):
        request_parameters_init += key + '=' + parameters[key] + '&'
    request_parameters = request_parameters_init[:-1]
    return request_parameters

def signV4Request(access_key, secret_key, service, req_query, req_body):
    if access_key is None or secret_key is None:
        print('No access key is available.')
        sys.exit()

    t = datetime.datetime.utcnow()
    current_date = t.strftime('%Y%m%dT%H%M%SZ')
    datestamp = t.strftime('%Y%m%d')
    canonical_uri = '/'
    canonical_querystring = req_query
    signed_headers = 'content-type;host;x-content-sha256;x-date'
    payload_hash = hashlib.sha256(req_body.encode('utf-8')).hexdigest()
    content_type = 'application/json'
    canonical_headers = 'content-type:' + content_type + '\\n' + 'host:' + host + \
        '\\n' + 'x-content-sha256:' + payload_hash + \
        '\\n' + 'x-date:' + current_date + '\\n'
    canonical_request = method + '\\n' + canonical_uri + '\\n' + canonical_querystring + \
        '\\n' + canonical_headers + '\\n' + signed_headers + '\\n' + payload_hash
    algorithm = 'HMAC-SHA256'
    credential_scope = datestamp + '/' + region + '/' + service + '/' + 'request'
    string_to_sign = algorithm + '\\n' + current_date + '\\n' + credential_scope + '\\n' + hashlib.sha256(
        canonical_request.encode('utf-8')).hexdigest()
    signing_key = getSignatureKey(secret_key, datestamp, region, service)
    signature = hmac.new(signing_key, (string_to_sign).encode(
        'utf-8'), hashlib.sha256).hexdigest()
    authorization_header = algorithm + ' ' + 'Credential=' + access_key + '/' + \
        credential_scope + ', ' + 'SignedHeaders=' + \
        signed_headers + ', ' + 'Signature=' + signature
    headers = {'X-Date': current_date,
               'Authorization': authorization_header,
               'X-Content-Sha256': payload_hash,
               'Content-Type': content_type
               }
    request_url = endpoint + '?' + canonical_querystring
    r = requests.post(request_url, headers=headers, data=req_body)
    resp_str = r.text.replace("\\u0026", "&")
    print(resp_str)
```

## 提示词工程（实用建议）

- 内容与美学分离：`主体 + 行为/姿态 + 环境/背景` + `风格, 色彩, 光照, 构图, 质量`
- 指定比例建议优先用 `width/height` 参数
- 需要文字时，用英文双引号包裹内容可提升准确率
- 图像编辑：用“变化动作 + 变化对象 + 变化特征”的清晰指令
