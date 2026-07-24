using UnityEngine;

namespace Study_ActionPlatformer
{
    public class Boss : Enemy
    {
        // 보스는
        // 콜라이더, 스프라이트 렌더러 각각 3개씩 가지고 있음
        // BossParts를 만들어서 각 개체를 분리해놓은 구조에서
        // 전투시스템이 작동하게끔 구현

        private BossController BossController { get; set; }
        [SerializeField] private float PartsDamageMultiplier = 0.8f;

        // 각 패턴(Pattern0/1/2)이 skillIndex로 이 배열을 참조해 데미지를 뽑습니다.
        [SerializeField] private AttackInfo[] monsterSkillLibrary = new AttackInfo[3];
        public AttackInfo[] MonsterSkillLibrary => monsterSkillLibrary;

        // 보스 패턴은 몬스터와 같은 스킬을 쓰지만 위력은 더 높아야 하므로 배율을 둡니다.
        // 밸런싱은 이 값 하나로 조절하세요.
        [SerializeField] private float bossSkillPowerMultiplier = 2.0f;

        protected override int DefaultMaxHp => 300;

        protected override void Awake()
        {
            base.Awake();
            BossController = GetComponent<BossController>();

            EnsureMonsterSkillLibrary();
        }

        private void EnsureMonsterSkillLibrary()
        {
            if (monsterSkillLibrary == null || monsterSkillLibrary.Length == 0)
                monsterSkillLibrary = new AttackInfo[3];

            for (int i = 0; i < monsterSkillLibrary.Length; ++i)
            {
                if (monsterSkillLibrary[i].IsEmpty == false) continue;
                monsterSkillLibrary[i] = WeaponLibrary.CreateRandom();
            }
        }

        public AttackInfo GetMonsterSkill(int index)
        {
            if (monsterSkillLibrary == null || monsterSkillLibrary.Length == 0)
                return WeaponLibrary.CreateFist();

            if (index < 0 || index >= monsterSkillLibrary.Length)
            {
                Debug.LogWarning($"{name} : 잘못된 보스 스킬 인덱스({index})입니다. 0번으로 대체합니다.");
                index = 0;
            }

            AttackInfo skill = monsterSkillLibrary[index];

            // 위력 배율은 "표의 원본 데이터"가 아니라 "보스가 쓸 때의 값"에만 적용합니다.
            skill.MinDamage = Mathf.RoundToInt(skill.MinDamage * bossSkillPowerMultiplier);
            skill.MaxDamage = Mathf.RoundToInt(skill.MaxDamage * bossSkillPowerMultiplier);
            return skill;
        }

        public override void TakeHeal(int heal)
        {

        }

        // 부위 공격 데미지에 배율을 적용하여 최종 데미지를 계산하는 함수
        public override int CalculateFinalDamage(int damage)
        {
            return Mathf.RoundToInt(damage * PartsDamageMultiplier);
        }

        // 함수 오버로딩
        // 매개변수가 다른 함수들을 같은 함수명으로 정의하는 것
        // 실제 체력 차감은 아래 ApplyDamage에 위임한다.
        // (피격 연출은 맞은 부위(BossParts)가 스스로 재생하므로 여기서는 처리하지 않는다)
        public void TakeDamage(BossParts parts, int damage)
        {
            ApplyDamage(damage);
        }

        public override void TakeDamage(int damage)
        {
            ApplyDamage(damage);
            StartCoroutine(HitEffectCoroutine());
        }

        private void ApplyDamage(int damage)
        {
            if (Stat.ApplyDamage(damage)) BossController.Dead();
        }
    }

}
