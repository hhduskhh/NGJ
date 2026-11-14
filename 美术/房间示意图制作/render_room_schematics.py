"""Render rough room-layout schematics from RoomSchematicExportTests JSON.

Usage:
  python render_room_schematics.py
  python render_room_schematics.py --json TestResults/room-schematics.json

No game art is used: walls, floors, solid props, decorations and doors are
drawn as labeled color blocks from the actual P0 layout data.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONT = "C:/Windows/Fonts/msyh.ttc"
FONT_BOLD = "C:/Windows/Fonts/msyhbd.ttc"

THEME = {
    1: dict(name="静钟街区", floor=(74, 69, 91), wall=(44, 41, 61), grid=(60, 55, 75),
            solid=(190, 141, 86), solid_edge=(235, 196, 130), decor=(235, 196, 130),
            decal=(100, 88, 120), wall_decor=(120, 100, 135)),
    2: dict(name="锈鸣工坊", floor=(60, 59, 70), wall=(38, 37, 49), grid=(49, 47, 58),
            solid=(161, 107, 73), solid_edge=(217, 158, 96), decor=(217, 158, 96),
            decal=(82, 68, 56), wall_decor=(120, 95, 72)),
    3: dict(name="原音圣所", floor=(48, 59, 84), wall=(32, 41, 65), grid=(39, 49, 70),
            solid=(196, 184, 158), solid_edge=(238, 224, 194), decor=(238, 224, 194),
            decal=(68, 88, 112), wall_decor=(100, 125, 150)),
}
DOOR = (237, 189, 120)
START = (104, 190, 138)
BOSS = (242, 104, 112)
TRAP = (226, 76, 92)
TRAP_BOMB = (250, 140, 80)
NO_SPAWN = (104, 124, 78)
TEXT = (235, 238, 244)
MUTED = (160, 174, 192)
KIND = {
    "crate": "C", "pillar": "P", "station": "S", "rack": "R", "bench": "B",
    "pipe": "I", "pipeBend": "b", "panel": "N", "crystal": "Y", "fork": "F",
    "conduit": "D", "spares": "s",
}
ROLE = {"Start": "起始", "Middle": "中间", "Boss": "Boss"}
SHAPE = {"Single": "1×1", "Line2": "1×2", "Line3": "1×3", "Square2": "2×2", "L": "L 形"}


def font(path: str, size: int):
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default()


def build_mask(room: dict[str, Any], grid: int, cw: int, ch: int) -> list[list[int]]:
    w, h = room["width"], room["height"]
    mask = [[0] * w for _ in range(h)]
    cells = {(c["x"], c["y"]) for c in room["cells"]}

    def fill(x0: int, y0: int, fw: int, fh: int):
        for x in range(max(0, x0), min(w, x0 + fw)):
            for y in range(max(0, y0), min(h, y0 + fh)):
                mask[y][x] = 1

    for cell in room["cells"]:
        x0 = cell["x"] * cw - room["x"]
        y0 = (grid - 1 - cell["y"]) * ch - room["y"]
        fill(x0 + 1, y0 + 1, cw - 2, ch - 2)
        if (cell["x"] + 1, cell["y"]) in cells:
            fill(x0 + cw - 1, y0 + 1, 2, ch - 2)
        if (cell["x"], cell["y"] + 1) in cells:
            fill(x0 + 1, y0 - 1, cw - 2, 2)
        if all((cell["x"] + dx, cell["y"] + dy) in cells
               for dx, dy in ((1, 0), (0, 1), (1, 1))):
            fill(x0 + cw - 1, y0 - 1, 2, 2)
    for opening in room["openings"]:
        fill(opening["x"] - room["x"], opening["y"] - room["y"],
             opening["width"], opening["height"])
    return mask


def rect_for(room: dict[str, Any], x: int, y: int, w: int, h: int,
             scale: int, x_margin: int, header: int):
    left = x_margin + (x - room["x"]) * scale
    top = header + (room["height"] - (y - room["y"]) - h) * scale
    return left, top, left + w * scale, top + h * scale


def draw_room(room: dict[str, Any], theme: dict[str, Any], grid: int,
              cw: int, ch: int, scale: int = 4) -> Image.Image:
    header, footer = 76, 44
    content_w = room["width"] * scale
    canvas_w = max(content_w + 44, 420)
    mx = (canvas_w - content_w) // 2
    h = header + room["height"] * scale + footer
    image = Image.new("RGB", (canvas_w, h), (16, 19, 28))
    d = ImageDraw.Draw(image)
    title_font = font(FONT_BOLD, 20)
    sub_font = font(FONT, 12)
    small_font = font(FONT, 9)
    tiny_font = font(FONT, 8)

    role_color = START if room["role"] == "Start" else BOSS if room["role"] == "Boss" else theme["solid_edge"]
    title = f"#{room['index']:02d}  {room['name']}"
    d.text((mx, 10), title, fill=TEXT, font=title_font)
    sub = (f"{ROLE.get(room['role'], room['role'])} · {SHAPE.get(room['shape'], room['shape'])} · "
           f"{room['doors']} 门 · 实心 {room['solids']} / {room['coverage'] * 100:.1f}%")
    d.text((mx, 38), sub, fill=MUTED, font=sub_font)
    d.line((mx, 62, mx + content_w, 62), fill=(45, 52, 68), width=1)

    mask = build_mask(room, grid, cw, ch)
    for ly in range(room["height"]):
        for lx in range(room["width"]):
            x, y = mx + lx * scale, header + (room["height"] - 1 - ly) * scale
            if mask[ly][lx]:
                d.rectangle((x, y, x + scale - 1, y + scale - 1), fill=theme["floor"])
            else:
                d.rectangle((x, y, x + scale - 1, y + scale - 1), fill=theme["wall"])
    # Subtle tile grid.
    for lx in range(0, room["width"] + 1, 1):
        x = mx + lx * scale
        d.line((x, header, x, header + room["height"] * scale), fill=theme["grid"])
    for ly in range(0, room["height"] + 1, 1):
        y = header + ly * scale
        d.line((mx, y, mx + room["width"] * scale, y), fill=theme["grid"])

    for zone in room.get("noSpawn", []):
        left, top, right, bottom = rect_for(
            room, zone["x"], zone["y"], zone["width"], zone["height"],
            scale, mx, header)
        d.rectangle((left, top, right - 1, bottom - 1), fill=NO_SPAWN)

    for wall in room.get("walls", []):
        left, top, right, bottom = rect_for(
            room, wall["x"], wall["y"], wall["width"], wall["height"],
            scale, mx, header)
        d.rectangle((left, top, right - 1, bottom - 1), fill=theme["wall"],
                    outline=theme["grid"])

    for spec in room["decor"]:
        layer = spec["layer"]
        lx = spec["x"] - room["x"]
        ly = spec["y"] - room["y"]
        if layer == "WallObject":
            x0, y0 = mx + lx * scale, header + (room["height"] - 1 - ly) * scale
            d.rectangle((x0 + 1, y0 + 1, x0 + scale - 2, y0 + scale - 2), fill=theme["wall_decor"])
        elif layer == "FloorDecal":
            x0, y0 = mx + lx * scale, header + (room["height"] - 1 - ly) * scale
            d.ellipse((x0 - scale, y0 - scale, x0 + 2 * scale, y0 + 2 * scale), fill=theme["decal"])
        else:
            x0, y0 = mx + lx * scale, header + (room["height"] - 1 - ly) * scale
            d.ellipse((x0 + 1, y0 + 1, x0 + scale - 2, y0 + scale - 2), fill=theme["decor"])

    for spec in room["solidsList"]:
        left, top, right, bottom = rect_for(
            room, spec["x"], spec["y"], spec["width"], spec["height"], scale, mx, header)
        d.rectangle((left, top, right - 1, bottom - 1), fill=theme["solid"],
                    outline=theme["solid_edge"], width=1)
        label = KIND.get(spec["kind"], spec["kind"][:1].upper())
        if right - left >= 20 and bottom - top >= 14:
            d.text(((left + right) / 2, (top + bottom) / 2 - 5), label,
                   fill=(30, 28, 36), font=small_font, anchor="mm")

    for trap in room.get("traps", []):
        left, top, right, bottom = rect_for(
            room, trap["x"], trap["y"], 1, 1, scale, mx, header)
        color = TRAP_BOMB if trap.get("bomb") else TRAP
        d.rectangle((left, top, right - 1, bottom - 1), fill=color)

    for opening in room["openings"]:
        left, top, right, bottom = rect_for(
            room, opening["x"], opening["y"], opening["width"], opening["height"],
            scale, mx, header)
        d.rectangle((left, top, right - 1, bottom - 1), fill=DOOR)
        if right - left >= 12:
            d.text(((left + right) / 2, (top + bottom) / 2), "门", fill=(60, 45, 20),
                   font=tiny_font, anchor="mm")

    d.rectangle((mx - 2, header - 2, mx + room["width"] * scale + 1,
                 header + room["height"] * scale + 1), outline=role_color, width=2)

    legend = [("墙/隔断", theme["wall"]), ("地面", theme["floor"]), ("实心", theme["solid"]),
              ("装饰", theme["decor"]), ("门", DOOR), ("地刺", TRAP),
              ("炸弹", TRAP_BOMB), ("禁刷", NO_SPAWN)]
    x, y = 22, h - 36
    for text, color in legend:
        if x + 62 > canvas_w - 8:
            x, y = 22, y + 13
        d.rectangle((x, y, x + 10, y + 10), fill=color)
        d.text((x + 14, y - 1), text, fill=MUTED, font=tiny_font)
        x += 62
    return image


def draw_overview(floor: dict[str, Any], theme: dict[str, Any]) -> Image.Image:
    scale, margin, header = 3, 26, 66
    mw, mh = floor["mapWidth"], floor["mapHeight"]
    image = Image.new("RGB", (margin * 2 + mw * scale, header + mh * scale + 38), (16, 19, 28))
    d = ImageDraw.Draw(image)
    title_font = font(FONT_BOLD, 22)
    small_font = font(FONT, 11)
    tiny_font = font(FONT, 8)
    d.text((margin, 10), f"第 {floor['floor']} 层 · {floor['floorTitle']}", fill=TEXT, font=title_font)
    grid = floor["gridSize"]
    d.text((margin, 39), f"{grid}×{grid} 房间格 · {mw}×{mh} · 门/起点/终点/实心物示意",
           fill=MUTED, font=small_font)
    for room in floor["rooms"]:
        mask = build_mask(room, floor["gridSize"], floor["cellWidth"], floor["cellHeight"])
        for ly in range(room["height"]):
            for lx in range(room["width"]):
                gx = room["x"] + lx
                gy = room["y"] + ly
                x = margin + gx * scale
                y = header + (mh - 1 - gy) * scale
                d.rectangle((x, y, x + scale - 1, y + scale - 1),
                            fill=theme["floor"] if mask[ly][lx] else theme["wall"])
        for wall in room.get("walls", []):
            left = margin + wall["x"] * scale
            top = header + (mh - (wall["y"] + wall["height"])) * scale
            d.rectangle((left, top,
                         left + wall["width"] * scale - 1,
                         top + wall["height"] * scale - 1), fill=theme["wall"])
        for spec in room["solidsList"]:
            left = margin + spec["x"] * scale
            top = header + (mh - (spec["y"] + spec["height"])) * scale
            d.rectangle((left, top,
                         left + spec["width"] * scale - 1,
                         top + spec["height"] * scale - 1), fill=theme["solid"])
        for trap in room.get("traps", []):
            left = margin + trap["x"] * scale
            top = header + (mh - (trap["y"] + 1)) * scale
            d.rectangle((left, top, left + scale - 1, top + scale - 1),
                        fill=TRAP_BOMB if trap.get("bomb") else TRAP)
        for opening in room["openings"]:
            left = margin + opening["x"] * scale
            top = header + (mh - (opening["y"] + opening["height"])) * scale
            d.rectangle((left, top,
                         left + opening["width"] * scale - 1,
                         top + opening["height"] * scale - 1), fill=DOOR)
        left = margin + room["x"] * scale
        top = header + (mh - (room["y"] + room["height"])) * scale
        right = left + room["width"] * scale - 1
        bottom = top + room["height"] * scale - 1
        color = START if room["role"] == "Start" else BOSS if room["role"] == "Boss" else (120, 132, 154)
        d.rectangle((left, top, right, bottom), outline=color, width=2)
        cx, cy = (left + right) / 2, (top + bottom) / 2
        d.text((cx, cy - 7), f"#{room['index']:02d}", fill=TEXT, font=tiny_font, anchor="mm")
        d.text((cx, cy + 5), room["name"], fill=(210, 218, 230), font=tiny_font, anchor="mm")
    d.text((margin, image.height - 27),
           "起/终边框：起点绿、Boss 红；金色为门；深色为墙；亮色为实心物",
           fill=MUTED, font=tiny_font)
    return image


def safe_name(floor_number: int, room: dict[str, Any]) -> str:
    return (f"floor{floor_number}-room{room['index']:02d}-"
            f"{room['role'].lower()}-{room['shape'].lower()}.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", default=str(Path(__file__).parent / "room-schematics.json"))
    parser.add_argument("--out", default=str(ROOT / "Docs/Images/房间示意图-20261114"))
    parser.add_argument("--doc", default=str(ROOT / "Docs/062-当前房间示意图.md"))
    args = parser.parse_args()

    data = json.loads(Path(args.json).read_text(encoding="utf-8"))
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    lines = [
        "# 062-当前房间示意图",
        "",
        "由当前实际生成数据导出的 P0 房间示意图；**不是正式美术**，只表示墙体、门、实心物、装饰和房间形状。",
        "",
        "- 数据来源：`RoomSchematicExportTests`，seed 713，第 1–3 层；JSON 保存在 `美术/房间示意图制作/room-schematics.json`。",
        "- 图例：深色=墙与室内隔断，紫色/灰色=地面，橙色块=实心物（字母为道具类型），金色=门，红色=地刺，橙红=炸弹，橄榄绿=禁刷区，小点=非碰撞装饰。",
        "- 类型缩写：C 箱、P 柱、S 工作台、R 武器架、B 长椅、I 直管、b 弯管、N 面板、Y 晶体、F 音叉、D 导管、s 零件。",
        "- 房间使用三层 P1 原生美术；实心覆盖率按 `NavigationBounds` 统计。陷阱标记来自运行时实例的真实位置，禁刷区包含完整菱形内侧。",
        "",
    ]
    for floor in data["floors"]:
        theme = THEME.get(floor["floor"], THEME[1])
        overview = draw_overview(floor, theme)
        overview_name = f"floor{floor['floor']}-overview.png"
        overview.save(out / overview_name)
        lines += [
            f"## 第 {floor['floor']} 层 · {floor['floorTitle']}",
            "",
            f"![第 {floor['floor']} 层总览](Images/房间示意图-20261114/{overview_name})",
            "",
            "| # | 房间 | 角色 / 房型 | 门 | 实心物 / 覆盖率 | 示意图 |",
            "| ---: | --- | --- | ---: | ---: | --- |",
        ]
        for room in floor["rooms"]:
            image = draw_room(room, theme, floor["gridSize"], floor["cellWidth"], floor["cellHeight"])
            name = safe_name(floor["floor"], room)
            image.save(out / name)
            lines.append(
                f"| {room['index']:02d} | {room['name']} | {ROLE.get(room['role'], room['role'])} / "
                f"{SHAPE.get(room['shape'], room['shape'])} | {room['doors']} | "
                f"{room['solids']} / {room['coverage'] * 100:.1f}% | "
                f"[打开](Images/房间示意图-20261114/{name}) |"
            )
        lines.append("")
        for room in floor["rooms"]:
            name = safe_name(floor["floor"], room)
            lines += [
                f"### #{room['index']:02d} {room['name']}",
                "",
                f"![{room['name']}](Images/房间示意图-20261114/{name})",
                "",
            ]
    Path(args.doc).write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"rendered {len(data['floors'])} floors to {out}")
    print(f"doc: {args.doc}")


if __name__ == "__main__":
    main()
