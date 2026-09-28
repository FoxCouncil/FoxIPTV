#!/usr/bin/env bash
# Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

set -euo pipefail

RID="${1:?usage: build/ffmpeg.sh <win-x64|linux-x64|osx-arm64>}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE="$ROOT/native/.cache"
OUT="$ROOT/native/$RID"
WORK="${WORK:-/tmp/ffmpeg-build-$RID}"
PREFIX="$WORK/prefix"
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"

FFMPEG_VERSION=9.0.1
FFMPEG_SHA256=cf38e0e28c7e5605942c4a77755349b0145804a397af37eb1fb4c77cb237f635
LIBDRM_VERSION=2.4.125
LIBDRM_SHA256=d4bae92797a50f81a93524762e0410a49cd84cfa0f997795bc0172ac8fb1d96a
LIBVA_VERSION=2.23.0
LIBVA_SHA256=9ac190a87017bfd49743248f5df7cf3b18a99a9962175caf6bbe3f1ea41b6dbb

cpu_count()
{
    getconf _NPROCESSORS_ONLN 2>/dev/null || sysctl -n hw.ncpu
}

sha256()
{
    if command -v sha256sum >/dev/null; then
        sha256sum "$1" | cut -d' ' -f1
    else
        shasum -a 256 "$1" | cut -d' ' -f1
    fi
}

fetch()
{
    local url="$1" file="$2" sum="$3"

    mkdir -p "$CACHE"

    if [ ! -f "$CACHE/$file" ]; then
        curl -fsSL -A "$UA" -o "$CACHE/$file.part" "$url"
        mv "$CACHE/$file.part" "$CACHE/$file"
    fi

    if [ "$(sha256 "$CACHE/$file")" != "$sum" ]; then
        echo "Checksum mismatch for $file" >&2
        exit 1
    fi
}

unpack()
{
    rm -rf "$WORK/$2"
    tar -xf "$CACHE/$1" -C "$WORK"
}

apt_install()
{
    local sudo=""

    if [ "$(id -u)" != "0" ]; then
        sudo="sudo"
    fi

    $sudo apt-get update -qq
    DEBIAN_FRONTEND=noninteractive $sudo apt-get install -y -qq --no-install-recommends "$@"
}

build_libva()
{
    fetch "https://dri.freedesktop.org/libdrm/libdrm-$LIBDRM_VERSION.tar.xz" "libdrm-$LIBDRM_VERSION.tar.xz" "$LIBDRM_SHA256"
    fetch "https://github.com/intel/libva/releases/download/$LIBVA_VERSION/libva-$LIBVA_VERSION.tar.bz2" "libva-$LIBVA_VERSION.tar.bz2" "$LIBVA_SHA256"

    unpack "libdrm-$LIBDRM_VERSION.tar.xz" "libdrm-$LIBDRM_VERSION"
    unpack "libva-$LIBVA_VERSION.tar.bz2" "libva-$LIBVA_VERSION"

    export PKG_CONFIG_PATH="$PREFIX/lib/pkgconfig"

    (
        cd "$WORK/libdrm-$LIBDRM_VERSION"
        meson setup build --prefix="$PREFIX" --libdir=lib --buildtype=release -Ddefault_library=static \
            -Dintel=disabled -Dradeon=disabled -Damdgpu=disabled -Dnouveau=disabled -Dvmwgfx=disabled -Domap=disabled -Dexynos=disabled \
            -Dfreedreno=disabled -Dtegra=disabled -Dvc4=disabled -Detnaviv=disabled -Dcairo-tests=disabled -Dman-pages=disabled -Dvalgrind=disabled \
            -Dtests=false -Dinstall-test-programs=false
        ninja -C build install
    )

    (
        cd "$WORK/libva-$LIBVA_VERSION"
        meson setup build --prefix="$PREFIX" --libdir=lib --buildtype=release \
            -Dwith_x11=no -Dwith_glx=no -Dwith_wayland=no -Ddisable_drm=false -Denable_docs=false \
            -Ddriverdir=/usr/lib/x86_64-linux-gnu/dri:/usr/lib64/dri:/usr/lib/dri:/usr/local/lib/dri
        ninja -C build install
    )
}

COMPONENTS=(
    --disable-everything
    --enable-decoder=h264,hevc,mpeg1video,mpeg2video,mpeg4,vp9,av1
    --enable-decoder=aac,aac_latm,ac3,eac3,mp2,mp2float,mp3,mp3float,opus,flac,vorbis,dca,truehd,mlp,pcm_s16le,pcm_s16be,pcm_s24le,pcm_bluray,pcm_dvd
    --enable-decoder=ccaption,subrip,ass,ssa,webvtt,movtext,dvbsub,dvdsub,pgssub
    --enable-parser=h264,hevc,mpegvideo,mpeg4video,vp9,av1,aac,aac_latm,ac3,mpegaudio,opus,flac,vorbis,dca,mlp,dvbsub,dvdsub
    --enable-demuxer=mpegts,mov,matroska,avi,flv,live_flv,ogg,wav,mpegps,aac,ac3,eac3,mp3,loas,rtsp,sdp,rtp
    --enable-protocol=file,tcp,udp,rtp,rtmp
    --enable-filter=bwdif,yadif,scale,format,null
)

COMMON=(
    --prefix="$PREFIX"
    --enable-shared
    --disable-static
    --disable-programs
    --disable-doc
    --disable-debug
    --disable-autodetect
    --disable-avdevice
    --enable-avfilter
    --enable-swscale
    --enable-swresample
    --enable-network
)

mkdir -p "$WORK"

case "$RID" in
    win-x64)
    {
        if command -v apt-get >/dev/null && ! command -v x86_64-w64-mingw32-gcc >/dev/null; then
            apt_install build-essential nasm pkg-config mingw-w64 curl ca-certificates xz-utils
        fi

        PLATFORM=(
            --target-os=mingw32 --arch=x86_64 --cross-prefix=x86_64-w64-mingw32- --enable-cross-compile
            --enable-w32threads
            --enable-d3d11va
            --enable-hwaccel=h264_d3d11va,hevc_d3d11va,mpeg2_d3d11va,vp9_d3d11va,av1_d3d11va
            --enable-hwaccel=h264_d3d11va2,hevc_d3d11va2,mpeg2_d3d11va2,vp9_d3d11va2,av1_d3d11va2
            --extra-cflags=-fno-stack-protector
            --extra-ldflags=-static-libgcc
        )
    }
    ;;

    linux-x64)
    {
        if command -v apt-get >/dev/null && ! command -v meson >/dev/null; then
            apt_install build-essential nasm pkg-config meson ninja-build python3 patchelf curl ca-certificates xz-utils bzip2
        fi

        build_libva

        PLATFORM=(
            --enable-pthreads
            --enable-vaapi
            --enable-hwaccel=h264_vaapi,hevc_vaapi,mpeg2_vaapi,mpeg4_vaapi,vp9_vaapi,av1_vaapi
            --pkg-config-flags=--static
        )
    }
    ;;

    osx-arm64)
    {
        PLATFORM=(
            --arch=arm64 --cc=clang
            --enable-pthreads
            --enable-videotoolbox
            --enable-hwaccel=h264_videotoolbox,hevc_videotoolbox,mpeg2_videotoolbox,mpeg4_videotoolbox,vp9_videotoolbox,av1_videotoolbox
            --install-name-dir=@rpath
            --extra-cflags=-mmacosx-version-min=11.0
            --extra-ldflags="-mmacosx-version-min=11.0 -Wl,-rpath,@loader_path"
        )
    }
    ;;

    *)
    {
        echo "Unknown runtime identifier: $RID" >&2
        exit 1
    }
    ;;
esac

fetch "https://ffmpeg.org/releases/ffmpeg-$FFMPEG_VERSION.tar.xz" "ffmpeg-$FFMPEG_VERSION.tar.xz" "$FFMPEG_SHA256"
unpack "ffmpeg-$FFMPEG_VERSION.tar.xz" "ffmpeg-$FFMPEG_VERSION"

(
    cd "$WORK/ffmpeg-$FFMPEG_VERSION"
    ./configure "${COMMON[@]}" "${COMPONENTS[@]}" "${PLATFORM[@]}"
    make -j"$(cpu_count)"
    make install
)

rm -rf "$OUT"
mkdir -p "$OUT"

case "$RID" in
    win-x64)
    {
        cp "$PREFIX"/bin/*.dll "$OUT/"
        x86_64-w64-mingw32-strip --strip-unneeded "$OUT"/*.dll
        x86_64-w64-mingw32-objdump -p "$OUT"/*.dll | grep -E "^/|DLL Name"
    }
    ;;

    linux-x64)
    {
        for lib in avcodec avformat avutil avfilter swresample swscale; do
            cp -L "$PREFIX"/lib/lib$lib.so.[0-9]* "$OUT/"
        done

        mkdir -p "$OUT/libva"
        cp -L "$PREFIX"/lib/libva.so.2 "$PREFIX"/lib/libva-drm.so.2 "$OUT/libva/"

        find "$OUT" -name '*.so.*.*' -delete
        strip --strip-unneeded "$OUT"/*.so.* "$OUT"/libva/*.so.*
        patchelf --set-rpath '$ORIGIN' "$OUT"/*.so.* "$OUT"/libva/*.so.*
    }
    ;;

    osx-arm64)
    {
        for lib in avcodec avformat avutil avfilter swresample swscale; do
            cp -L "$PREFIX"/lib/lib$lib.[0-9]*.dylib "$OUT/"
        done

        find "$OUT" -name '*.dylib' -exec codesign --force --sign - {} \;
    }
    ;;
esac

cp "$WORK/ffmpeg-$FFMPEG_VERSION/COPYING.LGPLv2.1" "$OUT/FFMPEG-LICENSE.txt"

echo "FFmpeg $FFMPEG_VERSION for $RID:"
ls -la "$OUT"
