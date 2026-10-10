using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // 기획서 5-2 무기 표의 줄 구분 + 마법 = "4카테고리" (weapons.tsv Family 열)
    public enum WeaponFamily
    {
        None,       // 계열 없음 (주먹 : 시너지 개수에 세지 않음)
        Melee,      // 근접 : 주먹, 검, 창, 도끼, 단검, 망치
        Heavy,      // 중병기 : 채찍, 글레이브, 삼지창, 사슬낫, 워해머, 도리깨
        Ranged,     // 원거리 : 총, 화살, 석궁, 표창, 부메랑, 슬링
        Magic,      // 마법 : 불, 물, 전기, 어둠
    }

    [Serializable]
    public class SynergyDef
    {
        public int ID;
        public WeaponFamily Family;
        public int Count;
        public StatType StatType;
        public ModifierOp Op;
        public float Value;

        // PlayerStat에 붙일 때의 출처 이름. 시너지가 꺼지면 이 이름으로 뗍니다.
        public string Source => $"Synergy.{Family}.{ID}";
    }

    /// <summary>
    /// synergies.tsv : 같은 계열을 Count개 이상 보유하면 붙는 스탯 보정.
    /// </summary>
    public class SynergyTable
    {
        private const string TableName = "Table/DesertChess/synergies.tsv";

        private readonly List<SynergyDef> defs = new();

        public IReadOnlyList<SynergyDef> Defs => defs;

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

                    defs.Add(new SynergyDef
                    {
                        ID = int.Parse(cells[0]),
                        Family = (WeaponFamily)Enum.Parse(typeof(WeaponFamily), cells[1]),
                        Count = int.Parse(cells[2]),
                        StatType = (StatType)Enum.Parse(typeof(StatType), cells[3]),
                        Op = (ModifierOp)Enum.Parse(typeof(ModifierOp), cells[4]),
                        Value = float.Parse(cells[5], CultureInfo.InvariantCulture),
                    });
                }
            }
        }
    }
}
