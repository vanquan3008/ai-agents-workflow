#!/bin/sh
# Sinh cấu hình runtime cho dashboard từ biến môi trường DEVAGENT_* lúc container khởi động.
# nginx phục vụ file này tại /config.js (xem nginx.conf). Không đặt secret vào các biến này.
set -eu

# Thoát ký tự \ và " để giá trị nằm an toàn trong chuỗi JSON.
json_escape() {
  printf '%s' "$1" | sed 's/[\\"]/\\&/g'
}

OUT=/tmp/devagent-config.js
ENVIRONMENT=$(json_escape "${DEVAGENT_ENVIRONMENT:-Production}")
API_BASE_URL=$(json_escape "${DEVAGENT_API_BASE_URL:-}")
SIGNALR_HUB_PATH=$(json_escape "${DEVAGENT_SIGNALR_HUB_PATH:-/hubs/tasks}")
APP_NAME=$(json_escape "${DEVAGENT_APP_NAME:-DevAgent}")

printf 'window.__DEVAGENT_CONFIG__ = {\n  "ENVIRONMENT": "%s",\n  "API_BASE_URL": "%s",\n  "SIGNALR_HUB_PATH": "%s",\n  "APP_NAME": "%s"\n};\n' \
  "$ENVIRONMENT" "$API_BASE_URL" "$SIGNALR_HUB_PATH" "$APP_NAME" > "$OUT"

echo "devagent: đã sinh $OUT (ENVIRONMENT=${DEVAGENT_ENVIRONMENT:-Production})"
