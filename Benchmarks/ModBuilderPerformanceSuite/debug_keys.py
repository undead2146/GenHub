import os
from compare_textures_payload import extract_big

py_big = r"Z:\GeneralsGamePatch\Patch104pZH\.Build_Python_Full\BigBundleItems\600_900_SuperPatch_CoreTextures.big"
cs_big = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\BigBundleItems\600_900_SuperPatch_CoreTextures.big"

files_py = extract_big(py_big, r"Z:\GeneralsGamePatch\Patch104pZH\.Build\test_py")
files_cs = extract_big(cs_big, r"Z:\GeneralsGamePatch\Patch104pZH\.Build\test_cs")

print("Python keys (first 5):", list(files_py.keys())[:5])
print("C# keys (first 5):", list(files_cs.keys())[:5])
