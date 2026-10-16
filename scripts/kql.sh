#!/usr/bin/env bash
# Run a KQL query against the app's Log Analytics workspace (Application Insights data).
#
#   scripts/kql.sh "AppRequests | where Success == false | take 20"
#   scripts/kql.sh "AppExceptions | take 5" P7D        # optional ISO-8601 timespan (default P1D)
#
# Auth (Entra only, no API keys):
#   - If AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET are set (Claude Code cloud
#     environments), uses the read-only service principal via client credentials.
#   - Otherwise uses the signed-in Azure CLI (`az login`) on the local machine.
#
# Prints the result table as tab-separated rows with a header (raw JSON if jq isn't installed).
set -euo pipefail

QUERY="${1:?usage: scripts/kql.sh \"<KQL query>\" [timespan, default P1D]}"
TIMESPAN="${2:-P1D}"
# Workspace ID of law-recipehelper-germanycentral -- an identifier, not a secret.
WORKSPACE_ID="${LOG_ANALYTICS_WORKSPACE_ID:-08fe8a78-2078-4d77-9527-2d81121b48cb}"
RESOURCE="https://api.loganalytics.io"

if [ -n "${AZURE_CLIENT_SECRET:-}" ]; then
    TOKEN=$(curl -sS --fail-with-body \
        "https://login.microsoftonline.com/${AZURE_TENANT_ID:?AZURE_TENANT_ID not set}/oauth2/v2.0/token" \
        --data-urlencode "grant_type=client_credentials" \
        --data-urlencode "client_id=${AZURE_CLIENT_ID:?AZURE_CLIENT_ID not set}" \
        --data-urlencode "client_secret=${AZURE_CLIENT_SECRET}" \
        --data-urlencode "scope=${RESOURCE}/.default" \
        | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
else
    TOKEN=$(az account get-access-token --resource "$RESOURCE" --query accessToken -o tsv)
fi
[ -n "$TOKEN" ] || { echo "Failed to get an access token" >&2; exit 1; }

# JSON-escape the query (backslashes, quotes, newlines/tabs) without depending on jq.
json_escape() {
    local s="$1"
    s="${s//\\/\\\\}"; s="${s//\"/\\\"}"
    s="${s//$'\n'/\\n}"; s="${s//$'\r'/}"; s="${s//$'\t'/\\t}"
    printf '%s' "$s"
}
BODY="{\"query\":\"$(json_escape "$QUERY")\",\"timespan\":\"$TIMESPAN\"}"

RESPONSE=$(curl -sS --fail-with-body -X POST \
    "${RESOURCE}/v1/workspaces/${WORKSPACE_ID}/query" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    --data "$BODY")

if command -v jq >/dev/null 2>&1; then
    printf '%s' "$RESPONSE" | jq -r '.tables[0] | ([.columns[].name] | @tsv), (.rows[] | map(tostring) | @tsv)'
else
    printf '%s\n' "$RESPONSE"
fi
