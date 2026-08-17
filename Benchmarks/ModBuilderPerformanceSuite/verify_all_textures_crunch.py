import os
import hashlib

py_dir = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\test_py"
cs_dir = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\test_cs"

py_files = {}
for root, _, files in os.walk(py_dir):
    for f in files:
        full = os.path.join(root, f)
        base = os.path.basename(full).lower()
        py_files[base] = full

cs_files = {}
for root, _, files in os.walk(cs_dir):
    for f in files:
        full = os.path.join(root, f)
        base = os.path.basename(full).lower()
        cs_files[base] = full

common = sorted(set(py_files.keys()) & set(cs_files.keys()))
matches = 0
diffs = []

for base in common:
    p_path = py_files[base]
    c_path = cs_files[base]

    with open(p_path, "rb") as f1, open(c_path, "rb") as f2:
        b1 = f1.read()
        b2 = f2.read()
        if b1 == b2:
            matches += 1
        else:
            first_diff = next((i for i, (x, y) in enumerate(zip(b1, b2)) if x != y), None)
            diffs.append((base, len(b1), len(b2), first_diff))

print(f"=== CoreTextures Bitwise Crunch Parity Verification ===")
print(f"Total Compared DDS Textures: {len(common)}")
print(f"100% Exact Bit-for-Bit Matches: {matches} / {len(common)} ({matches/len(common)*100:.2f}%)")
print(f"Mismatches: {len(diffs)}")

if diffs:
    print("\nMismatches detail:")
    for base, s1, s2, diff in diffs[:10]:
        print(f"  - {base}: PySize={s1}, CsSize={s2}, DiffOffset=0x{diff:X}")
