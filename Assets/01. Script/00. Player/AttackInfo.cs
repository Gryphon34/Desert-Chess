using UnityEngine;

namespace Study_ActionPlatformer
{
    public enum AttackSlotCategory
    {
        Weapon,
        Magic,
    }

    public enum WeaponId
    {
        None = 0,

        // 무기
        Fist,       // 주먹
        Sword,      // 검
        Spear,      // 창
        Axe,        // 도끼
        Dagger,     // 단검
        Hammer,     // 망치
        Glaive,     // 글레이브
        Trident,    // 삼지창
        WarHammer,  // 워해머
        Gun,        // 총
        Bow,        // 활

        // 마법
        Fire,       // 불
        Water,      // 물
        Lightning,  // 전기
        Dark,       // 어둠
    }

    public enum AttackKey
    {
        None = 0,
        Combo1,
        Combo2,
        Combo3,
        JumbAttack,
        Magic1,
        Magic2,
        Magic3,
    }

    [System.Serializable]
    public struct AttackInfo
    {
        // 어떤 무기인지, UI 표시와 시너지 계산의 기준이 됩니다.
        public WeaponId Id;
        public AttackSlotCategory Category;
        public AttackKey Key;
        public int MinDamage;
        public int MaxDamage;
        public int RemainingUses;
        public AnimationCurve damageCurve;

        // 슬롯이 비었는지 판단하는 기준을 한 곳으로 모읍니다.
        // (여기저기서 Key == AttackKey.None을 직접 비교하면 규칙이 바뀔 때 다 고쳐야 합니다)
        public bool IsEmpty => Key == AttackKey.None;

        // 구조체(Struct)도 메서드를 가질 수 있다.
        // "이 데이터를 어떻게 해석(데미지 계산 공식)하는가?"에 대한 내용은
        // 데이터 곁에 두는것이 응집도가 좋습니다. 
        // 기능이 많아지만 분리(확장함수)하는게 좋지만,
        // 몇개 없으면 struct 내부에다가 구현해놓고 사용

        public int RollDamage()
        {
            // Curve에 t역할을 검색할 랜덤한 값을 추출함
            // ex) 0~99까지의 랜덤한 숫자를 뽑음
            float randomRoll = Random.Range(0f, 1f);

            // 커브를 통해 가중치(t) 평가 = .Evaluate()릴 이용해 Y축 값 추출
            // 주의 : 코드에서 만든 AttackInfo나 커브를 안 그린 프리팹은 damageCurve가
            // null이거나 키가 0개입니다. 그대로 Evaluate하면 NullReference가 나거나
            // 항상 0(= 무조건 MinDamage)이 나오므로, 그 경우엔 균등 분포로 대체합니다.
            float evaluation = (damageCurve != null && damageCurve.length > 0)
                ? damageCurve.Evaluate(randomRoll)
                : randomRoll;

            float finalDamage = Mathf.Lerp(MinDamage, MaxDamage, evaluation);

            // Mathf.RoundToInt() : float를 반올림 하는 함수

            // Round : 반올림
            // Ceil : 올림
            // Floor : 버림(내림)
            // 접미사 ToInt() 붙는 함수의 경우 float로
            // 반환하는게 아니라 int로 반환을 합니다

            return Mathf.RoundToInt(finalDamage);
        }
    }


}