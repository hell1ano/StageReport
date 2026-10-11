"""Add dedicated recap textures to an existing Unity bundle without rebuilding prefabs.

Install: python -m pip install UnityPy==1.25.4 Pillow
Usage: python tools/add_recap_icons.py path/to/original/stagerecap path/to/output/stagerecap
"""
import argparse
import copy
from pathlib import Path

import UnityPy
from PIL import Image
from UnityPy.classes import PPtr


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve():
        parser.error("Use a separate output path to preserve the source bundle.")

    env = UnityPy.load(str(args.source))
    bundle_reader = next(o for o in env.objects if o.type.name == "AssetBundle")
    bundle = bundle_reader.read()
    prototype = next(o for o in env.objects if o.type.name == "Texture2D" and o.read().m_Name == "ShrineHalcyonite")
    original = {o.path_id: o.get_raw_data() for o in env.objects}
    modified_ids = {bundle_reader.path_id}
    icons = Path(__file__).resolve().parents[1] / "Assets/StageReport/Interactables"
    expected_images = {}
    for name in ("ConceptReliquary", "WakeAltar"):
        image = Image.open(icons / (name + ".png")).convert("RGBA")
        if image.size != (128, 128):
            raise ValueError(name + " must be 128 x 128")
        expected_images[name] = image
        reader = next((o for o in env.objects if o.type.name == "Texture2D" and o.read().m_Name == name), None)
        if reader is None:
            reader = copy.copy(prototype)
            reader.path_id = 1000000001
            while reader.path_id in prototype.assets_file.objects:
                reader.path_id += 1
            reader.data = prototype.get_raw_data()
            prototype.assets_file.objects[reader.path_id] = reader
            pointer = PPtr(m_FileID=0, m_PathID=reader.path_id, assetsfile=prototype.assets_file)
            info = copy.copy(bundle.m_Container[0][1])
            info.asset = pointer
            info.preloadIndex = len(bundle.m_PreloadTable)
            info.preloadSize = 1
            bundle.m_PreloadTable.append(pointer)
            bundle.m_Container.append(("assets/stagereport/interactables/" + name.lower() + ".png", info))
        modified_ids.add(reader.path_id)
        texture = reader.read()
        texture.object_reader = reader
        texture.m_Name = name
        texture.set_image(image, target_format=4, mipmap_count=1)  # RGBA32, including alpha
        texture.save()
    bundle.save()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(env.file.save(packer="lz4"))

    restored = UnityPy.load(str(args.output))
    objects = {o.path_id: o for o in restored.objects}
    for path_id, raw in original.items():
        if path_id not in modified_ids and objects[path_id].get_raw_data() != raw:
            raise ValueError("Unexpected change to original object " + str(path_id))
    entries = dict(next(o.read() for o in restored.objects if o.type.name == "AssetBundle").m_Container)
    for name, expected in expected_images.items():
        key = "assets/stagereport/interactables/" + name.lower() + ".png"
        texture = objects[entries[key].asset.m_PathID].read()
        if texture.image.convert("RGBA").tobytes() != expected.tobytes():
            raise ValueError("Texture verification failed for " + name)
    print("Verified both icons and preserved original bundle objects:", args.output)


if __name__ == "__main__":
    main()
