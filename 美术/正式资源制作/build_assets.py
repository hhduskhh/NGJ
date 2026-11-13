"""Purpose-made animation frames -> fixed pixel grids; extract existing pixel tiles.

Requires Pillow. Run from any directory: python build_assets.py --floors 1
Second batch: python build_assets.py --floors 2 3
Source concepts remain untouched. No generated background is used as a game map.
"""
from pathlib import Path
import argparse
import json
import math
import shutil
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).parent / '原始帧图'
OUT = ROOT / 'Assets/Echoes/Resources/Art'
PACK = ROOT / '美术/Pixel Crawler - Free Pack (Exterior 0.1)/Pixel Crawler - Free Pack'
PALETTE = ['151724','252838','343348','49465c','656078','898298',
           '46343b','64464b','885952','aa765b','cf9864','edbd78','ffe0a0',
           '1d3d46','305762','47727a','679298','94b9b7','cee0ce',
           '33533e','587449','91a85a','bece80','e5c985','f5d898',
           'f26870','b63e56','782f49','512f56','765380','a07eae',
           'eee1c4','c8bca6','a1968d','394f60','536879','78959e']
COLORS = [tuple(bytes.fromhex(h)) for h in PALETTE]


def clean(im):
    im = im.convert('RGBA')
    mask = im.getchannel('A').point(lambda a: 255 if a >= 210 else 0)
    mask = mask.filter(ImageFilter.MedianFilter(3))
    im.putalpha(mask)
    return im


def remove_strays(im):
    # Some generated rows overlap the regular grid by a few antenna pixels.
    # Keep meaningful disconnected armor parts, remove tiny fragments of adjacent cells.
    mask = bytearray(im.getchannel('A').tobytes())
    width, height = im.size
    components = []
    for start in range(len(mask)):
        if not mask[start]: continue
        mask[start] = 0
        todo = [start]; component = []
        while todo:
            point = todo.pop(); component.append(point)
            x, y = point % width, point // width
            neighbors = []
            if x: neighbors.append(point - 1)
            if x + 1 < width: neighbors.append(point + 1)
            if y: neighbors.append(point - width)
            if y + 1 < height: neighbors.append(point + width)
            for other in neighbors:
                if mask[other]: mask[other] = 0; todo.append(other)
        components.append(component)
    largest = max(map(len, components), default=0)
    keep = bytearray(width * height)
    for component in components:
        if len(component) >= max(16, largest * .02):
            for point in component: keep[point] = 255
    im.putalpha(Image.frombytes('L', im.size, bytes(keep)))
    return im


def pixel_palette(im):
    result = Image.new('RGBA', im.size)
    lookup = {}
    data = []
    for r, g, b, a in im.getdata():
        if a < 128:
            data.append((0, 0, 0, 0))
            continue
        key = (r, g, b)
        if key not in lookup:
            lookup[key] = min(COLORS, key=lambda c: sum((c[i] - key[i]) ** 2 for i in range(3)))
        data.append((*lookup[key], 255))
    result.putdata(data)
    return result


def extract_grid(path, cols, rows):
    im = Image.open(path).convert('RGBA')
    frames = []
    for row in range(rows):
        for col in range(cols):
            cell = clean(im.crop((round(col * im.width / cols), round(row * im.height / rows),
                                  round((col + 1) * im.width / cols), round((row + 1) * im.height / rows))))
            cell = remove_strays(cell)
            bbox = cell.getbbox()
            if not bbox:
                raise ValueError(f'Empty frame {path} / {row},{col}')
            frames.append(cell.crop(bbox))
    return frames


def align(frames, size, max_height=None):
    # One scale for an entire character, not a different enlargement per pose.
    scale = min((size - 4) / max(f.width for f in frames),
                (max_height or size - size // 8) / max(f.height for f in frames))
    result = []
    for f in frames:
        f = pixel_palette(f.resize((max(1, round(f.width * scale)), max(1, round(f.height * scale))),
                                   Image.Resampling.NEAREST))
        canvas = Image.new('RGBA', (size, size))
        canvas.alpha_composite(f, ((size - f.width) // 2, size - (size // 8 - 1) - f.height))
        result.append(canvas)
    return result


def sheet(frames, cols, path):
    size = frames[0].width
    im = Image.new('RGBA', (size * cols, math.ceil(len(frames) / cols) * size))
    for i, frame in enumerate(frames):
        im.alpha_composite(frame, ((i % cols) * size, (i // cols) * size))
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)


THEMES = [
    dict(base='#4a455b', dark='#302d41', mid='#625b74', light='#898098',
         metal='#be8d56', high='#ebc482', accent='#567c7e'),
    dict(base='#3d3c46', dark='#282733', mid='#62514f', light='#997059',
         metal='#a16b49', high='#d99e60', accent='#54716d'),
    dict(base='#303b54', dark='#222b43', mid='#48516b', light='#718098',
         metal='#c4b89e', high='#eee0c2', accent='#789eaa')]


def tile(theme, index):
    p = THEMES[theme]
    if theme == 0:
        # Actual 16px dungeon tiles, preserving native pixels, then material recoloring.
        original = Image.open(PACK / 'Environment/Tilesets/Dungeon_Tiles.png').convert('RGBA')
        im = original.crop((64 + index % 4 * 16, 16, 80 + index % 4 * 16, 32))
        colors = ['#444052', '#484456', '#4c485b']
        im.putdata([(*bytes.fromhex(colors[min(2, (r+g+b)//150)].lstrip('#')), 255)
                    for r, g, b, a in im.getdata()])
    else:
        im = Image.new('RGBA', (16, 16), p['base'])
    d = ImageDraw.Draw(im)
    d.line((0, 15, 15, 15), fill=p['dark'])
    d.line((15, 0, 15, 15), fill=p['dark'])
    if theme == 1:
        d.line((1, 0, 14, 0), fill=p['mid'])
        for x, y in [(2, 2), (13, 2), (2, 13), (13, 13)]:
            d.point((x, y), fill=p['dark'])
            d.point((x, y - 1), fill=p['light'])
        if index % 2:
            d.line((4, 8, 11, 8), fill=p['dark'])
    elif theme == 2:
        d.line((1, 0, 14, 0), fill=p['mid'])
        if index == 3:
            d.line([(8, 4), (11, 8), (8, 12), (5, 8), (8, 4)], fill=p['mid'])
    elif index == 2:
        d.line([(4, 3), (5, 5), (4, 7)], fill=p['dark'])
    return im


def wall(theme, index):
    p = THEMES[theme]
    im = Image.new('RGBA', (16, 16), p['dark'])
    d = ImageDraw.Draw(im)
    cap = p['mid'] if theme < 2 else p['high']
    face = p['base'] if theme < 2 else p['metal']
    d.rectangle((1, 1, 14, 5), fill=cap)
    d.line((2, 0, 13, 0), fill=p['light'] if theme < 2 else '#fff0d4')
    d.rectangle((1, 7, 14, 13), fill=face)
    d.line((2, 7, 13, 7), fill=cap)
    if theme == 0:
        d.line((7 if index % 2 else 11, 8, 7 if index % 2 else 11, 13), fill=p['dark'])
        d.point((3, 10), fill=p['mid'])
    elif theme == 1:
        for x in [2, 13]:
            d.point((x, 2), fill=p['high'])
            d.point((x, 11), fill=p['metal'])
        if index % 2:
            d.rectangle((6, 7, 8, 13), fill=p['accent'])
    else:
        d.line((2, 12, 13, 12), fill=p['high'])
        if index % 2:
            d.line((8, 7, 8, 11), fill=p['accent'])
    return im


def prop(theme, kind):
    p = THEMES[theme]
    im = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(im)
    outline = '#191d2c'
    if kind == 'rack':
        original = Image.open(PACK / 'Environment/Props/Static/Dungeon_Props.png').convert('RGBA')
        return original.crop((0, 32, 32, 64))
    if kind == 'crate':
        d.rectangle((5, 12, 26, 27), fill=outline)
        d.rectangle((6, 13, 25, 25), fill=p['metal'])
        d.rectangle((7, 10, 24, 15), fill=p['high'])
        for x in [9, 22]:
            d.line((x, 12, x, 25), fill=p['dark'], width=2)
        d.line((7, 22, 24, 22), fill=p['dark'])
    elif kind == 'lamp':
        d.rectangle((13, 4, 18, 26), fill=outline)
        d.rectangle((14, 5, 17, 22), fill=p['metal'])
        d.rectangle((9, 8, 22, 19), fill=outline)
        d.rectangle((10, 9, 21, 17), fill=p['metal'])
        d.rectangle((12, 10, 19, 16), fill='#edbd78' if theme < 2 else '#94b9b7')
        d.line((14, 10, 14, 15), fill='#ffe0a0')
    elif kind == 'doorpost':
        d.rectangle((11, 2, 21, 29), fill=outline)
        d.rectangle((12, 3, 20, 26), fill=p['metal'] if theme < 2 else p['high'])
        d.rectangle((13, 5, 19, 7), fill=p['high'])
        d.rectangle((13, 11, 19, 24), fill=p['mid'] if theme < 2 else p['metal'])
        d.line((15, 13, 15, 23), fill=p['accent'])
        d.rectangle((10, 26, 22, 29), fill=p['light'])
    elif kind == 'station':
        d.rectangle((4, 12, 27, 28), fill=outline)
        d.rectangle((5, 13, 26, 25), fill=p['metal'])
        d.rectangle((7, 3, 24, 17), fill=outline)
        d.rectangle((8, 4, 23, 15), fill=p['mid'] if theme < 2 else p['high'])
        d.ellipse((11, 6, 20, 15), fill=p['dark'])
        d.ellipse((13, 8, 18, 13), fill=p['accent'])
        d.line((8, 21, 23, 21), fill=p['dark'], width=2)
    elif kind == 'signature':
        if theme == 0:
            d.rectangle((5, 5, 26, 7), fill=p['metal'])
            d.line((8, 6, 8, 11), fill=p['high'])
            d.polygon([(11, 10),(20, 10),(24, 22),(7, 22)], fill=outline)
            d.polygon([(12, 11),(19, 11),(22, 20),(9, 20)], fill=p['metal'])
            d.line((12, 12, 11, 18), fill=p['high'])
            d.rectangle((7, 21, 24, 23), fill=p['high'])
            d.rectangle((15, 24, 17, 25), fill=p['dark'])
        elif theme == 1:
            d.rectangle((7, 6, 25, 26), fill=outline)
            d.rectangle((8, 7, 24, 25), fill=p['accent'])
            d.ellipse((8, 3, 24, 11), fill=p['metal'], outline=outline)
            d.ellipse((11, 4, 21, 8), fill=p['high'])
            for y in [12, 21]:
                d.rectangle((8, y, 24, y+2), fill=p['metal'])
            d.ellipse((13, 14, 20, 20), fill=p['dark'])
            d.line((16, 15, 16, 19), fill=p['high'])
        else:
            d.rectangle((7, 25, 24, 28), fill=p['metal'])
            d.polygon([(16, 2),(23, 11),(16, 23),(9, 11)], fill=p['accent'], outline=outline)
            d.line((16, 3, 16, 20), fill='#cee0ce')
            d.line((10, 11, 16, 14, 22, 11), fill=p['high'])
    elif kind == 'altar':
        # Walkable challenge seal: flat floor inlay, not a solid-looking pedestal.
        d.ellipse((3,3,28,28), fill=outline)
        d.ellipse((4,4,27,27), fill=p['metal'])
        d.ellipse((6,6,25,25), fill=p['dark'])
        d.line([(16,7),(24,16),(16,24),(7,16),(16,7)],fill=p['high'])
        d.rectangle((14,12,17,20),fill=p['accent'])
        d.rectangle((10,11,12,15),fill=p['high'])
        d.rectangle((19,11,21,15),fill=p['high'])
    elif kind == 'pillar':
        d.rectangle((7, 25, 24, 28), fill=outline)
        d.rectangle((10, 7, 21, 26), fill=p['metal'])
        d.rectangle((11, 8, 14, 24), fill=p['high'])
        d.rectangle((7, 4, 24, 9), fill=p['high'])
        d.line((8, 9, 23, 9), fill=p['dark'])
        if theme == 2:
            d.rectangle((14, 0, 17, 5), fill=p['dark'])
    elif kind in ('pipe','pipeBend','conduit'):
        d.rectangle((1, 14, 30, 21), fill=outline)
        d.rectangle((1, 15, 30, 19), fill=p['accent'])
        d.line((1, 15, 30, 15), fill=p['high'])
        if kind=='pipeBend':
            d.rectangle((14, 2, 21, 20), fill=outline)
            d.rectangle((15, 2, 19, 19), fill=p['accent'])
            d.line((15, 2, 15, 18),fill=p['high'])
        for x in [5,24]:
            d.rectangle((x, 13, x+2, 22), fill=p['metal'])
        if kind=='conduit':
            d.rectangle((12, 14, 20, 21),fill=p['dark'])
            d.line((13,17,19,17),fill=p['accent'])
    elif kind=='bench':
        d.rectangle((4, 11, 27, 14),fill=outline)
        d.rectangle((5, 12, 26, 13),fill=p['metal'])
        d.rectangle((4, 18, 27, 21),fill=p['metal'])
        for x in [6,24]: d.rectangle((x, 14, x+1, 26),fill=p['dark'])
    elif kind=='fork':
        d.rectangle((7, 25, 24, 28),fill=outline)
        d.line([(11,3),(11,14),(15,18),(20,14),(20,3)],fill=p['metal'],width=3)
        d.rectangle((14,18,17,25),fill=p['high'])
        d.line((11,4,11,13),fill=p['high'])
    elif kind=='crystal':
        d.rectangle((7,25,24,28),fill=p['metal'])
        d.polygon([(16,3),(24,13),(16,24),(8,13)],fill=p['accent'],outline=outline)
        d.line((16,4,16,23),fill='#cee0ce')
        d.line((9,13,15,16),fill=p['high'])
    elif kind=='panel':
        d.rectangle((5,5,26,26),fill=outline)
        d.rectangle((6,6,25,24),fill=p['metal'])
        for y in [10,15,20]:
            d.line((9,y,22,y),fill=p['dark'],width=2)
    elif kind=='spares':
        for x,y in [(7,21),(17,24),(22,18)]:
            d.rectangle((x-3,y-2,x+3,y+2),fill=p['metal'],outline=outline)
            d.line((x,y-2,x+2,y-1),fill=p['high'])
    return im


def common_art():
    hero = align(extract_grid(SOURCE / 'hero.png', 4, 5), 32)
    sheet(hero, 4, OUT / 'Common/hero.png')
    frames = []
    for kind in range(8):
        im = Image.new('RGBA', (16,16)); d = ImageDraw.Draw(im)
        if kind == 0:  # Independent tuning fork pistol, points right.
            d.rectangle((2, 6, 8, 9), fill='#252838')
            d.rectangle((3, 7, 8, 8), fill='#be8d56')
            d.rectangle((5, 9, 7, 12), fill='#64464b')
            d.rectangle((8, 3, 14, 5), fill='#edbd78')
            d.rectangle((8, 10, 14, 12), fill='#edbd78')
            d.rectangle((8, 4, 9, 11), fill='#be8d56')
            d.line((10, 3, 14, 3), fill='#ffe0a0')
        elif kind == 1:
            d.rectangle((7, 2, 9, 3), fill='#94b9b7')
            d.polygon([(6,4),(10,4),(12,11),(4,11)], fill='#be8d56', outline='#252838')
            d.line((6,5,5,9), fill='#ffe0a0')
            d.rectangle((3,11,13,12), fill='#edbd78')
            d.rectangle((7,13,9,14), fill='#64464b')
        elif kind in (2,3):
            c = '#edbd78' if kind==2 else '#f26870'
            d.ellipse((4,5,11,10), fill='#64464b' if kind==2 else '#782f49')
            d.ellipse((5,6,10,9), fill=c)
            d.line((6,6,9,6), fill='#ffe0a0' if kind==2 else '#eee1c4')
        elif kind==4:
            d.ellipse((3,6,12,10), fill=(12,15,24,100))
        elif kind==5:  # Sleep indicator: closed eye, not a text label.
            d.line([(4,7),(7,9),(10,9),(12,7)], fill='#94b9b7', width=1)
        elif kind==6:
            d.rectangle((7,3,8,8), fill='#f26870'); d.rectangle((7,11,8,12), fill='#ffe0a0')
        else:
            d.line([(5,5),(8,3),(11,5),(8,8),(8,9)],fill='#edbd78')
            d.point((8,12),fill='#ffe0a0')
        frames.append(im)
    sheet(frames,8,OUT/'Common/items.png')
    effects=[]
    for frame in range(8):
        im=Image.new('RGBA',(16,16)); d=ImageDraw.Draw(im)
        if frame<4:
            r=[3,6,5,3][frame]
            for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                d.line((8+dx*2,8+dy*2,8+dx*r,8+dy*r),fill='#edbd78')
            d.rectangle((7,7,9,9),fill='#ffe0a0')
        else:
            r=[3,5,6,7][frame-4]
            for dx,dy in [(1,1),(-1,1),(1,-1),(-1,-1)]:
                d.rectangle((8+dx*r//2,8+dy*r//2,9+dx*r//2,9+dy*r//2),fill='#edbd78')
        effects.append(im)
    sheet(effects,8,OUT/'Common/effects.png')
    solid=Image.new('RGBA',(16,16),'white'); solid.save(OUT/'Common/solid.png')


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--floors',type=int,nargs='+',default=[1,2,3])
    floors=parser.parse_args().floors
    common_art()
    for floor in floors:
        folder=OUT/f'Floor{floor}'; folder.mkdir(parents=True,exist_ok=True)
        raw=extract_grid(SOURCE/f'floor{floor}.png',6,4)
        for row,name in enumerate(['walker','guard','elite','boss']):
            frames=align(raw[row*6:row*6+6],64 if name=='boss' else 32)
            sheet(frames,6,folder/f'{name}.png')
        sheet([tile(floor-1,i) for i in range(4)]+[wall(floor-1,i) for i in range(4)],8,folder/'tiles.png')
        names=['crate','lamp','doorpost','station','signature','altar','pillar','rack',
               'pipe','pipeBend','bench','fork','crystal','panel','spares','conduit']
        sheet([prop(floor-1,n) for n in names],8,folder/'props.png')
    shutil.copyfile(PACK/'Terms.txt',OUT/'PixelCrawler-Terms.txt')
    (OUT/'Provenance.txt').write_text('Pixel Crawler Free Pack by Anokolisa: Floor1 ground tiles and rack prop extracted from native pixel art; recoloring permitted by included Terms. Other sprites: purpose-made image_gen frames, alpha-cleaned, palette reduced, aligned to fixed pixel cells. Tiles, weapons and additional props redrawn for this project. Source frame sheets, prompts and build_assets.py retained under 美术/正式资源制作. Concepts are not used as scene backgrounds.\n',encoding='utf-8')
    print(json.dumps({'floors':floors,'pngs':len(list(OUT.rglob('*.png')))},ensure_ascii=False))


if __name__=='__main__': main()
