// Run from the repository root: node Tools/BossMap/build-boss-map.cjs
// --init creates the LDtk source once. Normal runs export the edited LDtk file.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve(__dirname, '../..');
const dir = path.join(root, 'Assets/98. Tools/00. LDTK/OriginalBoss');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const write = (p, data) => fs.writeFileSync(path.join(dir, p), data);
const guid = name => crypto.createHash('md5').update('DesertChess.OriginalBoss.' + name).digest('hex');
const iid = name => { const s = guid(name); return `${s.slice(0,8)}-${s.slice(8,12)}-${s.slice(12,16)}-${s.slice(16,20)}-${s.slice(20)}`; };
const clone = x => JSON.parse(JSON.stringify(x));
const W = 1728, H = 920, G = 8, PPU = 32, CW = W/G, CH = H/G;
fs.mkdirSync(dir, {recursive:true});
if (process.argv.includes('--init')) {
  if (fs.existsSync(path.join(dir, 'OriginalBoss.ldtk'))) throw Error('LDtk source already exists; refusing to overwrite edits.');
  const src = process.argv[process.argv.indexOf('--init') + 1];
  if (!src || !fs.existsSync(src)) throw Error('Supply original_boss.png after --init.');
  fs.copyFileSync(src, path.join(dir, 'original_boss.png'));
  const p = JSON.parse(read('Assets/98. Tools/00. LDTK/Study_01.ldtk'));
  const layerTemplate = clone(p.defs.layers[0]);
  const level = clone(p.levels[0]);
  const layers = [];
  function layer(name, uid, colors) {
    const def = {...clone(layerTemplate), identifier:name, uid, gridSize:G, tilesetDefUid:null,
      autoRuleGroups:[], autoSourceLayerDefUid:null, displayOpacity:0.45,
      intGridValues:colors.map(([identifier,color],i)=>({value:i+1,identifier,color,tile:null,groupUid:0}))};
    layers.push(def);
    return {__identifier:name,__type:'IntGrid',__cWid:CW,__cHei:CH,__gridSize:G,__opacity:0.45,
      __pxTotalOffsetX:0,__pxTotalOffsetY:0,__tilesetDefUid:null,__tilesetRelPath:null,
      iid:iid(name),levelId:0,layerDefUid:uid,pxOffsetX:0,pxOffsetY:0,visible:true,
      optionalRules:[],intGridCsv:Array(CW*CH).fill(0),autoLayerTiles:[],gridTiles:[],entityInstances:[],seed:0};
  }
  const markers = layer('Markers',5,[['Spawn','#71FF90'],['Exit','#FFCE66'],['Checkpoint','#FFFF66']]);
  const ladders = layer('Climb',4,[['Ladder','#F7B955'],['Chain','#D589FC']]);
  const water = layer('Water',3,[['Waterfall','#37C9FF']]);
  const platforms = layer('Platforms',2,[['SolidPlatform','#FFCF80']]);
  const ground = layer('Ground',1,[['Solid','#FF647C']]);
  const paint = (l,r,value=1) => {
    const [x,y,w,h]=r;
    for(let cy=Math.floor(y/G);cy<Math.ceil((y+h)/G);cy++)
      for(let cx=Math.floor(x/G);cx<Math.ceil((x+w)/G);cx++)
        if(cx>=0&&cy>=0&&cx<CW&&cy<CH) l.intGridCsv[cy*CW+cx]=value;
  };
  ground.intGridCsv.fill(1);
  // Open interior, traced in source-image pixel coordinates. The black void is solid.
  const rooms = [[32,32,120,408],[152,336,1112,104],[1168,120,328,96],
    [1168,216,96,224],[1408,216,88,400],[1272,552,424,64],
    [1272,616,88,112],[936,648,424,80],[704,440,112,400],
    [704,744,312,96],[936,728,80,16]];
  rooms.forEach(r=>paint(ground,r,0));
  // Narrow ledges leave climbing gaps at the edge of each shaft.
  [[32,80,104,8],[32,200,104,8],[56,304,80,8],[32,384,32,8],
    [160,416,168,8],[352,392,72,8],[448,400,80,8],[640,416,128,8],
    [816,424,136,8],[912,384,64,8],[992,416,152,8],
    [1200,400,64,8],[1168,312,48,8],[1216,224,48,8],
    [1264,216,144,16],[1296,160,72,8],[1440,312,56,8],
    [1408,344,64,8],[1440,456,56,8],[1440,520,56,8],
    [1296,600,56,8],[1520,576,72,8],
    [728,504,64,8],[728,616,64,8],[704,736,112,8],
    [736,824,64,8]].forEach(r=>paint(platforms,r));
  [[136,64,16,376],[432,336,16,104],[952,384,16,56],
    [1200,136,16,264],[1480,232,16,80],[1344,600,16,128],
    [1592,552,16,64],[1008,728,8,112]].forEach(r=>paint(ladders,r));
  [[704,440,16,296],[800,440,16,304],[1408,216,16,400]].forEach(r=>paint(ladders,r,2));
  // Open one tile on the shaft landing beside the climb route.
  paint(platforms,[800,736,16,8],0);
  [[72,392,16,48],[200,416,16,24],[240,416,16,24],[384,400,8,40],
    [752,440,16,56],[1256,224,8,176],[1408,224,16,112],
    [1472,344,8,104],[1408,456,8,160]].forEach(r=>paint(water,r));
  paint(markers,[80,176,8,8],1); paint(markers,[1648,592,8,8],2);
  paint(markers,[840,816,8,8],3); paint(markers,[1320,192,8,8],3);
  Object.assign(level,{identifier:'OriginalBoss',iid:iid('level'),worldX:0,worldY:0,
    pxWid:W,pxHei:H,__bgColor:'#000000',bgColor:'#000000',useAutoIdentifier:false,
    bgRelPath:'original_boss.png',bgPos:'Unscaled',bgPivotX:0,bgPivotY:0,
    __smartColor:'#297DCE',__bgPos:{cropRect:[0,0,1721,914],scale:[1,1],topLeftPx:[0,0]},
    externalRelPath:null,layerInstances:[markers,ladders,water,platforms,ground]});
  Object.assign(p,{iid:iid('project'),nextUid:10,defaultGridSize:G,defaultLevelWidth:W,
    defaultLevelHeight:H,externalLevels:false,customCommands:[],imageExportMode:'None',
    worldLayout:'Free',bgColor:'#000000',defaultLevelBgColor:'#000000',
    defs:{...p.defs,layers,entities:[],tilesets:[],levelFields:[]},levels:[level],
    dummyWorldIid:iid('world')});
  write('OriginalBoss.ldtk',JSON.stringify(p,null,2)+'\n');
  let meta = read('Assets/98. Tools/00. LDTK/Study_01.ldtk.meta')
    .replaceAll('7c2eef304d58dc843bbce2069105c216',guid('ldtk'))
    .replace('_pixelsPerUnit: 16','_pixelsPerUnit: 32');
  meta = meta.slice(0,meta.indexOf('  _intGridValues:')) +
    `  _intGridValues:\n  - _key: Ground_1\n    _asset: {fileID: 11400000, guid: 4e2b244f0f270504c89dee018bf88677, type: 2}\n  - _key: Platforms_1\n    _asset: {fileID: 11400000, guid: 4e2b244f0f270504c89dee018bf88677, type: 2}\n  _entities: []\n  _enumGenerate: 0\n  _useLayerCustomSortingOrders: 0\n  _layerCustomSortingOrders: []\n`;
  write('OriginalBoss.ldtk.meta',meta);
  let tex = read('Assets/00. Study/14. Action Platformer/02. Resources/BackGround.png.meta');
  tex = tex.replace(/guid: [a-f0-9]+/,`guid: ${guid('image')}`)
    .replace(/  internalIDToNameTable:[\s\S]*?  externalObjects:/,'  internalIDToNameTable: []\n  externalObjects:')
    .replace('filterMode: 1','filterMode: 0').replace('spriteMode: 2','spriteMode: 1')
    .replace('spritePixelsToUnits: 100','spritePixelsToUnits: 32').replaceAll('textureCompression: 1','textureCompression: 0')
    .replace(/  spriteSheet:[\s\S]*?  mipmapLimitGroupName:/,'  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n    physicsShape: []\n    bones: []\n    spriteID: '+guid('sprite')+'\n    internalID: 0\n    secondaryTextures: []\n    nameFileIdTable: {}\n  mipmapLimitGroupName:');
  write('original_boss.png.meta',tex);
}

const project = JSON.parse(fs.readFileSync(path.join(dir,'OriginalBoss.ldtk'),'utf8'));
const level = project.levels[0];
if(level.pxWid!==W||level.pxHei!==H) throw Error('Update exporter dimensions if resizing the level.');
const layers = level.layerInstances;
for(const l of layers) {
  if(l.__gridSize!==G||l.intGridCsv.length!==CW*CH) throw Error('Invalid grid: '+l.__identifier);
  const def=project.defs.layers.find(d=>d.uid===l.layerDefUid);
  if(!def||l.intGridCsv.some(v=>v!==0&&!def.intGridValues.some(d=>d.value===v))) throw Error('Undefined cell value');
}
// Merge equal cells to rectangles, keeping geometry compact and exactly reproducible.
function rectangles(l) {
  const a=l.intGridCsv.slice(), result=[];
  for(let y=0;y<CH;y++) for(let x=0;x<CW;x++) {
    const v=a[y*CW+x]; if(!v) continue;
    let w=1,h=1;
    while(x+w<CW&&a[y*CW+x+w]===v) w++;
    outer: while(y+h<CH) {for(let k=0;k<w;k++) if(a[(y+h)*CW+x+k]!==v) break outer; h++;}
    for(let j=0;j<h;j++) for(let k=0;k<w;k++) a[(y+j)*CW+x+k]=0;
    result.push({x:x*G,y:y*G,w:w*G,h:h*G,v});
  }
  return result;
}
const rects=Object.fromEntries(layers.map(l=>[l.__identifier,rectangles(l)]));
const game=read('Assets/Scenes/Game.unity');
const sample=read('Assets/Scenes/SampleScene.unity');
function component(text,type,id,go) {
  const re=new RegExp('--- !u!'+type+' &[^\\n]+\\r?\\n[\\s\\S]*?(?=\\r?\\n--- !u!|$)');
  const match=text.match(re); if(!match) throw Error('Missing component template '+type);
  return match[0].replace(/&[^\n]+/,`&${id}`).replace(/m_GameObject: \{fileID: \d+\}/,`m_GameObject: {fileID: ${go}}`);
}
let blocks=[],next=1000;
const objects=[];
function obj(name,parent=0,pos=[0,0,0],layer=0,types=[]) {
  const go=next++,tr=next++,ids=types.map(()=>next++);
  const o={go,tr,ids,name,parent,pos,layer,children:[]};objects.push(o);
  if(parent) objects.find(o=>o.tr===parent).children.push(tr);
  return o;
}
const base=obj('OriginalBoss');
const art=obj('ReferenceArtwork',base.tr,[1721/PPU/2,-914/PPU/2,0],0,[212]);
let sr=component(game,212,art.ids[0],art.go)
  .replace(/m_Sprite: \{[^}]+\}/,`m_Sprite: {fileID: 21300000, guid: ${guid('image')}, type: 3}`)
  .replace(/m_Materials:\s*\n\s*- \{[^}]+\}/,'m_Materials:\n  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}')
  .replace(/m_Color: \{[^}]+\}/,'m_Color: {r: 1, g: 1, b: 1, a: 1}')
  .replace(/m_SortingOrder: -?\d+/,'m_SortingOrder: -100')
  .replace(/m_DrawMode: \d+/,'m_DrawMode: 0').replace(/m_FlipX: \d+/,'m_FlipX: 0').replace(/m_FlipY: \d+/,'m_FlipY: 0');
blocks.push(sr);
for(const l of layers) {
  const name=l.__identifier,group=obj(name,base.tr);
  const def=project.defs.layers.find(d=>d.uid===l.layerDefUid);
  rects[name].forEach((r,i)=>{
    const label=def.intGridValues.find(d=>d.value===r.v).identifier;
    const solid=['Ground','Platforms'].includes(name);
    const marker=name==='Markers';
    const o=obj(`${label}_${i.toString().padStart(3,'0')}`,group.tr,[(r.x+r.w/2)/PPU,-(r.y+r.h/2)/PPU,0],solid?3:0,marker?[]:[61]);
    if(marker) return;
    let c=component(game,61,o.ids[0],o.go)
      .replace(/m_IsTrigger: \d+/,'m_IsTrigger: '+(solid?0:1))
      .replace(/m_Offset: \{[^}]+\}/,'m_Offset: {x: 0, y: 0}')
      .replace(/m_Size: \{[^}]+\}/,`m_Size: {x: ${r.w/PPU}, y: ${r.h/PPU}}`)
      .replace(/m_EdgeRadius: [^\r\n]+/,'m_EdgeRadius: 0');
    blocks.push(c);
  });
}
function serializeObjects() {
  return objects.map(o=>`--- !u!1 &${o.go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: ${o.tr}}\n${o.ids.map(id=>`  - component: {fileID: ${id}}\n`).join('')}  m_Layer: ${o.layer}\n  m_Name: ${o.name}\n  m_TagString: ${o.name==='Main Camera'?'MainCamera':'Untagged'}\n  m_IsActive: 1\n--- !u!4 &${o.tr}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${o.go}}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: ${o.pos[0]}, y: ${o.pos[1]}, z: ${o.pos[2]}}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:${o.children.length?'\n'+o.children.map(id=>`  - {fileID: ${id}}`).join('\n'):' []'}\n  m_Father: {fileID: ${o.parent}}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}`).join('\n');
}
const header='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';
write('OriginalBoss.prefab',header+serializeObjects()+'\n'+blocks.join('\n')+'\n');
const cam=obj('Main Camera',0,[W/PPU/2,-H/PPU/2,-10],0,[20]);
blocks.push(component(sample,20,cam.ids[0],cam.go)
  .replace(/m_ClearFlags: \d+/,'m_ClearFlags: 2')
  .replace(/m_BackGroundColor: \{[^}]+\}/,'m_BackGroundColor: {r: 0, g: 0, b: 0, a: 1}')
  .replace(/orthographic: \d+/,'orthographic: 1')
  .replace(/orthographic size: [^\r\n]+/,'orthographic size: 16.5'));
write('OriginalBoss.unity',header+serializeObjects()+'\n'+blocks.join('\n')+
  `\n--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {fileID: ${base.tr}}\n  - {fileID: ${cam.tr}}\n`);
for(const [file,importer] of [['OriginalBoss.prefab','PrefabImporter'],['OriginalBoss.unity','DefaultImporter']]) {
  if(!fs.existsSync(path.join(dir,file+'.meta'))) write(file+'.meta',`fileFormatVersion: 2\nguid: ${guid(file)}\n${importer}:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}
// This overlay is a diagnostic document; the supplied image is copied without modification.
const colors={Ground:'#ff647c',Platforms:'#ffcf80',Climb:'#efb356',Water:'#37c9ff',Markers:'#71ff90'};
write('OriginalBoss.layout.html',`<!doctype html><meta charset="utf-8"><title>Original Boss · collision layout</title><style>body{margin:24px;background:#10151e;color:#dce8ff;font:16px system-ui}svg{width:100%;height:auto}label{margin-right:20px}h1{font-size:22px}</style><h1>Original Boss · LDtk collision layout</h1><p>Toggle the editable layers. Artwork is the unchanged reference image.</p>${layers.map(l=>`<label><input type="checkbox" checked onchange="document.getElementById('${l.__identifier}').style.display=this.checked?'':'none'">${l.__identifier}</label>`).join('')}<svg viewBox="0 0 ${W} ${H}"><image href="original_boss.png" width="1721" height="914"/>${layers.slice().reverse().map(l=>`<g id="${l.__identifier}" fill="${colors[l.__identifier]}" fill-opacity="0.2" stroke="${colors[l.__identifier]}" stroke-width="1">${rects[l.__identifier].map(r=>`<rect x="${r.x}" y="${r.y}" width="${r.w}" height="${r.h}"/>`).join('')}</g>`).join('')}</svg>`);
// Validate merged rectangle coverage and all marker locations against collision.
for(const l of layers) {
  const restored=Array(CW*CH).fill(0);
  for(const r of rects[l.__identifier]) for(let y=r.y/G;y<(r.y+r.h)/G;y++) for(let x=r.x/G;x<(r.x+r.w)/G;x++) restored[y*CW+x]=r.v;
  if(restored.some((v,i)=>v!==l.intGridCsv[i])) throw Error('Rectangle export mismatch');
}
const solid=layers.filter(l=>['Ground','Platforms'].includes(l.__identifier));
for(const r of rects.Markers) {
  const i=(r.y/G)*CW+r.x/G;
  if(solid.some(l=>l.intGridCsv[i])) throw Error('Marker inside solid');
}
// Geometric connectivity only: this does not simulate a character's jump or size.
const spawn=rects.Markers.find(r=>r.v===1);
if(!spawn) throw Error('Missing spawn');
const start=(spawn.y/G)*CW+spawn.x/G,seen=new Set([start]),queue=[start];
for(let q=0;q<queue.length;q++) {
  const i=queue[q],x=i%CW,y=Math.floor(i/CW);
  for(const [nx,ny] of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]]) {
    const j=ny*CW+nx;
    if(nx<0||ny<0||nx>=CW||ny>=CH||seen.has(j)||solid.some(l=>l.intGridCsv[j])) continue;
    seen.add(j); queue.push(j);
  }
}
for(const r of rects.Markers) if(!seen.has((r.y/G)*CW+r.x/G)) throw Error('Disconnected marker');
for(const file of ['OriginalBoss.prefab','OriginalBoss.unity']) {
  const yaml=fs.readFileSync(path.join(dir,file),'utf8');
  const ids=[...yaml.matchAll(/^--- !u!\d+ &(\d+)/gm)].map(m=>m[1]);
  if(new Set(ids).size!==ids.length) throw Error('Duplicate Unity object ID');
  for(const m of yaml.matchAll(/\{fileID: (\d+)\}/g)) if(m[1]!=='0'&&!ids.includes(m[1])) throw Error('Unresolved Unity object reference: '+m[1]);
}
console.log(JSON.stringify({level:level.identifier,size:[W,H],pixelsPerUnit:PPU,rectangles:Object.fromEntries(Object.entries(rects).map(([k,v])=>[k,v.length])),validation:'Grid definitions, rectangle coverage, marker clearance/connectivity, and Unity object references passed.'},null,2));
