"""Extract the raw Ren'Py script data from DDLC Plus (requires: pip install UnityPy).

Usage: python extract_assets.py "<game>/Doki Doki Literature Club Plus_Data" <out_dir>

Writes to out_dir:
  blocks.bin                 the compiled script (Blocks ScriptableObject)
  AudioDefines.bin           audio name -> file mapping
  Lines_<lang>.bin           localized dialogue text for each language
Decode them with tools/unser (see README.md).
"""
import os
import sys

import UnityPy

data_dir, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)


def class_name(obj):
    data = obj.read(check_read=False)
    try:
        return data.m_Script.read().m_ClassName, data.m_Name
    except Exception:
        return None, None


env = UnityPy.load(os.path.join(data_dir, "sharedassets2.assets"))
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    cls, name = class_name(obj)
    if cls == "Blocks":
        open(os.path.join(out_dir, "blocks.bin"), "wb").write(obj.get_raw_data())
    elif cls == "AudioDefines":
        open(os.path.join(out_dir, "AudioDefines.bin"), "wb").write(obj.get_raw_data())

# Language bundles are XOR-obfuscated with 40 (see ActiveAssetBundles / XorFileStream).
bundles = os.path.join(data_dir, "StreamingAssets", "AssetBundles", "Windows")
for f in os.listdir(bundles):
    if not f.startswith("lang"):
        continue
    data = bytes(b ^ 40 for b in open(os.path.join(bundles, f), "rb").read())
    for obj in UnityPy.load(data).objects:
        if obj.type.name == "MonoBehaviour":
            cls, name = class_name(obj)
            if cls == "Lines":
                open(os.path.join(out_dir, name + ".bin"), "wb").write(obj.get_raw_data())
                print(name)
