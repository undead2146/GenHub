import os
import sys
import hashlib
from pathlib import Path

def get_file_hash(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(65536):
            h.update(chunk)
    return h.hexdigest()

def compare_dirs(dir_a, dir_b, name_a="Python", name_b="C# Crunch"):
    print(f"=== Comparing '{name_a}' vs '{name_b}' ===")
    print(f"Dir A: {dir_a}")
    print(f"Dir B: {dir_b}")
    
    if not os.path.exists(dir_a):
        print(f"Error: {dir_a} does not exist")
        return
    if not os.path.exists(dir_b):
        print(f"Error: {dir_b} does not exist")
        return

    files_a = {}
    for root, _, files in os.walk(dir_a):
        for file in files:
            full = os.path.join(root, file)
            rel = os.path.relpath(full, dir_a).lower().replace('\\', '/')
            files_a[rel] = full

    files_b = {}
    for root, _, files in os.walk(dir_b):
        for file in files:
            full = os.path.join(root, file)
            rel = os.path.relpath(full, dir_b).lower().replace('\\', '/')
            files_b[rel] = full

    all_keys = sorted(set(files_a.keys()) | set(files_b.keys()))
    print(f"Total files in A: {len(files_a)}, in B: {len(files_b)}, unique across both: {len(all_keys)}")

    exact_matches = 0
    mismatches = []
    missing_in_b = []
    missing_in_a = []

    for rel in all_keys:
        if rel not in files_b:
            missing_in_b.append(rel)
            continue
        if rel not in files_a:
            missing_in_a.append(rel)
            continue
        
        path_a = files_a[rel]
        path_b = files_b[rel]

        size_a = os.path.getsize(path_a)
        size_b = os.path.getsize(path_b)
        
        if size_a != size_b:
            mismatches.append((rel, f"Size mismatch: {size_a} vs {size_b}"))
            continue

        hash_a = get_file_hash(path_a)
        hash_b = get_file_hash(path_b)

        if hash_a == hash_b:
            exact_matches += 1
        else:
            # Detailed byte diff
            with open(path_a, "rb") as fa, open(path_b, "rb") as fb:
                ba = fa.read()
                bb = fb.read()
                first_diff = -1
                diff_count = 0
                for idx, (b1, b2) in enumerate(zip(ba, bb)):
                    if b1 != b2:
                        if first_diff == -1:
                            first_diff = idx
                        diff_count += 1
                mismatches.append((rel, f"Hash mismatch (SHA-256 diff). First diff at byte offset {first_diff} (0x{first_diff:X}), total {diff_count}/{len(ba)} differing bytes"))

    print(f"Exact Bit-for-Bit Matches: {exact_matches} / {len(all_keys)} ({exact_matches/len(all_keys)*100:.2f}%)")
    print(f"Mismatches: {len(mismatches)}")
    print(f"Missing in B: {len(missing_in_b)}")
    print(f"Missing in A: {len(missing_in_a)}")

    if mismatches:
        print("\nFirst 10 mismatches details:")
        for rel, reason in mismatches[:10]:
            print(f"  - {rel}: {reason}")

if __name__ == "__main__":
    dir_py = r"Z:\GeneralsGamePatch\Patch104pZH\.Build_Python_Full"
    dir_cs = r"Z:\GeneralsGamePatch\Patch104pZH\.Build"
    compare_dirs(dir_py, dir_cs, "Python Reference", "C# Output (.Build)")
