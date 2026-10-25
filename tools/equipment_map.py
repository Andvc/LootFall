#!/usr/bin/env python3
"""The equipment screen's map: where every body site and every body slot is on the art (docs/equipment.md, "The figure").

Each view (the figure from the front and from behind, and each close-up view of the head, the hands and the feet) is cut
into body sites along cut lines placed by hand on the art, together with the edges the art draws between hair and skin,
and each part is named by a seed point. Every body slot gets a point on the whole figure, where the overview shows it,
and one on its close-up. Writes:

  assets/equipment/<view>_sites.png  the site under each pixel (red: its number in the view's list, 0 for none), which
                                     the screen reads for the mouse, highlights and the fills of what's worn
  assets/equipment/map.json          each view's sites (key, outline, centre), every slot's points, the region pages'
                                     panels, and what each site covers
  shots_map/                         review sheets: each view with its sites coloured and its slots marked

Run it after `python3 tools/equipment_art.py import`, and after a body site or slot is added or renamed (the slots come
from art/content.json, which `python3 tools/art.py manifest` refreshes), then `godot --headless --path . --import`.
Coordinates below are pixels on the delivered canvases (art/equipment/*.png), as `tools/equipment_art.py` crops them.
Needs Pillow, numpy, scipy and scikit-image.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from skimage import measure

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "equipment")
SHOTS = os.path.join(ROOT, "shots_map")
VIEWS = json.load(open(os.path.join(OUT, "views.json")))
SCALE = VIEWS["scale"]
CONTENT = json.load(open(os.path.join(ROOT, "art", "content.json")))
SITES = {s["key"]: s for s in CONTENT["sites"]}
CELLS = {c["key"]: dict(c, site=s["key"], region=s["region"]) for s in CONTENT["sites"] for c in s["slots"]}
# The figure's middle line on its canvas: one side mirrors the other.
MID = 1080
FINGERS = ["thumb", "index", "middle", "ring", "little"]
TOES = ["bigtoe", "toe2", "toe3", "toe4", "littletoe"]
WRAPPING = ("Under", "Mail", "Armor", "Over")


def other(side):
    return {"R": "L", "L": "R"}[side]


def mirror(p, axis=MID):
    return (2 * axis - p[0], p[1])


def spline(pts, steps=10):
    """A smooth line through the points (Catmull-Rom), as a polyline."""
    if len(pts) < 3:
        return list(pts)
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = (np.array(q, float) for q in p[i - 1:i + 3])
        for s in range(steps):
            t = s / steps
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                    + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(pts[-1]))
    return out


def circle(c, r, n=24):
    return [(c[0] + r * math.cos(a), c[1] + r * math.sin(a)) for a in np.linspace(0, 2 * math.pi, n + 1)]


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


# ---------------------------------------------------------------------------------------------------------------- a view

class View:
    """One picture: its cuts, seeds and slot points (canvas pixels in, texture pixels out)."""

    def __init__(self, name):
        v = VIEWS["views"][name]
        self.name, self.source, self.crop, self.size = name, v["source"], v["crop"], v["size"]
        self.rgba = np.asarray(Image.open(os.path.join(OUT, name + ".png")).convert("RGBA")).astype(np.int16)
        self.mask = self.rgba[..., 3] > 110
        self.cuts, self.seeds, self.points, self.hair_roi, self.hair_size = [], [], {}, None, 11
        self.axis = MID
        self.ids, self.sites = None, []

    def t(self, p):
        return ((p[0] - self.crop[0]) * SCALE, (p[1] - self.crop[1]) * SCALE)

    def c(self, p):
        """Texture pixels back to canvas pixels."""
        return (p[0] / SCALE + self.crop[0], p[1] / SCALE + self.crop[1])

    def m(self, p):
        return mirror(p, self.axis)

    def cut(self, *pts, smooth=True, mirrored=False):
        self.cuts.append((pts, smooth))
        if mirrored:
            self.cuts.append(([self.m(p) for p in pts], smooth))

    def ring(self, centre, r):
        self.cuts.append((circle(centre, r), False))

    def seed(self, site, *pts):
        for p in pts:
            self.seeds.append((site, p))

    def sided_seed(self, near, site, *pts):
        """A seed on the near side (the viewer's left) and its mirror for the other side's site."""
        self.seed(f"{near}.{site}", *pts)
        self.seed(f"{other(near)}.{site}", *[self.m(p) for p in pts])

    def point(self, cell, p):
        self.points[cell] = p

    # -------------------------------------------------------------------------------- the partition

    def hair_edges(self):
        """Where the art's hair (or beard) meets skin, inside the region of interest: the edge of its dark areas, smoothed
        so that thin ink lines and the beard's spiky edge don't count."""
        r, a = self.rgba[..., 0], self.rgba[..., 3] > 110
        dark = ndimage.uniform_filter(((r < 140) & a).astype(float), size=self.hair_size) > 0.5
        edge = (dark ^ ndimage.binary_erosion(dark)) | (ndimage.binary_dilation(dark) & ~dark)
        roi = Image.new("L", (self.mask.shape[1], self.mask.shape[0]), 0)
        ImageDraw.Draw(roi).polygon([self.t(p) for p in self.hair_roi], fill=255)
        return edge & (np.asarray(roi) > 0) & a

    def partition(self):
        h, w = self.mask.shape
        lines = Image.new("L", (w, h), 0)
        d = ImageDraw.Draw(lines)
        for pts, smooth in self.cuts:
            line = spline(pts) if smooth else list(pts)
            d.line([self.t(p) for p in line], fill=255, width=2)
        barrier = np.asarray(lines) > 0
        if self.hair_roi:
            barrier |= self.hair_edges()
        free = self.mask & ~barrier
        labels, _ = ndimage.label(free)
        names = sorted({s for s, _ in self.seeds})
        self.sites = names
        index = {s: i + 1 for i, s in enumerate(names)}
        owner = {}
        problems = []
        for site, p in self.seeds:
            x, y = (int(round(v)) for v in self.t(p))
            comp = 0
            for r in range(0, 7):
                win = labels[max(0, y - r):y + r + 1, max(0, x - r):x + r + 1]
                if (win > 0).any():
                    comp = int(win[win > 0][0])
                    break
            if comp == 0:
                problems.append(f"{self.name}: seed for {site} at {p} is off the figure or on a cut")
                continue
            if comp in owner and owner[comp] != site:
                problems.append(f"{self.name}: {site} and {owner[comp]} share a part (a cut is missing near {p})")
            owner[comp] = site
        ids = np.zeros((h, w), np.int32)
        for comp, site in owner.items():
            ids[labels == comp] = index[site]
        # Parts nobody named: say so if they're big, then fill them (and the cut lines) from their nearest neighbour.
        sizes = ndimage.sum_labels(np.ones_like(labels), labels, index=np.arange(labels.max() + 1))
        for comp in range(1, labels.max() + 1):
            if comp not in owner and sizes[comp] > 400:
                ys, xs = np.nonzero(labels == comp)
                problems.append(f"{self.name}: an unnamed part of {int(sizes[comp])} px near "
                                f"{tuple(int(v) for v in self.c((xs.mean(), ys.mean())))}")
        if (ids > 0).any():
            _, (iy, ix) = ndimage.distance_transform_edt(ids == 0, return_indices=True)
            hole = self.mask & (ids == 0)
            ids[hole] = ids[iy[hole], ix[hole]]
        self.ids = ids
        self.barrier = barrier
        return problems

    # -------------------------------------------------------------------------------- what comes out

    def site_pixels(self, site):
        k = self.sites.index(site) + 1
        ys, xs = np.nonzero(self.ids == k)
        return xs, ys

    def pole(self, site):
        """The point deepest inside a site (its centre even when it's curved), in texture pixels."""
        k = self.sites.index(site) + 1
        inside = np.pad(self.ids == k, 1)
        dist = ndimage.distance_transform_edt(inside)
        y, x = np.unravel_index(np.argmax(dist), dist.shape)
        return (float(x - 1), float(y - 1)), float(dist.max())

    def outlines(self, site):
        k = self.sites.index(site) + 1
        out = []
        for c in measure.find_contours(np.pad(self.ids == k, 1).astype(float), 0.5):
            c = measure.approximate_polygon(c, tolerance=0.7)
            if len(c) < 4:
                continue
            pts = [[round(float(x) - 1, 1), round(float(y) - 1, 1)] for y, x in c]
            area = 0.5 * abs(sum(pts[i][0] * pts[i - 1][1] - pts[i - 1][0] * pts[i][1] for i in range(len(pts))))
            if area >= 4:
                out.append(pts)
        return out

    def site_at(self, p):
        x, y = (int(round(v)) for v in self.t(p))
        if 0 <= x < self.ids.shape[1] and 0 <= y < self.ids.shape[0] and self.ids[y, x] > 0:
            return self.sites[self.ids[y, x] - 1]
        return None


def spread(view, site, cells):
    """Points for a site's slots that have none of their own: spread over the site (k-means of its pixels), the
    deepest layer nearest the top left, so each slot has its own place inside the site."""
    xs, ys = view.site_pixels(site)
    if len(xs) == 0 or not cells:
        return {}
    pts = np.stack([xs, ys], 1).astype(float)
    if len(pts) > 6000:
        pts = pts[np.linspace(0, len(pts) - 1, 6000).astype(int)]
    n = len(cells)
    if n == 1:
        (cx, cy), _ = view.pole(site)
        return {cells[0]: view.c((cx, cy))}
    # Start from points spread along the site's long axis, then a few rounds of k-means.
    mean = pts.mean(0)
    u, s, vt = np.linalg.svd(pts - mean, full_matrices=False)
    axis = vt[0]
    proj = (pts - mean) @ axis
    order = np.argsort(proj)
    centres = np.array([pts[order[int((i + 0.5) * len(order) / n)]] for i in range(n)])
    for _ in range(12):
        d = ((pts[:, None, :] - centres[None]) ** 2).sum(-1)
        near = d.argmin(1)
        for i in range(n):
            if (near == i).any():
                centres[i] = pts[near == i].mean(0)
    # Pull each centre onto the site (a curved site's mean can fall outside it).
    k = view.sites.index(site) + 1
    for i in range(n):
        x, y = centres[i]
        if view.ids[int(round(y)), int(round(x))] != k:
            j = ((pts - centres[i]) ** 2).sum(1).argmin()
            centres[i] = pts[j]
    # Deepest layer first, top left to bottom right.
    centres = sorted(centres.tolist(), key=lambda c: (c[1] + 0.35 * c[0]))
    layer_order = ["Skin", "Under", "Mail", "Armor", "Over", "Adorn", "Hold", "Graft", "Beyond"]
    cells = sorted(cells, key=lambda k: layer_order.index(CELLS[k]["layer"]))
    return {cell: view.c(tuple(cen)) for cell, cen in zip(cells, centres)}


# ---------------------------------------------------------------------------------------------------------------- the figure

def limb_cuts(v, back):
    """The arm and leg cuts on the viewer's left (mirrored for the right): the same joints from either side."""
    # Arm: the deltoid's lower edge, the armpit's wedge, the elbow and wrist bands, the knuckles and the thumb.
    v.cut((530, 1256), (610, 1224), (700, 1204), (776, 1190), (790, 1188), mirrored=True)
    v.cut((776, 1182), (760, 1280), (744, 1380), (736, 1430), mirrored=True)
    v.cut((776, 1182), (795, 1262), (800, 1340), (798, 1430), mirrored=True)
    v.cut((750, 1386), (812, 1386), smooth=False, mirrored=True)
    v.cut((452, 1424), (720, 1500), smooth=False, mirrored=True)
    v.cut((400, 1560), (668, 1668), smooth=False, mirrored=True)
    v.cut((314, 1834), (532, 1924), smooth=False, mirrored=True)
    v.cut((284, 1918), (502, 2008), smooth=False, mirrored=True)
    v.cut((236, 2024), (266, 2076), (290, 2102), (326, 2124), (378, 2138), (420, 2126), (452, 2098), (468, 2078), mirrored=True)
    v.cut((462, 2080), (482, 2020), (506, 1990), (536, 1972), mirrored=True)
    # Leg: hip and groin (front), the knee band, the ankle, the foot's toes.
    if not back:
        v.cut((670, 2050), (800, 2046), (900, 2032), (995, 2066), mirrored=True)
        v.cut((764, 2036), (760, 2200), (752, 2400), (746, 2580), mirrored=True)
    v.cut((680, 2566), (840, 2572), (1020, 2560), smooth=False, mirrored=True)
    v.cut((680, 2738), (840, 2734), (1010, 2726), smooth=False, mirrored=True)
    v.cut((650, 3288), (780, 3292), (910, 3285), smooth=False, mirrored=True)
    v.cut((630, 3420), (780, 3412), (910, 3405), smooth=False, mirrored=True)
    if not back:
        v.cut((600, 3522), (700, 3530), (770, 3528), (890, 3520), mirrored=True)
        for x in (653, 681, 712, 757):
            v.cut((x, 3516), (x - 2, 3640), smooth=False, mirrored=True)


def limb_seeds(v, near, back):
    up = "triceps" if back else "upperarm"
    v.sided_seed(near, "shoulder", (690, 1080))
    v.sided_seed(near, up, (620, 1330))
    v.sided_seed(near, "elbow", (575, 1535))
    v.sided_seed(near, "forearm", (470, 1750))
    v.sided_seed(near, "wrist", (410, 1915))
    v.sided_seed(near, "palm" if back else "hand", (380, 2030))
    for finger, p in zip(FINGERS, [(495, 2100), (400, 2200), (330, 2200), (268, 2165), (228, 2120)]):
        v.sided_seed(near, finger, p)
    v.sided_seed(near, "lat" if back else "armpit", (774, 1300))
    if back:
        v.sided_seed(near, "hamstring", (850, 2380))
        v.sided_seed(near, "kneepit", (830, 2650))
        v.sided_seed(near, "calf", (800, 3000))
        v.sided_seed(near, "ankle", (785, 3350))
        v.sided_seed(near, "heel", (770, 3500))
    else:
        v.sided_seed(near, "hip", (790, 1950))
        v.sided_seed(near, "thigh", (880, 2300))
        v.sided_seed(near, "outthigh", (734, 2300))
        v.sided_seed(near, "knee", (840, 2650))
        v.sided_seed(near, "shin", (800, 3000))
        v.sided_seed(near, "ankle", (785, 3350))
        v.sided_seed(near, "foot", (760, 3470))
        for toe, x in zip(TOES, (790, 734, 697, 667, 645)):
            v.sided_seed(near, toe, (x, 3560))


def front():
    v = View("front")
    v.hair_roi = [(850, 350), (1310, 350), (1310, 900), (850, 900)]
    # The head. The art's own edges part hair and beard from skin; these part the rest: the scalp from the hair below
    # the ears' tops, the ears, the brow over the eyebrows, the eyes, the nose, the cheeks, the mouth (lips and
    # moustache) from the beard, which is the chin, and the braid from the beard.
    v.cut((860, 606), (966, 606), smooth=False, mirrored=True)
    v.cut((957, 596), (954, 670), (952, 742), mirrored=True)
    v.cut((870, 734), (958, 734), smooth=False, mirrored=True)
    v.cut((948, 586), (1010, 573), (1062, 581), (1080, 590), (1098, 581), (1150, 573), (1212, 586))
    v.cut((1052, 572), (1050, 656), (1043, 722), mirrored=True)
    v.cut((945, 652), (1000, 657), (1056, 657), smooth=False, mirrored=True)
    v.cut((1028, 712), (1055, 708), (1080, 712), (1105, 708), (1132, 712))
    v.cut((1036, 700), (1020, 742), (1050, 772), (1080, 779), (1110, 772), (1140, 742), (1124, 700))
    v.cut((952, 730), (961, 790), (974, 850), (980, 880), (986, 910), mirrored=True)
    # The neck, the throat down its middle, the band at its base, the collarbones.
    v.cut((1048, 840), (1048, 952), smooth=False, mirrored=True)
    v.cut((930, 872), (970, 904), (1020, 934), (1080, 944), (1140, 934), (1190, 904), (1230, 872))
    v.cut((862, 906), (970, 960), (1030, 985), (1080, 995), (1130, 985), (1190, 960), (1298, 906))
    # The torso's front: shoulders off the chest, collarbones over the breasts, the breastbone, the breasts' lower
    # edge, the heart, the solar plexus, the ribs, the stomach, the navel, the flanks, the lower belly.
    v.cut((806, 930), (792, 1040), (780, 1120), (772, 1196), mirrored=True)
    v.cut((786, 1036), (880, 1031), (980, 1031), (1064, 1036), mirrored=True)
    v.cut((1058, 975), (1058, 1300), smooth=False, mirrored=True)
    v.cut((1050, 1290), (1110, 1290), smooth=False)
    v.cut((778, 1238), (860, 1297), (960, 1305), (1030, 1290), (1064, 1272), mirrored=True)
    v.ring((1182, 1206), 40)
    v.cut((990, 1296), (991, 1398), smooth=False, mirrored=True)
    v.cut((924, 1392), (1236, 1392), smooth=False)
    v.cut((930, 1386), (930, 1506), smooth=False, mirrored=True)
    v.cut((770, 1500), (936, 1500), smooth=False, mirrored=True)
    v.cut((930, 1494), (926, 1626), (916, 1806), mirrored=True)
    v.cut((920, 1626), (1000, 1629), (1080, 1631), (1160, 1629), (1240, 1626))
    v.ring((1077, 1653), 26)
    # The waistband (the braies' top), the hips, the groin.
    v.cut((760, 1752), (900, 1786), (1000, 1802), (1080, 1806), (1160, 1802), (1260, 1786), (1400, 1752))
    v.cut((750, 1814), (900, 1851), (1000, 1868), (1080, 1874), (1160, 1868), (1260, 1851), (1410, 1814))
    v.cut((926, 1846), (952, 1960), (990, 2066), mirrored=True)
    v.cut((980, 2056), (1030, 2120), (1072, 2178), mirrored=True)
    limb_cuts(v, back=False)

    v.seed("scalp", (1080, 430))
    v.seed("hair", (948, 800))
    v.seed("brow", (1080, 540))
    v.seed("nose", (1080, 670))
    v.seed("mouth", (1080, 728), (1080, 752))
    v.seed("chin", (1080, 822), (1000, 800), (1160, 800))
    v.seed("neck", (1000, 885), (1160, 885))
    v.seed("throat", (1080, 893))
    v.seed("collar", (1080, 968))
    v.seed("sternum", (1080, 1150))
    v.seed("heart", (1182, 1206))
    v.seed("plexus", (1080, 1340))
    v.seed("stomach", (1080, 1500))
    v.seed("navel", (1077, 1653))
    v.seed("lowbelly", (1080, 1715))
    v.seed("waist", (1080, 1840))
    v.seed("groin", (1080, 1985))
    for near in ("R",):
        v.sided_seed(near, "eye", (985, 630), (1010, 592))
        v.sided_seed(near, "cheek", (1000, 685))
        v.sided_seed(near, "ear", (926, 665))
        v.sided_seed(near, "clavicle", (920, 995))
        v.sided_seed(near, "pec", (900, 1170))
        v.sided_seed(near, "ribs", (860, 1400))
        v.sided_seed(near, "flank", (860, 1650))
        limb_seeds(v, near, back=False)
    return v


def back():
    v = View("back")
    v.hair_roi = [(850, 350), (1310, 350), (1310, 915), (1145, 915), (1145, 1160), (1015, 1160), (1015, 915), (850, 915)]
    # The braid leaves the scalp where it leaves the hair; the ears; the nape down to the base of the neck.
    v.cut((950, 742), (1210, 742), smooth=False)
    v.cut((962, 680), (958, 740), smooth=False, mirrored=True)
    v.cut((870, 866), (1000, 906), (1080, 912), (1160, 906), (1290, 866))
    # The back: shoulders, the spine down the middle, the shoulder blades, the sides, the kidneys, the small of the back.
    v.cut((806, 930), (792, 1040), (780, 1120), (772, 1196), mirrored=True)
    v.cut((1050, 900), (1050, 1506), smooth=False, mirrored=True)
    v.cut((1044, 1266), (1116, 1266), smooth=False)
    v.cut((758, 1158), (850, 1252), (930, 1322), (1000, 1292), (1056, 1268), mirrored=True)
    v.cut((790, 1566), (900, 1540), (962, 1500), (1056, 1500), mirrored=True)
    v.cut((1040, 1500), (1120, 1500), smooth=False)
    v.cut((962, 1494), (964, 1640), (966, 1800), mirrored=True)
    # The waistband, the seat down to the fold under it.
    v.cut((760, 1758), (900, 1768), (1080, 1772), (1260, 1768), (1400, 1758))
    v.cut((750, 1826), (900, 1834), (1080, 1838), (1260, 1834), (1410, 1826))
    v.cut((680, 2186), (850, 2202), (1000, 2192), (1076, 2170), mirrored=True)
    limb_cuts(v, back=True)
    # Seen from the palm side the fingers touch near their roots: part them along the gaps.
    v.cut((278, 2092), (250, 2150), (222, 2214), mirrored=True)
    v.cut((324, 2112), (300, 2190), (278, 2266), mirrored=True)

    v.seed("scalp", (1080, 500))
    v.seed("hair", (1084, 900), (1085, 1060))
    v.seed("nape", (1000, 830), (1160, 830))
    v.seed("upspine", (1080, 1180))
    v.seed("midspine", (1080, 1380))
    v.seed("lumbar", (1080, 1640))
    v.seed("waist", (1080, 1800))
    v.seed("seat", (1080, 2000))
    for near in ("L",):
        v.sided_seed(near, "ear", (930, 655))
        v.sided_seed(near, "blade", (900, 1100))
        v.sided_seed(near, "lat", (900, 1420))
        v.sided_seed(near, "kidney", (880, 1660))
        limb_seeds(v, near, back=True)
    return v


# ---------------------------------------------------------------------------------------------------------------- close-ups

def valleys(v):
    """Where the digits of a close-up (pointing up) part: the bottom of each gap between two of them, left to right,
    in canvas pixels. Found by following every gap in the silhouette down until it closes."""
    m = v.mask
    rows = np.nonzero(m.any(1))[0]
    open_gaps, found = [], []
    for y in range(rows[0], rows[-1]):
        xs = np.nonzero(m[y])[0]
        inside = m[y, xs[0]:xs[-1] + 1]
        d = np.diff(np.concatenate([[1], inside.astype(np.int8), [1]]))
        gaps = [(xs[0] + a, xs[0] + b) for a, b in zip(np.nonzero(d == -1)[0], np.nonzero(d == 1)[0])]
        still = []
        for g in open_gaps:
            nxt = [h for h in gaps if h[0] <= g["span"][1] + 1 and h[1] >= g["span"][0] - 1]
            if nxt:
                g["span"], g["rows"], g["wide"] = nxt[0], g["rows"] + 1, max(g["wide"], nxt[0][1] - nxt[0][0])
                still.append(g)
            elif g["rows"] >= 12 and g["wide"] >= 3:
                found.append(((g["span"][0] + g["span"][1]) / 2, y - 1, g["rows"]))
        for h in gaps:
            if not any(g["span"] == h for g in still):
                still.append({"span": h, "rows": 1, "wide": h[1] - h[0]})
        open_gaps = still
    # The digits' gaps open from the top: keep the ones that reach highest (a notch lower down isn't between digits).
    return found


def top_gaps(v, n):
    found = valleys(v)
    found = sorted(found, key=lambda g: g[1] - g[2])[:n]
    return sorted(v.c((x, y)) for x, y, _ in found)


def march(v, start, direction, beyond=24):
    """From a point along a direction, into the silhouette if it starts outside, then to just past its far edge
    (canvas pixels)."""
    d = np.array(direction, float)
    d /= np.linalg.norm(d)
    p = np.array(start, float)

    def inside(q):
        x, y = (int(round(c)) for c in v.t(q))
        return 0 <= x < v.mask.shape[1] and 0 <= y < v.mask.shape[0] and v.mask[y, x]

    entered = inside(p)
    for _ in range(4000):
        p += d * 2
        if inside(p):
            entered = True
        elif entered:
            break
    return tuple(p + d * beyond)


def sideways(v, pts):
    """A line through the valleys, carried out past the silhouette on both sides, keeping close to level."""
    def out(a, b, sign):
        d = np.array(a, float) - np.array(b, float)
        slope = max(-0.35, min(0.35, d[1] / d[0])) if abs(d[0]) > 1e-6 else 0
        return march(v, a, (sign, sign * slope))
    return [out(pts[0], pts[1], -1)] + list(pts) + [out(pts[-1], pts[-2], 1)]


def digit_axis(v, digit, base_site):
    """A finger's or toe's base (where it meets the hand or foot) and tip, in canvas pixels."""
    k = v.sites.index(digit) + 1
    kb = v.sites.index(base_site) + 1
    own = v.ids == k
    near = ndimage.binary_dilation(v.ids == kb, iterations=2) & own
    ys, xs = np.nonzero(near if near.any() else own)
    base = np.array([xs.mean(), ys.mean()])
    ys, xs = np.nonzero(own)
    pts = np.stack([xs, ys], 1)
    tip = pts[((pts - base) ** 2).sum(1).argmax()]
    return v.c(tuple(base)), v.c(tuple(tip))


def hand_page(name, side, thumb_left, palm):
    """A hand's close-up: the fingers off the hand along the valleys, the thumb, the wrist and the forearm."""
    v = View(name)
    gaps = top_gaps(v, 4)
    if len(gaps) != 4:
        raise SystemExit(f"{name}: found {len(gaps)} gaps between the fingers, want 4")
    # Left to right, the thumb's valley is the outermost on its side.
    thumb_gap = gaps[0] if thumb_left else gaps[-1]
    finger_gaps = gaps[1:] if thumb_left else gaps[:-1]
    knuckles = [(x, y - 14) for x, y in finger_gaps]
    v.cut(*sideways(v, knuckles))
    out = (-0.62, 0.78) if thumb_left else (0.62, 0.78)
    v.cut((thumb_gap[0] + (8 if thumb_left else -8), thumb_gap[1] - 30), (thumb_gap[0], thumb_gap[1] + 6),
          march(v, (thumb_gap[0], thumb_gap[1] + 6), out), smooth=False)
    # The wrist: the narrowest row between the knuckles and the cut at the bottom.
    rows = np.nonzero(v.mask.any(1))[0]
    lo = int(v.t((0, max(y for _, y in knuckles) + 300))[1])
    hi = rows[-1] - 20
    widths = [(v.mask[y].sum(), y) for y in range(lo, hi)]
    wy = v.c((0, min(widths)[1]))[1]
    x0, x1 = v.crop[0] - 50, v.crop[2] + 50
    v.cut((x0, wy - 70), (x1, wy - 70), smooth=False)
    v.cut((x0, wy + 120), (x1, wy + 120), smooth=False)
    # Seeds: each finger in its column at a fifth of the way down, the thumb, the hand, the wrist, the forearm.
    names = ["index", "middle", "ring", "little"] if thumb_left else ["little", "ring", "middle", "index"]
    xs = [finger_gaps[0][0] - (finger_gaps[1][0] - finger_gaps[0][0]) * 0.6] + \
         [(finger_gaps[i][0] + finger_gaps[i + 1][0]) / 2 for i in range(2)] + \
         [finger_gaps[-1][0] + (finger_gaps[-1][0] - finger_gaps[-2][0]) * 0.6]
    top = v.c((0, rows[0]))[1]
    for finger, x in zip(names, xs):
        # Down the column to the first pixel of the finger, then a little further.
        tx = int(v.t((x, 0))[0])
        col = np.nonzero(v.mask[:, tx])[0]
        v.seed(f"{side}.{finger}", (x, v.c((0, col[0] + 40))[1]))
    tg = thumb_gap
    v.seed(f"{side}.thumb", (tg[0] + (-120 if thumb_left else 120), tg[1] - 80))
    middle_x = sum(x for x, _ in finger_gaps) / 3
    v.seed(f"{side}.{'palm' if palm else 'hand'}", (middle_x, (max(y for _, y in knuckles) + wy - 70) / 2))
    v.seed(f"{side}.wrist", (middle_x, wy + 25))
    v.seed(f"{side}.forearm", (middle_x, wy + 300))
    return v


def foot_page(name, side, sole):
    """A foot's close-up: the toes off the foot along their roots (they touch, so the cuts are placed by hand on the
    right foot; the left foot's pictures are its mirror image), on the sole the heel, the ankle at the bottom."""
    v = View(name)
    v.axis = 879 if not sole else 2640
    flip = (lambda p: v.m(p)) if side == "L" else (lambda p: p)
    c = lambda *pts, **kw: v.cut(*[flip(p) for p in pts], **kw)
    sd = lambda site, *pts: v.seed(site, *[flip(p) for p in pts])
    rows = np.nonzero(v.mask.any(1))[0]
    top, bottom = v.c((0, rows[0]))[1], v.c((0, rows[-1]))[1]
    x0, x1 = v.crop[0] - 50, v.crop[2] + 50
    ankle = top + (bottom - top) * (0.80 if sole else 0.72)
    v.cut((x0, ankle), (x1, ankle), smooth=False)
    if not sole:
        # The big toe on the left (right foot, seen from above).
        c((470, 548), (540, 538), (652, 560), (764, 592), (908, 604), (1030, 642), (1130, 692), (1240, 724), (1300, 734))
        c((764, 120), (764, 606), smooth=False)
        c((907, 120), (908, 612), smooth=False)
        c((1031, 120), (1032, 650), smooth=False)
        c((1129, 120), (1130, 700), smooth=False)
        for toe, p in zip(TOES, [(640, 400), (845, 420), (970, 470), (1080, 540), (1180, 620)]):
            sd(f"{side}.{toe}", p)
        sd(f"{side}.foot", (880, 1000))
    else:
        # The big toe on the right (right foot, seen from below).
        c((2236, 768), (2300, 742), (2390, 702), (2495, 642), (2600, 612), (2755, 597), (2900, 582), (3070, 580))
        c((2390, 120), (2390, 708), smooth=False)
        c((2495, 120), (2495, 648), smooth=False)
        c((2600, 120), (2600, 618), smooth=False)
        c((2756, 120), (2756, 602), smooth=False)
        for toe, p in zip(TOES, [(2880, 400), (2670, 470), (2545, 510), (2440, 570), (2340, 630)]):
            sd(f"{side}.{toe}", p)
        heel = top + (bottom - top) * 0.62
        v.cut((x0, heel), (x1, heel), smooth=False)
        sd(f"{side}.sole", (2640, (700 + heel) / 2))
        sd(f"{side}.heel", (2640, (heel + ankle) / 2))
    sd(f"{side}.ankle", (v.axis if side == "R" else v.axis, (ankle + bottom) / 2))
    return v


def head_profile(name, near, axis=None):
    """The head from the side (the right profile faces left; the left one is its mirror image)."""
    v = View(name)
    flip = (lambda p: (3517 - p[0], p[1])) if axis else (lambda p: p)
    v.hair_roi = [flip(p) for p in [(0, 300), (1180, 300), (1180, 2050), (0, 2050)]]
    v.hair_size = 15
    c = lambda *pts, **kw: v.cut(*[flip(p) for p in pts], **kw)
    sd = lambda site, *pts: v.seed(site, *[flip(p) for p in pts])
    # Scalp from the hair over the ears and the braid; the brow over the eyebrow; the eye; the nose; the cheek; the
    # mouth from the beard; the ear; the throat down the neck's front; the nape behind; the base of the neck.
    c((560, 930), (800, 915), (1200, 980), smooth=False)
    c((60, 842), (300, 836), (500, 860), (640, 880))
    c((104, 812), (150, 900), (190, 1000), (214, 1100), (216, 1180), (200, 1256))
    c((150, 1046), (300, 1046), (520, 1030), (680, 1030))
    c((200, 1210), (292, 1250), (296, 1312), (226, 1350), (60, 1360))
    c((664, 880), (650, 1050), (682, 1250))
    c((630, 1240), (760, 1252), (900, 1236))
    c((430, 1440), (470, 1560), (520, 1760), (548, 1892))
    c((850, 1150), (870, 1300), (900, 1600), (930, 1892))
    c((200, 1885), (700, 1880), (1180, 1885), smooth=False)
    sd("scalp", (600, 560))
    sd("hair", (1030, 1500))
    sd("brow", (300, 760))
    sd(f"{near}.eye", (330, 960), (300, 875), (291, 964), (611, 975))
    sd("nose", (120, 1120))
    sd(f"{near}.cheek", (420, 1110), (656, 1179))
    sd("mouth", (200, 1290), (211, 1264))
    sd("chin", (350, 1440), (238, 1388))
    sd(f"{near}.ear", (760, 1060), (684, 955))
    sd("throat", (470, 1720))
    sd("neck", (700, 1700))
    sd("nape", (1000, 1750))
    sd("collar", (600, 1950))
    return v


def head_front():
    v = View("head_front")
    v.axis = 1763
    v.hair_roi = [(1240, 300), (2290, 300), (2290, 2050), (1240, 2050)]
    v.hair_size = 15
    v.cut((1280, 955), (1462, 955), smooth=False, mirrored=True)
    v.cut((1424, 935), (1418, 1100), (1428, 1262), mirrored=True)
    v.cut((1290, 1258), (1430, 1258), smooth=False, mirrored=True)
    v.cut((1440, 872), (1590, 855), (1700, 865), (1763, 880), (1826, 865), (1936, 855), (2086, 872))
    v.cut((1704, 850), (1700, 1060), (1686, 1262), mirrored=True)
    v.cut((1396, 1046), (1600, 1050), (1712, 1050), smooth=False, mirrored=True)
    v.cut((1650, 1252), (1710, 1244), (1763, 1250), (1816, 1244), (1876, 1252))
    v.cut((1660, 1236), (1622, 1300), (1680, 1352), (1763, 1366), (1846, 1352), (1904, 1300), (1866, 1236))
    v.cut((1462, 1230), (1466, 1500), (1472, 1760), smooth=False)
    v.cut((1734, 1600), (1734, 1800), smooth=False, mirrored=True)
    v.cut((1300, 1795), (1763, 1792), (2230, 1795), smooth=False)
    v.seed("scalp", (1763, 450))
    v.seed("hair", (1418, 1500))
    v.seed("brow", (1763, 760))
    v.seed("nose", (1763, 1150))
    v.seed("mouth", (1763, 1265), (1763, 1330), (1685, 1279), (1840, 1277))
    v.seed("chin", (1763, 1500), (1500, 1300), (2026, 1300), (1764, 1399), (1625, 1380), (1901, 1380))
    v.seed("neck", (1560, 1700), (1966, 1700))
    v.seed("throat", (1763, 1700))
    v.seed("collar", (1763, 1900))
    v.sided_seed("R", "eye", (1593, 1000), (1585, 905), (1500, 992), (1640, 1035))
    v.sided_seed("R", "cheek", (1560, 1140))
    v.sided_seed("R", "ear", (1360, 1100), (1401, 1037))
    return v


# ---------------------------------------------------------------------------------------------------------------- slots

# Slots the whole figure shows from behind (the rest from the front): the back's own, and what's on the back of a limb.
SHOWN_BEHIND = {"hair", "nape", "blade", "upspine", "midspine", "lat", "lumbar", "kidney", "back", "tail", "seat", "triceps",
                "elbow", "palm", "hamstring", "kneepit", "calf", "heel", "sole"}
# What a site covers besides itself: whatever the layer (a mask covers the face's parts, the torso's cloth its front and
# back), and only for what's wrapped round a limb or the body (cloth, mail, plate, over), never ink or jewellery.
BACK_LEAVES = ["L.blade", "R.blade", "upspine", "midspine", "L.lat", "R.lat", "lumbar", "L.kidney", "R.kidney"]
TORSO_LEAVES = ["L.clavicle", "R.clavicle", "L.pec", "R.pec", "heart", "sternum", "L.ribs", "R.ribs", "plexus", "stomach",
                "navel", "L.flank", "R.flank", "lowbelly", "L.armpit", "R.armpit"]
COVERS = {"face": ["R.eye", "L.eye", "R.cheek", "L.cheek", "nose", "mouth"], "torso": TORSO_LEAVES + BACK_LEAVES,
          "back": BACK_LEAVES, "L.pec": ["heart"], "stomach": ["navel"]}
WRAPS = {"neck": ["nape", "throat"], "groin": ["seat"]}
for sd in ("R", "L"):
    COVERS[f"{sd}.fingers"] = [f"{sd}.{f}" for f in FINGERS]
    COVERS[f"{sd}.toetips"] = [f"{sd}.{t}" for t in TOES]
    COVERS[f"{sd}.grip"] = [f"{sd}.palm", f"{sd}.hand", f"{sd}.fingers"]
    WRAPS[f"{sd}.upperarm"] = [f"{sd}.triceps"]
    WRAPS[f"{sd}.thigh"] = [f"{sd}.hamstring", f"{sd}.outthigh"]
    WRAPS[f"{sd}.knee"] = [f"{sd}.kneepit"]
    WRAPS[f"{sd}.shin"] = [f"{sd}.calf"]
    WRAPS[f"{sd}.hand"] = [f"{sd}.palm", f"{sd}.fingers"]
    WRAPS[f"{sd}.foot"] = [f"{sd}.toetips", f"{sd}.sole", f"{sd}.heel"]


def covered(site, leaves):
    """The leaf sites a site covers, each with how many steps away it is and whether only wrapping layers reach it."""
    out = {}

    def walk(s, depth, wrap_only):
        if s in leaves:
            prev = out.get(s)
            if prev is None or (prev[1] and not wrap_only) or (prev[1] == wrap_only and depth < prev[0]):
                out[s] = (depth, wrap_only)
        for c in COVERS.get(s, []):
            walk(c, depth + 1, wrap_only)
        for c in WRAPS.get(s, []):
            walk(c, depth + 1, True)

    walk(site, 0, False)
    return out


def base_of(site):
    return site[2:] if site[:2] in ("R.", "L.") else site


def overview_view(cell):
    return "back" if base_of(CELLS[cell]["site"]) in SHOWN_BEHIND else "front"


def union_spread(v, sites, cells):
    """Like spread, over several sites at once (a composite site's slots over its parts)."""
    k = [v.sites.index(s) + 1 for s in sites if s in v.sites]
    tmp = "__union__"
    keep_sites, keep_ids = v.sites, v.ids
    ids = np.where(np.isin(v.ids, k), len(v.sites) + 1, 0)
    v.sites, v.ids = keep_sites + [tmp], ids
    try:
        return spread(v, tmp, cells)
    finally:
        v.sites, v.ids = keep_sites, keep_ids


def leaf_sites(site, v):
    got = covered(site, set(v.sites))
    return [s for s, (_, wrap) in got.items() if not wrap]


def place(v, cells, fixed):
    """Every one of these slots a point on this view: its fixed point if it has one, otherwise spread over its site."""
    pts = dict(fixed)
    by_site = {}
    for cell in cells:
        if cell not in pts:
            by_site.setdefault(CELLS[cell]["site"], []).append(cell)
    for site, group in by_site.items():
        if site in v.sites:
            pts.update(spread(v, site, group))
        else:
            parts = leaf_sites(site, v)
            if not parts:
                raise SystemExit(f"{v.name}: nowhere to put {', '.join(group)}")
            pts.update(union_spread(v, parts, group))
    return pts


def finger_points(v, side, base_site, cells_wanted):
    """Rings at a finger's root, knuckle rings at its middle joint, claws at its tip; a toe's ring at its root."""
    out = {}
    for f in FINGERS:
        site = f"{side}.{f}"
        if site not in v.sites:
            continue
        b, t = digit_axis(v, site, base_site)
        thumb = f == "thumb"
        for cell, at in (("band", 0.36 if thumb else 0.2), ("knuckle", 0.62 if thumb else 0.45), ("tip", 0.88)):
            key = f"{site}.{cell}"
            if key in cells_wanted:
                out[key] = lerp(b, t, at)
    for toe in TOES:
        site = f"{side}.{toe}"
        if site in v.sites and f"{site}.band" in cells_wanted:
            b, t = digit_axis(v, site, base_site)
            out[f"{site}.band"] = lerp(b, t, 0.42)
    return out


def figure_points(v, beyond):
    """The whole figure's points: every slot shown on this side (see SHOWN_BEHIND), where it sits."""
    back = v.name == "back"
    cells = [k for k, c in CELLS.items() if c["region"] != "Beyond" and overview_view(k) == v.name and c["site"] not in
             ("halo", "horns")]
    fixed = {}
    m = v.m
    if not back:
        for near in ("R",):
            for key, p in {"ear.lobe": (932, 712), "ear.helix": (912, 648), "ear.tip": (924, 616),
                           "eye.skin": (1004, 640), "eye.adorn": (1018, 626), "eye.brow": (1022, 592)}.items():
                fixed[f"R.{key}"] = p
                fixed[f"L.{key}"] = m(p)
        fixed.update({"nose.bridge": (1080, 628), "nose.septum": (1080, 704), "nose.nostrilR": (1058, 694),
                      "nose.nostrilL": (1102, 694), "mouth.armor": (1080, 732), "mouth.lip": (1080, 760),
                      "mouth.teeth": (1056, 752), "mouth.tongue": (1104, 752), "face.mask": (1080, 600),
                      "L.pec.medal1": (1148, 1082), "L.pec.medal2": (1196, 1076), "L.pec.medal3": (1244, 1084),
                      "R.pec.badge": (934, 1080), "collar.chain": (1036, 962), "collar.chain2": (1124, 962),
                      "collar.pendant": (1080, 1012), "brow.jewel": (1080, 556), "brow.band": (1140, 530),
                      "brow.skin": (1020, 530), "torso.under": (1010, 1215), "torso.mail": (1150, 1215),
                      "torso.over": (1080, 1430), "torso.strap": (960, 1135)})
        for near in ("R", "L"):
            hand = [k for k in cells if CELLS[k]["site"] in [f"{near}.{f}" for f in FINGERS] + [f"{near}.{t}" for t in TOES]]
            fixed.update(finger_points(v, near, f"{near}.hand", set(hand)))
            toes = [k for k in cells if CELLS[k]["site"] in [f"{near}.{t}" for t in TOES]]
            fixed.update(finger_points(v, near, f"{near}.foot", set(toes)))
            # The fingers' ink across the knuckles, the toenails over the toes, the grip in the hand.
            b, _ = digit_axis(v, f"{near}.middle", f"{near}.hand")
            fixed[f"{near}.fingers.skin"] = b
            _, t = digit_axis(v, f"{near}.toe3", f"{near}.foot")
            fixed[f"{near}.toetips.tips"] = (t[0], t[1] - 14)
            xs, ys = v.site_pixels(f"{near}.hand")
            fixed[f"{near}.grip.hold"] = v.c((xs.mean(), ys.mean() + 6))
    else:
        fixed.update({"hair.pin1": (1010, 520), "hair.pin2": (1150, 520), "hair.ribbon": (1082, 988),
                      "hair.beads": (1084, 1070), "back.over": (1080, 1236), "back.mount": (968, 990),
                      "back.wings": (1192, 990), "tail.graft": (1080, 1890)})
        for near in ("R", "L"):
            xs, ys = v.site_pixels(f"{near}.heel")
            fixed[f"{near}.sole.sole"] = v.c((xs.mean(), ys.max() - 14))
    pts = place(v, cells, fixed)
    if not back:
        # Above, round and below the figure: the head's halo and horns, then what's around him.
        pts.update({"halo.halo": (1080, 346), "horns.graft": (998, 410)})
        pts.update(beyond)
    return pts


BEYOND = {"aura.aura": (330, 1520), "shadow.shadow": (1080, 3632), "companion.familiar": (1820, 3330),
          "companion.steed": (340, 3330), "orbit.stone1": (770, 520), "orbit.stone2": (730, 650), "orbit.stone3": (790, 780),
          "orbit.stone4": (1390, 520), "orbit.stone5": (1430, 650), "orbit.stone6": (1370, 780),
          "heavens.star1": (300, 420), "heavens.star2": (430, 520), "heavens.star3": (260, 650), "heavens.star4": (400, 780),
          "heavens.star5": (1860, 420), "heavens.star6": (1730, 520), "heavens.star7": (1900, 650),
          "heavens.star8": (1760, 780), "heavens.comet": (560, 380), "heavens.eclipse": (1600, 380)}


def head_points(views):
    """The head's close-up: the face on the front view, each ear and cheek (and the hair, the nape) on its profile."""
    right, front_, left = views
    flip = lambda p: (3517 - p[0], p[1])
    out = {}
    fr = {"halo.halo": (1763, 332), "horns.graft": (1600, 420), "face.mask": (1763, 1010),
          "nose.bridge": (1763, 1090), "nose.septum": (1763, 1232), "nose.nostrilR": (1714, 1214),
          "nose.nostrilL": (1812, 1214), "mouth.armor": (1763, 1278), "mouth.lip": (1763, 1338),
          "mouth.teeth": (1716, 1322), "mouth.tongue": (1810, 1322), "brow.skin": (1640, 760), "brow.band": (1886, 760),
          "brow.jewel": (1763, 800)}
    for key, p in {"eye.skin": (1590, 1022), "eye.adorn": (1593, 984), "eye.brow": (1600, 904)}.items():
        fr[f"R.{key}"] = p
        fr[f"L.{key}"] = front_.m(p)
    cells_front = [k for k, c in CELLS.items() if c["region"] == "Head" and c["site"] in
                   ("scalp", "brow", "R.eye", "L.eye", "nose", "mouth", "chin", "face", "horns", "halo")]
    out["head_front"] = place(front_, cells_front, fr)
    rp = {"R.ear.lobe": (716, 1186), "R.ear.helix": (856, 992), "R.ear.tip": (806, 930), "hair.pin1": (720, 600)}
    out["head_right"] = place(right, [k for k, c in CELLS.items() if c["site"] in ("R.ear", "R.cheek", "nape") or k == "hair.pin1"], rp)
    lp = {"L.ear.lobe": flip((716, 1186)), "L.ear.helix": flip((856, 992)), "L.ear.tip": flip((806, 930)),
          "hair.pin2": flip((720, 600)), "hair.ribbon": flip((1010, 1290)), "hair.beads": flip((1050, 1640))}
    out["head_left"] = place(left, [k for k, c in CELLS.items() if c["site"] in ("L.ear", "L.cheek")] +
                             ["hair.pin2", "hair.ribbon", "hair.beads"], lp)
    return out


def hand_points(back_view, palm_view, side):
    want = {k for k, c in CELLS.items() if c["region"] == ("Right hand" if side == "R" else "Left hand")}
    on_back = [k for k in want if CELLS[k]["site"] not in (f"{side}.palm", f"{side}.grip")]
    fixed = finger_points(back_view, side, f"{side}.hand", set(on_back))
    b, _ = digit_axis(back_view, f"{side}.middle", f"{side}.hand")
    fixed[f"{side}.fingers.skin"] = (b[0], b[1] - 40)
    back_pts = place(back_view, on_back, fixed)
    xs, ys = palm_view.site_pixels(f"{side}.palm")
    palm_pts = place(palm_view, [f"{side}.palm.skin", f"{side}.palm.adorn", f"{side}.grip.hold"],
                     {f"{side}.grip.hold": palm_view.c((xs.mean(), ys.mean()))})
    return {back_view.name: back_pts, palm_view.name: palm_pts}


def foot_points(top_view, sole_view, side):
    want = {k for k, c in CELLS.items() if c["region"] == ("Right foot" if side == "R" else "Left foot")}
    on_top = [k for k in want if CELLS[k]["site"] not in (f"{side}.sole", f"{side}.heel")]
    fixed = finger_points(top_view, side, f"{side}.foot", set(on_top))
    _, t = digit_axis(top_view, f"{side}.toe3", f"{side}.foot")
    fixed[f"{side}.toetips.tips"] = (t[0], t[1] + 40)
    top_pts = place(top_view, on_top, fixed)
    sole_pts = place(sole_view, [k for k in want if CELLS[k]["site"] in (f"{side}.sole", f"{side}.heel")], {})
    return {top_view.name: top_pts, sole_view.name: sole_pts}


# ---------------------------------------------------------------------------------------------------------------- pages

REGION_PAGES = {
    "Head": ["head_right", "head_front", "head_left"], "Right hand": ["hand_R_back", "hand_R_palm"],
    "Left hand": ["hand_L_back", "hand_L_palm"], "Right foot": ["foot_R_top", "foot_R_sole"],
    "Left foot": ["foot_L_top", "foot_L_sole"],
}


def frame(v, sites, pad=0.1, extra=()):
    """The part of a view that shows these sites (and points), padded, in texture pixels."""
    boxes = []
    for s in sites:
        if s in v.sites:
            xs, ys = v.site_pixels(s)
            boxes.append((xs.min(), ys.min(), xs.max(), ys.max()))
    for p in extra:
        x, y = v.t(p)
        boxes.append((x, y, x, y))
    x0, y0 = min(b[0] for b in boxes), min(b[1] for b in boxes)
    x1, y1 = max(b[2] for b in boxes), max(b[3] for b in boxes)
    w, h = x1 - x0, y1 - y0
    p = pad * max(w, h)
    return [round(float(max(0, x0 - p)), 1), round(float(max(0, y0 - p)), 1),
            round(float(min(v.size[0], x1 + p)), 1), round(float(min(v.size[1], y1 + p)), 1)]


def pages(by_name, overview_points):
    out = {}
    regions = sorted({s["region"] for s in SITES.values()}, key=lambda r: list(SITES[k]["region"] for k in SITES).index(r))
    for region in regions:
        sites = [k for k, s in SITES.items() if s["region"] == region]
        if region in REGION_PAGES:
            out[region] = [{"view": n, "rect": [0, 0, *by_name[n].size]} for n in REGION_PAGES[region]]
            continue
        if region == "Beyond":
            out[region] = [{"view": "front", "rect": [0, 0, *by_name["front"].size]}]
            continue
        panels = []
        for vn in ("front", "back"):
            v = by_name[vn]
            here = [s for s in sites if s in v.sites]
            pts = [overview_points[vn][k] for k in overview_points[vn] if CELLS[k]["site"] in sites]
            if not pts:
                continue
            if region == "Neck":
                here = here + ["chin"]
            panels.append({"view": vn, "rect": frame(v, here, 0.12, pts)})
        out[region] = panels
    return out


# ---------------------------------------------------------------------------------------------------------------- review

def palette(n):
    rng = np.random.default_rng(7)
    cols = []
    for i in range(n):
        h = (i * 0.618034) % 1
        s, l = 0.65 + 0.3 * rng.random(), 0.55
        # hsl to rgb
        c = (1 - abs(2 * l - 1)) * s
        x = c * (1 - abs((h * 6) % 2 - 1))
        m = l - c / 2
        r, g, b = [(c, x, 0), (x, c, 0), (0, c, x), (0, x, c), (x, 0, c), (c, 0, x)][int(h * 6) % 6]
        cols.append((int((r + m) * 255), int((g + m) * 255), int((b + m) * 255)))
    return cols


def font(size):
    try:
        return ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", size)
    except OSError:
        return ImageFont.load_default()


def review(v, points, scale=2.0, crop=None, lines=False):
    """The view with its sites coloured and outlined, their names, and every slot's point."""
    base = Image.fromarray(v.rgba.astype(np.uint8), "RGBA")
    h, w = v.ids.shape
    cols = palette(len(v.sites) + 1)
    colour = np.zeros((h, w, 4), np.uint8)
    for i, s in enumerate(v.sites):
        colour[v.ids == i + 1] = (*cols[i], 120)
    over = Image.fromarray(colour, "RGBA")
    bg = Image.new("RGBA", (w, h), (24, 20, 30, 255))
    bg.alpha_composite(base)
    bg.alpha_composite(over)
    edges = (v.ids != np.roll(v.ids, 1, 0)) | (v.ids != np.roll(v.ids, 1, 1))
    arr = np.asarray(bg).copy()
    arr[edges & v.mask] = (10, 8, 14, 255)
    if lines:
        arr[v.barrier & ~(edges & v.mask)] = (255, 255, 255, 150)
    im = Image.fromarray(arr, "RGBA")
    if crop:
        x0, y0, x1, y1 = (int(c) for c in (*v.t(crop[:2]), *v.t(crop[2:])))
        im = im.crop((x0, y0, x1, y1))
    else:
        x0 = y0 = 0
    im = im.resize((int(im.width * scale), int(im.height * scale)), Image.NEAREST)
    d = ImageDraw.Draw(im)
    f, fs = font(13), font(11)
    for s in v.sites:
        (px, py), _ = v.pole(s)
        d.text(((px - x0) * scale - 20, (py - y0) * scale - 7), s, fill=(255, 255, 255), font=f, stroke_width=2, stroke_fill=(0, 0, 0))
    for cell, p in points.items():
        tx, ty = v.t(p)
        X, Y = (tx - x0) * scale, (ty - y0) * scale
        site = CELLS[cell]["site"]
        good = v.site_at(p) in (site, None) or site in ("torso", "back", "face")
        d.ellipse((X - 4, Y - 4, X + 4, Y + 4), fill=(255, 255, 255) if good else (255, 40, 40), outline=(0, 0, 0))
        d.text((X + 5, Y - 6), cell.split(".")[-1], fill=(255, 240, 120), font=fs, stroke_width=1, stroke_fill=(0, 0, 0))
    return im


def keep_exact(path):
    """A site map is read pixel for pixel: imported lossless and without mipmaps."""
    if os.path.exists(path):
        text = open(path).read()
        text = text.replace("mipmaps/generate=true", "mipmaps/generate=false")
        for mode in ("1", "2", "3", "4"):
            text = text.replace(f"compress/mode={mode}", "compress/mode=0")
    else:
        text = '[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n\ncompress/mode=0\nmipmaps/generate=false\n'
    with open(path, "w") as f:
        f.write(text)


def r1(p):
    return [round(float(p[0]), 1), round(float(p[1]), 1)]


def main():
    os.makedirs(SHOTS, exist_ok=True)
    views = [front(), back(), head_profile("head_right", "R"), head_front(), head_profile("head_left", "L", axis=True),
             hand_page("hand_R_back", "R", True, False), hand_page("hand_R_palm", "R", False, True),
             hand_page("hand_L_back", "L", False, False), hand_page("hand_L_palm", "L", True, True),
             foot_page("foot_R_top", "R", False), foot_page("foot_R_sole", "R", True),
             foot_page("foot_L_top", "L", False), foot_page("foot_L_sole", "L", True)]
    by_name = {v.name: v for v in views}
    problems = []
    for v in views:
        problems += v.partition()
    # Every slot's points: the whole figure, then the close-ups.
    overview = {"front": figure_points(by_name["front"], BEYOND), "back": figure_points(by_name["back"], {})}
    closeups = head_points([by_name["head_right"], by_name["head_front"], by_name["head_left"]])
    for sd in ("R", "L"):
        closeups.update(hand_points(by_name[f"hand_{sd}_back"], by_name[f"hand_{sd}_palm"], sd))
        closeups.update(foot_points(by_name[f"foot_{sd}_top"], by_name[f"foot_{sd}_sole"], sd))
    # Checks: each slot once on the whole figure; each head, hand and foot slot once on its close-up; each point on
    # its own site (or a part of it).
    seen = {}
    for vn, pts in overview.items():
        for k in pts:
            if k in seen:
                problems.append(f"{k} is on both the front and the back")
            seen[k] = vn
    for k in CELLS:
        if k not in seen:
            problems.append(f"{k} has no point on the whole figure")
    close = {}
    for vn, pts in closeups.items():
        for k in pts:
            if k in close:
                problems.append(f"{k} is on two close-up views")
            close[k] = vn
    for k, c in CELLS.items():
        if c["region"] in ("Head", "Right hand", "Left hand", "Right foot", "Left foot") and k not in close:
            problems.append(f"{k} has no point on its close-up")
    # Where a slot's point may sit off its own site: hair pins in the hair on the scalp, a pendant hanging on the
    # breastbone, a tail from the seat, a sole under the heel.
    allowed = {"hair": {"scalp", "hair"}, "horns": {"scalp"}, "collar": {"sternum"}, "tail": {"seat"},
               "sole": {"R.heel", "L.heel"}}
    for vn, pts in list(overview.items()) + list(closeups.items()):
        v = by_name[vn]
        for k, p in pts.items():
            site = CELLS[k]["site"]
            if CELLS[k]["region"] == "Beyond" or site in ("halo",):
                continue
            ok = {site} | set(leaf_sites(site, v)) | (allowed.get(base_of(site)) or set())
            got = v.site_at(p)
            if got not in ok:
                problems.append(f"{vn}: {k} at {tuple(int(q) for q in p)} is on {got}")
    for pr in problems:
        print("  " + pr)

    # The site maps, the map, the review sheets.
    leaves = set().union(*(set(v.sites) for v in views))
    out = {"about": "Written by tools/equipment_map.py from the art in assets/equipment; don't edit by hand.",
           "views": {}, "overview": {}, "closeups": {}, "pages": pages(by_name, overview), "covers": {},
           "glyphs": VIEWS["glyphs"], "backdrops": VIEWS["backdrops"]}
    for v in views:
        Image.fromarray(v.ids.astype(np.uint8), "L").save(os.path.join(OUT, f"{v.name}_sites.png"), optimize=True)
        keep_exact(os.path.join(OUT, f"{v.name}_sites.png.import"))
        sites = []
        rgb = v.rgba[..., :3].astype(np.float32) / 255
        lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
        for st in v.sites:
            xs, ys = v.site_pixels(st)
            (cx, cy), depth = v.pole(st)
            # Busy: mostly hair (or a beard), whose strands shouldn't show through what's worn over it.
            busy = bool((lum[ys, xs] < 0.35).mean() > 0.4)
            sites.append({"key": st, "centre": [round(cx, 1), round(cy, 1)], "box": [int(xs.min()), int(ys.min()),
                          int(xs.max()) + 1, int(ys.max()) + 1], "busy": busy, "outline": v.outlines(st)})
        out["views"][v.name] = {"texture": VIEWS["views"][v.name]["file"], "sites_texture": f"res://assets/equipment/{v.name}_sites.png",
                                "size": v.size, "closeup": v.name not in ("front", "back"), "sites": sites}
    for vn, pts in overview.items():
        out["overview"][vn] = {k: r1(by_name[vn].t(p)) for k, p in sorted(pts.items())}
    for vn, pts in closeups.items():
        out["closeups"][vn] = {k: r1(by_name[vn].t(p)) for k, p in sorted(pts.items())}
    for site in SITES:
        got = covered(site, leaves)
        out["covers"][site] = [[leaf, depth, wrap] for leaf, (depth, wrap) in sorted(got.items(), key=lambda kv: kv[1][0])]
    with open(os.path.join(OUT, "map.json"), "w") as f:
        json.dump(out, f, separators=(",", ":"))
    for v in views:
        pts = dict(overview.get(v.name, {}))
        pts.update(closeups.get(v.name, {}))
        review(v, pts, 1.6 if v.name in ("front", "back") else 1.2).save(os.path.join(SHOTS, f"{v.name}.png"))
    n_out = sum(len(p) for p in overview.values())
    n_close = sum(len(p) for p in closeups.values())
    size = os.path.getsize(os.path.join(OUT, "map.json")) // 1024
    print(f"Wrote assets/equipment/map.json ({size} KB): {len(views)} views, {sum(len(v.sites) for v in views)} site areas, "
          f"{n_out} slots on the whole figure, {n_close} on the close-ups; {len(problems)} problems. Review: shots_map/")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
