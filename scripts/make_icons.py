"""Draws the two 1024 px app icons: a letter on a solid square, no transparency (Apple rule)."""
import sys
from PIL import Image, ImageDraw, ImageFont

ICONS = {"G": ((71, 140, 191), sys.argv[1]), "U": ((40, 40, 40), sys.argv[2])}

for letter, (color, path) in ICONS.items():
    img = Image.new("RGB", (1024, 1024), color)
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arialbd.ttf", 640)
    except OSError:
        font = ImageFont.load_default(640)
    draw.text((512, 512), letter, fill="white", font=font, anchor="mm")
    img.save(path)
