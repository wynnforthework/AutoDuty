#!/bin/sh
# User policy: local AI-assisted changes must not be contributed to upstream.
remote_name=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
remote_url=$(printf '%s' "$2" | tr '[:upper:]' '[:lower:]')
case "$remote_name|$remote_url" in
  upstream\|*|*github.com/erdelf/autoduty*|*github.com:erdelf/autoduty*)
    echo 'BLOCKED: user policy forbids AI code contributions to erdelf/AutoDuty.' >&2
    echo 'See AGENTS.md. Local development and fork backups are allowed; upstream PRs are not.' >&2
    exit 1
    ;;
esac
exit 0
