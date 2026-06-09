#!/usr/bin/env python3
# Воспроизводит стиль fig-3.1 (дерево хранилища): белый фон, чёрные line-art иконки
# папок/файлов, ветки-уголки слева, моноширинные подписи, описания справа.
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.path import Path
import matplotlib.patches as mpatches

MONO = {"family": "DejaVu Sans Mono", "size": 15}
SANS = {"family": "DejaVu Sans", "size": 14}
LW = 1.1
BLACK = "#000000"

# node: (parent_index, depth, kind, label, desc)   kind: folder | archive | file
nodes = [
    (-1, 0, "folder",  "Documents/PromeonLab/",  "— каталог экспорта"),
    ( 0, 1, "archive", "Робот — ходьба.zip",     "— самодостаточный ZIP-пакет"),
    ( 1, 2, "file",    "scene.json",             "— плоская схема (SceneBundle v1)"),
    ( 1, 2, "folder",  "models/",                "— копии моделей (.glb)"),
    ( 3, 3, "file",    "glb_2b9f54c1.glb",       "— ящик"),
    ( 3, 3, "file",    "rig_d18a07f3.glb",       "— робот (риг)"),
    ( 1, 2, "folder",  "textures/",              "— копии текстур (.png)"),
    ( 6, 3, "file",    "img_7c4e10ab.png",       "— референс-концепт"),
]

INDENT = 26.0
LEFT = 14.0
ROW_H = 30.0
TOP = 0.0
X_DESC = 268.0
LABEL_GAP = 20.0   # от левого края иконки до текста

def row_y(i):      # центр строки i (вниз — отрицательно)
    return TOP - i * ROW_H

def icon_x(depth):
    return LEFT + depth * INDENT

def draw_folder(ax, x, yc):
    w, h, tab_w, tab_h = 15.0, 11.5, 6.5, 3.2
    b = yc - h / 2
    ax.add_patch(mpatches.Rectangle((x, b), w, h, fill=True, facecolor="white",
                 edgecolor=BLACK, lw=LW, joinstyle="miter"))
    ax.add_patch(mpatches.Rectangle((x, b + h), tab_w, tab_h, fill=True, facecolor="white",
                 edgecolor=BLACK, lw=LW, joinstyle="miter"))

def draw_archive(ax, x, yc):
    # как папка, но с «замком-молнией»: маленькие штрихи по центру тела
    draw_folder(ax, x, yc)
    w, h = 15.0, 11.5
    b = yc - h / 2
    zx = x + w / 2
    for k in range(3):
        yy = b + 2.4 + k * 2.6
        ax.plot([zx - 1.1, zx + 1.1], [yy, yy], color=BLACK, lw=LW)

def draw_file(ax, x, yc):
    w, h, fold = 12.0, 14.0, 4.0
    b = yc - h / 2
    verts = [(x, b), (x, b + h), (x + w - fold, b + h), (x + w, b + h - fold),
             (x + w, b), (x, b)]
    codes = [Path.MOVETO, Path.LINETO, Path.LINETO, Path.LINETO, Path.LINETO, Path.CLOSEPOLY]
    ax.add_patch(mpatches.PathPatch(Path(verts, codes), fill=True, facecolor="white",
                 edgecolor=BLACK, lw=LW, joinstyle="miter"))
    # загнутый уголок
    ax.plot([x + w - fold, x + w - fold, x + w], [b + h, b + h - fold, b + h - fold],
            color=BLACK, lw=LW)
    # строки текста внутри
    for k in range(3):
        yy = b + 3.4 + k * 3.0
        ax.plot([x + 2.4, x + w - 2.4], [yy, yy], color=BLACK, lw=LW * 0.9)

DRAW = {"folder": draw_folder, "archive": draw_archive, "file": draw_file}
ICON_W = {"folder": 15.0, "archive": 15.0, "file": 12.0}

fig, ax = plt.subplots(figsize=(12.6, 4.6), dpi=200)

# связи-уголки: вертикаль от родителя к каждому ребёнку + горизонталь к иконке ребёнка
children = {}
for i, (p, *_rest) in enumerate(nodes):
    children.setdefault(p, []).append(i)

for parent, kids in children.items():
    if parent < 0:
        continue
    pdepth = nodes[parent][1]
    vx = icon_x(pdepth) + 4.0
    py = row_y(parent) - 6.0          # старт чуть ниже иконки родителя
    last_cy = row_y(kids[-1])
    ax.plot([vx, vx], [py, last_cy], color=BLACK, lw=LW)
    for c in kids:
        cy = row_y(c)
        cx = icon_x(nodes[c][1])
        ax.plot([vx, cx], [cy, cy], color=BLACK, lw=LW)

# иконки + подписи + описания
for i, (p, depth, kind, label, desc) in enumerate(nodes):
    yc = row_y(i)
    x = icon_x(depth)
    DRAW[kind](ax, x, yc)
    ax.text(x + LABEL_GAP, yc, label, va="center", ha="left", fontdict=MONO, color=BLACK)
    if desc:
        ax.text(X_DESC, yc, desc, va="center", ha="left", fontdict=SANS, color=BLACK)

ax.set_xlim(0, 470)
ax.set_ylim(row_y(len(nodes) - 1) - 18, TOP + 18)
ax.set_aspect("equal")
ax.axis("off")
plt.subplots_adjust(left=0, right=1, top=1, bottom=0)
fig.savefig("/sessions/trusting-tender-hawking/mnt/outputs/fig_export_bundle_tree.png",
            dpi=200, facecolor="white", bbox_inches="tight", pad_inches=0.12)
fig.savefig("/sessions/trusting-tender-hawking/mnt/outputs/fig_export_bundle_tree.svg",
            facecolor="white", bbox_inches="tight", pad_inches=0.12)
print("saved")
