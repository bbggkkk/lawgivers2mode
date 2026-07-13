from __future__ import annotations

import re
import struct
from pathlib import Path

GAME = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Lawgivers II")


def pe_info(path: Path) -> None:
    data = path.read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    machine = struct.unpack_from("<H", data, pe + 4)[0]
    magic = struct.unpack_from("<H", data, pe + 24)[0]
    print(f"{path.name}: machine=0x{machine:04x}, optional_magic=0x{magic:04x}")


def strings(data: bytes, minimum: int = 5):
    pattern = rb"[\x20-\x7e]{" + str(minimum).encode() + rb",}"
    for match in re.finditer(pattern, data):
        yield match.group().decode("ascii", "replace")


def main() -> None:
    pe_info(GAME / "Lawgivers II.exe")
    pe_info(GAME / "GameAssembly.dll")
    data_dir = GAME / "Lawgivers II_Data"
    metadata = data_dir / "il2cpp_data" / "Metadata" / "global-metadata.dat"
    raw = metadata.read_bytes()
    sanity, version = struct.unpack_from("<II", raw)
    print(f"metadata: sanity=0x{sanity:08x}, version={version}, size={len(raw)}")
    manager_strings = strings((data_dir / "globalgamemanagers").read_bytes())
    versions = sorted({s for s in manager_strings if re.fullmatch(r"20\d\d\.\d+\.\d+[A-Za-z0-9.]*", s)})
    print("Unity version candidates:", versions)
    keywords = re.compile(r"person|politic|party|loyal|money|wealth|action.?point|missile|army|military|probab|chance", re.I)
    matches = sorted({s for s in strings(raw, 4) if len(s) <= 160 and keywords.search(s)})
    print(f"keyword strings ({len(matches)}):")
    print("\n".join(matches))


if __name__ == "__main__":
    main()
