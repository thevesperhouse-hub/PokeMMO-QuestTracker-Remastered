"""Force une icone .ico dans un exe Windows (win32api UpdateResource)."""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from win32api import BeginUpdateResource, EndUpdateResource, UpdateResource

RT_ICON = 3
RT_GROUP_ICON = 14

# IDI_APPLICATION = 32512 : icone lue par l'explorateur (liste + apercu).
GROUP_IDS = (1, 32512)


class Structure:
    def __init__(self):
        size = self._sizeInBytes = struct.calcsize(self._format_)
        self._fields_ = list(struct.unpack(self._format_, b"\000" * size))
        self._indexes_ = {name: i for i, name in enumerate(self._names_)}

    def __getattr__(self, name):
        if name in self._names_:
            return self._fields_[self._indexes_[name]]
        raise AttributeError(name)

    def __setattr__(self, name, value):
        if name in self._names_:
            self._fields_[self._indexes_[name]] = value
        else:
            super().__setattr__(name, value)

    def tostring(self):
        return struct.pack(self._format_, *self._fields_)

    def fromfile(self, file):
        data = file.read(self._sizeInBytes)
        self._fields_ = list(struct.unpack(self._format_, data))


class ICONDIRHEADER(Structure):
    _names_ = ("idReserved", "idType", "idCount")
    _format_ = "hhh"


class ICONDIRENTRY(Structure):
    _names_ = (
        "bWidth",
        "bHeight",
        "bColorCount",
        "bReserved",
        "wPlanes",
        "wBitCount",
        "dwBytesInRes",
        "dwImageOffset",
    )
    _format_ = "bbbbhhii"


class GRPICONDIRENTRY(Structure):
    _names_ = (
        "bWidth",
        "bHeight",
        "bColorCount",
        "bReserved",
        "wPlanes",
        "wBitCount",
        "dwBytesInRes",
        "nID",
    )
    _format_ = "bbbbhhih"


class IconFile:
    def __init__(self, path: Path):
        with path.open("rb") as file:
            self.entries = []
            self.images = []
            header = self.header = ICONDIRHEADER()
            header.fromfile(file)
            for _ in range(header.idCount):
                entry = ICONDIRENTRY()
                entry.fromfile(file)
                self.entries.append(entry)
            for entry in self.entries:
                file.seek(entry.dwImageOffset, 0)
                self.images.append(file.read(entry.dwBytesInRes))

    def grp_icon_dir(self):
        return self.header.tostring()

    def grp_icondir_entries(self, icon_id: int = 1):
        data = b""
        current_id = icon_id
        for entry in self.entries:
            group_entry = GRPICONDIRENTRY()
            for name in group_entry._names_[:-1]:
                setattr(group_entry, name, getattr(entry, name))
            group_entry.nID = current_id
            current_id += 1
            data += group_entry.tostring()
        return data


def embed_icon(exe_path: Path, ico_path: Path) -> None:
    icon = IconFile(ico_path)
    handle = BeginUpdateResource(str(exe_path), False)
    icon_id = 1
    group_data = icon.grp_icon_dir() + icon.grp_icondir_entries(icon_id)
    for group_id in GROUP_IDS:
        UpdateResource(handle, RT_GROUP_ICON, group_id, group_data)
    for image_data in icon.images:
        UpdateResource(handle, RT_ICON, icon_id, image_data)
        icon_id += 1
    EndUpdateResource(handle, False)


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: embed_exe_icon.py <exe> <ico>", file=sys.stderr)
        return 2

    exe_path = Path(sys.argv[1]).resolve()
    ico_path = Path(sys.argv[2]).resolve()
    if not exe_path.is_file():
        print(f"EXE introuvable: {exe_path}", file=sys.stderr)
        return 1
    if not ico_path.is_file():
        print(f"ICO introuvable: {ico_path}", file=sys.stderr)
        return 1

    embed_icon(exe_path, ico_path)
    print(f"Icone forcee -> {exe_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
