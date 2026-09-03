#!/usr/bin/env bash
# =============================================
# Regenerates reference analysis frames from the reference video.
# Requires: ffmpeg on PATH, or python3 + imageio-ffmpeg (pip).
# Usage: ./scripts/extract_reference_frames.sh
# =============================================
set -euo pipefail
cd "$(dirname "$0")/.."

VIDEO="2d7a9744e7a9eb3cce978c7f45cbdcdb_1788379399516.mp4"
[ -f "$VIDEO" ] || { echo "ERROR: reference video not found at repo root: $VIDEO"; exit 1; }

if command -v ffmpeg >/dev/null 2>&1; then
  FF=ffmpeg
else
  FF=$(python3 -c "import imageio_ffmpeg; print(imageio_ffmpeg.get_ffmpeg_exe())" 2>/dev/null || true)
  [ -n "$FF" ] || { echo "ERROR: install ffmpeg or 'pip install imageio-ffmpeg' first."; exit 1; }
fi

mkdir -p reference/frames reference/chars

echo "== 1 frame / 10 s -> reference/frames/ =="
"$FF" -hide_banner -loglevel error -i "$VIDEO" -vf "fps=1/10" -q:v 3 reference/frames/f_%03d.jpg -y

echo "== timestamped contact sheet -> reference/contact_sheet.jpg =="
"$FF" -hide_banner -loglevel error -i "$VIDEO" -vf "fps=1/10,scale=192:341,tile=6x6" -frames:v 1 -q:v 4 reference/contact_sheet.jpg -y
python3 - <<'EOF' || echo "(PIL missing: contact sheet left without timestamp labels)"
from PIL import Image, ImageDraw
img = Image.open("reference/contact_sheet.jpg")
d = ImageDraw.Draw(img)
for i in range(35):
    r, c = divmod(i, 6)
    x, y = c*192, r*341
    t = i*10
    d.rectangle([x, y, x+40, y+14], fill="black")
    d.text((x+3, y+2), f"{t//60}:{t%60:02d}", fill="yellow")
img.save("reference/contact_sheet.jpg", quality=90)
print("timestamps added")
EOF

echo "== curated character frames -> reference/chars/ =="
for t in 0.5 8 10 12 14 20 30 40 60 70 230 240 270 320 330 340 345 347; do
  "$FF" -hide_banner -loglevel error -ss "$t" -i "$VIDEO" -frames:v 1 -q:v 2 "reference/chars/t_${t}.jpg" -y
done

echo "Done. See CHARACTER_REFERENCE.md §5 for the frame index."
