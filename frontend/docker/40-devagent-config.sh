#!/bin/sh
# Renders the dashboard runtime configuration from DEVAGENT_* variables when the container starts.
# nginx serves it at /config.js (see nginx.conf). Never put secrets in these variables.
set -eu

# Escape \ and " so values stay valid inside JSON strings.
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

echo "devagent: generated $OUT (ENVIRONMENT=${DEVAGENT_ENVIRONMENT:-Production})"
