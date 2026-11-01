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


if __name__ == "__main__":
    unittest.main()
