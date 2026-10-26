"""Regression checks for off-canvas reach in seeded region views."""
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


if __name__ == "__main__":
    unittest.main()
