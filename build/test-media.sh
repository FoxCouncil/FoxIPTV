#!/usr/bin/env bash
# Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV
set -euo pipefail

OUT=tests/FoxIPTV.Tests/Media
WORK=$(mktemp -d)

encode() {
    local name=$1 pattern=$2 size=$3 tone=$4 seconds=$5 offset=$6 type=$7
    local dir="$OUT/$name"
    local playlist="$WORK/${name//\//-}.m3u8"
    local extension=ts
    local segments=(-hls_segment_type mpegts)

    if [ "$type" = fmp4 ]; then
        extension=m4s
        segments=(-hls_segment_type fmp4 -hls_fmp4_init_filename init.mp4)
    fi

    mkdir -p "$dir"

    ffmpeg -hide_banner -loglevel error -y \
        -f lavfi -i "$pattern=size=$size:rate=25" \
        -f lavfi -i "sine=frequency=$tone:sample_rate=48000" \
        -t "$seconds" -map 0:v -map 1:a \
        -c:v libx264 -preset veryfast -profile:v baseline -pix_fmt yuv420p -g 25 -keyint_min 25 -sc_threshold 0 -crf 40 -threads 1 \
        -c:a aac -b:a 32k -ac 2 \
        -output_ts_offset "$offset" \
        -f hls -hls_time 1 -hls_list_size 0 -hls_playlist_type vod "${segments[@]}" \
        -hls_segment_filename "$dir/%d.$extension" "$playlist"

    if [ "$type" = fmp4 ]; then
        mv "$WORK/init.mp4" "$dir/init.mp4"
    fi
}

encode low testsrc2 160x90 440 4 10 ts
encode high testsrc2 320x180 440 4 10 ts
encode _ad/creative/0123456789abcdef0123 testsrc 160x90 880 2 80000 ts
encode fmp4 testsrc2 160x90 440 2 10 fmp4

find "$OUT" -type f -printf '%8s  %p\n' | sort -k2
