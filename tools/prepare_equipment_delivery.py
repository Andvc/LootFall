#!/usr/bin/env python3
"""Fit generated equipment art to the handoff's exact canvases.

Only technical framing is changed: uniform scale, translation, and flat key cleanup.
No anatomy is synthesized, mirrored, stretched, or painted by this script.
Usage: python tools/prepare_equipment_delivery.py /path/to/local-inputs.json
The input list supplies source, destination, kind, size, and optional views.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, PngImagePlugin

KEY = (0, 255, 0)
ROOT = Path(__file__).resolve().parents[1]


def clean_key(im):
    rgb = np.array(im.convert('RGB'))
    wide = rgb.astype(np.int16)
    mask = (wide[:, :, 1] > wide[:, :, 0] + 60) & (wide[:, :, 1] > wide[:, :, 2] + 60)
    rgb[mask] = KEY
    return Image.fromarray(rgb)


def crop_subject(im):
    rgb = np.array(im)
    mask = np.any(rgb != KEY, axis=2)
    yy, xx = np.where(mask)
    if not len(xx):
        raise ValueError('No subject')
    return im.crop((int(xx.min()), int(yy.min()), int(xx.max()) + 1, int(yy.max()) + 1))


def prepare(item):
    src = Image.open(item['source']).convert('RGB')
    size = tuple(item['size'])
    details = {'native_size': list(src.size), 'final_size': list(size)}
    if item['kind'] == 'backdrop':
        out = src.resize(size, Image.Resampling.LANCZOS)
        details['operation'] = 'Resize to exact handoff canvas'
    elif item['kind'] == 'figure':
        body = crop_subject(clean_key(src))
        scale = 3221 / body.height
        target = (round(body.width * scale), 3221)
        if target[0] > 1900:
            raise ValueError('Figure is too wide; regenerate without stretching')
        fitted = clean_key(body.resize(target, Image.Resampling.LANCZOS))
        out = Image.new('RGB', size, KEY)
        offset = ((size[0] - target[0]) // 2, 380)
        out.paste(fitted, offset)
        details.update(operation='Uniform fit and translation; flat key cleanup', scale=scale, offset=list(offset))
    elif item['kind'] == 'page':
        n = item['views']
        keyed = clean_key(src)
        parts = [crop_subject(keyed.crop((round(i * src.width / n), 0, round((i + 1) * src.width / n), src.height))) for i in range(n)]
        desired_height = item.get('subject_height', 1800 if n == 3 else 1900)
        margin = item.get('cell_margin', 100)
        scale = min(desired_height / max(p.height for p in parts), (size[0] / n - margin) / max(p.width for p in parts))
        out = Image.new('RGB', size, KEY)
        positions = []
        for i, part in enumerate(parts):
            target = (round(part.width * scale), round(part.height * scale))
            fitted = clean_key(part.resize(target, Image.Resampling.LANCZOS))
            offset = (round((i + 0.5) * size[0] / n - target[0] / 2), round((size[1] - target[1]) / 2))
            out.paste(fitted, offset)
            positions.append({'size': list(target), 'offset': list(offset)})
        details.update(operation='Same uniform scale for every view; center in each cell; flat key cleanup', scale=scale, views=positions)
    elif item['kind'] == 'glyphs':
        keyed = clean_key(src)
        out = Image.new('RGB', size, KEY)
        for i in range(10):
            x, y = i % 5, i // 5
            cell = keyed.crop((round(x * src.width / 5), round(y * src.height / 2), round((x + 1) * src.width / 5), round((y + 1) * src.height / 2)))
            glyph = crop_subject(cell)
            scale = 307 / max(glyph.size)
            target = (round(glyph.width * scale), round(glyph.height * scale))
            fitted = clean_key(glyph.resize(target, Image.Resampling.LANCZOS))
            out.paste(fitted, (x * 512 + (512 - target[0]) // 2, y * 512 + (512 - target[1]) // 2))
        details['operation'] = 'Uniformly fit each generated glyph to 60 percent of its 512px cell; flat key cleanup'
    else:
        out = clean_key(src.resize(size, Image.Resampling.LANCZOS))
        details['operation'] = 'Resize to exact handoff canvas; flat key cleanup'
    target_path = ROOT / item['destination']
    target_path.parent.mkdir(parents=True, exist_ok=True)
    info = PngImagePlugin.PngInfo()
    info.add(b'sRGB', b'\x00')
    out.save(target_path, pnginfo=info, optimize=True)
    print(item['destination'], src.size, '->', out.size)
    return dict(file=item['destination'], **details)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('inputs', type=Path)
    p.add_argument('--report', type=Path)
    args = p.parse_args()
    records = [prepare(item) for item in json.loads(args.inputs.read_text(encoding='utf-8'))]
    if args.report:
        args.report.write_text(json.dumps(records, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
