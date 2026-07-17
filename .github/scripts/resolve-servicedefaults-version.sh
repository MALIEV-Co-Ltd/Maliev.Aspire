#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 6 ]]; then
  echo "Expected event, ref, release tag, run number, run attempt, and commit SHA." >&2
  exit 64
fi

readonly event_name="$1"
readonly ref_name="$2"
readonly release_tag="$3"
readonly run_number="$4"
readonly run_attempt="$5"
readonly commit_sha="$6"
readonly semver_core='(0|[1-9][0-9]*)'

fail() {
  echo "$1" >&2
  exit 65
}

validate_run_metadata() {
  [[ "$run_number" =~ ^[1-9][0-9]*$ ]] || fail "Run number must be a positive integer."
  [[ "$run_attempt" =~ ^[1-9][0-9]*$ ]] || fail "Run attempt must be a positive integer."
  [[ "$commit_sha" =~ ^[0-9a-fA-F]{40}$ ]] || fail "Commit SHA must contain exactly 40 hexadecimal characters."
}

validate_run_metadata

case "$event_name" in
  push)
    readonly short_sha="${commit_sha:0:8}"
    case "$ref_name" in
      develop)
        version="1.0.${run_number}-alpha.${run_attempt}.${short_sha,,}"
        ;;
      *)
        fail "Unsupported publication branch '${ref_name}'."
        ;;
    esac
    ;;
  release)
    [[ "$run_attempt" == "1" ]] || fail "Stable release publications cannot be rerun with the same version."
    if [[ "$release_tag" =~ ^release/v${semver_core}\.${semver_core}\.${semver_core}$ ]]; then
      version="${release_tag#release/v}"
    else
      fail "Release tags must use release/vX.Y.Z with canonical numeric identifiers."
    fi
    ;;
  *)
    fail "Unsupported publication event '${event_name}'."
    ;;
esac

printf '%s\n' "$version"
