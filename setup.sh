#!/bin/bash
cd "$(dirname "$0")"
dotnet run --project GameDataConfigTool.csproj --verbosity quiet -- --setup
