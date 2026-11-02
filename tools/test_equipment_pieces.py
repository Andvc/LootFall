"""Regression checks for registered equipment geometry."""
import json
import unittest
from unittest.mock import patch

import equipment_pieces as ep


class SeededCloakMaskTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with open(ep.SPEC, encoding="utf-8") as stream:
            cls.assets = {a["id"]: a for a in json.load(stream)["assets"]}

    def test_drape_continues_to_region_crop_edges(self):
        # These are interior points of a calf-length cloak. Cropping out the
        # calves must not shorten its mask to the shoulder or waist.
        points = {
            "back": (544, 1370),
            "right_arm_front": (570, 1450),
            "right_arm_back": (160, 1500),
            "left_arm_front": (390, 1450),
            "left_arm_back": (800, 1500),
        }
        with patch.object(ep, "delivered", return_value=None):
            for base, (x, y) in points.items():
                with self.subTest(base=base):
                    asset = self.assets[f"pieces/{base}/cloak"]
                    area = ep.areas(asset, full=False)
                    self.assertTrue(area["paint"][y, x],
                                    f"{base}: the cloak stops before the crop edge")

    def test_front_drape_does_not_cover_the_visible_arm(self):
        with patch.object(ep, "delivered", return_value=None):
            area = ep.areas(self.assets["pieces/right_arm_front/cloak"], full=False)
        self.assertFalse(area["paint"][900, 400])
        self.assertFalse(area["paint"][100, 100])


class GambesonShoulderMaskTests(unittest.TestCase):
    def test_short_sleeves_cover_the_connecting_shoulders(self):
        garment = ep.family("gambeson")
        for name in ("figure_front", "figure_back", "torso", "back",
                     "right_arm_front", "left_arm_front", "right_arm_back", "left_arm_back"):
            base = ep.BASES[name]
            shoulders = base.mask(["L.shoulder", "R.shoulder"])
            with self.subTest(base=name):
                self.assertTrue(shoulders.any())
                paint = garment.area(base)[0]
                self.assertTrue(paint[shoulders].all(), "short sleeves must join the torso over the shoulder")

    def test_short_sleeves_do_not_extend_to_forearms_or_background(self):
        garment = ep.family("gambeson")
        for name, side in (("right_arm_front", "R"), ("left_arm_front", "L"),
                           ("right_arm_back", "R"), ("left_arm_back", "L")):
            base = ep.BASES[name]
            paint = garment.area(base)[0]
            with self.subTest(base=name):
                for site in (side + ".forearm", side + ".wrist"):
                    x, y = map(round, base.centre([site]))
                    self.assertFalse(paint[y, x], site)
                self.assertFalse(paint[100, 480])

    def test_requested_files_order_and_prompts_are_preserved(self):
        with open(ep.SPEC, encoding="utf-8") as stream:
            expected = json.load(stream)["assets"]
        actual = ep.build()
        self.assertEqual(len(actual), 869)
        keys = ("id", "n", "priority", "size", "prompt", "isolate")
        self.assertEqual([{k: a.get(k) for k in keys} for a in actual],
                         [{k: a.get(k) for k in keys} for a in expected])


class PlexusDiscStrapMaskTests(unittest.TestCase):
    def test_disc_strap_connects_to_both_armpits(self):
        garment = ep.family("plexus_disc")
        for name in ("figure_front", "torso"):
            base = ep.BASES[name]
            paint = garment.area(base)[0]
            centre = base.centre(["plexus"])
            for side in ("L", "R"):
                end = base.centre([side + ".armpit"])
                with self.subTest(base=name, side=side):
                    for amount in (0.15, 0.35, 0.65, 0.85):
                        x, y = (round(centre[i] + amount * (end[i] - centre[i])) for i in (0, 1))
                        self.assertTrue(paint[y, x], "the chest strap must connect the disc to the side")

    def test_disc_strap_does_not_fill_chest_belly_or_upper_arms(self):
        garment = ep.family("plexus_disc")
        base = ep.BASES["torso"]
        paint = garment.area(base)[0]
        for site in ("L.pec", "R.pec", "navel", "L.upperarm", "R.upperarm"):
            x, y = map(round, base.centre([site]))
            with self.subTest(site=site):
                self.assertFalse(paint[y, x])
        self.assertFalse(paint[100, 100])



class HeadFasteningMaskTests(unittest.TestCase):
    def test_nose_guard_band_reaches_across_the_brow(self):
        for name in ("head_front", "head_right", "head_left", "figure_front"):
            base = ep.BASES[name]
            brow = base.mask(["brow"])
            paint = ep.family("nose_guard").area(base)[0]
            with self.subTest(base=name):
                self.assertTrue(brow.any())
                self.assertTrue(paint[brow].all())

    def test_nose_guard_band_keeps_cheeks_and_background_clear(self):
        base = ep.BASES["head_front"]
        paint = ep.family("nose_guard").area(base)[0]
        for x, y in ((340, 680), (640, 680), (290, 820), (690, 820), (100, 100)):
            with self.subTest(point=(x, y)):
                self.assertFalse(paint[y, x])

    def test_coif_allows_ear_slits_chin_cord_knot_and_nape(self):
        paint = ep.family("coif").area(ep.BASES["head_front"])[0]
        for x, y in ((160, 820), (820, 820), (250, 1000), (490, 1180), (490, 1290)):
            with self.subTest(point=(x, y)):
                self.assertTrue(paint[y, x], "the coif needs a fastening below the scalp")
        for name in ("head_right", "head_left", "figure_back"):
            base = ep.BASES[name]
            nape = base.mask(["nape"])
            with self.subTest(base=name):
                self.assertTrue(nape.any())
                self.assertTrue(ep.family("coif").area(base)[0][nape].all())

    def test_coif_keeps_eyes_central_face_and_lower_neck_clear(self):
        paint = ep.family("coif").area(ep.BASES["head_front"])[0]
        for x, y in ((340, 680), (640, 680), (490, 850), (490, 980), (490, 1450), (40, 40)):
            with self.subTest(point=(x, y)):
                self.assertFalse(paint[y, x])


class ThumbTattooMaskTests(unittest.TestCase):
    def test_thumb_art_excludes_other_digits_and_palm(self):
        with open(ep.SPEC, encoding="utf-8") as stream:
            assets = [a for a in json.load(stream)["assets"]
                      if a.get("family") == "thumb_tattoo"]
        self.assertEqual(len(assets), 4)
        for asset in assets:
            base = ep.BASES[asset["base"]]
            side = asset["pieces"][0].split()[0][0]
            area = ep.areas(asset)
            with self.subTest(base=base.name):
                thumb = base.mask([side + ".thumb"])
                self.assertTrue(thumb.any())
                self.assertTrue((area["core"] == thumb).all())
                for site in ("index", "middle", "ring", "little", "hand"):
                    other = base.mask([side + "." + site])
                    self.assertFalse((area["core"] & other).any())
                self.assertTrue(area["paint"][thumb].all())
                self.assertFalse(area["allowed"][20, 20])


class BackpackStrapMaskTests(unittest.TestCase):
    def test_straps_run_from_shoulder_tops_to_armpits(self):
        for name in ("figure_front", "torso"):
            base = ep.BASES[name]
            paint = ep.family("backpack").area(base)[0]
            for side in ("L", "R"):
                shoulder = base.centre([side + ".shoulder"])
                clavicle = base.centre([side + ".clavicle"])
                top = ((shoulder[0] + clavicle[0]) / 2,
                       base.box([side + ".shoulder", side + ".clavicle"])[1])
                end = base.centre([side + ".armpit"])
                for fraction in (0, 0.25, 0.5, 0.75, 1):
                    x, y = (round(top[i] + fraction * (end[i] - top[i])) for i in (0, 1))
                    with self.subTest(base=name, side=side, fraction=fraction):
                        self.assertTrue(paint[y, x])
            for site in ("L.pec", "R.pec", "navel"):
                x, y = map(round, base.centre([site]))
                self.assertFalse(paint[y, x], "straps must leave the central chest and belly clear")


class QuiverStrapMaskTests(unittest.TestCase):
    def test_strap_connects_the_right_shoulder_to_the_left_flank(self):
        for name in ("figure_front", "torso"):
            base = ep.BASES[name]
            paint = ep.family("quiver").area(base)[0]
            shoulder = base.centre(["R.shoulder"])
            clavicle = base.centre(["R.clavicle"])
            start = ((shoulder[0] + clavicle[0]) / 2,
                     base.box(["R.shoulder", "R.clavicle"])[1])
            end = base.centre(["L.flank"])
            for amount in (0, 0.25, 0.5, 0.75, 1):
                x, y = (round(start[i] + amount * (end[i] - start[i])) for i in (0, 1))
                with self.subTest(base=name, amount=amount):
                    self.assertTrue(paint[y, x])
            for site in ("L.clavicle", "R.flank", "navel"):
                x, y = map(round, base.centre([site]))
                self.assertFalse(paint[y, x], "the strap must not cross on the opposite diagonal")


class BackScabbardStrapMaskTests(unittest.TestCase):
    def test_front_strap_runs_from_shoulder_top_to_opposite_hip(self):
        base = ep.BASES["figure_front"]
        paint = ep.family("back_scabbard").area(base)[0]
        shoulder = base.centre(["R.shoulder"])
        clavicle = base.centre(["R.clavicle"])
        start = ((shoulder[0] + clavicle[0]) / 2,
                 base.box(["R.shoulder", "R.clavicle"])[1])
        end = base.centre(["L.hip"])
        for amount in (0, 0.25, 0.5, 0.75, 1):
            x, y = (round(start[i] + amount * (end[i] - start[i])) for i in (0, 1))
            with self.subTest(amount=amount):
                self.assertTrue(paint[y, x])
        for site in ("L.clavicle", "R.flank", "navel"):
            x, y = map(round, base.centre([site]))
            self.assertFalse(paint[y, x], "the diagonal must leave unrelated sites clear")


class HandPlateCoverageTests(unittest.TestCase):
    def assets(self):
        with open(ep.SPEC, encoding="utf-8") as stream:
            return [a for a in json.load(stream)["assets"] if a.get("family") == "hand_plate"]

    def test_palm_views_require_a_fastening_rather_than_full_steel_coverage(self):
        assets = [a for a in self.assets() if a["base"] in
                  ("figure_back", "hand_R_palm", "hand_L_palm")]
        self.assertEqual(len(assets), 3)
        for asset in assets:
            with self.subTest(base=asset["base"]):
                self.assertEqual(ep.areas(asset)["coverage"], "part")

    def test_back_views_require_hand_coverage_and_leave_fingers_free(self):
        for asset in self.assets():
            base = ep.BASES[asset["base"]]
            area = ep.areas(asset)
            with self.subTest(base=base.name):
                if base.name in ("figure_front", "hand_R_back", "hand_L_back"):
                    self.assertEqual(area["coverage"], "full")
                    self.assertTrue(area["core"].any())
                for side in ("L", "R"):
                    digits = base.mask([side + "." + name for name in
                                        ("thumb", "index", "middle", "ring", "little")])
                    self.assertFalse((area["core"] & digits).any())
                self.assertFalse(area["core"][0, 0])


class ChinTattooMaskTests(unittest.TestCase):
    def test_short_dot_column_uses_the_area_below_the_lip(self):
        with open(ep.SPEC, encoding="utf-8") as stream:
            assets = [a for a in json.load(stream)["assets"]
                      if a.get("family") == "chin_tattoo"]
        self.assertTrue(assets)
        for asset in assets:
            base = ep.BASES[asset["base"]]
            core = ep.areas(asset)["core"]
            x0, y0, x1, y1 = base.box(["mouth"])
            cx = round((x0 + x1) / 2)
            with self.subTest(base=base.name):
                self.assertTrue(core[round(y1 + 0.2 * base.U), cx])
                self.assertFalse(core[round((y0 + y1) / 2), cx], "the lip is not tattooed")
                self.assertLess(core.sum(), base.mask(["chin"]).sum() / 8)
                self.assertFalse(core[20, 20])


if __name__ == "__main__":
    unittest.main()
