#!/bin/bash

set -ex

APP_NAME="NStrip"

rm -fr publish/*

function app_publish() {
  local dist_label="$1"
  shift
  dotnet publish -c Release -o "publish/${dist_label}" "$@"

  (
    cd "publish/${dist_label}"
    zip -r "../${APP_NAME}-${dist_label}.zip" "."
  )
}

function app_publish_dotnet() {
  app_publish "dotnet-$(dotnet --version)" \
    -p:UseAppHost=false \
    -p:PublishSingleFile=false \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=false
}

function app_publish_runtime() {
  local app_runtime="$1"
  shift
  app_publish "${app_runtime}" -r "${app_runtime}" --self-contained \
    -p:DebugType=none \
    -p:UseAppHost=true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=true \
    -p:IncludeNativeLibrariesForSelfExtract=true
}

app_publish_dotnet
app_publish_runtime "linux-x64"
app_publish_runtime "osx-x64"
app_publish_runtime "win-x64"
