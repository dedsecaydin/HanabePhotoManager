#!/usr/bin/env bash

set -euo pipefail

readonly app_name="Hanabe Photo Manager"
readonly executable_name="HanabePhotoManager.Desktop"

if [[ $# -ne 2 ]]; then
  printf 'Usage: %s <publish-directory> <output-directory>\n' "$0" >&2
  exit 64
fi

publish_input=$1
output_input=$2

if [[ ! -d "$publish_input" ]]; then
  printf 'Publish directory does not exist: %s\n' "$publish_input" >&2
  exit 1
fi

if ! mkdir -p -- "$output_input"; then
  printf 'Could not create output directory: %s\n' "$output_input" >&2
  exit 1
fi

script_directory=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
repository_root=$(cd -- "$script_directory/../.." && pwd -P)
publish_directory=$(cd -- "$publish_input" && pwd -P)
output_directory=$(cd -- "$output_input" && pwd -P)
plist_source="$repository_root/src/HanabePhotoManager.Desktop/Info.plist"
bundle_directory="$output_directory/$app_name.app"
contents_directory="$bundle_directory/Contents"
macos_directory="$contents_directory/MacOS"
resources_directory="$contents_directory/Resources"
app_host="$macos_directory/$executable_name"

if [[ ! -f "$plist_source" ]]; then
  printf 'Info.plist does not exist: %s\n' "$plist_source" >&2
  exit 1
fi

if [[ ! -f "$publish_directory/$executable_name" ]]; then
  printf 'Published app host does not exist: %s\n' "$publish_directory/$executable_name" >&2
  exit 1
fi

case "$bundle_directory" in
  "$publish_directory"|"$publish_directory"/*)
    printf 'Bundle target must not equal or be inside the publish directory.\n' >&2
    exit 1
    ;;
esac

case "$publish_directory" in
  "$bundle_directory"/*)
    printf 'Publish directory must not be inside the bundle target.\n' >&2
    exit 1
    ;;
esac

rm -rf -- "$bundle_directory"
mkdir -p -- "$macos_directory" "$resources_directory"

cp -- "$plist_source" "$contents_directory/Info.plist"
cp -R -- "$publish_directory/." "$macos_directory"
chmod +x -- "$app_host"

printf 'Created unsigned app bundle: %s\n' "$bundle_directory"
