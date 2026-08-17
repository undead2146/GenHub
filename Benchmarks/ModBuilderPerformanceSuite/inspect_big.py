import os
import struct

def read_big_entries(path):
    with open(path, "rb") as f:
        magic = f.read(4)
        if magic != b"BIGF":
            return []
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
            name = name_bytes.decode("ascii", errors="replace")
            entries.append((name, size, offset))
        return entries

py_big = r"Z:\GeneralsGamePatch\Patch104pZH\.Build_Python_Full\BigBundleItems\600_899_SuperPatch_OptionalLangBrazilian.big"
cs_big = r"Z:\GeneralsGamePatch\Patch104pZH\.Build\bundles\600_899_SuperPatch_OptionalLangBrazilian.big"

print("Python OptionalLangBrazilian entries:")
for name, sz, off in read_big_entries(py_big)[:10]:
    print(f"  {name}: {sz} bytes")
print(f"Total Python entries: {len(read_big_entries(py_big))}")

print("\nC# OptionalLangBrazilian entries:")
for name, sz, off in read_big_entries(cs_big)[:10]:
    print(f"  {name}: {sz} bytes")
print(f"Total C# entries: {len(read_big_entries(cs_big))}")
