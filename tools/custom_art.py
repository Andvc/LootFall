"""LootFall's own pixel art, drawn in code: everything the game shows that belongs to its world.

Shapes are drawn with a handful of primitives onto a grid of materials; rendering shades each material with its own
ramp (lit on the top and left edges, shadowed on the bottom and right) and adds a dark outline, so every sprite has the
same light, the same outline and the same palette. Called by tools/import_art.py; run that to rebuild assets/.
"""
from PIL import Image, ImageDraw

OUTLINE = (26, 20, 32, 255)

# Each material: (shadow, mid, light[, shine]). "flat" ones aren't shaded (glows, ink, fine detail).
MATERIALS = {
    "copper": [(110, 52, 30), (176, 98, 52), (222, 150, 90), (250, 206, 150)],
    "silver": [(92, 100, 122), (160, 170, 190), (212, 220, 234), (255, 255, 255)],
    "gold": [(150, 92, 20), (222, 168, 48), (255, 222, 110), (255, 250, 210)],
    "oldgold": [(120, 82, 30), (186, 140, 56), (226, 190, 100), (250, 230, 170)],
    "tarnish": [(70, 66, 56), (128, 116, 70), (170, 156, 90), (200, 190, 130)],
    "brass": [(116, 86, 36), (180, 146, 66), (220, 194, 108), (250, 236, 180)],
    "iron": [(60, 62, 74), (106, 110, 124), (154, 160, 176), (200, 206, 220)],
    "steel": [(86, 96, 120), (156, 170, 194), (212, 224, 240), (255, 255, 255)],
    "rust": [(90, 44, 26), (142, 76, 40), (182, 116, 64), (210, 150, 100)],
    "leather": [(72, 42, 24), (118, 72, 40), (160, 108, 62)],
    "wood": [(86, 50, 26), (136, 86, 44), (176, 122, 68)],
    "rag": [(74, 60, 50), (116, 96, 78), (152, 132, 108)],
    "hunter": [(34, 70, 42), (60, 114, 62), (100, 160, 86)],
    "silk": [(104, 22, 52), (166, 46, 86), (216, 94, 124), (255, 176, 190)],
    "mantle": [(24, 30, 88), (44, 62, 148), (78, 102, 198)],
    "royal": [(106, 14, 26), (172, 30, 42), (218, 68, 70)],
    "ermine": [(168, 168, 180), (220, 220, 230), (250, 250, 255)],
    "black": [(18, 16, 24), (34, 30, 40), (56, 50, 64)],
    "glass": [(116, 146, 168), (176, 206, 222), (228, 244, 250), (255, 255, 255)],
    "clay": [(92, 70, 46), (138, 106, 70), (176, 142, 98)],
    "swamp": [(50, 76, 28), (94, 124, 42), (140, 166, 68)],
    "blood": [(116, 14, 30), (192, 34, 54), (238, 88, 90)],
    "vigour": [(170, 72, 10), (236, 128, 30), (255, 190, 80)],
    "fire": [(186, 40, 20), (246, 116, 30), (255, 214, 90)],
    "nectar": [(186, 126, 30), (248, 196, 70), (255, 238, 168)],
    "cork": [(108, 70, 38), (158, 108, 58), (194, 148, 88)],
    "parchment": [(166, 128, 80), (212, 178, 120), (238, 212, 158)],
    "paper": [(186, 174, 148), (224, 214, 188), (248, 242, 226)],
    "bookbrown": [(78, 40, 24), (126, 66, 36), (166, 98, 54)],
    "bookpurple": [(50, 22, 68), (90, 40, 118), (130, 70, 164)],
    "bookblue": [(24, 36, 70), (40, 64, 118), (66, 98, 160)],
    "sand": [(168, 138, 84), (210, 182, 116), (236, 214, 158)],
    "ruby": [(108, 10, 30), (198, 30, 60), (255, 90, 110), (255, 220, 230)],
    "sapphire": [(20, 38, 118), (40, 88, 198), (100, 158, 255), (230, 240, 255)],
    "amber": [(148, 68, 10), (228, 138, 20), (255, 198, 60), (255, 248, 200)],
    "emerald": [(14, 78, 50), (30, 148, 90), (90, 208, 140), (210, 255, 230)],
    "paste": [(126, 126, 138), (172, 176, 188), (206, 210, 220), (236, 238, 244)],
    "skin": [(150, 92, 66), (206, 140, 100), (236, 182, 140)],
    # coin rims: a metal's shadow, flat
    "copperrim": [(124, 62, 34)],
    "silverrim": [(110, 118, 140)],
    "goldrim": [(170, 110, 26)],
    "oldgoldrim": [(136, 94, 36)],
    "tarnishrim": [(80, 74, 58)],
    # flat details
    "ink": [(70, 46, 34)],
    "inkred": [(186, 40, 40)],
    "rune": [(130, 226, 255)],
    "ember": [(255, 150, 40)],
    "line": [(26, 20, 32)],
    "hole": [(26, 20, 32)],
    "white": [(255, 255, 255)],
    "pupil": [(26, 20, 32)],
    "starlight": [(170, 206, 255)],
}


class Canvas:
    """A small grid of materials. Draw shapes onto it, then render() it into an RGBA image."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.cells = [[None] * w for _ in range(h)]
        self.shine = set()

    def _mask(self):
        return Image.new("1", (self.w, self.h), 0)

    def _apply(self, mask, mat):
        px = mask.load()
        for y in range(self.h):
            for x in range(self.w):
                if px[x, y]:
                    self.cells[y][x] = mat

    def rect(self, x0, y0, x1, y1, mat):
        for y in range(max(0, y0), min(self.h, y1 + 1)):
            for x in range(max(0, x0), min(self.w, x1 + 1)):
                self.cells[y][x] = mat
        return self

    def ellipse(self, x0, y0, x1, y1, mat):
        m = self._mask()
        ImageDraw.Draw(m).ellipse((x0, y0, x1, y1), fill=1)
        self._apply(m, mat)
        return self

    def poly(self, points, mat):
        m = self._mask()
        ImageDraw.Draw(m).polygon(points, fill=1)
        self._apply(m, mat)
        return self

    def line(self, points, mat, width=1):
        m = self._mask()
        ImageDraw.Draw(m).line(points, fill=1, width=width)
        self._apply(m, mat)
        return self

    def dot(self, x, y, mat):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.cells[y][x] = mat
        return self

    def dots(self, points, mat):
        for x, y in points:
            self.dot(x, y, mat)
        return self

    def clear(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.cells[y][x] = None
        return self

    def glint(self, x, y):
        """A shine pixel: the material's brightest tone."""
        self.shine.add((x, y))
        return self

    def at(self, x, y):
        return self.cells[y][x] if 0 <= x < self.w and 0 <= y < self.h else None

    def _edge(self, x, y, m):
        """Whether (x, y) is outside material m, for shading: flat details (an eye, a mouth) don't count as an edge."""
        n = self.at(x, y)
        return n != m and not (n is not None and len(MATERIALS[n]) == 1 and n not in ("hole", "line"))

    def render(self, outline=True):
        img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        px = img.load()
        for y in range(self.h):
            for x in range(self.w):
                m = self.cells[y][x]
                if m is None:
                    continue
                ramp = MATERIALS[m]
                if len(ramp) == 1:
                    px[x, y] = (*ramp[0], 255)
                    continue
                if (x, y) in self.shine:
                    px[x, y] = (*ramp[-1], 255)
                    continue
                lit = self._edge(x, y - 1, m) or self._edge(x - 1, y, m)
                dark = self._edge(x, y + 1, m) or self._edge(x + 1, y, m)
                tone = ramp[2] if lit and not dark else ramp[0] if dark and not lit else ramp[1]
                px[x, y] = (*tone, 255)
        if outline:
            out = img.copy()
            opx = out.load()
            for y in range(self.h):
                for x in range(self.w):
                    if px[x, y][3]:
                        continue
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < self.w and 0 <= ny < self.h and px[nx, ny][3] and self.cells[ny][nx] != "hole":
                            opx[x, y] = OUTLINE
                            break
            img = out
        return img


# ---------------------------------------------------------------------------------------------------------------------
# Things: six kinds, five qualities each, and three fakes. 24x24, shown at 2x on the table (one size for all, so a coin
# and a grimoire sit side by side the way they would).

S = 24


def coin(c, x, y, mat, w=8, h=5):
    """A coin lying flat, seen a little from above: an outline (so coins in a pile part), a dark rim, a lit face."""
    c.ellipse(x - 1, y - 1, x + w, y + h + 1, "line")
    c.ellipse(x, y + 1, x + w - 1, y + h, mat + "rim")
    c.ellipse(x, y, x + w - 1, y + h - 1, mat)
    c.glint(x + 2, y + 1)
    return c


def pile(c, mat, spots):
    """Coins lying about, back to front."""
    for x, y in spots:
        coin(c, x, y, mat)
    return c


def stack(c, x, y, mat, n, w=8):
    """Coins piled in a column, bottom to top: each one's rim shows under the next."""
    for i in range(n):
        top = y - i * 2
        c.ellipse(x - 1, top - 1, x + w, top + 5, "line")
        c.ellipse(x, top + 1, x + w - 1, top + 4, mat + "rim")
        c.ellipse(x, top, x + w - 1, top + 3, mat)
        if i < n - 1:
            c.line([(x + 1, top + 2), (x + w - 2, top + 2)], mat + "rim")
    c.glint(x + 2, y - (n - 1) * 2 + 1)
    return c


def copper_pennies():
    c = Canvas(S, S)
    pile(c, "copper", [(9, 8), (3, 11), (13, 13), (6, 15), (12, 17)])
    return c


def silver_marks():
    c = Canvas(S, S)
    stack(c, 3, 16, "silver", 4)
    stack(c, 12, 18, "silver", 3)
    pile(c, "silver", [(14, 7)])
    return c


def gold_crowns():
    c = Canvas(S, S)
    stack(c, 13, 19, "gold", 3)
    # A big crown piece, face on: a circle with a little crown stamped in it.
    c.ellipse(2, 3, 14, 15, "gold")
    c.ellipse(4, 5, 12, 13, "oldgold")
    c.dots([(6, 8), (8, 7), (10, 8)], "gold").rect(6, 9, 10, 10, "gold")
    c.glint(4, 6).glint(5, 5)
    return c


def old_sovereigns():
    c = Canvas(S, S)
    # A tied leather purse, spilling old gold.
    c.ellipse(2, 6, 14, 19, "leather")
    c.poly([(5, 7), (11, 7), (12, 3), (4, 3)], "leather")
    c.line([(4, 6), (12, 6)], "cork", 1)
    c.dot(13, 5, "cork").dot(14, 4, "cork")
    pile(c, "oldgold", [(14, 9), (11, 14), (15, 16), (6, 18)])
    return c


def dragon_gold():
    c = Canvas(S, S)
    # A hoard: a heap of gold coins with a ruby and a crown in it.
    c.poly([(1, 21), (22, 21), (17, 10), (12, 6), (7, 10)], "gold")
    pile(c, "gold", [(3, 15), (13, 15), (8, 11), (14, 9), (7, 17)])
    c.ellipse(10, 15, 13, 18, "ruby").glint(11, 16)
    c.rect(16, 3, 21, 6, "gold").dots([(16, 2), (18, 2), (21, 2)], "gold").dot(18, 4, "ruby")
    return c


def demon_gold():
    c = Canvas(S, S)
    # Gilded iron: the gilt worn through to grey, and something red where the Maw's eye was.
    pile(c, "tarnish", [(9, 7), (3, 11), (13, 12), (6, 15), (12, 17)])
    c.dots([(4, 12), (10, 18), (16, 13), (9, 15), (14, 8), (17, 18)], "iron")
    c.dot(11, 9, "ember").dot(16, 14, "ember")
    return c


def rusty_dagger():
    c = Canvas(S, S)
    # A short, broad blade gone brown, a bent crossguard, a leather grip.
    c.poly([(9, 13), (12, 16), (20, 5), (21, 2), (18, 3)], "rust")
    c.dots([(13, 11), (16, 8), (15, 10)], "iron")
    c.line([(5, 12), (12, 19)], "iron", 2)
    c.line([(4, 20), (8, 16)], "leather", 3)
    c.ellipse(2, 19, 4, 21, "iron")
    return c


def soldiers_helm():
    c = Canvas(S, S)
    c.ellipse(4, 3, 19, 18, "iron")
    c.rect(3, 13, 20, 17, "iron")
    c.rect(2, 16, 21, 18, "iron")
    c.line([(11, 3), (11, 13)], "steel", 1)
    c.rect(6, 12, 17, 13, "line")
    c.line([(2, 19), (21, 19)], "leather", 1)
    c.dots([(6, 15), (17, 15)], "line")
    c.glint(8, 6).glint(7, 7)
    return c


def steel_longsword():
    c = Canvas(S, S)
    c.line([(6, 17), (19, 4)], "steel", 3)
    c.line([(19, 4), (21, 2)], "steel", 2)
    c.line([(8, 16), (17, 7)], "silver", 1)
    c.line([(3, 13), (10, 20)], "iron", 2)
    c.line([(2, 22), (6, 18)], "leather", 2)
    c.ellipse(0, 21, 2, 23, "brass")
    c.glint(14, 9).glint(18, 5)
    return c


def knights_gauntlet():
    c = Canvas(S, S)
    # A plated glove, fingers up: a flared cuff, the back plate, four jointed fingers, a thumb.
    c.poly([(5, 21), (18, 21), (17, 15), (6, 15)], "steel")
    c.line([(5, 18), (18, 18)], "gold")
    c.poly([(6, 15), (17, 15), (17, 9), (6, 9)], "steel")
    for i, top in enumerate((4, 2, 3, 5)):
        x = 6 + i * 3
        c.rect(x, top, x + 1, 9, "steel")
        c.dot(x, top + 3, "line").dot(x + 1, top + 3, "line")
    c.poly([(17, 12), (21, 8), (22, 10), (18, 15)], "steel")
    c.line([(6, 9), (17, 9)], "line")
    c.line([(6, 15), (17, 15)], "line")
    c.glint(7, 10).glint(6, 16)
    return c


def runeblade():
    c = Canvas(S, S)
    c.line([(6, 17), (19, 4)], "steel", 3)
    c.line([(19, 4), (21, 2)], "steel", 2)
    for x, y in ((9, 14), (11, 12), (13, 10), (15, 8), (17, 6)):
        c.dot(x, y, "rune")
    c.line([(3, 13), (10, 20)], "gold", 2)
    c.dot(6, 16, "ruby")
    c.line([(2, 22), (6, 18)], "mantle", 2)
    c.ellipse(0, 21, 2, 23, "gold")
    c.glint(18, 5).glint(4, 14)
    return c


def cloak(c, mat, collar=None, clasp="brass"):
    """A cloak hung up: shoulders, a flared hem, folds."""
    c.poly([(8, 3), (15, 3), (19, 9), (22, 21), (1, 21), (4, 9)], mat)
    for x0, x1 in ((7, 5), (11, 11), (15, 18)):
        c.line([(x0, 9), (x1, 20)], "line")
    if collar:
        c.poly([(6, 4), (17, 4), (19, 8), (4, 8)], collar)
    c.dot(11, 6, clasp).dot(12, 6, clasp)
    return c


def moth_eaten_cloak():
    c = cloak(Canvas(S, S), "rag", clasp="iron")
    for x, y in ((6, 13), (15, 11), (10, 17), (17, 18), (13, 14)):
        c.dot(x, y, "hole")
    c.clear(2, 21).clear(9, 21).clear(10, 21).clear(18, 21)
    return c


def hunters_coat():
    c = Canvas(S, S)
    # A long coat with sleeves, a belt and a hood down the back.
    c.poly([(7, 3), (16, 3), (18, 7), (18, 21), (5, 21), (5, 7)], "hunter")
    c.poly([(5, 7), (2, 18), (4, 19), (7, 10)], "hunter")
    c.poly([(18, 7), (21, 18), (19, 19), (16, 10)], "hunter")
    c.line([(11, 4), (11, 21)], "line")
    c.rect(5, 13, 18, 14, "leather")
    c.rect(10, 13, 12, 14, "brass")
    c.poly([(8, 3), (15, 3), (13, 6), (10, 6)], "leather")
    return c


def silk_robe():
    c = Canvas(S, S)
    c.poly([(7, 3), (16, 3), (19, 8), (21, 21), (2, 21), (4, 8)], "silk")
    c.poly([(4, 8), (0, 15), (2, 16), (6, 11)], "silk")
    c.poly([(19, 8), (23, 15), (21, 16), (17, 11)], "silk")
    c.line([(11, 3), (8, 21)], "gold")
    c.rect(5, 12, 18, 13, "gold")
    c.glint(14, 6).glint(16, 16)
    return c


def archmages_mantle():
    c = cloak(Canvas(S, S), "mantle", collar="mantle", clasp="gold")
    c.line([(1, 21), (22, 21)], "gold")
    for x, y in ((6, 12), (16, 14), (10, 18), (14, 9), (19, 19)):
        c.dot(x, y, "white")
    c.dot(5, 16, "rune")
    return c


def royal_ermine():
    c = cloak(Canvas(S, S), "royal", collar="ermine", clasp="gold")
    c.rect(1, 19, 22, 21, "ermine")
    for x, y in ((7, 6), (12, 7), (16, 6), (4, 20), (10, 20), (16, 20), (21, 20)):
        c.dot(x, y, "black")
    return c


def bottle(c, body, liquid, top, neck=(10, 13), cork="cork", fill_from=None):
    """A bottle: a body shape (an ellipse box), a neck and a cork, filled with a liquid from fill_from down."""
    x0, y0, x1, y1 = body
    c.ellipse(x0, y0, x1, y1, "glass")
    c.rect(neck[0], top + 2, neck[1], y0 + 1, "glass")
    c.rect(neck[0], top, neck[1], top + 2, cork)
    level = fill_from if fill_from is not None else (y0 + y1) // 2 - 1
    m = Canvas(c.w, c.h).ellipse(x0 + 1, y0 + 1, x1 - 1, y1 - 1, "x")
    for y in range(level, y1):
        for x in range(x0, x1 + 1):
            if m.at(x, y) == "x":
                c.dot(x, y, liquid)
    return c


def swamp_tonic():
    c = Canvas(S, S)
    # A squat clay jug, stoppered, with green muck down its side.
    c.ellipse(4, 8, 19, 22, "clay")
    c.rect(9, 4, 14, 9, "clay")
    c.rect(9, 2, 14, 4, "cork")
    c.poly([(9, 8), (12, 8), (11, 14), (10, 16), (9, 12)], "swamp")
    c.line([(15, 10), (19, 8), (20, 12), (18, 14)], "clay", 2)
    c.dot(6, 18, "swamp").dot(7, 19, "swamp")
    return c


def healing_draught():
    c = bottle(Canvas(S, S), (4, 8, 19, 22), "blood", 3)
    c.glint(7, 11).glint(8, 10)
    return c


def elixir_of_vigour():
    c = Canvas(S, S)
    # A tall square bottle.
    c.rect(6, 8, 17, 22, "glass")
    c.rect(9, 4, 14, 8, "glass")
    c.rect(9, 2, 14, 4, "cork")
    c.rect(7, 12, 16, 21, "vigour")
    c.glint(8, 10).glint(8, 11)
    return c


def phoenix_tears():
    c = Canvas(S, S)
    # A teardrop vial with a fiery liquid and a feather tied on.
    c.poly([(11, 4), (12, 4), (18, 15), (17, 20), (12, 22), (6, 20), (5, 15)], "glass")
    c.poly([(8, 12), (15, 12), (17, 16), (16, 20), (11, 21), (7, 20), (6, 16)], "fire")
    c.rect(10, 1, 13, 4, "gold")
    c.line([(14, 4), (20, 2)], "fire", 2)
    c.dot(21, 1, "ember")
    c.glint(8, 14).glint(9, 13)
    return c


def ambrosia():
    c = Canvas(S, S)
    # An ornate golden decanter, glowing.
    c.ellipse(4, 9, 19, 22, "gold")
    c.ellipse(6, 11, 17, 20, "nectar")
    c.rect(9, 4, 14, 10, "gold")
    c.poly([(7, 4), (16, 4), (14, 2), (9, 2)], "gold")
    c.dot(11, 1, "ruby").dot(12, 1, "ruby")
    c.dots([(5, 15), (18, 15)], "ruby")
    c.glint(8, 13).glint(9, 12).glint(10, 6)
    return c


def tattered_map():
    c = Canvas(S, S)
    c.poly([(2, 4), (21, 3), (22, 19), (16, 21), (13, 19), (3, 21)], "parchment")
    for x, y in ((5, 7), (7, 9), (9, 10), (11, 12), (13, 13), (15, 13)):
        c.dot(x, y, "ink")
    c.dots([(16, 12), (17, 13), (18, 14), (18, 12), (16, 14)], "inkred")
    c.line([(4, 15), (7, 14), (9, 17)], "ink")
    c.clear(21, 10).clear(22, 11).clear(2, 13)
    return c


def spell_scroll():
    c = Canvas(S, S)
    c.rect(4, 6, 19, 17, "paper")
    c.rect(2, 4, 21, 7, "parchment")
    c.rect(2, 16, 21, 19, "parchment")
    for y in (9, 11, 13):
        c.line([(6, y), (17, y)], "ink")
    c.ellipse(13, 15, 18, 20, "ruby")
    c.line([(15, 20), (14, 23)], "ruby")
    c.line([(16, 20), (18, 22)], "ruby")
    return c


def book(c, cover, x0=3, y0=3, x1=20, y1=21):
    """A closed book, standing: a cover with a spine on the left and the page edge showing at the bottom right."""
    c.rect(x0, y0, x1, y1, cover)
    c.rect(x0 + 2, y1 - 2, x1, y1, "paper")
    c.line([(x0 + 2, y0), (x0 + 2, y1 - 3)], "line")
    return c


def bestiary():
    c = book(Canvas(S, S), "bookbrown")
    # Torn by something it describes: three claw marks through the cover.
    for i in range(3):
        c.line([(8 + i * 3, 6), (11 + i * 3, 13)], "paper")
    c.rect(5, 15, 18, 16, "leather")
    return c


def grimoire():
    c = book(Canvas(S, S), "bookpurple")
    for x, y in ((3, 3), (20, 3), (3, 21), (20, 21)):
        c.dot(x, y, "gold")
    c.ellipse(9, 7, 17, 14, "gold")
    c.ellipse(11, 9, 15, 12, "white")
    c.rect(12, 9, 13, 12, "pupil")
    return c


def codex_of_the_locking():
    c = book(Canvas(S, S), "bookblue")
    c.rect(3, 11, 20, 12, "iron")
    c.rect(10, 9, 15, 15, "gold")
    c.dot(12, 11, "line").dot(13, 11, "line").dot(12, 12, "line")
    for x, y in ((3, 3), (20, 3), (3, 21), (20, 21)):
        c.dot(x, y, "gold")
    c.glint(11, 10)
    return c


def sand_case():
    c = Canvas(S, S)
    # A scroll case with its cap off, pouring river sand.
    c.line([(3, 5), (14, 13)], "leather", 6)
    c.line([(2, 3), (5, 7)], "brass", 6)
    c.line([(12, 11), (14, 14)], "brass", 5)
    c.poly([(15, 13), (17, 13), (22, 22), (11, 22)], "sand")
    c.dots([(16, 17), (14, 20), (19, 20), (17, 21), (18, 15)], "parchment")
    return c


def ring(c, band, cx=11, cy=14, r=6, thick=2):
    c.ellipse(cx - r, cy - r + 2, cx + r, cy + r - 1, band)
    c.ellipse(cx - r + thick, cy - r + 2 + thick, cx + r - thick, cy + r - 1 - thick, "hole")
    for y in range(c.h):
        for x in range(c.w):
            if c.at(x, y) == "hole":
                c.clear(x, y)
    return c


def brass_ring():
    c = ring(Canvas(S, S), "brass")
    c.glint(7, 11)
    return c


def silver_amulet():
    c = Canvas(S, S)
    for i, (x, y) in enumerate(((5, 2), (6, 4), (7, 6), (8, 8), (16, 2), (15, 4), (14, 6), (13, 8))):
        c.dot(x, y, "silver")
    c.ellipse(6, 9, 16, 19, "silver")
    c.ellipse(9, 12, 13, 16, "emerald")
    c.glint(10, 13).glint(8, 11)
    return c


def ruby_ring():
    c = ring(Canvas(S, S), "gold", cy=16)
    c.rect(9, 8, 13, 10, "gold")
    c.poly([(8, 6), (14, 6), (12, 2), (10, 2)], "ruby")
    c.glint(10, 3).glint(7, 13)
    return c


def starstone():
    c = Canvas(S, S)
    # A star sapphire: a blue dome with a six-rayed star of light in it, in a little gold setting.
    c.ellipse(2, 3, 21, 21, "gold")
    c.ellipse(4, 5, 19, 19, "sapphire")
    for x, y in ((11, 8), (11, 9), (11, 10), (11, 12), (11, 13), (11, 14), (8, 9), (9, 10), (13, 12), (14, 13),
                 (14, 9), (13, 10), (9, 12), (8, 13)):
        c.dot(x, y, "starlight")
    c.dot(11, 11, "white")
    c.glint(7, 7).glint(8, 6)
    return c


def dragons_eye():
    c = Canvas(S, S)
    # An almond of amber with a slit pupil, held in a gold claw.
    c.poly([(1, 12), (6, 6), (11, 4), (16, 6), (22, 12), (16, 18), (11, 20), (6, 18)], "gold")
    c.poly([(3, 12), (7, 7), (11, 6), (15, 7), (20, 12), (15, 17), (11, 18), (7, 17)], "amber")
    c.poly([(11, 7), (12, 7), (13, 12), (12, 17), (11, 17), (10, 12)], "pupil")
    c.glint(7, 9).glint(8, 8)
    for x, y in ((3, 4), (20, 4), (3, 20), (20, 20)):
        c.line([(x, y), (x + (1 if x < 11 else -1) * 2, y + (1 if y < 12 else -1) * 2)], "gold", 2)
    return c


def paste_gem():
    c = Canvas(S, S)
    c.poly([(6, 6), (17, 6), (21, 11), (11, 21), (2, 11)], "paste")
    c.line([(6, 6), (8, 11), (11, 21)], "line")
    c.line([(17, 6), (15, 11), (11, 21)], "line")
    c.line([(2, 11), (21, 11)], "line")
    c.dots([(12, 8), (5, 10), (16, 14)], "tarnish")
    return c


# 0.17's things: everything you'd want to find, from barrow silver to a dragonslayer.
MATERIALS.update({
    "moon": [(150, 160, 196), (204, 212, 238), (238, 242, 255), (255, 255, 255)],
    "bronze": [(46, 84, 72), (78, 132, 108), (128, 178, 150)],
    "weed": [(30, 70, 40)],
    "shadow": [(20, 14, 30), (40, 30, 58), (66, 52, 92)],
    "wisp": [(150, 110, 220)],
    "flame": [(196, 52, 20), (250, 130, 30), (255, 222, 100)],
    "sea": [(20, 86, 120), (40, 150, 180), (110, 210, 220)],
    "ghost": [(196, 210, 230)],
    "spark": [(255, 236, 120)],
    "skull": [(170, 164, 140), (222, 216, 196), (248, 246, 236)],
    "scale": [(120, 20, 24), (190, 50, 30), (240, 110, 50)],
    "coal": [(40, 20, 20), (80, 30, 26), (130, 46, 30)],
    "stone": [(84, 76, 70), (130, 120, 108), (170, 160, 146)],
    "ribbon": [(200, 40, 60)],
})


def barrow_silver():
    c = Canvas(S, S)
    # Grave silver from the Barrow: old coins, cold to the touch, with a ghost of frost on them.
    pile(c, "silver", [(9, 8), (3, 11), (13, 13), (6, 15), (12, 17)])
    c.dots([(5, 12), (15, 14), (10, 9), (8, 18)], "ghost")
    return c


def moonsilver_marks():
    c = silver_marks()
    # Elven coin: each one stamped with a crescent moon.
    c.ellipse(13, 3, 19, 9, "moon").ellipse(15, 3, 20, 8, "hole")
    for y in range(c.h):
        for x in range(c.w):
            if c.at(x, y) == "hole":
                c.clear(x, y)
    return c


def sunken_sovereigns():
    c = Canvas(S, S)
    # Gold from the drowned town under the Aldwater: a heap of old sovereigns, river weed and a shell.
    pile(c, "oldgold", [(9, 7), (3, 11), (13, 12), (6, 15), (12, 17), (16, 8)])
    c.line([(2, 20), (5, 14), (4, 9)], "weed").line([(19, 21), (21, 15)], "weed")
    c.ellipse(15, 17, 20, 21, "parchment").line([(17, 17), (17, 21)], "line")
    return c


def goblin_cleaver():
    c = Canvas(S, S)
    # A goblin's cleaver: a broad, notched slab of iron on a rag-wrapped stick.
    c.poly([(8, 3), (20, 3), (21, 13), (9, 13)], "iron")
    c.dots([(12, 13), (16, 13), (19, 12)], "hole")
    c.line([(9, 4), (20, 4)], "steel")
    c.line([(9, 13), (3, 21)], "wood", 3)
    c.line([(6, 17), (4, 20)], "rag", 3)
    c.glint(18, 5).glint(17, 6)
    for y in range(c.h):
        for x in range(c.w):
            if c.at(x, y) == "hole":
                c.clear(x, y)
    return c


def dwarven_axe():
    c = Canvas(S, S)
    # A double-bitted dwarven axe: two crescent blades on a haft bound in gold.
    c.line([(5, 22), (17, 3)], "wood", 2)
    c.poly([(10, 4), (14, 8), (9, 13), (6, 11), (5, 6)], "steel")
    c.poly([(16, 9), (20, 13), (22, 20), (17, 17), (14, 13)], "steel")
    c.line([(11, 13), (13, 9)], "gold", 2)
    c.dots([(8, 18), (7, 20)], "gold")
    c.glint(7, 7).glint(18, 12)
    return c


def wardens_blade():
    c = steel_longsword()
    c.line([(2, 22), (6, 18)], "mantle", 2)
    c.dot(6, 16, "sapphire")
    return c


def flamebrand():
    c = Canvas(S, S)
    # A sword that burns: flames lick along the blade.
    c.line([(6, 17), (19, 4)], "steel", 3)
    c.line([(19, 4), (21, 2)], "steel", 2)
    for x, y in ((9, 12), (12, 9), (15, 6), (18, 3), (11, 13), (14, 10), (17, 7)):
        c.dot(x, y, "flame").dot(x - 1, y - 1, "flame")
    c.dots([(10, 10), (13, 7), (16, 4), (20, 1)], "spark")
    c.line([(3, 13), (10, 20)], "iron", 2)
    c.line([(2, 22), (6, 18)], "leather", 2)
    c.ellipse(0, 21, 2, 23, "fire")
    return c


def wolfskin_cloak():
    c = cloak(Canvas(S, S), "fur", clasp="bone")
    # A wolf's head for a hood, ears and all.
    c.poly([(7, 2), (16, 2), (17, 7), (6, 7)], "fur")
    c.dots([(7, 1), (16, 1)], "fur")
    c.dots([(9, 4), (14, 4)], "ember")
    c.line([(1, 21), (22, 21)], "hairwhite")
    return c


def elven_cloak():
    c = cloak(Canvas(S, S), "hunter", clasp="emerald")
    # Woven with leaves: it vanishes into any wood.
    for x, y in ((6, 12), (15, 10), (10, 17), (18, 18), (4, 19)):
        c.dot(x, y, "leaf").dot(x + 1, y - 1, "leaf")
    c.poly([(9, 4), (11, 2), (13, 4), (11, 6)], "emerald")
    return c


def cloak_of_shadows():
    c = cloak(Canvas(S, S), "shadow", clasp="silver")
    # Its edges smoke away into the dark.
    for x, y in ((2, 20), (6, 22), (12, 22), (18, 22), (21, 19), (3, 15), (20, 13)):
        c.dot(x, y, "wisp")
    c.clear(1, 21).clear(8, 21).clear(15, 21).clear(22, 21)
    return c


def phoenix_mantle():
    c = cloak(Canvas(S, S), "flame", collar="gold", clasp="ruby")
    # Feathers of fire, edged in gold.
    for x, y in ((6, 12), (11, 13), (16, 12), (8, 17), (14, 17), (19, 18), (4, 19)):
        c.line([(x, y), (x + 1, y + 2)], "spark")
    c.line([(1, 21), (22, 21)], "gold")
    return c


def invisibility_draught():
    c = Canvas(S, S)
    # A bottle half gone: the bottom of it has already vanished, all but a glimmer.
    bottle(c, (4, 8, 19, 22), "starlight", 3, fill_from=11)
    for y in range(15, 23):
        for x in range(0, S):
            if c.at(x, y) and (x + y) % 3:
                c.clear(x, y)
    c.glint(7, 11).glint(8, 10)
    return c


def mermaids_breath():
    c = Canvas(S, S)
    # A teardrop vial of the sea: breathe it and you breathe water. A shell for a stopper.
    c.poly([(11, 5), (12, 5), (18, 15), (17, 20), (12, 22), (6, 20), (5, 15)], "glass")
    c.poly([(8, 11), (15, 11), (17, 16), (16, 20), (11, 21), (7, 20), (6, 16)], "sea")
    c.dots([(10, 14), (13, 17), (9, 18), (14, 13)], "white")
    c.ellipse(8, 1, 15, 6, "shell")
    c.glint(8, 14)
    return c


def scroll_of_sparks():
    c = spell_scroll()
    # Sealed in blue, crackling.
    c.ellipse(13, 15, 18, 20, "sapphire")
    c.line([(15, 20), (14, 23)], "sapphire").line([(16, 20), (18, 22)], "sapphire")
    c.dots([(1, 2), (22, 2), (20, 9), (2, 12), (21, 14)], "spark")
    return c


def scroll_of_fireball():
    c = Canvas(S, S)
    # An open scroll with a ring of fire drawn on it, the ink still smouldering.
    c.rect(3, 4, 20, 19, "parchment")
    c.rect(2, 2, 21, 4, "wood")
    c.rect(2, 19, 21, 21, "wood")
    c.ellipse(6, 6, 17, 17, "flame").ellipse(8, 8, 15, 15, "parchment")
    c.ellipse(10, 10, 13, 13, "fire")
    c.dots([(5, 5), (18, 6), (19, 16)], "ember")
    return c


def lichs_tome():
    c = book(Canvas(S, S), "black")
    # Bound in black, with a skull on the cover and a green glow round its eyes.
    c.ellipse(8, 5, 16, 13, "skull")
    c.rect(10, 12, 14, 15, "skull")
    c.dots([(10, 9), (14, 9)], "pupil").dots([(11, 9), (13, 9)], "leaf")
    c.dots([(11, 14), (13, 14)], "line")
    for x, y in ((3, 3), (20, 3), (3, 21), (20, 21)):
        c.dot(x, y, "iron")
    return c


def moonstone():
    c = Canvas(S, S)
    # A moonstone: a milky oval with a blue light moving in it.
    c.ellipse(3, 5, 20, 20, "silver")
    c.ellipse(5, 7, 18, 18, "moon")
    c.dots([(9, 10), (10, 11), (11, 11), (12, 12)], "starlight")
    c.glint(7, 9).glint(8, 8)
    return c


def blood_ruby():
    c = Canvas(S, S)
    # A big cut ruby, dark at the heart.
    c.poly([(6, 5), (17, 5), (21, 10), (11, 21), (2, 10)], "ruby")
    c.line([(6, 5), (8, 10), (11, 21)], "blood")
    c.line([(17, 5), (15, 10), (11, 21)], "blood")
    c.line([(2, 10), (21, 10)], "blood")
    c.glint(7, 7).glint(8, 6).glint(16, 7)
    return c


ITEMS = {
    "barrow_silver": barrow_silver, "moonsilver_marks": moonsilver_marks, "dwarf_gold_crowns": gold_crowns,
    "sunken_sovereigns": sunken_sovereigns, "dragon_gold": dragon_gold, "demon_gold": demon_gold,
    "goblin_cleaver": goblin_cleaver, "dwarven_axe": dwarven_axe, "wardens_blade": wardens_blade,
    "flamebrand": flamebrand, "dragonslayer": runeblade,
    "wolfskin_cloak": wolfskin_cloak, "elven_cloak": elven_cloak, "cloak_of_shadows": cloak_of_shadows,
    "archmages_mantle": archmages_mantle, "phoenix_mantle": phoenix_mantle,
    "healing_draught": healing_draught, "elixir_of_vigour": elixir_of_vigour, "invisibility_draught": invisibility_draught,
    "mermaids_breath": mermaids_breath, "ambrosia": ambrosia,
    "scroll_of_sparks": scroll_of_sparks, "scroll_of_fireball": scroll_of_fireball, "summoners_grimoire": grimoire,
    "lichs_tome": lichs_tome, "keyhold_codex": codex_of_the_locking, "sand_case": sand_case,
    "moonstone": moonstone, "elfstone": silver_amulet, "blood_ruby": blood_ruby, "starstone": starstone,
    "dragons_eye": dragons_eye, "paste_gem": paste_gem,
}


# ---------------------------------------------------------------------------------------------------------------------
# Heartsongs: nine things loved more than gold (scripts/Core/Heartsongs.cs). Same size and light as the things, warmer.

def lullaby_box():
    c = Canvas(S, S)
    # A little music box, lid up, its gold comb showing and a dancer on top.
    c.rect(3, 12, 20, 21, "wood")
    c.poly([(3, 12), (20, 12), (22, 5), (5, 5)], "bookbrown")
    c.rect(5, 13, 18, 15, "gold")
    for x in range(6, 18, 2):
        c.dot(x, 14, "line")
    c.line([(3, 17), (20, 17)], "gold")
    c.rect(20, 15, 22, 16, "brass").dot(22, 14, "brass").dot(22, 17, "brass")
    c.line([(11, 5), (11, 2)], "silk").dot(10, 3, "silk").dot(12, 3, "silk").dot(11, 1, "skin")
    return c


def shepherds_pipe():
    c = Canvas(S, S)
    # A shepherd's pan-pipe: seven reeds of falling length, bound with a red ribbon.
    for i in range(7):
        x = 2 + i * 3
        c.rect(x, 2 + i * 2, x + 1, 21, "cork")
        c.dot(x, 2 + i * 2, "line").dot(x + 1, 2 + i * 2, "line")
    c.rect(2, 10, 21, 11, "ribbon")
    c.line([(21, 12), (23, 17)], "ribbon").line([(20, 12), (20, 18)], "ribbon")
    return c


def fey_chimes():
    c = Canvas(S, S)
    # A silver crescent on a twig, hung with rods of fey glass that ring like nothing in this world.
    c.line([(1, 2), (22, 2)], "wood", 2)
    c.dot(4, 1, "leaf").dot(19, 0, "leaf").dot(20, 1, "leaf")
    for i, (x, length) in enumerate(((3, 11), (7, 16), (11, 19), (15, 15), (19, 10))):
        c.line([(x, 3), (x, 5)], "line")
        c.rect(x - 1, 5, x, 5 + length, "emerald" if i % 2 else "moon")
    return c


def dragons_egg():
    c = Canvas(S, S)
    # A dragon's egg, scaled in red, still warm: light shows through the cracks where its heart beats.
    c.ellipse(4, 2, 19, 22, "scale")
    for y in range(5, 21, 3):
        for x in range(6 + (y // 3) % 2 * 2, 18, 4):
            c.dot(x, y, "blood")
    c.line([(9, 11), (11, 13), (10, 16), (13, 18)], "ember")
    c.line([(14, 8), (13, 10)], "ember")
    c.glint(8, 5).glint(7, 6)
    return c


def clockwork_lark():
    c = Canvas(S, S)
    # A brass lark on a perch, a winding key in its back.
    c.ellipse(5, 8, 17, 17, "brass")
    c.ellipse(13, 4, 20, 11, "brass")
    c.poly([(20, 7), (23, 8), (20, 9)], "gold")
    c.dot(17, 7, "pupil")
    c.poly([(5, 12), (0, 16), (6, 16)], "brass")
    c.line([(8, 11), (13, 14)], "line")
    c.rect(8, 5, 9, 8, "iron").rect(6, 3, 11, 5, "iron")
    c.line([(3, 20), (20, 20)], "wood", 2)
    c.line([(10, 17), (10, 19)], "iron").line([(13, 17), (13, 19)], "iron")
    c.glint(14, 5).glint(7, 10)
    return c


def hearthstone():
    c = Canvas(S, S)
    # A round stone from the first dwarven hearth, still warm: firelight in its cracks.
    c.ellipse(2, 5, 21, 21, "stone")
    c.line([(6, 12), (10, 10), (13, 14), (17, 11)], "ember")
    c.line([(9, 17), (12, 15), (15, 18)], "ember")
    c.dots([(7, 15), (16, 8), (12, 7)], "amber")
    c.glint(6, 8).glint(7, 7)
    return c


def drowned_bell():
    c = Canvas(S, S)
    # A chapel bell gone green under the river, barnacles and weed on it.
    c.ellipse(6, 3, 17, 12, "bronze")
    c.poly([(6, 8), (17, 8), (18, 14), (22, 19), (1, 19), (5, 14)], "bronze")
    c.rect(10, 0, 13, 3, "bronze")
    c.rect(1, 18, 22, 20, "bronze")
    c.ellipse(10, 19, 13, 23, "iron")
    c.dots([(7, 15), (15, 9), (17, 16), (9, 8)], "paper")
    c.line([(4, 18), (3, 13)], "weed").line([(17, 5), (19, 10)], "weed")
    c.glint(8, 6).glint(7, 8)
    return c


def bottled_star():
    c = Canvas(S, S)
    # A round flask, corked, with a star caught in it.
    c.ellipse(3, 7, 20, 23, "glass")
    c.rect(9, 3, 14, 8, "glass")
    c.rect(9, 1, 14, 3, "cork")
    c.poly([(11, 9), (13, 13), (17, 15), (13, 17), (11, 21), (10, 17), (6, 15), (10, 13)], "nectar")
    c.rect(10, 14, 12, 16, "white")
    c.dots([(6, 10), (16, 11), (17, 19), (6, 20)], "starlight")
    c.glint(6, 12).glint(7, 11)
    return c


def imps_ember():
    c = Canvas(S, S)
    # An imp's heart: a coal the shape of a heart that never goes out.
    c.ellipse(2, 6, 12, 15, "coal").ellipse(11, 6, 21, 15, "coal")
    c.poly([(3, 12), (20, 12), (11, 22)], "coal")
    c.line([(6, 10), (9, 13), (12, 11), (15, 14), (17, 10)], "ember")
    c.line([(9, 16), (11, 18), (13, 16)], "ember")
    c.dots([(6, 4), (11, 3), (16, 4), (8, 2), (14, 1)], "flame")
    c.glint(5, 8).glint(14, 8)
    return c


HEARTSONGS = {
    "lullaby_box": lullaby_box, "shepherds_pipe": shepherds_pipe, "fey_chimes": fey_chimes, "dragons_egg": dragons_egg,
    "clockwork_lark": clockwork_lark, "hearthstone": hearthstone, "drowned_bell": drowned_bell, "bottled_star": bottled_star,
    "imps_ember": imps_ember,
}


def items():
    """Every thing's sprite (and every Heartsong's), by its key in scripts/Core/Items.cs and Heartsongs.cs."""
    return {key: draw().render() for key, draw in {**ITEMS, **HEARTSONGS}.items()}


# ---------------------------------------------------------------------------------------------------------------------
# Portraits: 38x38 busts, built from the same parts (a head, a neck, shoulders, hair, headwear, a face), shaded the same
# way, over a backdrop in the colour of where they're from. Each person is a spec (below) plus a few touches of their own.

P = 38

MATERIALS.update({
    "fair": [(178, 112, 84), (232, 170, 132), (250, 204, 170)],
    "tan": [(140, 86, 54), (194, 132, 88), (226, 170, 120)],
    "brown": [(88, 50, 32), (140, 86, 54), (180, 120, 80)],
    "ruddy": [(166, 82, 62), (220, 128, 98), (244, 168, 136)],
    "pale": [(150, 150, 166), (204, 204, 214), (236, 236, 244)],
    "goblin": [(58, 98, 34), (100, 150, 52), (148, 196, 84)],
    "imp": [(118, 22, 26), (186, 44, 40), (232, 92, 70)],
    "shade": [(30, 20, 44), (54, 38, 76), (84, 64, 110)],
    "bone": [(150, 140, 120), (206, 198, 176), (240, 236, 220)],
    "gilt": [(150, 96, 22), (220, 170, 52), (255, 226, 118)],
    "fur": [(84, 58, 40), (128, 92, 62), (168, 130, 92)],
    "hairbrown": [(60, 36, 22), (98, 62, 36), (138, 92, 56)],
    "hairblack": [(18, 16, 24), (40, 36, 50), (66, 62, 80)],
    "hairred": [(128, 42, 24), (186, 72, 36), (230, 116, 60)],
    "hairblond": [(168, 120, 46), (220, 176, 80), (248, 220, 130)],
    "hairgrey": [(116, 116, 126), (164, 164, 174), (206, 206, 214)],
    "hairwhite": [(176, 176, 188), (222, 222, 230), (250, 250, 255)],
    "hairauburn": [(92, 34, 30), (142, 58, 44), (184, 92, 66)],
    "hairorange": [(150, 66, 20), (210, 110, 40), (244, 160, 80)],
    "tunicgreen": [(40, 80, 44), (66, 124, 66), (104, 164, 92)],
    "tunicblue": [(34, 54, 104), (58, 88, 156), (92, 128, 200)],
    "tunicred": [(108, 28, 32), (164, 50, 50), (206, 88, 78)],
    "plum": [(56, 22, 60), (96, 42, 102), (138, 74, 142)],
    "lavender": [(88, 70, 126), (136, 112, 178), (180, 158, 220)],
    "wine": [(84, 14, 38), (132, 30, 62), (178, 62, 92)],
    "olive": [(66, 70, 34), (104, 108, 54), (144, 148, 80)],
    "moss": [(44, 66, 34), (74, 104, 52), (110, 144, 78)],
    "habit": [(70, 48, 30), (110, 78, 50), (148, 112, 76)],
    "apron": [(176, 172, 160), (224, 220, 206), (248, 246, 236)],
    "grey": [(70, 70, 80), (110, 110, 122), (150, 150, 164)],
    "straw": [(166, 128, 56), (214, 176, 88), (240, 214, 130)],
    "red": [(140, 22, 30), (200, 44, 48), (240, 96, 90)],
    "bramfelt": [(120, 20, 46), (176, 38, 70), (214, 78, 106)],
    "eyewhite": [(240, 240, 244)],
    "eyeyellow": [(250, 210, 70)],
    "eyered": [(255, 70, 50)],
    "eyeviolet": [(200, 140, 255)],
    "eyegold": [(255, 236, 140)],
    "mouth": [(110, 40, 40)],
    "teeth": [(244, 240, 226)],
    "blush": [(232, 120, 110)],
    "freckle": [(170, 100, 70)],
    "mud": [(110, 84, 56)],
    "leaf": [(96, 150, 60)],
    "pearl": [(240, 236, 250)],
    "plume": [(200, 40, 50)],
})

# Backdrops by place: stall at dusk, Thornwick's green, Crownhold's slate, the Emporium's crimson; and the story's own.
BACKDROPS = {
    "stall": ((52, 52, 96), (28, 28, 58)), "thornwick": ((44, 78, 58), (22, 42, 32)),
    "crownhold": ((62, 70, 92), (32, 36, 52)), "emporium": ((110, 30, 46), (52, 14, 26)),
    "story": ((58, 40, 82), (26, 18, 40)), "kin": ((96, 70, 44), (48, 34, 22)),
}


def backdrop(place):
    """A soft vignette: lighter behind the head, darker at the corners, in a few flat bands (it's pixel art)."""
    inner, outer = BACKDROPS[place]
    img = Image.new("RGBA", (P, P))
    px = img.load()
    for y in range(P):
        for x in range(P):
            d = ((x - 19) ** 2 + (y - 14) ** 2) ** 0.5 / 26
            t = min(1.0, round(d * 4) / 4)
            px[x, y] = (*(round(inner[i] + (outer[i] - inner[i]) * t) for i in range(3)), 255)
    return img


class Face:
    """Where things go on a 38x38 bust: the head's box and the eye line, which every part works from."""

    def __init__(self, c, top=6, width=17, height=20, cx=19):
        self.c, self.top, self.cx = c, top, cx
        self.left, self.right = cx - width // 2, cx + width // 2
        self.bottom = top + height
        self.eyes = top + height // 2 + 1


def body(c, mat, collar=None, wide=15, neck="fair"):
    """Shoulders and chest, and the neck above them."""
    c.ellipse(19 - wide, 29, 19 + wide, 46, mat)
    c.rect(16, 24, 22, 31, neck)
    if collar:
        c.poly([(13, 30), (25, 30), (22, 35), (19, 37), (16, 35)], collar)
    return c


def head(f, skin, ears="round", jaw=0):
    c = f.c
    if ears == "pointy":
        c.poly([(f.left + 1, f.eyes - 1), (f.left - 6, f.eyes - 7), (f.left + 1, f.eyes + 3)], skin)
        c.poly([(f.right - 1, f.eyes - 1), (f.right + 6, f.eyes - 7), (f.right - 1, f.eyes + 3)], skin)
    elif ears == "bat":
        c.poly([(f.left + 2, f.top + 4), (f.left - 6, f.top - 5), (f.left - 2, f.eyes + 2)], skin)
        c.poly([(f.right - 2, f.top + 4), (f.right + 6, f.top - 5), (f.right + 2, f.eyes + 2)], skin)
    elif ears == "round":
        c.ellipse(f.left - 2, f.eyes - 2, f.left + 2, f.eyes + 3, skin)
        c.ellipse(f.right - 2, f.eyes - 2, f.right + 2, f.eyes + 3, skin)
    c.ellipse(f.left, f.top, f.right, f.bottom, skin)
    if jaw:
        c.rect(f.left + 2, f.eyes + 2, f.right - 2, f.bottom - 3 + jaw, skin)
    return f


def eyes(f, kind="normal", colour="pupil", gap=4, dy=0):
    c, y = f.c, f.eyes + dy
    lx, rx = f.cx - gap, f.cx + gap - 1
    for x in (lx, rx):
        if kind == "normal":
            c.dot(x, y, "eyewhite").dot(x + 1, y, colour).dot(x, y + 1, colour).dot(x + 1, y + 1, colour)
        elif kind == "dot":
            c.dot(x, y, colour).dot(x, y + 1, colour)
        elif kind == "closed":
            c.dot(x - 1, y + 1, "line").dot(x, y + 1, "line").dot(x + 1, y + 1, "line")
        elif kind == "narrow":
            c.dot(x - 1, y, "line").dot(x, y + 1, colour).dot(x + 1, y + 1, colour).dot(x + 2, y, "line")
        elif kind == "glow":
            c.dot(x, y, colour).dot(x + 1, y, colour).dot(x, y + 1, colour).dot(x + 1, y + 1, colour)
        elif kind == "wide":
            c.rect(x - 1, y - 1, x + 2, y + 1, "eyewhite").dot(x, y, colour).dot(x + 1, y, colour)
    return f


def brows(f, mat="line", gap=4, dy=-2, angry=False, bushy=False):
    c, y = f.c, f.eyes + dy
    lx, rx = f.cx - gap, f.cx + gap - 1
    c.line([(lx - 1, y + (1 if angry else 0)), (lx + 2, y)], mat)
    c.line([(rx - 1, y), (rx + 2, y + (1 if angry else 0))], mat)
    if bushy:
        c.line([(lx - 1, y - 1), (lx + 2, y - 1)], mat)
        c.line([(rx - 1, y - 1), (rx + 2, y - 1)], mat)
    return f


def nose(f, mat, big=False):
    c, y = f.c, f.eyes + 3
    c.dot(f.cx, y, mat).dot(f.cx, y + 1, mat)
    if big:
        c.dot(f.cx - 1, y + 2, mat).dot(f.cx + 1, y + 2, mat).dot(f.cx, y + 2, mat)
    return f


def mouth(f, kind="smile", dy=0, width=2):
    c, y, x = f.c, f.eyes + 6 + dy, f.cx
    if kind == "smile":
        c.dot(x - width - 1, y - 1, "mouth").line([(x - width, y), (x + width, y)], "mouth").dot(x + width + 1, y - 1, "mouth")
    elif kind == "flat":
        c.line([(x - width, y), (x + width, y)], "mouth")
    elif kind == "frown":
        c.dot(x - width - 1, y + 1, "mouth").line([(x - width, y), (x + width, y)], "mouth").dot(x + width + 1, y + 1, "mouth")
    elif kind == "grin":
        c.dot(x - width - 1, y - 1, "mouth").dot(x + width + 1, y - 1, "mouth")
        c.line([(x - width, y), (x + width, y)], "teeth").line([(x - width + 1, y + 1), (x + width - 1, y + 1)], "mouth")
    elif kind == "fangs":
        c.line([(x - width - 1, y - 1), (x + width + 1, y - 1)], "mouth").line([(x - width, y), (x + width, y)], "mouth")
        c.dots([(x - width, y), (x + width, y), (x - width, y + 1), (x + width, y + 1)], "teeth")
    elif kind == "smirk":
        c.line([(x - width, y), (x + width, y - 1)], "mouth").dot(x + width + 1, y - 2, "mouth")
    elif kind == "open":
        c.rect(x - 1, y - 1, x + 1, y + 1, "mouth")
    return f


def hair(f, mat, style="short"):
    """Hair over the top of the head (drawn after the face, so a fringe sits on the forehead)."""
    c, l, r, t = f.c, f.left, f.right, f.top
    if style in ("short", "messy", "bob", "long", "bun", "updo", "slick", "wild", "ponytail", "combover", "braid"):
        c.ellipse(l - 1, t - 2, r + 1, t + 6, mat)
        c.rect(l - 1, t + 2, l + 1, f.eyes - 2, mat).rect(r - 1, t + 2, r + 1, f.eyes - 2, mat)
        # a parting: the hairline dips to one side
        c.poly([(l + 2, t + 4), (f.cx + 2, t + 4), (f.cx - 3, t + 7), (l + 2, t + 7)], mat)
    if style == "messy":
        c.dots([(l + 2, t - 3), (l + 6, t - 4), (f.cx, t - 4), (r - 5, t - 3), (r - 1, t - 2), (l, t - 1)], mat)
        c.poly([(l + 3, t + 8), (l + 6, t + 6), (l + 8, t + 9)], mat)
    if style == "wild":
        for x, y in ((l - 4, t + 4), (r + 4, t + 4), (l - 3, t - 1), (r + 3, t - 1), (l + 3, t - 4), (r - 3, t - 4), (f.cx, t - 5),
                     (l - 4, f.eyes + 3), (r + 4, f.eyes + 3)):
            c.line([(f.cx + (x - f.cx) // 3, t + 3), (x, y)], mat, 2)
        c.rect(l - 2, t + 3, l, f.eyes + 2, mat).rect(r, t + 3, r + 2, f.eyes + 2, mat)
    if style in ("bob", "long"):
        c.rect(l - 2, t + 3, l + 1, f.eyes + (10 if style == "long" else 5), mat)
        c.rect(r - 1, t + 3, r + 2, f.eyes + (10 if style == "long" else 5), mat)
    if style == "bun":
        c.ellipse(f.cx - 4, t - 7, f.cx + 4, t, mat)
    if style == "updo":
        c.ellipse(f.cx - 6, t - 9, f.cx + 6, t + 1, mat)
    if style == "ponytail":
        c.poly([(r, t + 2), (r + 5, t + 5), (r + 6, t + 17), (r + 2, t + 12)], mat)
    if style == "braid":
        for i in range(4):
            c.ellipse(r - 1, t + 8 + i * 4, r + 3, t + 12 + i * 4, mat)
    if style == "slick":
        c.line([(l + 1, t + 2), (r - 1, t + 2)], "line")
    if style == "combover":
        c.rect(l + 2, t - 1, r - 2, t + 2, f.c.at(f.cx, t + 6) or mat)
        c.line([(l, t + 2), (r - 3, t)], mat, 2)
    if style == "tonsure":
        c.rect(l - 1, t + 4, r + 1, t + 7, mat)
        c.rect(l - 1, t + 7, l + 1, f.eyes - 1, mat).rect(r - 1, t + 7, r + 1, f.eyes - 1, mat)
    if style == "fringe":
        c.line([(l + 1, t + 6), (r - 1, t + 6)], mat, 2)
    return f


def beard(f, mat, style="full"):
    c, y = f.c, f.eyes + 3
    if style == "full":
        c.poly([(f.left, y - 1), (f.right, y - 1), (f.right - 1, f.bottom + 1), (f.cx, f.bottom + 5), (f.left + 1, f.bottom + 1)], mat)
        c.rect(f.cx - 3, f.eyes + 6, f.cx + 3, f.eyes + 7, "mouth")
    elif style == "long":
        c.poly([(f.left, y - 1), (f.right, y - 1), (f.right - 2, f.bottom + 8), (f.cx, f.bottom + 12), (f.left + 2, f.bottom + 8)], mat)
        c.line([(f.cx - 2, f.eyes + 6), (f.cx + 2, f.eyes + 6)], "mouth")
    elif style == "stubble":
        for yy in range(f.eyes + 5, f.bottom - 1, 2):
            for xx in range(f.left + 3 + (yy % 4), f.right - 2, 4):
                if c.at(xx, yy) not in ("mouth", "teeth"):
                    c.dot(xx, yy, mat)
    if style in ("full", "long", "moustache", "handlebar"):
        c.line([(f.cx - 4, f.eyes + 5), (f.cx + 4, f.eyes + 5)], mat, 2)
    if style == "handlebar":
        c.dot(f.cx - 5, f.eyes + 4, mat).dot(f.cx + 5, f.eyes + 4, mat)
    if style == "thin":
        c.line([(f.cx - 3, f.eyes + 5), (f.cx + 3, f.eyes + 5)], mat)
    return f


def hood(f, mat):
    """The back of a hood (draw it before the head)."""
    f.c.ellipse(f.left - 4, f.top - 5, f.right + 4, f.bottom + 2, mat)
    f.c.poly([(f.left - 4, f.eyes), (f.right + 4, f.eyes), (f.right + 8, f.bottom + 8), (f.left - 8, f.bottom + 8)], mat)
    return f


def hood_rim(f, mat):
    """The hood's edge round the face (draw it after the face): over the brow and down both cheeks."""
    c = f.c
    c.poly([(f.left - 1, f.top + 7), (f.left + 1, f.top + 1), (f.cx, f.top - 1), (f.right - 1, f.top + 1), (f.right + 1, f.top + 7),
            (f.right - 2, f.top + 4), (f.cx, f.top + 2), (f.left + 2, f.top + 4)], mat)
    c.rect(f.left - 1, f.top + 6, f.left, f.bottom, mat).rect(f.right, f.top + 6, f.right + 1, f.bottom, mat)
    return f


def portrait(place, build):
    """A finished portrait: the person drawn by `build` over their place's backdrop."""
    c = Canvas(P, P)
    build(c)
    img = backdrop(place)
    img.alpha_composite(c.render())
    return img


def _tam(c):
    body(c, "tunicgreen", neck="fair")
    f = head(Face(c), "fair")
    eyes(f, "normal"), brows(f, "hairbrown"), nose(f, "freckle"), mouth(f, "grin")
    c.dots([(15, 20), (14, 21), (24, 20), (25, 21)], "freckle")
    hair(f, "hairbrown", "messy")
    c.line([(23, 3), (26, 7), (25, 10)], "leaf", 1)  # millpond weed


def _penn(c):
    body(c, "black", collar="apron", neck="fair")
    f = head(Face(c, top=7, height=19), "fair")
    hair(f, "hairgrey", "bun")
    eyes(f, "closed"), brows(f, "hairgrey"), nose(f, "freckle"), mouth(f, "smile", width=1)
    c.dots([(13, 21), (25, 21)], "blush")
    c.poly([(9, 28), (29, 28), (31, 33), (7, 33)], "plum")  # a shawl


def _hob(c):
    body(c, "habit", neck="ruddy", wide=16)
    f = head(Face(c, top=8, width=19, height=19), "ruddy", jaw=1)
    c.ellipse(4, 4, 34, 10, "straw").ellipse(10, -2, 28, 9, "straw")  # a straw hat
    c.line([(10, 7), (28, 7)], "habit")
    eyes(f, "dot"), brows(f, "hairbrown", bushy=True), nose(f, "ruddy", big=True), mouth(f, "grin", width=3)
    beard(f, "hairbrown", "stubble")


def _mags(c):
    body(c, "tunicgreen", collar="apron", neck="ruddy", wide=17)
    f = head(Face(c, width=19, height=20), "ruddy")
    hair(f, "hairred", "updo")
    c.dots([(f.left - 1, f.top - 3), (f.right, f.top - 4), (f.cx, f.top - 9)], "hairred")
    eyes(f, "normal"), brows(f, "hairred"), nose(f, "ruddy"), mouth(f, "grin")
    c.dots([(13, 21), (14, 21), (24, 21), (25, 21)], "blush")


def _dorrin(c):
    body(c, "leather", collar="rag", neck="tan")
    f = head(Face(c), "tan")
    hair(f, "hairblond", "messy")
    c.rect(f.left - 1, f.top + 4, f.right + 1, f.top + 5, "rag")  # a headband
    eyes(f, "normal"), brows(f, "hairblond"), nose(f, "brown"), mouth(f, "grin")
    c.rect(24, 21, 26, 22, "apron")  # a bandage
    c.line([(6, 37), (12, 30)], "wood", 2)  # a torch handle over the shoulder
    c.ellipse(9, 25, 14, 30, "ember")


def _snikkit(c):
    hood(Face(c, top=10, width=13, height=13), "leather")
    body(c, "rag", neck="goblin", wide=13)
    f = head(Face(c, top=8, width=17, height=17), "goblin", ears="pointy")
    eyes(f, "glow", colour="eyeyellow", gap=4), brows(f, "line", angry=True), nose(f, "goblin", big=True)
    mouth(f, "fangs", width=3)
    hood_rim(f, "rag")


def _wenna(c):
    hood(Face(c), "hunter")
    body(c, "leather", collar="hunter", neck="tan")
    f = head(Face(c), "tan")
    hair(f, "hairbrown", "fringe")
    eyes(f, "normal"), brows(f, "hairbrown", angry=True), nose(f, "brown"), mouth(f, "flat")
    hood_rim(f, "hunter")
    c.line([(29, 5), (33, 0)], "plume", 2)  # a feather in her hood
    c.line([(5, 37), (12, 26)], "wood", 1)  # her bow over the shoulder


def _bramble(c):
    hood(Face(c, top=8), "moss")
    body(c, "moss", neck="tan")
    f = head(Face(c, top=8, height=19), "tan")
    hair(f, "hairwhite", "wild")
    eyes(f, "closed"), nose(f, "brown", big=True), mouth(f, "smile")
    c.dots([(14, 23), (24, 23), (15, 12), (23, 12)], "brown")  # lines of age
    for x, y in ((9, 6), (13, 2), (19, 1), (25, 2), (29, 6)):
        c.dot(x, y, "leaf").dot(x, y + 1, "wood")  # a crown of twigs and leaves


def _ansel(c):
    body(c, "habit", neck="fair")
    c.line([(15, 30), (19, 37)], "straw", 1)  # the rope belt's end
    f = head(Face(c, top=6, height=21), "fair")
    hair(f, "hairbrown", "tonsure")
    eyes(f, "normal"), brows(f, "hairbrown"), nose(f, "freckle"), mouth(f, "smile", width=1)
    c.poly([(11, 30), (27, 30), (24, 34), (14, 34)], "habit")


def _fennick(c):
    body(c, "black", collar="leather", neck="tan")
    f = head(Face(c, top=8, height=19), "tan")
    hair(f, "hairblack", "short")
    c.ellipse(3, 5, 35, 11, "black").ellipse(9, -1, 29, 8, "black")  # a wide black hat
    c.line([(9, 7), (29, 7)], "leather")
    eyes(f, "narrow"), brows(f, "hairblack", angry=True), nose(f, "brown"), mouth(f, "smirk")
    beard(f, "hairblack", "stubble")
    c.dot(f.right + 1, f.eyes + 4, "gold")  # an earring


def _wren(c):
    body(c, "tunicblue", collar="apron", neck="fair")
    f = head(Face(c), "fair")
    hair(f, "hairred", "bob")
    eyes(f, "wide"), brows(f, "hairred"), nose(f, "freckle"), mouth(f, "open")
    c.dots([(14, 21), (24, 21)], "freckle")


def _orrin(c):
    body(c, "plum", collar="gold", neck="fair")
    f = head(Face(c, top=10, height=18), "fair")
    beard(f, "hairwhite", "long")
    eyes(f, "dot"), brows(f, "hairwhite", bushy=True), nose(f, "fair", big=True)
    c.poly([(19, -6), (33, 12), (5, 12)], "mantle")  # a tall hat
    c.rect(4, 10, 34, 12, "mantle")
    c.dots([(18, 4), (22, 8), (14, 9)], "white")


def _grit(c):
    body(c, "iron", collar="leather", neck="ruddy", wide=17)
    f = head(Face(c, top=9, width=19, height=17), "ruddy")
    beard(f, "hairorange", "long")
    c.ellipse(8, 3, 30, 15, "iron").rect(8, 10, 30, 12, "iron")  # an iron cap
    c.rect(11, 11, 27, 13, "brass")  # goggles
    c.ellipse(12, 10, 16, 14, "glass").ellipse(22, 10, 26, 14, "glass")
    eyes(f, "dot", dy=2), nose(f, "ruddy", big=True)


def _corisande(c):
    body(c, "lavender", collar="apron", neck="fair")
    f = head(Face(c), "fair")
    hair(f, "hairauburn", "updo")
    c.dots([(14, 2), (19, -1), (24, 2)], "pearl")
    eyes(f, "normal"), brows(f, "hairauburn"), nose(f, "fair"), mouth(f, "smile", width=1)
    c.dot(24, 22, "line")  # a beauty mark
    c.dots([(15, 31), (17, 32), (19, 33), (21, 32), (23, 31)], "pearl")


def _pell(c):
    body(c, "olive", collar="apron", neck="fair")
    f = head(Face(c), "fair")
    hair(f, "hairgrey", "combover")
    eyes(f, "narrow"), brows(f, "hairgrey"), nose(f, "fair", big=True), mouth(f, "smirk")
    beard(f, "hairgrey", "thin")
    c.ellipse(21, 15, 26, 20, "glass").dot(23, 17, "pupil")  # a monocle
    c.line([(26, 19), (28, 30)], "gold")


def _shelly(c):
    body(c, "rag", neck="fair")
    f = head(Face(c), "fair")
    hair(f, "hairblond", "messy")
    c.poly([(f.left - 1, f.top + 1), (f.right + 1, f.top + 1), (f.right + 1, f.top + 5), (f.left - 1, f.top + 5)], "tunicblue")
    eyes(f, "normal"), brows(f, "hairblond"), nose(f, "freckle"), mouth(f, "grin")
    c.dots([(13, 21), (14, 22), (15, 21), (25, 23)], "mud")


def _aldous(c):
    body(c, "steel", collar="tunicred", neck="fair", wide=17)
    f = head(Face(c, top=9, height=18), "fair")
    eyes(f, "normal"), brows(f, "hairbrown", bushy=True), nose(f, "fair", big=True), mouth(f, "flat")
    beard(f, "hairbrown", "handlebar")
    c.ellipse(9, 2, 29, 16, "steel").rect(9, 10, 11, 22, "steel").rect(27, 10, 29, 22, "steel")  # an open helm
    c.rect(18, 9, 20, 16, "steel")  # its nasal
    c.poly([(19, 2), (23, -3), (27, -2), (22, 3)], "plume")


def _vexis(c):
    body(c, "black", collar="gold", neck="imp", wide=13)
    f = head(Face(c, top=9, height=17), "imp", ears="pointy")
    c.poly([(13, 10), (10, 1), (16, 8)], "bone").poly([(25, 10), (28, 1), (22, 8)], "bone")
    eyes(f, "glow", colour="eyeyellow"), brows(f, "line", angry=True), nose(f, "imp"), mouth(f, "smirk", width=3)
    c.rect(28, 28, 33, 37, "gold")  # the hourglass
    c.rect(29, 29, 32, 31, "sand").rect(29, 34, 32, 36, "sand")


def _malbranche(c):
    body(c, "black", collar="gold", neck="bone", wide=14)
    f = head(Face(c, top=8, width=15, height=18), "bone")
    c.poly([(13, 10), (6, 0), (15, 6)], "black").poly([(25, 10), (32, 0), (23, 6)], "black")
    c.ellipse(14, 13, 17, 17, "line").ellipse(21, 13, 24, 17, "line")
    c.dot(15, 15, "eyered").dot(22, 15, "eyered")
    c.dot(19, 19, "line").dot(19, 20, "line")
    for x in range(15, 24, 2):
        c.line([(x, 22), (x, 24)], "line")


def _skrae(c):
    body(c, "shade", neck="shade", wide=14)
    f = head(Face(c, top=10, width=15, height=16), "shade", ears="bat")
    eyes(f, "glow", colour="eyered", gap=3), brows(f, "line", angry=True), mouth(f, "fangs", width=2)


def _ermine(c):
    body(c, "plum", collar="ermine", neck="pale")
    c.dots([(14, 31), (24, 31), (19, 34)], "black")
    f = head(Face(c), "pale")
    hair(f, "hairwhite", "updo")
    c.dots([(14, 1), (24, 1)], "pearl")
    eyes(f, "narrow"), brows(f, "hairwhite"), nose(f, "pale"), mouth(f, "flat", width=1)
    c.dot(13, 21, "blush").dot(25, 21, "blush")


def _silas(c):
    body(c, "plum", collar="black", neck="fair")
    c.line([(14, 31), (19, 35), (24, 31)], "gold")  # a gold chain
    f = head(Face(c), "fair")
    hair(f, "hairblack", "slick")
    eyes(f, "narrow"), brows(f, "hairblack"), nose(f, "fair"), mouth(f, "grin", width=2)
    c.dot(20, 22, "gold")  # a gold tooth
    beard(f, "hairblack", "thin")


def _aldo(c):
    hood(Face(c, top=8), "grey")
    body(c, "grey", neck="fair")
    f = head(Face(c, top=8, height=19), "fair")
    beard(f, "hairwhite", "full")
    eyes(f, "closed"), brows(f, "hairwhite", bushy=True), nose(f, "fair", big=True)
    hood_rim(f, "grey")
    c.rect(26, 32, 30, 35, "skin")  # a hand at his chest, and on it the ring
    c.dot(27, 32, "gold").dot(28, 32, "ruby")


def _maw(c):
    body(c, "gilt", collar="ruby", neck="gilt", wide=16)
    f = head(Face(c, top=9, height=18), "gilt", ears="pointy")
    for x in (11, 15, 19, 23, 27):
        c.poly([(x - 2, 10), (x, 2 if x == 19 else 4), (x + 2, 10)], "gold")  # a crown of spikes
    c.ellipse(17, 10, 21, 13, "ruby")
    eyes(f, "glow", colour="eyered"), brows(f, "line", angry=True), mouth(f, "fangs", width=3)
    c.dots([(f.left - 4, f.eyes + 3), (f.right + 4, f.eyes + 3)], "gold")  # rings in his ears
    for x in range(8, 31, 4):
        c.dot(x, 33, "ruby" if x % 8 else "sapphire")  # chains of gems


def _bram(c):
    body(c, "tunicred", collar="apron", neck="ruddy", wide=16)
    f = head(Face(c, top=8, width=19, height=19), "ruddy")
    c.ellipse(9, 2, 29, 11, "bramfelt").dot(19, 1, "bramfelt")  # his old red beret
    eyes(f, "dot"), brows(f, "hairwhite", bushy=True), nose(f, "ruddy", big=True)
    beard(f, "hairwhite", "handlebar")
    mouth(f, "smile", dy=2)


def _vane(c):
    body(c, "grey", collar="black", neck="pale", wide=13)
    c.line([(12, 31), (19, 36), (26, 31)], "gold")  # the Collector's gilded chain
    c.dot(19, 36, "gold")
    f = head(Face(c, top=6, width=15, height=21), "pale")
    hair(f, "hairblack", "slick")
    eyes(f, "narrow"), brows(f, "hairblack", angry=True), nose(f, "pale"), mouth(f, "smirk", width=1)


def _hood(c):
    hood(Face(c), "plum")
    body(c, "plum", neck="shade")
    f = head(Face(c), "shade")
    eyes(f, "glow", colour="eyeviolet")
    hood_rim(f, "plum")
    c.line([(13, 22), (12, 30)], "hairwhite")  # a strand of silver hair


def _nim(c):
    body(c, "tunicred", collar="leather", neck="fur")
    c.line([(8, 30), (16, 37)], "leather", 2)  # a pack strap
    f = head(Face(c), "fur", ears="pointy")
    c.poly([(13, 8), (8, 0), (6, 3), (11, 10)], "bone").poly([(25, 8), (30, 0), (32, 3), (27, 10)], "bone")
    eyes(f, "glow", colour="eyegold"), nose(f, "line"), mouth(f, "grin", width=3)


def _stranger(c):
    hood(Face(c), "black")
    body(c, "black", neck="shade")
    f = head(Face(c), "shade")
    c.dot(15, f.eyes, "white").dot(22, f.eyes, "white")
    hood_rim(f, "black")
    c.line([(5, 32), (33, 32)], "iron", 2)  # chains
    for x in range(6, 33, 3):
        c.dot(x, 31, "line")


def _kit(c):
    body(c, "tunicgreen", neck="tan")
    f = head(Face(c), "tan")
    hair(f, "hairbrown", "ponytail")
    c.rect(f.left - 1, f.top + 3, f.right + 1, f.top + 4, "red")
    eyes(f, "normal"), brows(f, "hairbrown"), nose(f, "brown"), mouth(f, "grin")


def _hesk(c):
    body(c, "olive", collar="apron", neck="fair", wide=16)
    f = head(Face(c, top=8, height=19), "fair")
    c.ellipse(9, 3, 29, 12, "olive").rect(8, 9, 31, 11, "olive")  # a grocer's cap
    eyes(f, "dot"), brows(f, "hairbrown", bushy=True), nose(f, "fair", big=True), mouth(f, "flat")
    beard(f, "hairbrown", "full")


def _rosa(c):
    body(c, "hunter", collar="tunicred", neck="fair")
    f = head(Face(c), "fair")
    hair(f, "hairred", "long")
    eyes(f, "normal"), brows(f, "hairred", angry=True), nose(f, "fair"), mouth(f, "smirk", width=2)
    c.dots([(f.left - 2, f.eyes + 4), (f.right + 2, f.eyes + 4)], "gold")


def _isolde(c):
    body(c, "wine", collar="gold", neck="fair")
    f = head(Face(c), "fair")
    hair(f, "hairblond", "long")
    c.dots([(15, 4), (19, 3), (23, 4)], "pearl")
    eyes(f, "normal"), brows(f, "hairblond"), nose(f, "fair"), mouth(f, "smile", width=1)


PORTRAITS = {
    "tam": ("stall", _tam), "penn": ("stall", _penn), "hob": ("stall", _hob), "mags": ("stall", _mags),
    "dorrin": ("stall", _dorrin),
    "snikkit": ("thornwick", _snikkit), "wenna": ("thornwick", _wenna), "bramble": ("thornwick", _bramble),
    "ansel": ("thornwick", _ansel), "fennick": ("thornwick", _fennick),
    "wren": ("crownhold", _wren), "orrin": ("crownhold", _orrin), "grit": ("crownhold", _grit),
    "corisande": ("crownhold", _corisande), "pell": ("crownhold", _pell), "shelly": ("crownhold", _shelly),
    "aldous": ("crownhold", _aldous),
    "vexis": ("emporium", _vexis), "malbranche": ("emporium", _malbranche), "skrae": ("emporium", _skrae),
    "ermine": ("emporium", _ermine), "silas": ("emporium", _silas), "aldo": ("emporium", _aldo), "maw": ("emporium", _maw),
    "bram": ("kin", _bram), "vane": ("story", _vane), "hood": ("story", _hood), "nim": ("story", _nim),
    "stranger": ("story", _stranger),
    "kit": ("kin", _kit), "hesk": ("kin", _hesk), "rosa": ("kin", _rosa), "isolde": ("kin", _isolde),
}


def portraits():
    """Every face, by the key the game loads it under (assets/sprites/sellers/{key}.png)."""
    return {key: portrait(place, build) for key, (place, build) in PORTRAITS.items()}


# ---------------------------------------------------------------------------------------------------------------------
# The chest: a warded strongbox, 16x14, a lid of seven rows over a body of five, in the colours of the base skin
# (CHEST_BASE in import_art.py recolours it per colour). A ward rune on the lid, corner straps, a riveted band, and the
# lock's place on the band where ChestView paints it (lid row 6, columns 7-9; body rows 0-1, columns 6-9).
# Letters: a outline, b main, c dark, e highlight, g shade, d light metal, f dark metal.

CHEST_LETTERS = {"a": "#020202", "b": "#dc2728", "c": "#711335", "d": "#ffe199", "e": "#ff5733", "f": "#e67732", "g": "#c03a24"}

CHEST_CLOSED = [
    "................",
    "................",
    ".aaaaaaaaaaaaaa.",
    ".adeeeeeeeeeeda.",
    ".afbbbbddbbbbfa.",
    "aafbbbdbbdbbbfaa",
    "agfbbbbddbbbbfga",
    "agfccccccccccfga",
    "agccccdfffdcccga",
    "adfdfffaafffdfda",
    "aaccbcffffcbccaa",
    ".afbbccccccccfa.",
    ".afbbbbbbbbbbfa.",
    ".aaaaaaaaaaaaaa.",
]

CHEST_OPEN = [
    "................",
    "................",
    "................",
    "................",
    "adddddddddddddda",
    "adaaaaaaaaaaaada",
    "adacccccccccccda",
    "adacccccccccccda",
    "adddddddddddddda",
] + CHEST_CLOSED[9:]


def _grid(rows, letters):
    img = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                h = letters[ch]
                px[x, y] = (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), 255)
    return img


def chest():
    """The closed and open chest, in the base skin's colours: (closed 16x14, open 16x14)."""
    return _grid(CHEST_CLOSED, CHEST_LETTERS), _grid(CHEST_OPEN, CHEST_LETTERS)


def key_shard():
    """A shard of the Aldric Key: an ornate gold bow and half a shaft, snapped off jagged. 18x10, shown at 2x."""
    c = Canvas(18, 10)
    c.ellipse(1, 1, 8, 8, "gold")
    c.ellipse(3, 3, 6, 6, "hole")
    c.rect(8, 4, 15, 5, "gold")
    c.dots([(16, 4), (15, 6), (12, 6), (12, 7)], "gold")
    c.dot(4, 0, "gold").dot(5, 0, "gold")
    c.glint(2, 3)
    for y in range(10):
        for x in range(18):
            if c.at(x, y) == "hole":
                c.clear(x, y)
    return c.render()


def guild_sign():
    """The Gilded Guild's sign: a gold coin stamped with a keyhole under a watching eye. 22x24, shown at 2x."""
    c = Canvas(22, 24)
    c.ellipse(1, 1, 20, 22, "gold")
    c.ellipse(3, 3, 18, 20, "oldgold")
    c.poly([(5, 8), (11, 5), (16, 8), (11, 11)], "white")
    c.ellipse(9, 6, 12, 10, "pupil")
    c.ellipse(9, 12, 12, 15, "line")
    c.poly([(10, 14), (11, 14), (12, 19), (9, 19)], "line")
    c.glint(5, 5).glint(4, 6)
    return c.render()


# ---------------------------------------------------------------------------------------------------------------------
# Curios: Nim's oddments, 24x24 like the things in chests (Curios.All in scripts/Core/Curios.cs, by icon).

MATERIALS.update({
    "shell": [(176, 108, 98), (232, 160, 140), (252, 210, 190)],
    "witch": [(70, 24, 96), (128, 52, 170), (180, 110, 230)],
    "moon": [(150, 150, 176), (204, 206, 226), (240, 242, 255)],
    "clover": [(30, 96, 40), (58, 150, 62), (110, 200, 96)],
    "kid": [(186, 182, 172), (230, 226, 216), (252, 250, 244)],
    "felt": [(54, 40, 70), (88, 66, 110), (124, 100, 150)],
    "string": [(176, 40, 40)],
    "ridge": [(176, 108, 98)],
    "glowwarm": [(255, 214, 120)],
})


def _curio_coin():
    c = Canvas(S, S)
    c.ellipse(3, 4, 20, 21, "gold").ellipse(10, 11, 13, 14, "hole")
    c.line([(11, 11), (14, 1), (18, 0)], "string", 1)
    c.glint(6, 7).glint(7, 6)
    return c


def _curio_shell():
    c = Canvas(S, S)
    c.poly([(11, 3), (21, 10), (18, 19), (5, 19), (2, 10)], "shell")
    for x in (5, 8, 11, 14, 17):
        c.line([(11, 4), (x, 18)], "ridge")
    c.rect(8, 19, 14, 21, "shell")
    return c


def _curio_lantern():
    c = Canvas(S, S)
    c.ellipse(9, 1, 14, 5, "iron").rect(6, 5, 17, 7, "iron")
    c.rect(7, 8, 16, 18, "glass").rect(9, 10, 14, 16, "glowwarm")
    c.rect(6, 18, 17, 20, "iron").line([(7, 8), (7, 18)], "iron").line([(16, 8), (16, 18)], "iron")
    return c


def _curio_catalogue():
    c = book(Canvas(S, S), "bookbrown", 4, 3, 19, 21)
    for y, mat in ((5, "ruby"), (9, "sapphire"), (13, "emerald")):
        c.rect(20, y, 21, y + 2, mat)
    c.rect(8, 7, 16, 11, "paper")
    c.line([(9, 9), (15, 9)], "ink")
    return c


def _curio_crate():
    c = Canvas(S, S)
    c.rect(2, 8, 21, 21, "wood")
    for y in (12, 17):
        c.line([(2, y), (21, y)], "line")
    c.line([(2, 8), (21, 21)], "wood", 2)
    c.poly([(4, 8), (8, 3), (11, 7), (14, 2), (17, 7), (20, 8)], "straw")
    return c


def _curio_oil():
    c = Canvas(S, S)
    c.ellipse(4, 9, 18, 21, "brass")
    c.line([(15, 11), (22, 4)], "brass", 2)
    c.rect(9, 6, 13, 9, "brass")
    c.dot(22, 6, "nectar").dot(22, 7, "nectar")
    c.glint(7, 12)
    return c


def _curio_purse():
    c = Canvas(S, S)
    c.ellipse(4, 8, 19, 22, "tunicblue")
    c.poly([(7, 9), (16, 9), (17, 4), (6, 4)], "tunicblue")
    c.line([(6, 8), (17, 8)], "string")
    c.poly([(11, 12), (13, 15), (11, 18), (9, 15)], "glass")
    return c


def _curio_ticket():
    c = Canvas(S, S)
    c.poly([(3, 7), (17, 4), (21, 17), (7, 20)], "paper")
    c.ellipse(5, 9, 7, 11, "hole")
    for x, y in ((10, 9), (12, 9), (14, 8), (10, 13), (13, 12), (15, 12)):
        c.dot(x, y, "ink")
    c.line([(5, 10), (1, 3)], "string")
    return c


def _curio_signet():
    c = ring(Canvas(S, S), "gold", cy=15)
    c.rect(7, 4, 15, 10, "gold").rect(9, 5, 13, 9, "oldgold")
    c.dot(11, 7, "line").dot(10, 6, "line").dot(12, 6, "line")
    return c


def _curio_jar():
    c = Canvas(S, S)
    c.rect(5, 6, 18, 21, "glass").rect(7, 3, 16, 6, "cork")
    pile(c, "copper", [(7, 16), (10, 13)])
    c.line([(8, 4), (15, 4)], "line")
    c.glint(6, 8).glint(6, 9)
    return c


def _curio_letter():
    c = Canvas(S, S)
    c.rect(2, 6, 21, 19, "paper")
    c.line([(2, 6), (11, 13), (21, 6)], "line")
    c.ellipse(9, 11, 14, 16, "ruby")
    return c


def _curio_knife():
    c = Canvas(S, S)
    c.line([(9, 14), (20, 3)], "steel", 2)
    c.line([(3, 20), (9, 14)], "black", 3)
    c.dot(21, 2, "steel")
    return c


def _curio_hook():
    c = Canvas(S, S)
    c.line([(11, 2), (11, 14)], "iron", 2)
    for dx in (-1, 1):
        c.line([(11, 14), (11 + dx * 6, 18), (11 + dx * 8, 13)], "iron", 2)
    c.line([(11, 14), (11, 21)], "iron", 2)
    c.line([(12, 2), (17, 1), (21, 5)], "straw")
    return c


def _curio_draught():
    c = bottle(Canvas(S, S), (4, 8, 19, 22), "witch", 3, fill_from=11)
    c.dots([(9, 13), (13, 15), (11, 11)], "white")
    return c


def _curio_watch():
    c = Canvas(S, S)
    c.ellipse(3, 5, 20, 22, "gold").ellipse(5, 7, 18, 20, "paper")
    c.line([(11, 13), (11, 9)], "line").line([(11, 13), (14, 15)], "line")
    c.rect(10, 2, 13, 5, "gold")
    c.line([(13, 2), (19, 0)], "gold")
    return c


def _curio_token():
    c = Canvas(S, S)
    c.rect(2, 8, 11, 17, "paper").rect(12, 4, 21, 13, "paper")
    c.dots([(4, 10), (6, 12), (8, 14), (14, 6), (19, 11), (14, 11), (19, 6)], "line")
    return c


def _curio_crescent():
    c = Canvas(S, S)
    c.ellipse(3, 3, 20, 20, "moon").ellipse(8, 1, 23, 17, "hole")
    for y in range(S):
        for x in range(S):
            if c.at(x, y) == "hole":
                c.clear(x, y)
    c.dots([(18, 19), (20, 16), (21, 21)], "witch")
    return c


def _curio_marker():
    c = Canvas(S, S)
    c.poly([(4, 3), (19, 3), (19, 14), (11, 21), (4, 14)], "brass")
    c.poly([(7, 8), (11, 6), (16, 8), (11, 10)], "white").dot(11, 8, "pupil").dot(12, 8, "pupil")
    c.glint(6, 5)
    return c


def _curio_gauntlet():
    c = Canvas(S, S)
    c.rect(6, 15, 17, 21, "leather").rect(6, 8, 17, 15, "leather")
    for i in range(4):
        c.rect(6 + i * 3, 3 + (i % 2), 7 + i * 3, 8, "leather")
    c.poly([(17, 11), (21, 8), (22, 10), (18, 14)], "leather")
    c.line([(6, 16), (17, 16)], "line")
    return c


def _curio_cap():
    c = Canvas(S, S)
    c.ellipse(2, 14, 21, 20, "felt").ellipse(5, 4, 18, 17, "felt")
    c.rect(5, 12, 18, 14, "royal")
    c.line([(16, 2), (21, 9)], "steel").dot(21, 10, "string")
    return c


def _curio_loupe():
    c = Canvas(S, S)
    c.ellipse(2, 2, 15, 15, "brass").ellipse(4, 4, 13, 13, "glass")
    c.line([(13, 13), (21, 21)], "wood", 3)
    c.glint(6, 6).glint(7, 6)
    return c


def _curio_clover():
    c = Canvas(S, S)
    for x, y in ((6, 3), (13, 3), (6, 10), (13, 10)):
        c.ellipse(x - 1, y - 1, x + 5, y + 5, "clover")
    c.line([(11, 12), (14, 22)], "clover")
    return c


def _curio_book():
    c = Canvas(S, S)
    c.poly([(1, 8), (11, 6), (11, 20), (1, 21)], "paper").poly([(12, 6), (22, 8), (22, 21), (12, 20)], "paper")
    for y in (10, 13, 16):
        c.line([(3, y), (9, y - 1)], "ink").line([(14, y - 1), (20, y)], "ink")
    c.line([(18, 0), (12, 10)], "white", 2)
    return c


def _curio_gloves():
    c = Canvas(S, S)
    # A pair of white kid gloves, one laid over the other.
    for x0, y0 in ((2, 4), (11, 7)):
        c.rect(x0, y0 + 6, x0 + 7, y0 + 14, "kid")
        for i in range(4):
            c.rect(x0 + i * 2, y0 + (i % 2), x0 + i * 2 + 1, y0 + 6, "kid")
        c.poly([(x0 + 7, y0 + 8), (x0 + 10, y0 + 5), (x0 + 10, y0 + 7), (x0 + 8, y0 + 11)], "kid")
        c.line([(x0, y0 + 12), (x0 + 7, y0 + 12)], "pearl")
        c.line([(x0 - 1, y0 + 6), (x0 + 8, y0 + 6)], "line") if x0 == 11 else None
    return c


def _curio_dome():
    c = Canvas(S, S)
    c.rect(3, 18, 20, 21, "wood")
    c.ellipse(5, 2, 18, 17, "glass").rect(5, 10, 18, 18, "glass")
    c.ellipse(10, 13, 13, 16, "ruby")
    c.glint(8, 5).glint(7, 6).glint(7, 7)
    return c


CURIOS = {
    "coin": _curio_coin, "shell": _curio_shell, "lantern": _curio_lantern, "catalogue": _curio_catalogue,
    "crate": _curio_crate, "oil": _curio_oil, "purse": _curio_purse, "ticket": _curio_ticket, "signet": _curio_signet,
    "jar": _curio_jar, "letter": _curio_letter, "knife": _curio_knife, "hook": _curio_hook, "draught": _curio_draught,
    "watch": _curio_watch, "token": _curio_token, "crescent": _curio_crescent, "marker": _curio_marker,
    "gauntlet": _curio_gauntlet, "cap": _curio_cap, "loupe": _curio_loupe, "clover": _curio_clover, "book": _curio_book,
    "gloves": _curio_gloves, "dome": _curio_dome,
}


def curios():
    """Every curio's icon, by Curios.All's Icon."""
    out = {}
    for key, draw in CURIOS.items():
        c = draw()
        for y in range(S):
            for x in range(S):
                if c.at(x, y) == "hole":
                    c.clear(x, y)
        out[key] = c.render()
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Backdrops for the four places (640x360, shown at 2x behind the counter; import_art.py's build_shops draws each shop's
# architecture over them), and the props that stand on the counters. Flat bands of colour, hard edges, a little
# dithering where two bands meet: painted the way the sprites are.

import math
import random

W, H = 640, 360


def _hex(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def bands(img, top, bottom, colours, dither=True):
    """A sky or a wall in horizontal bands of colour, top to bottom, checkered where they meet."""
    d = ImageDraw.Draw(img)
    n = len(colours)
    step = (bottom - top) / n
    for i, col in enumerate(colours):
        y0, y1 = int(top + i * step), int(top + (i + 1) * step)
        d.rectangle((0, y0, W, y1), fill=(*col, 255))
        if dither and i + 1 < n:
            for y in range(y1 - 2, y1 + 2):
                for x in range(y % 2, W, 2):
                    if y >= y1:
                        img.putpixel((x, y), (*col, 255))
    return img


def ridge(img, base, height, colour, seed, roughness=6, step=8):
    """A line of hills: a random walk along the top, filled down to the bottom."""
    rng = random.Random(seed)
    d = ImageDraw.Draw(img)
    pts, y = [], base - height // 2
    for x in range(-step, W + step * 2, step):
        y = max(base - height, min(base, y + rng.randint(-roughness, roughness)))
        pts.append((x, y))
    d.polygon(pts + [(W + step, H), (-step, H)], fill=(*colour, 255))
    return img


def trees(img, xs, base, colour, size=10, seed=0):
    rng = random.Random(seed)
    d = ImageDraw.Draw(img)
    for x in xs:
        s = size + rng.randint(-3, 3)
        d.polygon([(x, base - s * 2), (x + s, base), (x - s, base)], fill=(*colour, 255))
        d.polygon([(x, base - s * 3), (x + s * 3 // 4, base - s), (x - s * 3 // 4, base - s)], fill=(*colour, 255))
    return img


def clouds(img, spots, colour):
    d = ImageDraw.Draw(img)
    for x, y, w in spots:
        d.ellipse((x, y, x + w, y + w // 3), fill=(*colour, 255))
        d.ellipse((x + w // 4, y - w // 6, x + w * 3 // 4, y + w // 4), fill=(*colour, 255))
    return img


def crossroads():
    """Bram's stall at the Brindle crossroads, at dusk: the river, the mill and its pond, the road north, and on the
    far hill the Barrow's standing stones."""
    img = Image.new("RGBA", (W, H))
    bands(img, 0, 170, [_hex(c) for c in ("#2b2d5c", "#3e3f75", "#5b5288", "#8a6390", "#c27d86", "#e99f7a", "#f6c27c")])
    clouds(img, [(60, 80, 90), (300, 60, 120), (500, 95, 80)], (222, 150, 140))
    ridge(img, 160, 40, (96, 82, 120), 3)
    d = ImageDraw.Draw(img)
    # The Barrow: a long mound on the far hill, ringed with standing stones.
    d.polygon([(370, 160), (420, 128), (440, 122), (490, 122), (510, 128), (560, 160)], fill=(84, 72, 108, 255))
    for x, h in ((418, 12), (434, 16), (452, 14), (472, 17), (490, 13), (506, 11)):
        top = 124 - h + (4 if x in (418, 506) else 0)
        d.rectangle((x, top, x + 5, top + h), fill=(62, 54, 84, 255))
        d.line((x, top, x + 5, top), fill=(110, 96, 136, 255))
    ridge(img, 182, 26, (74, 96, 84), 11, roughness=4)
    # The river, winding down to the mill.
    d.polygon([(0, 205), (180, 196), (330, 200), (640, 192), (640, 206), (330, 214), (180, 210), (0, 218)], fill=(120, 150, 190, 255))
    d.line((0, 206, 640, 199), fill=(170, 196, 226, 255))
    ridge(img, 238, 22, (58, 84, 56), 23, roughness=3)
    # The mill: a stone tower, a wheel, a roof.
    d.rectangle((92, 150, 132, 205), fill=(120, 104, 96, 255), outline=(52, 40, 46, 255))
    d.polygon([(86, 152), (112, 128), (138, 152)], fill=(132, 60, 50, 255), outline=(52, 40, 46, 255))
    d.ellipse((128, 172, 160, 204), outline=(84, 60, 40, 255), width=3)
    for a in range(0, 360, 45):
        d.line((144, 188, 144 + 15 * math.cos(math.radians(a)), 188 + 15 * math.sin(math.radians(a))), fill=(84, 60, 40, 255), width=2)
    d.rectangle((108, 186, 116, 205), fill=(52, 40, 46, 255))
    # The signpost at the crossroads.
    d.rectangle((560, 150, 564, 232), fill=(96, 64, 36, 255))
    d.polygon([(536, 156), (586, 156), (594, 162), (586, 168), (536, 168)], fill=(150, 104, 58, 255), outline=(60, 40, 22, 255))
    d.polygon([(590, 176), (540, 176), (532, 182), (540, 188), (590, 188)], fill=(150, 104, 58, 255), outline=(60, 40, 22, 255))
    trees(img, (24, 210, 250, 360, 620), 226, (40, 64, 44), seed=5)
    return img


def thornwick_wall():
    """Thornwick: an old stone wall, every stone a little different, with a window onto the Old Wood."""
    img = Image.new("RGBA", (W, H), (88, 80, 76, 255))
    rng = random.Random(9)
    d = ImageDraw.Draw(img)
    y = 0
    while y < H:
        h = rng.randint(14, 20)
        x = -rng.randint(0, 30)
        while x < W:
            w = rng.randint(26, 46)
            shade = rng.randint(-14, 14)
            base = (150 + shade, 138 + shade, 124 + shade)
            d.rectangle((x + 1, y + 1, x + w - 1, y + h - 1), fill=(*base, 255))
            d.line((x + 1, y + 1, x + w - 2, y + 1), fill=(min(255, base[0] + 22), min(255, base[1] + 22), min(255, base[2] + 22), 255))
            d.line((x + 1, y + h - 1, x + w - 1, y + h - 1), fill=(base[0] - 30, base[1] - 30, base[2] - 30, 255))
            x += w
        y += h
    # A window onto the Old Wood.
    wx = 420
    d.rectangle((wx, 52, wx + 96, 132), fill=(52, 40, 30, 255))
    view = Image.new("RGBA", (88, 72))
    bands(view, 0, 72, [(150, 190, 170), (120, 170, 140), (90, 140, 110)], dither=False)
    trees(view, (6, 24, 44, 62, 82), 76, (40, 76, 48), size=12, seed=2)
    trees(view, (14, 34, 54, 74), 80, (28, 56, 36), size=14, seed=3)
    img.alpha_composite(view, (wx + 4, 56))
    d.rectangle((wx + 46, 56, wx + 49, 128), fill=(52, 40, 30, 255))
    d.rectangle((wx + 4, 90, wx + 92, 93), fill=(52, 40, 30, 255))
    d.rectangle((wx - 6, 132, wx + 102, 138), fill=(120, 108, 96, 255), outline=(60, 50, 44, 255))
    return img


def crownhold_view(w=380, h=72):
    """Crownhold from the shop window, at sunset: roofs, chimneys, spires, and the palace on its hill."""
    img = Image.new("RGBA", (w, h))
    sky = [(70, 60, 110), (140, 80, 120), (220, 120, 110), (250, 180, 120)]
    for i, col in enumerate(sky):
        ImageDraw.Draw(img).rectangle((0, i * h // 6, w, (i + 1) * h // 6 + 1), fill=(*col, 255))
    ImageDraw.Draw(img).rectangle((0, len(sky) * h // 6, w, h), fill=(*sky[-1], 255))
    d = ImageDraw.Draw(img)
    # The palace on its hill: towers and a dome, with the Gate below it glinting.
    d.polygon([(180, 70), (220, 40), (300, 40), (340, 70)], fill=(90, 66, 100, 255))
    for x, top in ((228, 14), (252, 22), (276, 10), (300, 18)):
        d.rectangle((x, top, x + 10, 44), fill=(90, 66, 100, 255))
        d.polygon([(x - 2, top), (x + 5, top - 9), (x + 12, top)], fill=(90, 66, 100, 255))
    d.ellipse((250, 26, 276, 46), fill=(90, 66, 100, 255))
    d.rectangle((262, 60, 274, 70), fill=(232, 186, 70, 255))
    rng = random.Random(4)
    x = 0
    while x < w:
        bw, bh = rng.randint(18, 34), rng.randint(14, 30)
        col = rng.choice([(110, 70, 82), (96, 62, 78), (126, 82, 88)])
        d.rectangle((x, h - bh, x + bw, h), fill=(*col, 255))
        d.polygon([(x - 2, h - bh), (x + bw // 2, h - bh - 10), (x + bw + 2, h - bh)], fill=(150, 64, 60, 255))
        if rng.random() < 0.5:
            d.rectangle((x + bw - 7, h - bh - 12, x + bw - 3, h - bh - 4), fill=(*col, 255))
        for wx in range(x + 4, x + bw - 4, 8):
            if rng.random() < 0.4:
                d.rectangle((wx, h - bh + 6, wx + 2, h - bh + 9), fill=(255, 214, 120, 255))
        x += bw + rng.randint(0, 4)
    return img


def emporium_hall():
    """The Royal Emporium: a marble colonnade open to the sky, and down the avenue the Gilded Gate, glowing."""
    img = Image.new("RGBA", (W, H))
    bands(img, 0, 240, [(104, 160, 222), (130, 182, 232), (160, 202, 240), (196, 222, 246), (226, 238, 250)])
    clouds(img, [(120, 70, 110), (380, 50, 140), (520, 110, 90)], (246, 248, 255))
    d = ImageDraw.Draw(img)
    # The city's far walls, and the Gate in the middle of them, gold, with light leaking through.
    d.rectangle((0, 168, W, 238), fill=(176, 170, 190, 255))
    for x in range(0, W, 24):
        d.rectangle((x, 160, x + 12, 168), fill=(176, 170, 190, 255))
    d.rectangle((272, 120, 368, 238), fill=(150, 110, 40, 255))
    d.ellipse((272, 96, 368, 150), fill=(150, 110, 40, 255))
    d.rectangle((282, 130, 358, 238), fill=(222, 176, 64, 255))
    d.ellipse((282, 108, 358, 152), fill=(222, 176, 64, 255))
    for x in range(290, 352, 10):
        d.rectangle((x, 120, x + 3, 238), fill=(150, 110, 40, 255))
    d.rectangle((316, 120, 324, 238), fill=(255, 236, 160, 255))
    # The colonnade: columns and the floor of the hall.
    for x in (70, 170, 470, 570):
        d.rectangle((x, 40, x + 26, 238), fill=(232, 228, 220, 255), outline=(160, 150, 140, 255))
        for fx in range(x + 5, x + 24, 6):
            d.line((fx, 46, fx, 232), fill=(206, 200, 192, 255))
        d.rectangle((x - 6, 36, x + 32, 44), fill=(244, 240, 232, 255), outline=(160, 150, 140, 255))
        d.rectangle((x - 6, 230, x + 32, 238), fill=(244, 240, 232, 255), outline=(160, 150, 140, 255))
    d.rectangle((0, 30, W, 36), fill=(244, 240, 232, 255), outline=(160, 150, 140, 255))
    return img


def _prop(draw, w, h):
    c = Canvas(w, h)
    draw(c)
    return c.render()


def props():
    """Things that stand about the shops: a crate, a barrel, jars and a bottle (drawn at 1x on the 640x360 picture)."""
    def crate(c):
        c.rect(1, 1, 22, 20, "wood")
        for y in (7, 14):
            c.line([(1, y), (22, y)], "line")
        c.line([(1, 1), (22, 20)], "wood", 2)

    def barrel(c):
        c.ellipse(1, 0, 18, 6, "wood").rect(1, 3, 18, 24, "wood").ellipse(1, 21, 18, 26, "wood")
        for y in (6, 20):
            c.line([(1, y), (18, y)], "iron")
        c.line([(9, 3), (9, 24)], "line")

    def jar(c):
        c.ellipse(1, 4, 14, 19, "clay").rect(4, 1, 11, 5, "clay").rect(3, 0, 12, 1, "clay")
        c.line([(2, 10), (13, 10)], "royal")

    def small_jar(c):
        c.ellipse(1, 3, 10, 13, "brass").rect(3, 0, 8, 3, "brass")

    def flask(c):
        c.ellipse(1, 7, 10, 17, "glass").rect(4, 1, 7, 8, "glass").rect(4, 0, 7, 1, "cork")
        c.rect(2, 11, 9, 16, "blood")

    return {"crate": _prop(crate, 24, 22), "barrel": _prop(barrel, 20, 28), "jar": _prop(jar, 16, 21),
            "small_jar": _prop(small_jar, 12, 15), "flask": _prop(flask, 12, 19)}


def burst(frames=6, w=112, h=100):
    """The ring that bursts from a chest as it opens: sparks, a flash, a ring that spreads and breaks into embers.
    Pale, so the game can tint it with the best thing's colour (it's drawn additively)."""
    sheet = Image.new("RGBA", (w * frames, h))
    hot, warm, edge = (255, 250, 220, 255), (255, 214, 110, 255), (240, 140, 70, 255)
    rng = random.Random(7)
    cx, cy = w // 2, h // 2
    for f in range(frames):
        img = Image.new("RGBA", (w, h))
        d = ImageDraw.Draw(img)
        t = f / (frames - 1)
        if f == 0:
            for a in range(0, 360, 45):
                r = 10 + (a // 45) % 2 * 6
                x, y = cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))
                d.rectangle((x - 2, y - 2, x + 2, y + 2), fill=warm)
            d.rectangle((cx - 3, cy - 3, cx + 3, cy + 3), fill=hot)
        elif f == 1:
            d.ellipse((cx - 14, cy - 14, cx + 14, cy + 14), fill=warm)
            d.ellipse((cx - 10, cy - 10, cx + 10, cy + 10), fill=hot)
            for a in range(0, 360, 30):
                x, y = cx + 20 * math.cos(math.radians(a)), cy + 20 * math.sin(math.radians(a))
                d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=warm)
        else:
            r = int(18 + 30 * t)
            thick = max(2, 7 - f)
            d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=edge, width=thick + 1)
            d.ellipse((cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1), outline=warm, width=thick)
            d.ellipse((cx - r + 2, cy - r + 2, cx + r - 2, cy + r - 2), outline=hot, width=max(1, thick - 3))
            # Puffs along the ring, and gaps opening in it as it fades.
            for i in range(10):
                a = math.radians(i * 36 + rng.randint(-10, 10))
                x, y = cx + r * math.cos(a), cy + r * math.sin(a)
                pr = rng.randint(3, 6) - (f - 2)
                if pr > 1:
                    d.ellipse((x - pr, y - pr, x + pr, y + pr), fill=warm if i % 2 else edge)
            if f >= 4:
                for i in range(6 * (f - 3)):
                    a = math.radians(rng.randint(0, 359))
                    x, y = cx + r * math.cos(a), cy + r * math.sin(a)
                    d.rectangle((x - 3, y - 3, x + 3, y + 3), fill=(0, 0, 0, 0))
            if f == frames - 1:
                for i in range(12):
                    a = math.radians(rng.randint(0, 359))
                    rr = r + rng.randint(-4, 8)
                    x, y = cx + rr * math.cos(a), cy + rr * math.sin(a)
                    d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=warm)
        sheet.alpha_composite(img, (f * w, 0))
    return sheet
