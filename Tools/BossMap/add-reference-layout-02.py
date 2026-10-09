"""Append the second reference layout without modifying existing levels.
Run once with Python + Pillow. Refuses to overwrite a previously added level.
"""
import copy
import json
import uuid
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'Assets/98. Tools/00. LDTK'
FILE = BASE / 'ReferenceLayout.ldtk'
NAME = 'ReferenceLayout_02'
W, H, G = 256, 176, 8
project = json.loads(FILE.read_text(encoding='utf-8-sig'))
assert not project['externalLevels'], 'Expected embedded levels'
assert not any(l['identifier'] == NAME for l in project['levels']), 'Level already exists; edit it in LDtk instead'
original_levels = copy.deepcopy(project['levels'])
uid = project['nextUid']
identity = lambda name: str(uuid.uuid5(uuid.NAMESPACE_URL, 'desert-chess/reference-layout-02/' + name))
area = [0] * (W*H)
ground = [0] * (W*H)

def at(grid,x,y):
    return grid[y*W+x] if 0 <= x < W and 0 <= y < H else 0

def paint(grid,x,y,w,h):
    for cy in range(y,y+h):
        for cx in range(x,x+w):
            assert 0 <= cx < W and 0 <= cy < H
            grid[cy*W+cx] = 1

# Traced outer silhouette: left U-turn, upper inverted U, right descending shaft.
# The tiny separate room at the upper left is also retained from the reference.
rooms = [(0,0,19,19), (0,35,55,18), (37,43,18,43),
         (37,69,54,17), (73,35,18,51), (73,35,54,18),
         (110,1,18,52), (110,1,54,18), (146,1,18,69),
         (146,52,54,18), (182,52,18,86), (182,120,36,18),
         (200,120,18,51), (200,154,56,18)]
for room in rooms:
    paint(area,*room)
for y in range(H):
    for x in range(W):
        if at(area,x,y) and any(not at(area,x+dx,y+dy) for dx,dy in
                               ((-1,0),(1,0),(0,-1),(0,1))):
            ground[y*W+x] = 1

# Fixed ledges keep the vertical sections usable without ladder tiles/entities.
ledges = [(8,47,12,1),(25,48,11,1), (38,61,6,1),(47,67,7,1),
          (40,78,9,1),(56,80,9,1),
          (74,74,6,1),(83,68,7,1),(74,62,6,1),(83,56,7,1),
          (74,50,6,1),(91,46,12,1),
          (111,44,6,1),(120,38,7,1),(111,32,6,1),(120,26,7,1),
          (111,20,6,1),(120,14,7,1), (130,13,10,1),
          (147,23,6,1),(157,30,6,1),(147,37,6,1),(157,44,6,1),
          (148,62,10,1),(166,62,10,1),
          (183,72,6,1),(193,78,6,1),(183,84,6,1),(193,90,6,1),
          (183,96,6,1),(193,102,6,1),(183,108,6,1),(193,114,6,1),
          (183,120,6,1),(188,130,10,1),
          (201,137,6,1),(211,143,6,1),(201,149,6,1),(211,155,6,1),
          (204,166,9,1),(224,166,10,1),(241,164,8,1)]
for rect in ledges:
    paint(ground,*rect)
assert all(not v or area[i] for i,v in enumerate(ground)), 'Ledge outside room'

defs = {d['identifier']:d for d in project['defs']['layers']}
ts = next(t for t in project['defs']['tilesets'] if t['uid']==defs['Ground']['tilesetDefUid'])
assert ts['tileGridSize']==G
def tile(x,y,t,flip=0,rule=None):
    return {'px':[x*G,y*G],'src':[(t%ts['__cWid'])*G,(t//ts['__cWid'])*G],
            'f':flip,'t':t,'d':[y*W+x] if rule is None else [rule,y*W+x],'a':1}

def random01(x,y,seed):
    return (((x*73856093) ^ (y*19349663) ^ seed) & 0xffff)/65536

auto=[]
for group in defs['Ground']['autoRuleGroups']:
    if not group['active']:
        continue
    for y in range(H):
        for x in range(W):
            if not at(area,x,y):
                continue
            for rule in group['rules']:
                if not rule['active'] or random01(x,y,rule['uid'])>=rule['chance']:
                    continue
                matched=False
                for flip in ([0,1] if rule['flipX'] else [0]):
                    radius=rule['size']//2
                    ok=True
                    for i,expected in enumerate(rule['pattern']):
                        if not expected:
                            continue
                        dx=i%rule['size']-radius
                        dy=i//rule['size']-radius
                        actual=at(ground,x+(-dx if flip else dx),y+dy)
                        if (expected>0 and actual!=expected) or (expected<0 and actual==-expected):
                            ok=False
                            break
                    if ok:
                        choices=rule['tileRectsIds']
                        t=choices[int(random01(x,y,rule['uid']+17)*len(choices))][0]
                        auto.append(tile(x,y,t,flip,rule['uid']))
                        matched=True
                        break
                if matched and rule['breakOnMatch']:
                    break

level=copy.deepcopy(project['levels'][0])
level.update(identifier=NAME,iid=identity('level'),uid=uid,
             worldX=max(l['worldX']+l['pxWid'] for l in project['levels'])+128,worldY=0,
             pxWid=W*G,pxHei=H*G,__bgColor='#000000',bgColor='#000000',
             useAutoIdentifier=False,bgRelPath=None,bgPos=None,__bgPos=None,
             externalRelPath=None,__neighbours=[],fieldInstances=[])
for l in level['layerInstances']:
    l.update(__cWid=W,__cHei=H,__gridSize=G,iid=identity(l['__identifier']),levelId=uid,
             intGridCsv=ground if l['__identifier']=='Ground' else [],
             autoLayerTiles=auto if l['__identifier']=='Ground' else [],
             gridTiles=[],entityInstances=[])
    if l['__identifier']=='BackGround':
        l['gridTiles']=[tile(x,y,0) for y in range(H) for x in range(W) if at(area,x,y)]

# Verify room connectivity separately from character movement simulation.
start=43*W+5
seen={start}
queue=[start]
for i in queue:
    x,y=i%W,i//W
    for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
        j=ny*W+nx
        if at(area,nx,ny) and not at(ground,nx,ny) and j not in seen:
            seen.add(j)
            queue.append(j)
assert 160*W+250 in seen, 'Main route disconnected'
assert all(0<=t['t']<ts['__cWid']*ts['__cHei'] for t in auto)

project['levels'].append(level)
project['nextUid']=uid+1
assert project['levels'][:-1]==original_levels
FILE.write_text(json.dumps(project,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

atlas=Image.open(BASE/ts['relPath']).convert('RGBA')
preview=Image.new('RGBA',(W*G,H*G),(0,0,0,255))
for l in reversed(level['layerInstances']):
    for t in l['gridTiles']+l['autoLayerTiles']:
        sx,sy=t['src']
        image=atlas.crop((sx,sy,sx+G,sy+G))
        if t['f']&1:
            image=image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        preview.alpha_composite(image,tuple(t['px']))
preview.convert('RGB').save(BASE/'ReferenceLayout/ReferenceLayout_02.preview.png')
print(f'Added {NAME}, uid={uid}, {W*G}x{H*G}px at ({level["worldX"]},0).')
print(f'Existing levels unchanged. {sum(ground)} solid cells, {len(auto)} auto tiles. Main route connected; no ladders.')
