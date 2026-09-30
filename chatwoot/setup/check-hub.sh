#!/usr/bin/env bash
# Proves initializers/spirit_hub.rb is live on Desk. Each check is one curl -si call, so the
# status, headers, and body of a response all come from the same request. Never follows a
# redirect (no curl -L): a check must see the 302 itself.
#
#   chatwoot/setup/check-hub.sh <desk-url> <hub-origin>
set -euo pipefail

desk="${1:?usage: check-hub.sh <desk-url> <hub-origin>}"
hub="${2:?usage: check-hub.sh <desk-url> <hub-origin>}"
login_url="$hub/chat/login.html?app=desk"
failed=0

get() { curl --silent --show-error -i "$1"; }
status_of() { printf '%s' "$1" | head -n1 | tr -d '\r' | awk '{print $2}'; }
# A response that lacks the header (no redirect, or a plain page) is not an error: print nothing.
header_of() { printf '%s' "$1" | { grep -i "^$2:" || true; } | head -n1 | cut -d: -f2- | sed 's/^[[:space:]]*//' | tr -d '\r'; }

pass() { echo "OK: $1"; }
miss() { echo "FAIL: $1" >&2; failed=1; }

# 1. A plain visit leaves for the Hub. Also proves a repeated or trailing slash cannot bypass the
# match: a Spirit reviewer found both spellings slip past a plain string check.
for path in /app/login //app/login /app/login/ /app//login/; do
    response="$(get "$desk$path")"
    status="$(status_of "$response")"
    location="$(header_of "$response" location)"
    if [ "$status" = "302" ] && [ "$location" = "$login_url" ]; then
        pass "GET $path -> 302 $login_url"
    else
        miss "GET $path: status=$status location='$location', wanted 302 $login_url"
    fi
done

# A token in the query string must reach Chatwoot's own sign-in, never the Hub.
response="$(get "$desk/app/login?sso_auth_token=x")"
status="$(status_of "$response")"
if [ "$status" != "302" ]; then
    pass "GET /app/login?sso_auth_token=x -> $status, not redirected"
else
    miss "GET /app/login?sso_auth_token=x was redirected to the Hub; a token must not be"
fi

# 2/3. The back-door form still shows, and Desk now lets the Hub frame it.
response="$(get "$desk/app/login?local=1")"
status="$(status_of "$response")"
if [ "$status" = "200" ]; then
    pass "GET /app/login?local=1 -> 200 (the back-door form)"
else
    miss "GET /app/login?local=1: status=$status, wanted 200"
fi

xfo="$(header_of "$response" x-frame-options)"
if [ -z "$xfo" ]; then
    pass "GET /app/login?local=1 carries no x-frame-options"
else
    miss "GET /app/login?local=1 still carries x-frame-options: $xfo"
fi

csp="$(header_of "$response" content-security-policy)"
case "$csp" in
    *"frame-ancestors 'self' $hub"*) pass "GET /app/login?local=1 carries frame-ancestors 'self' $hub" ;;
    *) miss "GET /app/login?local=1 content-security-policy '$csp' lacks frame-ancestors 'self' $hub" ;;
esac

# 4. Signing out tells the Hub.
response="$(get "$desk/spirit/sign-out")"
status="$(status_of "$response")"
if [ "$status" = "200" ] && printf '%s' "$response" | grep -q 'hub:signed-out'; then
    pass "GET /spirit/sign-out -> 200 and posts hub:signed-out"
else
    miss "GET /spirit/sign-out: status=$status, body did not carry hub:signed-out"
fi

if [ "$failed" -ne 0 ]; then
    echo "check-hub: at least one check failed." >&2
    exit 1
fi
echo "check-hub: all checks passed."
