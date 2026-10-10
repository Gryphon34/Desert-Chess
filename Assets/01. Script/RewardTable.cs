using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public enum RewardId
    {
        RoundHeal,      // 라운드 클리어 보상 : 피 회복
        Enhance,        // 라운드 클리어 보상 : 능력 강화
        BossClearHeal,  // 보스 처치 확정 보상
    }

    [Serializable]
    public class RewardDef
    {
        public int ID;
        public RewardId RewardId;
        public int BaseAmount;
        public int AmountPerRound;
        public int DamageBoost;
        public int UsesBoost;
    }

    /// <summary>
    /// rewards.tsv : 보상 수치. RewardManager가 인스펙터 값 대신 이 표를 읽습니다.
    /// </summary>
    public class RewardTable
    {
        private const string TableName = "Table/DesertChess/rewards.tsv";

        private Dictionary<RewardId, RewardDef> defs = new();

        public RewardDef Get(RewardId id) => defs[id];

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

                    RewardDef def = new RewardDef
                    {
                        ID = int.Parse(cells[0]),
                        RewardId = (RewardId)Enum.Parse(typeof(RewardId), cells[1]),
                        BaseAmount = int.Parse(cells[2]),
                        AmountPerRound = int.Parse(cells[3]),
                        DamageBoost = int.Parse(cells[4]),
                        UsesBoost = int.Parse(cells[5]),
                    };

                    defs[def.RewardId] = def;
                }
            }
        }
    }
}
