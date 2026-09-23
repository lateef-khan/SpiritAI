#!/usr/bin/env bash
# Makes (or updates) what Spirit needs in Chatwoot: one API inbox whose webhook calls Spirit,
# and one agent bot whose token Spirit posts the AI's messages with. The bot is connected to the
# inbox, so Chatwoot treats every conversation there as the bot's until a person takes it: new
# ones start pending, and a resolved one reopens as pending, not open. Safe to run again: both
# are found by name, and only the webhook URL and the connection are set on a second run.
#
# Needs, in the environment or chatwoot/.env:
#   CHATWOOT_ADMIN_TOKEN  an administrator's access token (Profile settings > Access token)
#   CHATWOOT_ACCOUNT_ID   the account id from the dashboard URL (/app/accounts/<id>/...)
#   SPIRIT_WEBHOOK_URL    where Chatwoot sends inbox events, e.g. https://spirit.example.com/chatwoot/webhook
# Optional:
#   CHATWOOT_URL          defaults to http://localhost:$CHATWOOT_PORT
#
# Prints Spirit's settings. They include secrets: put them in user secrets or the host's
# environment, never in a committed file.
set -euo pipefail

inbox_name="Spirit"
bot_name="Spirit AI"

# chatwoot/.env is not sourced: its values are not shell-quoted (MAILER_SENDER_EMAIL has a <).
env_file="$(dirname "$0")/../.env"
from_env_file() {
    [ -f "$env_file" ] && grep -oP "^$1=\K.*" "$env_file" | tail -1 || true
}
for key in CHATWOOT_ADMIN_TOKEN CHATWOOT_ACCOUNT_ID SPIRIT_WEBHOOK_URL CHATWOOT_URL CHATWOOT_PORT; do
    [ -z "${!key:-}" ] && printf -v "$key" '%s' "$(from_env_file "$key")"
done

: "${CHATWOOT_ADMIN_TOKEN:?set CHATWOOT_ADMIN_TOKEN}"
: "${CHATWOOT_ACCOUNT_ID:?set CHATWOOT_ACCOUNT_ID}"
: "${SPIRIT_WEBHOOK_URL:?set SPIRIT_WEBHOOK_URL}"
base="${CHATWOOT_URL:-http://localhost:${CHATWOOT_PORT:-53000}}"
api="$base/api/v1/accounts/$CHATWOOT_ACCOUNT_ID"

call() {
    local method="$1" path="$2" body="${3:-}"
    local args=(--silent --show-error --fail-with-body --request "$method"
        --header "api_access_token: $CHATWOOT_ADMIN_TOKEN"
        --header "Content-Type: application/json")
    [ -n "$body" ] && args+=(--data "$body")
    curl "${args[@]}" "$api$path"
}

inbox_id="$(call GET /inboxes | jq --arg name "$inbox_name" \
    '[.payload[] | select(.name == $name and .channel_type == "Channel::Api")][0].id // empty')"

if [ -z "$inbox_id" ]; then
    inbox_id="$(call POST /inboxes "$(jq -n --arg name "$inbox_name" --arg url "$SPIRIT_WEBHOOK_URL" \
        '{name: $name, channel: {type: "api", webhook_url: $url}}')" | jq '.id')"
    echo "Made the API inbox '$inbox_name' (id $inbox_id)." >&2
else
    call PATCH "/inboxes/$inbox_id" "$(jq -n --arg url "$SPIRIT_WEBHOOK_URL" '{channel: {webhook_url: $url}}')" >/dev/null
    echo "Found the API inbox '$inbox_name' (id $inbox_id); set its webhook URL." >&2
fi

inbox="$(call GET "/inboxes/$inbox_id")"

bot_id="$(call GET /agent_bots | jq --arg name "$bot_name" \
    '[.[] | select(.name == $name and .system_bot == false)][0].id // empty')"

if [ -z "$bot_id" ]; then
    # No outgoing_url: Spirit never takes the bot's webhook, it only posts with its token.
    bot_id="$(call POST /agent_bots "$(jq -n --arg name "$bot_name" \
        '{name: $name, description: "Posts the Spirit AI answers into the Spirit inbox."}')" | jq '.id')"
    echo "Made the agent bot '$bot_name' (id $bot_id)." >&2
else
    echo "Found the agent bot '$bot_name' (id $bot_id)." >&2
fi

# The bot has no outgoing_url, so connecting it sends no webhooks; it only makes Chatwoot treat
# the inbox as a bot's (Inbox#active_bot?).
call POST "/inboxes/$inbox_id/set_agent_bot" "$(jq -n --argjson bot "$bot_id" '{agent_bot: $bot}')" >/dev/null
echo "Connected the agent bot to the inbox." >&2

bot="$(call GET "/agent_bots/$bot_id")"

cat <<EOF

Spirit settings:
Chatwoot__BaseUrl=$base
Chatwoot__AccountId=$CHATWOOT_ACCOUNT_ID
Chatwoot__InboxId=$inbox_id
Chatwoot__InboxIdentifier=$(jq -r '.inbox_identifier' <<<"$inbox")
Chatwoot__WebhookSecret=$(jq -r '.secret' <<<"$inbox")
Chatwoot__BotToken=$(jq -r '.access_token' <<<"$bot")
EOF
