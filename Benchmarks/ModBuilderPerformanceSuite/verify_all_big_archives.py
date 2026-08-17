#!/usr/bin/env python3
import os
import glob
import struct
import hashlib
import json

py_dir = r"Z:\GeneralsGamePatch\Patch104pZH\.Build_Python_Full\BigBundleItems"
big_files = sorted(glob.glob(os.path.join(py_dir, "*.big")))
print(f"=== Validating {len(big_files)} Python BIG Archives in .Build_Python_Full ===\n")

total_files = 0
total_bytes = 0
results = []

for bf in big_files:
    fname = os.path.basename(bf)
    with open(bf, "rb") as f:
        data = f.read()
    magic = data[0:4].decode("ascii", errors="ignore")
    num_files_be, header_size_be = struct.unpack(">II", data[8:16])
    total_files += num_files_be
    total_bytes += len(data)
    results.append({
        "filename": fname,
        "magic": magic,
        "files_count": num_files_be,
        "size_mb": len(data) / (1024 * 1024),
        "sha256": hashlib.sha256(data).hexdigest()
    })
    print(f"{fname:<48} | Magic: {magic} | Files: {num_files_be:>4} | Size: {len(data)/(1024*1024):>6.2f} MB")

print(f"\nTotal: {len(big_files)} Archives | {total_files} Files | {total_bytes/(1024*1024):.2f} MB")

out_json = r"Z:\GeneralsHub\Benchmarks\ModBuilderPerformanceSuite\results_gamepatch\all_big_archives_validation.json"
with open(out_json, "w") as f:
    json.dump({"total_archives": len(big_files), "total_files": total_files, "total_mb": total_bytes/(1024*1024), "archives": results}, f, indent=2)
print(f"Saved to: {out_json}")
