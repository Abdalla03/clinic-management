#!/bin/sh
set -e

# Write the runtime API-URL override consumed by src/lib/api/client.ts.
# config.js is referenced from index.html and loaded before the app bundle.
if [ -n "$WEB_API_URL" ]; then
  printf "window.__CLINIC_API_URL__ = '%s';\n" "$WEB_API_URL" > /usr/share/nginx/html/config.js
else
  echo "window.__CLINIC_API_URL__ = '';" > /usr/share/nginx/html/config.js
fi

# Demo sign-in hints are shown only when explicitly enabled (never in production).
if [ "$WEB_SHOW_DEMO_LOGINS" = "true" ]; then
  echo "window.__CLINIC_SHOW_DEMO__ = 'true';" >> /usr/share/nginx/html/config.js
fi

exec "$@"