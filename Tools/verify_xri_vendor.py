"""Verify embedded XRI against the official archive and the reviewed patch.

Usage: python3 Tools/verify_xri_vendor.py /path/to/com.unity.xr.interaction.toolkit-2.6.5.tgz
No files are extracted or changed. Download URL and checksums are in Patches/.
"""
import difflib
import hashlib
import json
from pathlib import Path
import sys
import tarfile


def verify(archive):
    root = Path(__file__).resolve().parent.parent
    provenance = json.loads((root / 'Tools/Patches/xri-2.6.5-provenance.json').read_text())
    data = Path(archive).read_bytes()
    for algorithm in ('sha1', 'sha256'):
        if hashlib.new(algorithm, data).hexdigest() != provenance[algorithm]:
            raise ValueError(f'Official archive {algorithm} mismatch')
    vendor = root / 'Packages/com.unity.xr.interaction.toolkit'
    expected = {}
    with tarfile.open(archive, 'r:gz') as source:
        for member in source.getmembers():
            if not member.isfile():
                continue
            relative = Path(member.name).relative_to('package')
            if str(relative) in provenance['omitted'] or relative.parts[0] in provenance['omitted']:
                continue
            expected[relative.as_posix()] = source.extractfile(member).read()
    actual = {p.relative_to(vendor).as_posix(): p.read_bytes()
              for p in vendor.rglob('*') if p.is_file()}
    if expected.keys() != actual.keys():
        raise ValueError(f'Unexpected or missing files: {expected.keys() ^ actual.keys()}')
    changed = sorted(name for name in expected if expected[name] != actual[name])
    if changed != sorted(provenance['changed']):
        raise ValueError(f'Unexpected changed files: {changed}')
    patch = ''.join(line for name in changed for line in difflib.unified_diff(
        expected[name].decode().splitlines(True), actual[name].decode().splitlines(True),
        fromfile='a/' + name, tofile='b/' + name))
    if patch != (root / 'Tools/Patches/xri-2.6.5-unity-6000.6.patch').read_text():
        raise ValueError('Embedded source differs from the reviewed patch')
    print(f'XRI verified: {len(actual)} files; only {len(changed)} reviewed debugger changes')


if __name__ == '__main__':
    verify(sys.argv[1])
