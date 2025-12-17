#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE' >&2
Usage:
  generate_imdream_image.sh "<prompt>" [legacy_count] [options]

Options:
  --ref <url>            追加一张参考图，可多次指定（最多 10 张）
  --width <int>          指定生成图宽度（需与 --height 同时使用）
  --height <int>         指定生成图高度（需与 --width 同时使用）
  --size <int>           指定生成图面积（宽高未指定时生效）
  --scale <float>        调整文本提示影响力，范围 [0,1]
  --force-single         强制只返回 1 张图
  --min-ratio <float>    生图宽高比下限
  --max-ratio <float>    生图宽高比上限
  -h, --help             显示此帮助

Environment variables:
  IMDREAM_ACCESS_KEY       必填，VolcEngine 的 Access Key
  IMDREAM_SECRET_KEY       必填，VolcEngine 的 Secret Key
  IMDREAM_MODEL            可选，req_key，默认为 jimeng_t2i_v40
  IMDREAM_IMAGE_REFS       可选，逗号分隔的参考图 URL 列表
USAGE
}

# 检查依赖
if ! command -v curl >/dev/null 2>&1; then
  echo "generate_imdream_image: 需要 curl，请先安装。" >&2
  exit 1
fi
if ! command -v jq >/dev/null 2>&1; then
  echo "generate_imdream_image: 需要 jq，请先安装。" >&2
  exit 1
fi

# 参数检查
if [ $# -lt 1 ]; then
  usage
  exit 1
fi

# 尝试从 .env 加载 AK/SK
if [ -z "${IMDREAM_ACCESS_KEY:-}" ] || [ -z "${IMDREAM_SECRET_KEY:-}" ]; then
  if [ -f ".env" ]; then
    set -a
    source ".env"
    set +a
  fi
fi

if [ -z "${IMDREAM_ACCESS_KEY:-}" ] || [ -z "${IMDREAM_SECRET_KEY:-}" ]; then
  echo "generate_imdream_image: 请先设置 IMDREAM_ACCESS_KEY 和 IMDREAM_SECRET_KEY。" >&2
  exit 1
fi

PROMPT=""
LEGACY_COUNT=""
MODEL=${IMDREAM_MODEL:-jimeng_t2i_v40}
SIZE=""
WIDTH=""
HEIGHT=""
SCALE=""
FORCE_SINGLE=""
MIN_RATIO=""
MAX_RATIO=""
declare -a REF_IMAGES=()

parse_number() {
  local label=$1
  local value=$2
  local pattern=$3
  if ! [[ $value =~ $pattern ]]; then
    echo "generate_imdream_image: $label 参数格式非法：$value" >&2
    exit 1
  fi
}

while [ $# -gt 0 ]; do
  case "$1" in
    --ref)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --ref 需要一个 URL 参数" >&2
        exit 1
      fi
      REF_IMAGES+=("$2")
      shift 2
      ;;
    --width)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --width 需要整数参数" >&2
        exit 1
      fi
      parse_number "--width" "$2" '^[0-9]+$'
      WIDTH=$2
      shift 2
      ;;
    --height)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --height 需要整数参数" >&2
        exit 1
      fi
      parse_number "--height" "$2" '^[0-9]+$'
      HEIGHT=$2
      shift 2
      ;;
    --size)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --size 需要整数参数" >&2
        exit 1
      fi
      parse_number "--size" "$2" '^[0-9]+$'
      SIZE=$2
      shift 2
      ;;
    --scale)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --scale 需要浮点数参数" >&2
        exit 1
      fi
      parse_number "--scale" "$2" '^[0-9]+(\.[0-9]+)?$'
      SCALE=$2
      shift 2
      ;;
    --force-single)
      FORCE_SINGLE="true"
      shift
      ;;
    --min-ratio)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --min-ratio 需要浮点数参数" >&2
        exit 1
      fi
      parse_number "--min-ratio" "$2" '^[0-9]+(\.[0-9]+)?$'
      MIN_RATIO=$2
      shift 2
      ;;
    --max-ratio)
      if [ $# -lt 2 ]; then
        echo "generate_imdream_image: --max-ratio 需要浮点数参数" >&2
        exit 1
      fi
      parse_number "--max-ratio" "$2" '^[0-9]+(\.[0-9]+)?$'
      MAX_RATIO=$2
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      break
      ;;
    -*)
      echo "generate_imdream_image: 未知参数 $1" >&2
      usage
      exit 1
      ;;
    *)
      if [ -z "$PROMPT" ]; then
        PROMPT=$1
        shift
      elif [ -z "$LEGACY_COUNT" ]; then
        LEGACY_COUNT=$1
        shift
      else
        echo "generate_imdream_image: 多余的位置参数：$1" >&2
        usage
        exit 1
      fi
      ;;
  esac
done

if [ -z "$PROMPT" ]; then
  echo "generate_imdream_image: 缺少必填的 prompt 参数" >&2
  usage
  exit 1
fi

if { [ -n "$WIDTH" ] && [ -z "$HEIGHT" ]; } || { [ -n "$HEIGHT" ] && [ -z "$WIDTH" ]; }; then
  echo "generate_imdream_image: --width 与 --height 必须同时提供" >&2
  exit 1
fi

if [ -n "${LEGACY_COUNT:-}" ]; then
  if [[ ! "$LEGACY_COUNT" =~ ^[0-9]+$ ]]; then
    echo "generate_imdream_image: legacy_count 仅接受整数，当前值为 $LEGACY_COUNT，将忽略。" >&2
  elif [ "$LEGACY_COUNT" -gt 1 ]; then
    echo "generate_imdream_image: 警告：即梦 4.0 API 不再支持直接指定图片数量，忽略 legacy_count=$LEGACY_COUNT" >&2
  fi
fi

if [ -n "${IMDREAM_IMAGE_REFS:-}" ]; then
  IFS=',' read -ra env_refs <<<"${IMDREAM_IMAGE_REFS}"
  for ref in "${env_refs[@]}"; do
    if [ -n "$ref" ]; then
      REF_IMAGES+=("$ref")
    fi
  done
fi

if [ "${#REF_IMAGES[@]}" -gt 10 ]; then
  echo "generate_imdream_image: 参考图数量超过 10 张，请精简后再试" >&2
  exit 1
fi

# 公共 API 参数
ACTION="CVSync2AsyncSubmitTask"
VERSION="2022-08-31"
REGION="cn-north-1"
SERVICE="cv"
HOST="visual.volcengineapi.com"
ENDPOINT="/"  # 路径
CONTENT_TYPE="application/json; charset=utf-8"

# 构造 URL（只带公共参数在 query 部分）
REQUEST_URL="https://${HOST}?Action=${ACTION}&Version=${VERSION}"

# 构造参考图 JSON 数组
refs_json="[]"
if [ "${#REF_IMAGES[@]}" -gt 0 ]; then
  refs_json=$(printf '%s\n' "${REF_IMAGES[@]}" | jq -R . | jq -s .)
fi

# 构造 body JSON（业务参数）
jq_args=(
  --arg req_key "$MODEL"
  --arg prompt "$PROMPT"
  --argjson image_urls "$refs_json"
)
jq_expr='{req_key: $req_key, prompt: $prompt} + (if ($image_urls | length) > 0 then {image_urls: $image_urls} else {} end)'

if [ -n "$SIZE" ]; then
  jq_args+=(--argjson size "$SIZE")
  jq_expr="$jq_expr + {size: \$size}"
fi
if [ -n "$WIDTH" ]; then
  jq_args+=(--argjson width "$WIDTH")
  jq_expr="$jq_expr + {width: \$width}"
fi
if [ -n "$HEIGHT" ]; then
  jq_args+=(--argjson height "$HEIGHT")
  jq_expr="$jq_expr + {height: \$height}"
fi
if [ -n "$SCALE" ]; then
  jq_args+=(--argjson scale "$SCALE")
  jq_expr="$jq_expr + {scale: \$scale}"
fi
if [ -n "$FORCE_SINGLE" ]; then
  jq_args+=(--argjson force_single "$FORCE_SINGLE")
  jq_expr="$jq_expr + {force_single: \$force_single}"
fi
if [ -n "$MIN_RATIO" ]; then
  jq_args+=(--argjson min_ratio "$MIN_RATIO")
  jq_expr="$jq_expr + {min_ratio: \$min_ratio}"
fi
if [ -n "$MAX_RATIO" ]; then
  jq_args+=(--argjson max_ratio "$MAX_RATIO")
  jq_expr="$jq_expr + {max_ratio: \$max_ratio}"
fi

body=$(jq -nc "${jq_args[@]}" "$jq_expr")

# 通过 Python 辅助脚本计算签名
sign_json=$(printf '%s' "$body" | tools/imdream_sign_helper.py \
  --action "$ACTION" \
  --version "$VERSION" \
  --region "$REGION" \
  --service "$SERVICE" \
  --host "$HOST" \
  --method "POST" \
  --path "$ENDPOINT" \
  --content-type "$CONTENT_TYPE")

if echo "$sign_json" | jq -e '.error?' >/dev/null; then
  echo "generate_imdream_image: 签名计算失败 -> $(echo "$sign_json" | jq -r '.error')" >&2
  exit 1
fi

Authorization_Header=$(echo "$sign_json" | jq -r '.authorization')
XDATE=$(echo "$sign_json" | jq -r '.x_date')
BODY_HASH=$(echo "$sign_json" | jq -r '.x_content_sha256')
SESSION_TOKEN=$(echo "$sign_json" | jq -r '.x_security_token // empty')

# 发送请求
curl_args=(
  curl -sS -X POST "${REQUEST_URL}"
  -H "Content-Type: ${CONTENT_TYPE}"
  -H "Host: ${HOST}"
  -H "X-Date: ${XDATE}"
  -H "X-Content-Sha256: ${BODY_HASH}"
  -H "Authorization: ${Authorization_Header}"
)

if [ -n "$SESSION_TOKEN" ]; then
  curl_args+=( -H "X-Security-Token: ${SESSION_TOKEN}" )
fi

curl_args+=( -d "$body" )

response=$("${curl_args[@]}")

# 解析错误或返回结果
if echo "$response" | jq -e '.code != 10000' >/dev/null; then
  err_msg=$(echo "$response" | jq -r '.message // (.error.message // "unknown error")')
  echo "generate_imdream_image: API 返回错误 -> $err_msg" >&2
  echo "generate_imdream_image: 原始响应 -> $response" >&2
  exit 1
fi

# 返回 task_id 或者直接返回 image_urls 可能要调用查询接口
# 这里我直接回显 data.task_id
echo "$response" | jq -r '.data.task_id'
