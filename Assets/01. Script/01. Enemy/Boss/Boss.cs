using System.Collections.Generic;
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

        // RoundManager가 SeedSkillsFromMonsters()를 이미 호출했는지 표시합니다.
        // 게임 루프(RoundManager가 스폰)에서는 실제로 등장한 몬스터 목록으로 채우고,
        // 그게 없는 상황(개발용 씬에 보스를 직접 배치한 경우 등)에서만
        // 예전 방식(완전 무작위)으로 안전하게 대체합니다.
        private bool skillsSeeded = false;

        protected override void Awake()
        {
            base.Awake();
            BossController = GetComponent<BossController>();

            // 여기서 바로 채우지 않습니다. RoundManager가 Instantiate 직후,
            // Start()가 호출되기 전에 SeedSkillsFromMonsters()를 부를 기회를 줘야 하기 때문입니다.
            // (Awake는 Instantiate 호출 중 즉시 실행되지만, Start는 다음 프레임 초입에
            //  호출되므로 그 사이에 외부에서 값을 주입할 여지가 생깁니다)
        }

        protected override void Start()
        {
            base.Start();

            if (skillsSeeded) return;
            EnsureMonsterSkillLibrary();
        }

        /// <summary>
        /// 기획서 6-2 : "보스 스킬 = 몬스터의 모든 스킬들 보유".
        /// 1~5라운드에서 실제로 등장했던 몬스터들의 드랍 무기 목록을 받아, 그 안에서
        /// 보스 스킬을 뽑습니다. RoundManager.SpawnBoss()가 Instantiate 직후에 호출합니다.
        /// 인스펙터에 이미 지정된 슬롯은 그대로 존중하고, 비어 있는 슬롯만 채웁니다.
        /// </summary>
        public void SeedSkillsFromMonsters(IReadOnlyList<WeaponId> encounteredWeaponIds)
        {
            skillsSeeded = true;

            if (monsterSkillLibrary == null || monsterSkillLibrary.Length == 0)
                monsterSkillLibrary = new AttackInfo[3];

            if (encounteredWeaponIds == null || encounteredWeaponIds.Count == 0)
            {
                // 등장한 몬스터 정보가 없으면(예: 1라운드도 없이 바로 보스만 테스트하는 경우)
                // 예전처럼 완전 무작위로 대체합니다.
                EnsureMonsterSkillLibrary();
                return;
            }

            for (int i = 0; i < monsterSkillLibrary.Length; ++i)
            {
                if (monsterSkillLibrary[i].IsEmpty == false) continue;

                WeaponId id = encounteredWeaponIds[Random.Range(0, encounteredWeaponIds.Count)];
                monsterSkillLibrary[i] = GameManager.Instance.AttackTable.Get(id).ToAttackInfo(Player.MAX_USES);
            }
        }

        private void EnsureMonsterSkillLibrary()
        {
            if (monsterSkillLibrary == null || monsterSkillLibrary.Length == 0)
                monsterSkillLibrary = new AttackInfo[3];

            for (int i = 0; i < monsterSkillLibrary.Length; ++i)
            {
                if (monsterSkillLibrary[i].IsEmpty == false) continue;
                monsterSkillLibrary[i] = GameManager.Instance.AttackTable.GetRandomDef().ToAttackInfo(Player.MAX_USES);
            }
        }

        public AttackInfo GetMonsterSkill(int index)
        {
            if (monsterSkillLibrary == null || monsterSkillLibrary.Length == 0)
                return GameManager.Instance.AttackTable.Get(WeaponId.Fist).ToAttackInfo(Player.UNLIMITED_USES);

            if (index < 0 || index >= monsterSkillLibrary.Length)
            {
                Debug.LogWarning($"{name} : 잘못된 보스 스킬 인덱스({index})입니다. 0번으로 대체합니다.");
                index = 0;
            }

            AttackInfo skill = monsterSkillLibrary[index];

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
