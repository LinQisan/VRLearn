"""Bake the browser's three signal plates with Pillow and the project's Japanese font.

Run with a Python installation that provides Pillow. Outputs are static textures;
the headset does not rasterize sign text or require additional runtime glyphs.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONT = ROOT / 'Assets/_Project/UI/Fonts/NotoSansJP-SemiBold.ttf'
OUT = ROOT / 'Assets/_Project/Hikone/Textures'
SCALE = 3


def plate(kind):
    width, height = {'Name': (160, 400), 'Phase': (420, 160), 'Walk': (64, 300)}[kind]
    image = Image.new('RGB', (width * SCALE, height * SCALE))
    draw = ImageDraw.Draw(image)

    def rect(box, colour, border=None, line=1):
        draw.rectangle(tuple(round(v * SCALE) for v in box), fill=colour,
                       outline=border, width=line * SCALE)

    def text(value, x, y, size, colour):
        font = ImageFont.truetype(str(FONT), size * SCALE)
        draw.text((x * SCALE, y * SCALE), value, font=font, fill=colour, anchor='mm')

    if kind == 'Name':
        rect((0, 0, width, height), '#f4f6f4')
        rect((8, 8, width - 8, height - 8), '#f4f6f4', '#2f5fae', 6)
        text('彦根市', width / 2, 38, 22, '#2f5fae')
        text('京', width / 2, 120, 96, '#2f5fae')
        text('橋', width / 2, 230, 96, '#2f5fae')
        text('Kyobashi', width / 2, 340, 26, '#2f5fae')
    elif kind == 'Phase':
        rect((0, 0, width, height), '#d8652d')
        rect((12, 12, width - 12, height - 12), '#eef2f0')
        text('歩車分離式', width / 2, 58, 62, '#2652a3')
        text('7:30 − 18:00', width / 2, 118, 44, '#2652a3')
    else:
        rect((0, 0, width, height), '#2652a3')
        for i, character in enumerate('歩車分離式'):
            text(character, width / 2, 40 + i * 55, 40, '#f4f6f4')
    return image.resize((width, height), Image.Resampling.LANCZOS)


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for kind in ('Name', 'Phase', 'Walk'):
        path = OUT / f'HK_T_Signal{kind}.png'
        plate(kind).save(path)
        print(path.relative_to(ROOT))
