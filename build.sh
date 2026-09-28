#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
DOTNET_DIR="$REPO_ROOT/.dotnet"
CONFIGURATION="Release"

cd "$REPO_ROOT"

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1

run_command()
{
  echo ">> $*"
  "$@"
}

# True if dotnet has the SDK from global.json and the runtimes used by the tests
has_dotnet()
{
  [ -x "$1" ] && "$1" --version > /dev/null 2>&1 || return 1
  local runtimes
  runtimes="$("$1" --list-runtimes)"
  grep -q "^Microsoft\.NETCore\.App 8\." <<< "$runtimes" && grep -q "^Microsoft\.NETCore\.App 9\." <<< "$runtimes"
}

# Prefer dotnet on PATH, otherwise use the repo local .dotnet
DOTNET="$(command -v dotnet || true)"

if ! has_dotnet "$DOTNET"; then
  DOTNET="$DOTNET_DIR/dotnet"

  if ! has_dotnet "$DOTNET"; then
    mkdir -p "$DOTNET_DIR"
    run_command curl -sSL --retry 3 -o "$DOTNET_DIR/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
    chmod +x "$DOTNET_DIR/dotnet-install.sh"
    run_command "$DOTNET_DIR/dotnet-install.sh" --jsonfile "$REPO_ROOT/global.json" --install-dir "$DOTNET_DIR" --no-path
    run_command "$DOTNET_DIR/dotnet-install.sh" --runtime dotnet --channel 8.0 --install-dir "$DOTNET_DIR" --no-path
    run_command "$DOTNET_DIR/dotnet-install.sh" --runtime dotnet --channel 9.0 --install-dir "$DOTNET_DIR" --no-path
  fi

  export DOTNET_ROOT="$DOTNET_DIR"
  export PATH="$DOTNET_DIR:$PATH"
fi

run_command "$DOTNET" --info
run_command rm -rf "$ARTIFACTS_DIR"
run_command "$DOTNET" msbuild build/version.proj -nologo -v:m
run_command "$DOTNET" build NuGet.CatalogReader.slnx -c "$CONFIGURATION"
run_command "$DOTNET" pack NuGet.CatalogReader.slnx -c "$CONFIGURATION" --no-build
run_command "$DOTNET" test --solution NuGet.CatalogReader.slnx -c "$CONFIGURATION" --no-build --results-directory "$ARTIFACTS_DIR/TestResults" --report-trx --hangdump --hangdump-timeout 20m --hangdump-type Mini

echo "Success!"
