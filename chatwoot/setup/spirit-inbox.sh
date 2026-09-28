#!/usr/bin/env bash

set -euo pipefail

env="${1:-dev}"
secrets_dir="$(cd "$(dirname "$0")/../../secrets" && pwd)"
secrets_file="$secrets_dir/$env.env"

inbox_name="Spirit Chat/Phone"
bot_name="Spirit AI"

# The secrets file is not sourced: its values are not shell-quoted.
read_secret() {
    grep -m1 "^$1=" "$secrets_file" | cut -d= -f2- || true
}

if [ ! -f "$secrets_file" ]; then
    echo "$secrets_file is missing. Run: just secrets init $env" >&2
    exit 1
fi

admin_token="$(read_secret CHATWOOT_ADMIN_TOKEN)"
account_id="$(read_secret Chatwoot__AccountId)"
base="$(read_secret Chatwoot__BaseUrl)"
service_token="$(read_secret Chatwoot__ServiceToken)"

: "${admin_token:?set CHATWOOT_ADMIN_TOKEN in secrets/$env.env}"
: "${account_id:?set Chatwoot__AccountId in secrets/$env.env}"
: "${base:?set Chatwoot__BaseUrl in secrets/$env.env}"
api="$base/api/v1/accounts/$account_id"

call() {
    local method="$1" path="$2" body="${3:-}"
    local args=(--silent --show-error --fail-with-body --request "$method"
        --header "api_access_token: $admin_token"
        --header "Content-Type: application/json")
    [ -n "$body" ] && args+=(--data "$body")
    curl "${args[@]}" "$api$path"
}

inbox_id="$(call GET /inboxes | jq --arg name "$inbox_name" \
    '[.payload[] | select(.name == $name and .channel_type == "Channel::Api")][0].id // empty')"

# The inbox has no webhook: the widget hears Chatwoot's socket, and the AI catches up from
# Chatwoot on its next turn. Leave the inbox's out-of-office message empty too: the handoff
# skill says what to tell a person while the office is closed, and Chatwoot's message would
# say it a second time.
if [ -z "$inbox_id" ]; then
    inbox_id="$(call POST /inboxes "$(jq -n --arg name "$inbox_name" \
        '{name: $name, channel: {type: "api", webhook_url: ""}}')" | jq '.id')"
    echo "Made the API inbox '$inbox_name' (id $inbox_id)." >&2
else
    call PATCH "/inboxes/$inbox_id" '{"channel": {"webhook_url": ""}}' >/dev/null
    echo "Found the API inbox '$inbox_name' (id $inbox_id); cleared its webhook URL." >&2
fi

inbox="$(call GET "/inboxes/$inbox_id")"

bot_id="$(call GET /agent_bots | jq --arg name "$bot_name" \
    '[.[] | select(.name == $name and .system_bot == false)][0].id // empty')"

if [ -z "$bot_id" ]; then
    # No outgoing_url: Spirit never takes the bot's webhook, it only posts with its token. The bot
    # stays connected to the inbox, so a new conversation starts pending, the AI's, and a resolved
    # one comes back pending when the visitor writes again.
    bot_id="$(call POST /agent_bots "$(jq -n --arg name "$bot_name" \
        '{name: $name, description: "Posts the Spirit AI answers into the Spirit inbox."}')" | jq '.id')"
    echo "Made the agent bot '$bot_name' (id $bot_id)." >&2
else
    echo "Found the agent bot '$bot_name' (id $bot_id)." >&2
fi

call POST "/inboxes/$inbox_id/set_agent_bot" "$(jq -n --argjson bot "$bot_id" '{agent_bot: $bot}')" >/dev/null
echo "Connected the agent bot to the inbox." >&2

bot="$(call GET "/agent_bots/$bot_id")"

# A plain agent only sees conversations in its own inboxes. Spirit finds a caller's conversation
# as the service user, so the service user joins the inbox. A repeat add is harmless.
if [ -n "$service_token" ]; then
    service_id="$(curl --silent --show-error --fail-with-body \
        --header "api_access_token: $service_token" "$base/api/v1/profile" | jq '.id')"
    call POST /inbox_members "$(jq -n --argjson inbox "$inbox_id" --argjson user "$service_id" \
        '{inbox_id: $inbox, user_ids: [$user]}')" >/dev/null
    echo "Added the service user (id $service_id) to the inbox." >&2
fi

# The AI picks the team of a handoff by its description (list_teams). Chatwoot saves a team name
# in lowercase. A team that exists is left as staff have it.
teams="$(call GET /teams)"
while IFS=$'\t' read -r team_name team_description; do
    if jq -e --arg name "$team_name" 'any(.[]; .name == $name)' <<<"$teams" >/dev/null; then
        echo "Found the team '$team_name'." >&2
    else
        call POST /teams "$(jq -n --arg name "$team_name" --arg description "$team_description" \
            '{name: $name, description: $description}')" >/dev/null
        echo "Made the team '$team_name'." >&2
    fi
done <<'TEAMS'
sales	Buying new equipment: prices, models, quotes, orders, and finding a dealer.
service	Equipment already owned: repairs, parts, warranty claims, and technician visits.
TEAMS

# A handoff with a phone number puts the number in the conversation field callback_phone and the
# label callback on the conversation. When staff dial that number, Spirit assigns the conversation
# to them; the label stays until staff resolve it, because GoTo cannot tell a person from a
# voicemail. Chatwoot drops a value for a field it does not define. Match these to CallbackQueue.cs.
if call GET /labels | jq -e 'any(.payload[]; .title == "callback")' >/dev/null; then
    echo "Found the label 'callback'." >&2
else
    call POST /labels '{"title": "callback", "description": "Waits for staff to call the visitor back.", "color": "#D97706", "show_on_sidebar": true}' >/dev/null
    echo "Made the label 'callback'." >&2
fi

if call GET "/custom_attribute_definitions?attribute_model=0" | jq -e 'any(.[]; .attribute_key == "callback_phone")' >/dev/null; then
    echo "Found the conversation field 'callback_phone'." >&2
else
    call POST /custom_attribute_definitions '{"attribute_display_name": "Callback phone", "attribute_key": "callback_phone", "attribute_model": 0, "attribute_display_type": 0, "attribute_description": "The number the visitor typed for a call back. Not checked."}' >/dev/null
    echo "Made the conversation field 'callback_phone'." >&2
fi

# Resolving a conversation takes it out of the call-back queue. The trigger is "conversation
# updated" with status resolved, not "conversation resolved": the dashboard offers label actions
# only for the first, so staff can read and edit the rule there.
rule_name="Resolved leaves the call-back queue"
if call GET /automation_rules | jq -e --arg name "$rule_name" 'any(.payload[]; .name == $name)' >/dev/null; then
    echo "Found the automation rule '$rule_name'." >&2
else
    call POST /automation_rules "$(jq -n --arg name "$rule_name" '{
        name: $name,
        description: "Takes the label callback off a conversation when staff resolve it.",
        event_name: "conversation_updated",
        active: true,
        conditions: [{attribute_key: "status", filter_operator: "equal_to", values: ["resolved"], query_operator: null}],
        actions: [{action_name: "remove_label", action_params: ["callback"]}]
    }')" >/dev/null
    echo "Made the automation rule '$rule_name'." >&2
fi

"$secrets_dir/set.sh" "$env" Chatwoot__InboxIdentifier "$(jq -r '.inbox_identifier' <<<"$inbox")"
"$secrets_dir/set.sh" "$env" Chatwoot__BotToken "$(jq -r '.access_token' <<<"$bot")"
echo "Wrote Chatwoot__InboxIdentifier and Chatwoot__BotToken to secrets/$env.env." >&2

if [ -z "$service_token" ]; then
    echo "Chatwoot__ServiceToken is not set: Spirit cannot list teams or find calls. See the top of this script." >&2
fi
