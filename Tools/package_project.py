"""Archive an actually editor-built project; refuse to call source-only files ready for submission."""
from pathlib import Path
import zipfile

ROOT=Path(__file__).resolve().parents[1]
required=['Assets/Generated/SetupComplete.txt','Assets/Scenes/Variant_Андижан.unity','Assets/Scenes/Import_Audio_Андижан.unity']
for name in required:
    if not (ROOT/name).is_file():raise SystemExit('Сначала соберите и проверьте сцены в Unity. Отсутствует: '+name)
excluded={'.git','Library','Temp','Logs','Obj','Builds','UserSettings','.vs','.idea','Artifacts','__pycache__'}
target=ROOT/'Artifacts/VRpractice_Андижан.zip';target.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as archive:
    for p in sorted(ROOT.rglob('*')):
        if p.is_file() and not any(part in excluded for part in p.relative_to(ROOT).parts) and p.suffix not in {'.zip','.pyc','.csproj','.sln'}:
            archive.write(p,p.relative_to(ROOT))
print(target)
