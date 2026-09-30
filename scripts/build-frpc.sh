#!/usr/bin/env bash
#
# 本地构建内置 frpc,产物落在 LoliaFrpClient.Android/lib/<abi>/libfrpc.so。
#
# 为什么需要它:LoliaFrpClient.Android/lib/ 在 .gitignore 里(两个 ABI 加起来 35MB),
# 所以新克隆的仓库没有 frpc,直接构建只会得到一个不含 frpc 的包 —— 界面上会显示「未安装」。
#
# 需要 Android NDK(见下方 find_ndk),不装会直接报错退出,不会替你下 1GB。
# CI 走的是同一套构建,见 .github/workflows/android.yml。
#
#   ./scripts/build-frpc.sh              # 默认版本
#   ./scripts/build-frpc.sh v0.70.0      # 指定 lolia-frp 的 tag / 分支 / commit
#
set -euo pipefail

REF="${1:-v0.71.1}"
REPO="${FRPC_REPO:-https://github.com/Lolia-FRP/lolia-frp.git}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$(mktemp -d)"
trap 'rm -rf "$SRC"' EXIT

# ── 找 NDK ──
find_ndk() {
    if [ -n "${ANDROID_NDK_HOME:-}" ] && [ -d "$ANDROID_NDK_HOME" ]; then
        echo "$ANDROID_NDK_HOME"; return
    fi
    if [ -n "${ANDROID_NDK_ROOT:-}" ] && [ -d "$ANDROID_NDK_ROOT" ]; then
        echo "$ANDROID_NDK_ROOT"; return
    fi
    local sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}}"
    [ -d "$sdk/ndk" ] && ls -1d "$sdk/ndk/"*/ 2>/dev/null | sort -V | tail -1 && return
    return 1
}

if ! NDK="$(find_ndk)"; then
    cat >&2 <<'EOF'
找不到 Android NDK。

为什么必须要它:frpc 得用 GOOS=android + CGO 编译,让域名解析交给 bionic 的 getaddrinfo。
用 GOOS=linux + CGO_ENABLED=0 编出来的静态二进制虽然能跑,但解析不了任何域名 ——
Android 上没有 /etc/resolv.conf,Go 的纯 Go 解析器会退到 [::1]:53 然后失败。
而 app 启动 frpc 时只传 -t id:token,服务器地址要靠 frpc 自己去 api.lolia.link 取,
所以解析不了域名 = 隧道连不上。

安装方式(任选其一):
  sdkmanager "ndk;26.1.10909125"          # 经由 Android SDK
  或从 https://developer.android.com/ndk/downloads 下载 r26c 解压后
  export ANDROID_NDK_HOME=/path/to/android-ndk-r26c
EOF
    exit 1
fi
echo "NDK: $NDK"

# NDK 的预编译工具链按宿主机分目录,Windows 上的编译器包装是 .cmd。
case "$(uname -s)" in
    Linux)  HOST=linux-x86_64 ;;
    Darwin) HOST=darwin-x86_64 ;;
    MINGW*|MSYS*|CYGWIN*) HOST=windows-x86_64; SUFFIX=.cmd ;;
    *) echo "不支持的宿主机:$(uname -s)" >&2; exit 1 ;;
esac
TOOLCHAIN="$NDK/toolchains/llvm/prebuilt/$HOST/bin"
[ -d "$TOOLCHAIN" ] || { echo "NDK 工具链不存在:$TOOLCHAIN" >&2; exit 1; }

# fetch 指定 ref 而不是 clone --branch:后者接不了 commit SHA。
echo "拉取 $REPO @ $REF"
git init --quiet "$SRC"
git -C "$SRC" remote add origin "$REPO"
git -C "$SRC" fetch --quiet --depth 1 origin "$REF"
git -C "$SRC" checkout --quiet FETCH_HEAD

# -checklinkname=0:Go 1.23+ 对 pion 依赖 wlynxg/anet 的 //go:linkname net.zoneCache
#   在 android 目标上会链接失败,上游 LDFLAGS 里也有这一条。
# noweb:浅克隆里没有 web/*/dist,不排掉会在 embed 处报 "pattern dist: no matching files"。
build() {
    local abi="$1" goarch="$2" ccname="$3" machine="$4"
    local dest="$ROOT/LoliaFrpClient.Android/lib/$abi"

    mkdir -p "$dest"
    echo "构建 $abi (GOOS=android GOARCH=$goarch CC=$ccname)"
    ( cd "$SRC" && \
      CC="$TOOLCHAIN/$ccname${SUFFIX:-}" CXX="$TOOLCHAIN/$ccname++${SUFFIX:-}" \
      CGO_ENABLED=1 GOOS=android GOARCH="$goarch" \
        go build -trimpath -ldflags "-s -w -checklinkname=0" -tags "frpc,noweb" \
        -o "$dest/libfrpc.so" ./cmd/frpc )

    local f="$dest/libfrpc.so"
    local got
    got="$(od -An -tx1 -j18 -N2 "$f" | tr -d ' \n')"
    if [ "$got" != "$machine" ]; then
        echo "  架构不符:e_machine=0x$got,应为 0x$machine" >&2; exit 1
    fi
    # 静态二进制(误用 GOOS=linux)能过上面那项检查,却会在设备上解析不了域名。
    if ! grep -qa "/system/bin/linker" "$f"; then
        echo "  不是 Android 目标:找不到 /system/bin/linker64 解释器。" >&2
        echo "  多半是误用了 GOOS=linux,那样编出来的静态二进制在设备上解析不了域名。" >&2
        exit 1
    fi

    echo "  -> lib/$abi/libfrpc.so($(wc -c < "$f") 字节)"
}

build arm64-v8a arm64 aarch64-linux-android21-clang b700   # e_machine 0xB7,AArch64
build x86_64    amd64 x86_64-linux-android21-clang 3e00    # e_machine 0x3E,x86-64(主要给模拟器)

echo "完成。"
