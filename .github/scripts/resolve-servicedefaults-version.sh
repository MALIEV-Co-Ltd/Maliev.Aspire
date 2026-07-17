#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 7 ]]; then
  echo "Expected event, ref, release tag, manual version, run number, run attempt, and commit SHA." >&2
  exit 64
fi

readonly event_name="$1"
readonly ref_name="$2"
readonly release_tag="$3"
readonly manual_version="$4"
readonly run_number="$5"
readonly run_attempt="$6"
readonly commit_sha="$7"
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

case "$event_name" in
  push)
    validate_run_metadata
    readonly short_sha="${commit_sha:0:8}"
    case "$ref_name" in
      develop)
        version="1.0.${run_number}-alpha.${run_attempt}.${short_sha,,}"
        ;;
      staging)
        version="1.0.${run_number}-beta.${run_attempt}.${short_sha,,}"
        ;;
      main)
        [[ "$run_attempt" == "1" ]] || fail "Stable main publications cannot be rerun with the same version."
        version="1.0.${run_number}"
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
  workflow_dispatch)
    [[ "$run_attempt" == "1" ]] || fail "Manual stable publications cannot be rerun with the same version."
    normalized_version="${manual_version#v}"
    if [[ "$normalized_version" =~ ^${semver_core}\.${semver_core}\.${semver_core}$ ]]; then
      version="$normalized_version"
    else
      fail "Manual versions must use X.Y.Z or vX.Y.Z with canonical numeric identifiers."
    fi
    ;;
  *)
    fail "Unsupported publication event '${event_name}'."
    ;;
esac

printf '%s\n' "$version"
