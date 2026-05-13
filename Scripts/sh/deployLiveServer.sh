#!/usr/bin/env sh

set -eu

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SERVER_DIR="${SERVER_DIR:-/opt/vrs/server}"
RES_DIR="$SERVER_DIR/Resources"
ASM_DIR="$RES_DIR/Assemblies"
CONFIG_FILE="${CONFIG_FILE:-/opt/vrs/server_config.toml}"
DATA_DIR="${DATA_DIR:-/opt/vrs/data}"

log() {
    printf "[deploy] %s\n" "$1"
}

require_file() {
    if [ ! -f "$1" ]; then
        printf "[deploy] missing required file: %s\n" "$1" >&2
        exit 1
    fi
}

cd "$REPO_ROOT"

log "Building Content.Server (Release, FullRelease=True)"
dotnet build Content.Server/Content.Server.csproj -c Release -p:FullRelease=True

log "Publishing Content.Server to $SERVER_DIR"
dotnet publish Content.Server/Content.Server.csproj -c Release -p:FullRelease=True -o "$SERVER_DIR" --no-build

log "Copying content assemblies to $ASM_DIR"
for name in Content.Server Content.Shared Content.Server.Database Content.Shared.Database Content.Packaging; do
    require_file "$SERVER_DIR/$name.dll"
    cp "$SERVER_DIR/$name.dll" "$ASM_DIR/"
    if [ -f "$SERVER_DIR/$name.pdb" ]; then
        cp "$SERVER_DIR/$name.pdb" "$ASM_DIR/"
    fi
done

log "Syncing Resources"
rsync -a --delete "$REPO_ROOT/Resources/Prototypes/" "$RES_DIR/Prototypes/"
rsync -a --delete "$REPO_ROOT/Resources/Locale/" "$RES_DIR/Locale/"
rsync -a --delete "$REPO_ROOT/Resources/Maps/" "$RES_DIR/Maps/"
rsync -a "$REPO_ROOT/Resources/Audio/" "$RES_DIR/Audio/"

for f in migration.yml mono_migration.yml hl_migration.yml nf_migration.yml mapping_actions.yml manifest.yml keybinds.yml clientCommandPerms.yml engineCommandPerms.yml toolshedEngineCommandPerms.yml map_attributions.txt; do
    if [ -f "$REPO_ROOT/Resources/$f" ]; then
        cp "$REPO_ROOT/Resources/$f" "$RES_DIR/"
    fi
done

log "Packaging fresh client zip"
dotnet run --project Content.Packaging -- client --configuration Release
require_file "$REPO_ROOT/release/SS14.Client.zip"
cp "$REPO_ROOT/release/SS14.Client.zip" "$SERVER_DIR/Content.Client.zip"

log "Restarting vrs.service"
sudo systemctl reset-failed vrs.service
sudo systemctl restart vrs.service

log "Waiting for server readiness"
sleep 8
sudo systemctl is-active vrs.service >/dev/null

LATEST_LOG=$(ls -t /opt/vrs/logs/log_*.txt | head -1)
log "Latest log: $LATEST_LOG"
tail -n 25 "$LATEST_LOG"

log "Status endpoint"
curl -sS http://127.0.0.1:1212/info | head -c 400 || true
printf "\n"

log "Done"
