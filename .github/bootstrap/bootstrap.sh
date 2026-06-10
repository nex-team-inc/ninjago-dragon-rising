#!/usr/bin/env bash
set -euo pipefail

CONFIG_FILE="${CONFIG_FILE:-.github/bootstrap/bootstrap-config.json}"

: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is required}"
: "${GH_TOKEN:?GH_TOKEN is required}"

repo="$GITHUB_REPOSITORY"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command '$1' was not found." >&2
    exit 1
  fi
}

require_command gh
require_command jq

if [[ ! -f "$CONFIG_FILE" ]]; then
  echo "Error: config file not found: $CONFIG_FILE" >&2
  exit 1
fi

echo "Repository: $repo"
echo "Config file: $CONFIG_FILE"

json_escape_for_jq() {
  jq -Rr @json <<< "$1"
}

create_label_if_missing() {
  local name="$1"
  local color="$2"
  local description="$3"

  if [[ -z "$name" ]]; then
    echo "Error: label name cannot be empty." >&2
    exit 1
  fi

  if [[ -z "$color" ]]; then
    color="7f7f7f"
  fi

  echo "Checking label: $name"

  if gh api "repos/$repo/labels/$name" >/dev/null 2>&1; then
    echo "Label already exists: $name"
    return
  fi

  echo "Creating label: $name"

  gh api \
    --method POST \
    "repos/$repo/labels" \
    -f "name=$name" \
    -f "color=$color" \
    -f "description=$description" \
    >/dev/null
}

create_milestone_if_missing() {
  local title="$1"
  local description="$2"

  if [[ -z "$title" ]]; then
    echo "Error: milestone title cannot be empty." >&2
    exit 1
  fi

  echo "Checking milestone: $title"

  local title_json
  title_json="$(json_escape_for_jq "$title")"

  local existing
  existing="$(
    gh api "repos/$repo/milestones?state=all&per_page=100" \
      --jq ".[] | select(.title == $title_json) | .number" || true
  )"

  if [[ -n "$existing" ]]; then
    echo "Milestone already exists: $title"
    return
  fi

  echo "Creating milestone: $title"

  gh api \
    --method POST \
    "repos/$repo/milestones" \
    -f "title=$title" \
    -f "description=$description" \
    >/dev/null
}

get_milestone_number() {
  local title="$1"

  if [[ -z "$title" ]]; then
    echo ""
    return
  fi

  local title_json
  title_json="$(json_escape_for_jq "$title")"

  gh api "repos/$repo/milestones?state=all&per_page=100" \
    --jq ".[] | select(.title == $title_json) | .number"
}

issue_exists_by_key() {
  local key="$1"

  if [[ -z "$key" ]]; then
    return 1
  fi

  local marker="bootstrap-key: $key"

  local result
  result="$(
    gh search issues \
      "$marker repo:$repo in:body type:issue" \
      --json number \
      --jq '.[0].number' 2>/dev/null || true
  )"

  [[ -n "$result" ]]
}

create_issue_if_missing() {
  local issue_json="$1"

  local key
  local title
  local body
  local milestone_title

  key="$(jq -r '.key // empty' <<< "$issue_json")"
  title="$(jq -r '.title // empty' <<< "$issue_json")"
  body="$(jq -r '.body // ""' <<< "$issue_json")"
  milestone_title="$(jq -r '.milestone // ""' <<< "$issue_json")"

  if [[ -z "$key" ]]; then
    echo "Error: every issue must have a unique key." >&2
    exit 1
  fi

  if [[ -z "$title" ]]; then
    echo "Error: issue with key '$key' is missing title." >&2
    exit 1
  fi

  echo "Checking issue: $title"

  if issue_exists_by_key "$key"; then
    echo "Issue already exists: $title"
    return
  fi

  local milestone_number=""
  if [[ -n "$milestone_title" ]]; then
    milestone_number="$(get_milestone_number "$milestone_title")"

    if [[ -z "$milestone_number" ]]; then
      echo "Error: issue '$title' references missing milestone '$milestone_title'." >&2
      exit 1
    fi
  fi

  local full_body
  full_body="${body}"$'\n\n'"<!-- bootstrap-key: ${key} -->"

  local payload
  payload="$(
    jq -n \
      --arg title "$title" \
      --arg body "$full_body" \
      --argjson milestone "${milestone_number:-null}" \
      --argjson labels "$(jq -c '.labels // []' <<< "$issue_json")" \
      --argjson assignees "$(jq -c '.assignees // []' <<< "$issue_json")" \
      '
      {
        title: $title,
        body: $body,
        labels: $labels,
        assignees: $assignees
      }
      + if $milestone == null then {} else { milestone: $milestone } end
      '
  )"

  echo "Creating issue: $title"

  gh api \
    --method POST \
    "repos/$repo/issues" \
    --input - <<< "$payload" \
    >/dev/null
}

echo ""
echo "Validating config JSON..."
jq empty "$CONFIG_FILE"

echo ""
echo "Creating labels..."
jq -c '.labels // [] | .[]' "$CONFIG_FILE" | while read -r label_json; do
  name="$(jq -r '.name // empty' <<< "$label_json")"
  color="$(jq -r '.color // "ededed"' <<< "$label_json")"
  description="$(jq -r '.description // ""' <<< "$label_json")"

  create_label_if_missing "$name" "$color" "$description"
done

echo ""
echo "Creating milestones..."
jq -c '.milestones // [] | .[]' "$CONFIG_FILE" | while read -r milestone_json; do
  title="$(jq -r '.title // empty' <<< "$milestone_json")"
  description="$(jq -r '.description // ""' <<< "$milestone_json")"

  create_milestone_if_missing "$title" "$description"
done

echo ""
echo "Creating issues..."
jq -c '.issues // [] | .[]' "$CONFIG_FILE" | while read -r issue_json; do
  create_issue_if_missing "$issue_json"
done

echo ""
echo "Bootstrap complete."
