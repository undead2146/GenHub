#!/usr/bin/env bash
# build-check.sh - Serialized build/check script for GenHub on Linux/macOS.
# Linux counterpart to scripts/build-check.ps1. Uses flock to ensure only one
# build runs at a time and refuses to build while output DLLs are locked.
#
# Usage:
#   ./scripts/build-check.sh                        # quick compile check on full solution
#   ./scripts/build-check.sh -p GenHub.Core/GenHub.Core.csproj
#   ./scripts/build-check.sh -m build               # full build with output
#   ./scripts/build-check.sh -m restore             # NuGet restore only
#   ./scripts/build-check.sh -t 300                 # longer lock timeout

set -u

MODE="check"
PROJECT=""
TIMEOUT_SECONDS=120
VERBOSITY="quiet"

usage() {
    sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        check|build|restore)
            MODE="$1"
            shift
            ;;
        -m|--mode)
            MODE="${2:-}"
            shift 2
            ;;
        -p|--project)
            PROJECT="${2:-}"
            shift 2
            ;;
        -t|--timeout)
            TIMEOUT_SECONDS="${2:-120}"
            shift 2
            ;;
        -v|--verbosity)
            VERBOSITY="${2:-quiet}"
            shift 2
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo "[build-check] ERROR: Unknown argument: $1" >&2
            usage
            ;;
    esac
done

case "$MODE" in
    check|build|restore) ;;
    *)
        echo "[build-check] ERROR: Invalid mode '$MODE' (expected check, build, or restore)." >&2
        exit 1
        ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_DIR="$(cd "$SCRIPT_DIR/.." && pwd)/GenHub"
SOLUTION_FILE="$SOLUTION_DIR/GenHub.sln"
LOCK_FILE="$SOLUTION_DIR/build.lock"
LOCK_TIMEOUT=$((TIMEOUT_SECONDS))

log_status() {
    printf '\033[2m[build-check]\033[0m %s\n' "$1"
}

log_err() {
    printf '\033[2m[build-check]\033[0m \033[31mERROR: %s\033[0m\n' "$1" >&2
}

cleanup() {
    rm -f "$LOCK_FILE"
    if [[ -n "${LOCK_FD:-}" ]]; then
        eval "exec $LOCK_FD>&-"
    fi
}
trap cleanup EXIT

if ! command -v dotnet >/dev/null 2>&1; then
    if [[ -x "$HOME/.dotnet/dotnet" ]]; then
        export PATH="$HOME/.dotnet:$PATH"
    else
        log_err "dotnet CLI not found in PATH or ~/.dotnet."
        exit 1
    fi
fi

if [[ ! -f "$SOLUTION_FILE" ]]; then
    log_err "Solution not found at: $SOLUTION_FILE"
    exit 1
fi

if ! command -v flock >/dev/null 2>&1; then
    log_err "flock not found. Install util-linux (usually preinstalled on Linux)."
    exit 1
fi

TARGET="$SOLUTION_FILE"
NO_DEPENDENCIES=()
if [[ -n "$PROJECT" ]]; then
    TARGET="$SOLUTION_DIR/$PROJECT"
    if [[ ! -f "$TARGET" ]]; then
        log_err "Project not found: $TARGET"
        exit 1
    fi
    NO_DEPENDENCIES=(--no-dependencies)
fi

log_status "Acquiring build lock (timeout: ${TIMEOUT_SECONDS}s)..."

exec 9>"$LOCK_FILE"
if ! flock -w "$LOCK_TIMEOUT" 9; then
    log_err "Timed out waiting for build lock after ${TIMEOUT_SECONDS}s."
    log_err "Another agent or process is currently building."
    exit 3
fi

printf '{"pid": %d, "mode": "%s", "project": "%s", "startedAt": "%s"}\n' \
    "$$" "$MODE" "${PROJECT:-GenHub.sln}" "$(date -Iseconds)" >"$LOCK_FILE"

log_status "Build lock acquired."

DOTNET_ARGS=(--nologo --verbosity "$VERBOSITY" -maxcpucount:2)

case "$MODE" in
    check)
        log_status "Running compile check on: $(basename "$TARGET")"
        dotnet build "$TARGET" --no-restore "${DOTNET_ARGS[@]}" "${NO_DEPENDENCIES[@]}"
        ;;
    build)
        log_status "Running full build on: $(basename "$TARGET")"
        dotnet build "$TARGET" "${DOTNET_ARGS[@]}"
        ;;
    restore)
        log_status "Running NuGet restore on: $(basename "$TARGET")"
        dotnet restore "$TARGET" --verbosity "$VERBOSITY"
        ;;
esac

EXIT_CODE=$?

if [[ $EXIT_CODE -eq 0 ]]; then
    log_status "Completed successfully with no errors."
else
    log_err "Build/check failed with exit code: $EXIT_CODE"
fi

exit $EXIT_CODE
