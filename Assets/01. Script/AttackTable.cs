using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class AttackTable
    {
        private const string TableName = "Table/DesertChess/weapons.tsv";

        private Dictionary<WeaponId, AttackDef> defs = new();

        public AttackDef Get(WeaponId id) => defs[id];

        public bool TryGet(WeaponId id, out AttackDef def) => defs.TryGetValue(id, out def);

        public IReadOnlyCollection<AttackDef> GetDefs() => defs.Values;

        public void Load()
        {
            defs.Clear();
            
            string path = Application.streamingAssetsPath + "/" + TableName;
            List<AttackDTO> rows = new List<AttackDTO>();
            
            using (StreamReader reader = new StreamReader(path))
            {
                reader.ReadLine(); 
                while (reader.EndOfStream == false)
                {
                    AttackDTO dto = new();
                    string line = reader.ReadLine();
                    string[] cells = line.Split('\t');
                    
                    dto.ID = int.Parse(cells[0]);
                    dto.Category = cells[1];
                    dto.WeaponId = cells[2];
                    dto.AttackKey = cells[3];
                    dto.MinDamage = int.Parse(cells[4]);
                    dto.MaxDamage = int.Parse(cells[5]);
                    dto.Speed = int.Parse(cells[6]);
                    dto.Range = int.Parse(cells[7]);
                    // Family 열이 없던 예전 표도 읽을 수 있도록 비어 있으면 None으로 둡니다.
                    dto.Family = cells.Length > 8 ? cells[8].Trim() : nameof(WeaponFamily.None);
                    
                    rows.Add(dto);
                }
            }

            foreach (AttackDTO dto in rows)
            {
                defs[(WeaponId)Enum.Parse(typeof(WeaponId), dto.WeaponId)] = new AttackDef
                {
                    ID = dto.ID,
                    Category = (AttackSlotCategory)Enum.Parse(typeof(AttackSlotCategory), dto.Category),
                    WeaponId = (WeaponId)Enum.Parse(typeof(WeaponId), dto.WeaponId),
                    Key = (AttackKey)Enum.Parse(typeof(AttackKey), dto.AttackKey),
                    MinDamage = dto.MinDamage,
                    MaxDamage = dto.MaxDamage,
                    Speed = dto.Speed,
                    Range = dto.Range,
                    Family = (WeaponFamily)Enum.Parse(typeof(WeaponFamily), dto.Family),
                };
            }
        }
        
        public AttackDef GetRandomDef()
        {
            if (defs.Count == 0) return null;
    
            int randomIndex = UnityEngine.Random.Range(0, defs.Count);
            int currentIndex = 0;
    
            foreach (var def in defs.Values)
            {
                if (currentIndex == randomIndex) return def;
                currentIndex++;
            }
    
            return null;
        }
    }
}