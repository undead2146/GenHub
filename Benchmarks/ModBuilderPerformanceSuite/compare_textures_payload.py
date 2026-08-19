import os
import struct
import hashlib

def extract_big(big_path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    with open(big_path, "rb") as f:
        magic = f.read(4)
        if magic != b"BIGF":
            print(f"Not a BIG file: {big_path}")
            return {}
        total_size, file_count, header_size = struct.unpack(">III", f.read(12))
        entries = []
        for _ in range(file_count):
            offset, size = struct.unpack(">II", f.read(8))
            name_bytes = bytearray()
            while True:
                b = f.read(1)
                if not b or b == b"\x00":
                    break
                name_bytes.extend(b)
            name = name_bytes.decode("ascii", errors="replace").replace('/', '\\')
            entries.append((name, size, offset))
        
        extracted = {}
        for name, size, offset in entries:
            f.seek(offset)
            data = f.read(size)
            out_file = os.path.join(out_dir, name)
            os.makedirs(os.path.dirname(out_file), exist_ok=True)
            with open(out_file, "wb") as out_f:
                out_f.write(data)
            extracted[name.lower()] = data
        return extracted

py_big = r"Z:\GeneralsGamePatch\Patch104pZH\.Build_Python_Full\BigBundleItems\600_900_SuperPatch_CoreTextures.big"
cs_big = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\BigBundleItems\600_900_SuperPatch_CoreTextures.big"

out_py = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\inspect_py_coretextures"
out_cs = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\inspect_cs_coretextures"

files_py = extract_big(py_big, out_py)
files_cs = extract_big(cs_big, out_cs)

print(f"Extracted CoreTextures files: Python={len(files_py)}, C#={len(files_cs)}")

all_files = sorted(set(files_py.keys()) | set(files_cs.keys()))
matches = 0
diffs = []

for f in all_files:
    if f not in files_py:
        diffs.append((f, "Missing in Python"))
        continue
    if f not in files_cs:
        diffs.append((f, "Missing in C#"))
        continue
    
    data_py = files_py[f]
    data_cs = files_cs[f]

    if data_py == data_cs:
        matches += 1
    else:
        first_diff = next((i for i, (b1, b2) in enumerate(zip(data_py, data_cs)) if b1 != b2), None)
        diff_str = f"0x{first_diff:X}" if first_diff is not None else "Length/Prefix difference"
        diffs.append((f, f"Data mismatch: PySize={len(data_py)}, CsSize={len(data_cs)}, FirstDiff={diff_str}"))

print(f"\n--- Texture Bitwise Comparison (CoreTextures.big) ---")
print(f"Total Textures: {len(all_files)}")
print(f"Bit-for-Bit Exact Matches: {matches} / {len(all_files)} ({matches/len(all_files)*100:.2f}%)")
print(f"Mismatches: {len(diffs)}")

if diffs:
    print("\nFirst 10 differences:")
    for f, msg in diffs[:10]:
        print(f"  - {f}: {msg}")
