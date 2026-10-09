"""Build the supplied 256x71 layout with Study_01's actual 8px tiles.
Re-running overwrites only ReferenceLayout.ldtkl and its preview.
"""
import copy
import json
import re
from pathlib import Path
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'Assets/98. Tools/00. LDTK'
PROJECT = BASE / 'Study_01.ldtk'
OUTPUT = BASE / 'Study_01/ReferenceLayout.ldtkl'
W, H, G = 256, 72, 8
NAME = 'ReferenceLayout'
def identity(s):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, 'desert-chess/reference-layout/' + s))

project_text = PROJECT.read_text(encoding='utf-8-sig')
project = json.loads(project_text)
template = json.loads((BASE / 'Study_01/Level_0.ldtkl').read_text(encoding='utf-8-sig'))
old = next((l for l in project['levels'] if l['identifier'] == NAME), None)
uid = old['uid'] if old else project['nextUid']
area = [0] * (W * H)
ground = [0] * (W * H)

def paint(grid, x, y, w, h, value=1):
    for cy in range(y, y+h):
        for cx in range(x, x+w):
            assert 0 <= cx < W and 0 <= cy < H
            grid[cy*W+cx] = value

# One thumbnail pixel is one 8px tile. Include the small detached top-left room.
rooms = [(0,0,14,14), (0,59,68,12), (54,38,14,33),
         (65,35,70,13), (121,44,14,27), (121,59,54,12),
         (161,24,14,47), (161,24,28,12), (175,0,14,36),
         (175,0,81,13)]
for room in rooms:
    paint(area, *room)

def at(grid, x, y, outside=0):
    return grid[y*W+x] if 0 <= x < W and 0 <= y < H else outside

# Continuous perimeter from the union of rooms, not closed boxes at room joins.
for y in range(H):
    for x in range(W):
        if at(area,x,y) and any(not at(area,x+dx,y+dy) for dx,dy in
                               ((-1,0),(1,0),(0,-1),(0,1))):
            ground[y*W+x] = 1

# Ledges and stepped floors visible in the reference. No ladder art or triggers.
ledges = [(14,66,13,1),(27,63,12,1),(40,66,9,1),
          (55,60,5,1),(62,55,5,1),(55,50,5,1),(62,45,5,1),
          (122,54,5,1),(129,59,5,1),(122,64,5,1),
          (138,65,8,1),(153,67,8,1),
          (162,65,5,1),(169,60,5,1),(162,55,5,1),(169,50,5,1),
          (162,45,5,1),(169,40,5,1),(162,35,5,1),(169,30,5,1),
          (176,25,5,1),(183,20,5,1),(176,15,5,1),(183,10,5,1),
          (177,6,15,1),(193,8,9,1),(210,10,15,1),(227,6,14,1),
          (241,1,1,3),(241,9,1,3)]
for rect in ledges:
    paint(ground,*rect)
assert all(not v or area[i] for i,v in enumerate(ground))

layer_defs = {d['identifier']:d for d in project['defs']['layers']}
ts = next(t for t in project['defs']['tilesets'] if t['uid']==layer_defs['Ground']['tilesetDefUid'])
assert ts['tileGridSize'] == G
def tile(x,y,t,flip=0,rule=None):
    return {'px':[x*G,y*G],'src':[(t%ts['__cWid'])*G,(t//ts['__cWid'])*G],
            'f':flip,'t':t,'d':[y*W+x] if rule is None else [rule,y*W+x],'a':1}

def random01(x,y,seed):
    return (((x*73856093) ^ (y*19349663) ^ seed) & 0xffff) / 65536

auto=[]
# Bake the existing LDtk rules, including their mirror variants and decoration.
# Rules themselves remain unchanged, so LDtk can recompute them on future edits.
for group in layer_defs['Ground']['autoRuleGroups']:
    if not group['active']:
        continue
    group_tiles=[]
    for y in range(H):
        for x in range(W):
            if not at(area,x,y):
                continue
            for rule in group['rules']:
                if not rule['active'] or random01(x,y,rule['uid']) >= rule['chance']:
                    continue
                matched=False
                for flip in ([0,1] if rule['flipX'] else [0]):
                    radius=rule['size']//2
                    ok=True
                    for i,expected in enumerate(rule['pattern']):
                        if expected==0:
                            continue
                        dx=i%rule['size']-radius
                        dy=i//rule['size']-radius
                        actual=at(ground,x+(-dx if flip else dx),y+dy)
                        if (expected>0 and actual!=expected) or (expected<0 and actual==-expected):
                            ok=False
                            break
                    if ok:
                        choices=rule['tileRectsIds']
                        selection=int(random01(x,y,rule['uid']+17)*len(choices))
                        group_tiles.append(tile(x,y,choices[selection][0],flip,rule['uid']))
                        matched=True
                        break
                if matched and rule['breakOnMatch']:
                    break
    auto.extend(group_tiles)

instances=[]
for source in template['layerInstances']:
    l=copy.deepcopy(source)
    l.update(__cWid=W,__cHei=H,iid=identity(l['__identifier']),levelId=uid,
             intGridCsv=ground if l['__identifier']=='Ground' else [],
             autoLayerTiles=auto if l['__identifier']=='Ground' else [],
             gridTiles=[],entityInstances=[])
    if l['__identifier']=='BackGround':
        # Tile 0 is the same dark rock backing already used by Level_0.
        l['gridTiles']=[tile(x,y,0) for y in range(H) for x in range(W) if at(area,x,y)]
    instances.append(l)
level=copy.deepcopy(template)
level.update(identifier=NAME,iid=identity('level'),uid=uid,worldX=0,worldY=512,
             pxWid=W*G,pxHei=H*G,__bgColor='#000000',bgColor='#000000',
             bgRelPath=None,bgPos=None,__bgPos=None,useAutoIdentifier=False,
             externalRelPath=None,layerInstances=instances,__neighbours=[])
OUTPUT.write_text(json.dumps(level,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
summary={k:v for k,v in level.items() if k!='__header__'}
summary.update(externalRelPath='Study_01/ReferenceLayout.ldtkl',layerInstances=None)
if old:
    project['levels'][project['levels'].index(old)]=summary
else:
    project['levels'].append(summary)
    project['nextUid']=uid+1
# Preserve the existing tileset/rule formatting and all unrelated project settings.
project_text=re.sub(r'("nextUid"\s*:\s*)\d+',lambda m:m[1]+str(project['nextUid']),project_text,count=1)
levels_text=json.dumps(project['levels'],ensure_ascii=False,indent='\t')
project_text=re.sub(r'("levels"\s*:\s*)\[[\s\S]*?\](?=\s*,\s*"worlds")',
                    lambda m:m[1]+levels_text,project_text,count=1)
PROJECT.write_text(project_text,encoding='utf-8')
meta=OUTPUT.with_suffix('.ldtkl.meta')
if not meta.exists():
    text=(BASE/'Study_01/Level_0.ldtkl.meta').read_text()
    text=text.replace('b8b25b8def14cbb4ab45243dd49c22f8',identity('asset').replace('-',''))
    meta.write_text(text,encoding='utf-8')

# Render actual tile instances for visual inspection; no reference image backdrop.
atlas=Image.open(BASE/ts['relPath']).convert('RGBA')
preview=Image.new('RGBA',(W*G,H*G),(0,0,0,255))
for l in reversed(instances):
    for t in l['gridTiles']+l['autoLayerTiles']:
        sx,sy=t['src']
        image=atlas.crop((sx,sy,sx+G,sy+G))
        if t['f']&1:
            image=image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        preview.alpha_composite(image,tuple(t['px']))
preview.convert('RGB').save(BASE/'Study_01/ReferenceLayout.preview.png')

# Check the main route is connected geometrically (the small room is detached).
start=64*W+5
seen={start}
queue=[start]
for i in queue:
    x,y=i%W,i//W
    for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
        j=ny*W+nx
        if at(area,nx,ny) and not at(ground,nx,ny) and j not in seen:
            seen.add(j)
            queue.append(j)
assert 6*W+248 in seen, 'Main route is disconnected'
assert all(0<=t['t']<ts['__cWid']*ts['__cHei'] for t in auto)
assert all(l['__identifier'] in ('Ground','BackGround') for l in instances)
print(f'{NAME}: {W*G}x{H*G}px; {sum(ground)} collision cells; {len(auto)} auto tiles; no ladders; main route connected.')
