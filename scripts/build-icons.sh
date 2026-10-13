#!/usr/bin/env bash

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_png="$root/assets/brand/AppIcon_Full_Color.png"
destination="$root/LoliaFrpClient.Desktop/Assets/app.icns"

if [[ ! -f "$source_png" ]]; then
    echo "找不到源图: $source_png" >&2
    exit 1
fi

if ! command -v sips >/dev/null || ! command -v iconutil >/dev/null; then
    echo "需要 macOS 自带的 sips 与 iconutil;此脚本只能在 macOS 上运行。" >&2
    exit 1
fi

scratch="$(mktemp -d "${TMPDIR:-/tmp}/lfc-icns.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

iconset="$scratch/app.iconset"
mkdir -p "$iconset"

emit() {
    local pixels="$1" name="$2"
    sips -z "$pixels" "$pixels" "$source_png" --out "$iconset/$name.png" >/dev/null
}

emit 16   icon_16x16
emit 32   icon_16x16@2x
emit 32   icon_32x32
emit 64   icon_32x32@2x
emit 128  icon_128x128
emit 256  icon_128x128@2x
emit 256  icon_256x256
emit 512  icon_256x256@2x
emit 512  icon_512x512
emit 1024 icon_512x512@2x

mkdir -p "$(dirname "$destination")"
iconutil -c icns "$iconset" -o "$destination"

echo "已生成 $destination ($(wc -c < "$destination" | tr -d ' ') 字节)"
