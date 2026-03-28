#!/bin/sh
set -eu

cat <<EOF >/usr/share/nginx/html/config.js
window.LANGOOSE_CONFIG = {
  apiBaseUrl: "${LANGOOSE_API_BASE_URL}"
};
EOF
