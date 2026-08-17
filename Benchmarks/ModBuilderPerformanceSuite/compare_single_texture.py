import os
import struct

py_file = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\test_py\art\textures\atbarrslab_d.dds"
cs_file = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\test_cs\art\textures\generatemip\atbarrslab_d.dds"

if os.path.exists(py_file) and os.path.exists(cs_file):
    with open(py_file, "rb") as f1, open(cs_file, "rb") as f2:
        b1 = f1.read()
        b2 = f2.read()
        print(f"File: atbarrslab_d.dds")
        print(f"Py size: {len(b1)}, C# size: {len(b2)}")
        if b1 == b2:
            print(">>> 100% BIT-FOR-BIT EXACT MATCH! <<<")
        else:
            first_diff = next((i for i, (x, y) in enumerate(zip(b1, b2)) if x != y), None)
            print(f"Diff at offset 0x{first_diff:X}")
