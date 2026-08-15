#!/usr/bin/env bash

set -euo pipefail

readonly app_name="Hanabe Photo Manager"
readonly executable_name="HanabePhotoManager.Desktop"

script_directory=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
repository_root=$(cd -- "$script_directory/../.." && pwd -P)
bundle_script="$script_directory/create-app-bundle.sh"
temporary_root=$(mktemp -d "${TMPDIR:-/tmp}/hanabe-bundle-test.XXXXXX")

cleanup() {
  rm -rf -- "$temporary_root"
}

trap cleanup EXIT

assert_tracked_executable() {
  local relative_path=$1
  local mode

  mode=$(git -C "$repository_root" ls-files --stage -- "$relative_path" | awk '{ print $1 }')
  if [[ "$mode" != "100755" ]]; then
    printf 'Expected %s to be tracked as executable, found mode %s.\n' "$relative_path" "${mode:-missing}" >&2
    exit 1
  fi
}

assert_rejected_without_deleting_source() {
  local case_name=$1
  local publish_directory=$2
  local output_directory=$3
  local expected_message=$4
  local sentinel="$publish_directory/source-sentinel.txt"
  local output

  mkdir -p -- "$publish_directory"
  printf 'app host\n' > "$publish_directory/$executable_name"
  printf '%s\n' "$case_name" > "$sentinel"

  if output=$(bash "$bundle_script" "$publish_directory" "$output_directory" 2>&1); then
    printf 'Expected %s to be rejected.\n' "$case_name" >&2
    exit 1
  fi

  if [[ "$output" != *"$expected_message"* ]]; then
    printf 'Expected %s to report "%s", received: %s\n' "$case_name" "$expected_message" "$output" >&2
    exit 1
  fi

  if [[ ! -f "$sentinel" ]]; then
    printf 'Source sentinel was deleted for %s.\n' "$case_name" >&2
    exit 1
  fi
}

assert_tracked_executable "tools/macos/create-app-bundle.sh"
assert_tracked_executable "tools/macos/test-create-app-bundle.sh"

target_inside_source_publish="$temporary_root/target-inside-source/publish"
assert_rejected_without_deleting_source \
  "target inside source" \
  "$target_inside_source_publish" \
  "$target_inside_source_publish/output" \
  "Bundle target must not equal or be inside the publish directory."

source_inside_target_output="$temporary_root/source-inside-target/output"
assert_rejected_without_deleting_source \
  "source inside target" \
  "$source_inside_target_output/$app_name.app/publish" \
  "$source_inside_target_output" \
  "Publish directory must not be inside the bundle target."

equal_target_output="$temporary_root/equal-target"
assert_rejected_without_deleting_source \
  "source equals target" \
  "$equal_target_output/$app_name.app" \
  "$equal_target_output" \
  "Bundle target must not equal or be inside the publish directory."

printf 'Bundle overlap tests passed.\n'
