#!/bin/sh

CONFIG_FILE="$(pwd)/server_config.local.toml"

if [ -f "$CONFIG_FILE" ]; then
	dotnet run --project Content.Server -- --config-file "$CONFIG_FILE"
else
	dotnet run --project Content.Server
fi

read -p "Press enter to continue"
