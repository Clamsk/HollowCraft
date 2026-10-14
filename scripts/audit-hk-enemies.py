"""Offline inventory of enemy health/colliders in the user's original HK data.

Requires UnityPy and TypeTreeGenerator, installed separately. Does not launch the
game or write to its installation. Raw reports belong in ignored artifacts/.
"""
import argparse
import csv
import gc
import json
import re
import sys
from collections import Counter
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--data', required=True, type=Path)
parser.add_argument('--libs', type=Path, default=Path('dependencies/unity-inspect'))
parser.add_argument('--out', type=Path, default=Path('artifacts/enemy-audit.json'))
parser.add_argument('--file', help='Optional single serialized file for investigation')
parser.add_argument('--coverage-csv', type=Path, help='Optional compact actor inventory; excludes raw game assets')
args = parser.parse_args()
sys.path.insert(0, str(args.libs.resolve()))
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

root = args.data.resolve()
generator = TypeTreeGenerator('6000.0.61f1')
generator.load_local_dll_folder(str(root / 'Managed'))
settings_env = UnityPy.load(str(root / 'globalgamemanagers'))
settings = next(o.read_typetree() for o in settings_env.objects if o.type.name == 'BuildSettings')
scenes = settings.get('scenes', settings.get('m_Scenes', []))
files = sorted(p for p in root.iterdir() if re.fullmatch(r'level\d+|sharedassets\d+\.assets|resources\.assets', p.name))
if args.file:
    files = [p for p in files if p.name == args.file]
rows, errors = [], []
script_names = {}
for index, path in enumerate(files):
    env = UnityPy.load(str(path))
    env.typetree_generator = generator
    local = next((f for f in env.files.values() if Path(f.name).name == path.name), None)
    if local is None:
        errors.append({'file': path.name, 'error': 'serialized file not loaded'})
        continue
    objects = local.objects.values()
    health = []
    for obj in objects:
        if obj.type.name != 'MonoBehaviour':
            continue
        try:
            head = obj.parse_monobehaviour_head()
            key = (head.m_Script.m_FileID, head.m_Script.m_PathID)
            if key not in script_names:
                script_names[key] = head.m_Script.read().m_ClassName
            if script_names[key] in ('HealthManager', 'ScuttlerControl'):
                health.append((obj, head, script_names[key]))
        except Exception as exc:
            errors.append({'file': path.name, 'id': obj.path_id, 'error': str(exc)[:180]})
    # Script PPtrs are relative to the file, not global across different scenes.
    script_names.clear()
    for obj, head, script in health:
        try:
            tree = obj.read_typetree()
            go = head.m_GameObject.read()
            components, colliders = [], []
            for component in go.m_Component:
                component = component.component if hasattr(component, 'component') else component[1]
                reader = component.deref()
                klass = reader.parse_monobehaviour_head().m_Script.read().m_ClassName if reader.type.name == 'MonoBehaviour' else reader.type.name
                components.append(klass)
                if klass.endswith('Collider2D'):
                    colliders.append({'type': klass, 'tree': component.read_typetree()})
            file_scene = int(path.name[5:]) if re.fullmatch(r'level\d+', path.name) else -1
            rows.append({'file': path.name, 'scene': str(scenes[file_scene]) if 0 <= file_scene < len(scenes) else '',
                         'id': obj.path_id, 'name': go.m_Name, 'responder': script, 'active': go.m_IsActive,
                         'tag': go.m_Tag, 'layer': go.m_Layer,
                         'health': {k: v for k, v in tree.items() if isinstance(v, (str, int, float, bool))},
                         'components': components, 'colliders': colliders})
        except Exception as exc:
            if args.file:
                import traceback
                traceback.print_exc(limit=5)
            errors.append({'file': path.name, 'id': obj.path_id, 'healthManager': True, 'error': str(exc)[:180]})
    if index % 40 == 0 or index == len(files)-1:
        print(f'{index+1}/{len(files)} files, {len(rows)} health objects, {len(errors)} errors', flush=True)
    del env
    gc.collect()

summary = {'files': len(files), 'healthObjects': sum(r['responder']=='HealthManager' for r in rows),
           'oneHitObjects': sum(r['responder']=='ScuttlerControl' for r in rows), 'names': len(set(r['name'] for r in rows)),
           'components': dict(Counter(c for r in rows for c in r['components'])),
           'invincibleDirections': dict(Counter(str(r['health'].get('invincibleFromDirection')) for r in rows)),
           'errors': len(errors)}
args.out.parent.mkdir(parents=True, exist_ok=True)
args.out.write_text(json.dumps({'summary': summary, 'enemies': rows, 'errors': errors}, ensure_ascii=False, indent=2), encoding='utf-8')
if args.coverage_csv:
    groups = {}
    for row in rows:
        groups.setdefault((row['name'], row['responder']), []).append(row)
    args.coverage_csv.parent.mkdir(parents=True, exist_ok=True)
    with args.coverage_csv.open('w', encoding='utf-8', newline='') as output:
        writer = csv.writer(output)
        writer.writerow(['actor', 'responder', 'instances', 'scenes', 'native_hp_min', 'native_hp_max',
                         'invincible_cases', 'guard_directions', 'special_death_cases', 'without_own_collider', 'route'])
        for (name, responder), group in sorted(groups.items()):
            hp = [r['health'].get('hp', 1) for r in group]
            writer.writerow([name, responder, len(group), len(set(r['scene'] for r in group)), min(hp), max(hp),
                             sum(bool(r['health'].get('invincible')) for r in group),
                             '|'.join(map(str, sorted(set(r['health'].get('invincibleFromDirection', 0) for r in group)))),
                             sum(bool(r['health'].get('hasSpecialDeath')) for r in group),
                             sum(not r['colliders'] for r in group),
                             'vanilla MC damage + original HK guard/phase/death' if responder=='HealthManager'
                             else 'original one-hit responder (melee and arrows)'])
print(json.dumps(summary, ensure_ascii=False), flush=True)
if errors:
    sys.exit(1)
