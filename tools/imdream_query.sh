#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE' >&2
Usage: imdream_query.sh <task_id> [output_dir]

Environment variables:
  IMDREAM_ACCESS_KEY       必填，VolcEngine Access Key
  IMDREAM_SECRET_KEY       必填，VolcEngine Secret Key（原始字符串，若需 Base64 解码请设置 IMDREAM_SECRET_KEY_DECODE_BASE64=1）
  IMDREAM_MODEL            可选，模型名称，默认 jimeng_t2i_v40
USAGE
}

if ! command -v curl >/dev/null 2>&1; then
  echo "imdream_query: 需要 curl，请先安装。" >&2
  exit 1
fi
if ! command -v jq >/dev/null 2>&1; then
  echo "imdream_query: 需要 jq，请先安装。" >&2
  exit 1
fi

if [ $# -lt 1 ]; then
  usage
  exit 1
fi

if [ -f ".env" ]; then
  set -a
  source ".env"
  set +a
fi

if [ -z "${IMDREAM_ACCESS_KEY:-}" ] || [ -z "${IMDREAM_SECRET_KEY:-}" ]; then
  echo "imdream_query: 请先设置 IMDREAM_ACCESS_KEY 和 IMDREAM_SECRET_KEY。" >&2
  exit 1
fi

TASK_ID=$1
OUTPUT_DIR=${2:-AutomationOutputs/Imdream}
MODEL=${IMDREAM_MODEL:-jimeng_t2i_v40}

ACTION="CVSync2AsyncGetResult"
VERSION="2022-08-31"
REGION="cn-north-1"
SERVICE="cv"
HOST="visual.volcengineapi.com"
ENDPOINT="/"
CONTENT_TYPE="application/json; charset=utf-8"

REQUEST_URL="https://${HOST}?Action=${ACTION}&Version=${VERSION}"

body=$(jq -nc \
  --arg req_key "$MODEL" \
  --arg task_id "$TASK_ID" \
  '{req_key: $req_key, task_id: $task_id, req_json: {return_url: true}}')

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
  echo "imdream_query: 签名计算失败 -> $(echo "$sign_json" | jq -r '.error')" >&2
  exit 1
fi

Authorization_Header=$(echo "$sign_json" | jq -r '.authorization')
XDATE=$(echo "$sign_json" | jq -r '.x_date')
BODY_HASH=$(echo "$sign_json" | jq -r '.x_content_sha256')
SESSION_TOKEN=$(echo "$sign_json" | jq -r '.x_security_token // empty')

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

echo "$response" | jq -r '.'

mapfile -t images < <(echo "$response" | jq -r '.data.binary_data_base64[]?')

if [ "${#images[@]}" -gt 0 ]; then
  mkdir -p "$OUTPUT_DIR"
  for idx in "${!images[@]}"; do
    b64=${images[$idx]}
    [ -z "$b64" ] && continue
    ext="png"
    if [[ $b64 == /9j/* ]]; then
      ext="jpg"
    elif [[ $b64 == iVBORw0KGgo* ]]; then
      ext="png"
    fi
    filepath="${OUTPUT_DIR}/${TASK_ID}_${idx}.${ext}"
    if ! printf '%s' "$b64" | base64 --decode > "$filepath" 2>/dev/null; then
      printf '%s' "$b64" | base64 -d > "$filepath"
    fi
    echo "imdream_query: 已保存图片 -> $filepath" >&2
  done
fi
