using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Study_ActionPlatformer
{
    [Serializable]
    public class RoundDef
    {
        public int Round;
        public int MonsterCount;
        // RoundManager.enemyPrefabs 중 이 라운드에 나올 프리팹 이름들
        public string[] EnemyNames;
    }

    /// <summary>
    /// rounds.tsv : 일반 라운드별 몬스터 수와 등장 몬스터 구성.
    /// 보스 라운드는 보스만 스폰하므로 표에 넣지 않습니다.
    /// </summary>
    public class RoundTable
    {
        private const string TableName = "Table/DesertChess/rounds.tsv";

        private Dictionary<int, RoundDef> defs = new();

        public bool TryGet(int round, out RoundDef def) => defs.TryGetValue(round, out def);

        public void Load()
        {
            defs.Clear();

            string path = Application.streamingAssetsPath + "/" + TableName;

            using (StreamReader reader = new StreamReader(path))
            {
                reader.ReadLine();
                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] cells = line.Split('\t');

                    List<string> enemyNames = new List<string>();
                    if (cells.Length > 2)
                    {
                        foreach (string name in cells[2].Split(','))
                        {
                            string trimmed = name.Trim();
                            if (trimmed.Length > 0) enemyNames.Add(trimmed);
                        }
                    }

                    RoundDef def = new RoundDef
                    {
                        Round = int.Parse(cells[0]),
                        MonsterCount = int.Parse(cells[1]),
                        EnemyNames = enemyNames.ToArray(),
                    };

                    defs[def.Round] = def;
                }
            }
        }
    }
}
