using UnityEngine;

namespace Study_ActionPlatformer
{
    // # PlayerAnimStateBase
    // - 플레이어 애니메이션 상태 클래스들의 공통 부모 (상태 패턴).
    // - 매 프레임 "지금 어떤 태그의 상태인가"를 관찰해서 그에 맞는 행동을 실행한다.
    // - 다른 상태패턴과는 다르게 전이(Transition)를 결정하지 않습니다(전이는 애니메이터가)
    // - 보스 상태패턴과는 다르게 순수 C# 클래스로 작성되어있습니다

    public abstract class PlayerAnimStateBase
    {
        // # 추상 클래스(Abstract Class)?
        // - 객체 지향 프로그래밍에서 "미완성된 베이스 설계도"역할을 하는 중요한 개념.
        // - 스스로 완벽히 실재하는 객체가 될 수는 없지만, 다른 클래스들이 상속받아
        //  완성 할 수 있도록 시스템의 공통적인 특징과 견고한 뼈대를 제공합니다.

        // ## 핵심 특징
        // - 인스턴스화 불가 : new 키워드를 사용하여 직접 객체(인스턴스)를 생성할 수
        //  없습니다. 오직 상속을 위한 부모 클래스로만 존재합니다.
        // - 추상 메서드(Abstract Methods) : 선언만 있고 구현부(Body)가 없는 메서드
        //  입니다. 자식클래스에서 override 키워드를 이용(오버라이딩)하여 구체적인
        //  로직을 구현하는 "강제적 계약"을 부여합니다.
        // - 일반 멤버 보유 : 인터페이스(Interface)와 달리, 완전히 구현된 일반 메서드나
        //  멤버 변수(필드), 프로퍼티를 가질 수 있습니다.
        // - 단일 상속 : C#에서는 다중 상속을 지원하지 않으므로 하나의 자식 클래스는
        //  단 하나의 추상 클래스만 상속받을 수 있습니다.

        // ## 사용해서 얻을 수 있는 이점
        // - 코드 재사용성 극대화
        // - 다형성의 올바른 구현
        // - 확작성(OCP)의 확보 : 새로운 기능이나 객체 타입이 필요할 떄 기존 코드를
        //  거의 수정하지 않고, 추상 클래스를 상속받는 새로운 자식클래스를 추가하는
        //  방식으로 시스템을 유연하고 안전하게 확장 할 수 있습니다


        public const float INPUT_RESET_TIME = 0.4f;
        public const float COMBO_INPUT_END_TIME = 0.8f;

        protected PlayerController Owner { get; }
        protected Animator Animator => Owner.Animator;

        protected PlayerAnimStateBase(PlayerController owner)
        {
            Owner = owner;
        }

        // 이 상태로 "진입한 순간" 한 번 호출된다.
        public virtual void Enter() { }
        // 이 상태에 머무는 동안 매 프레임 호출된다. (자식이 반드시 구현)
        public abstract void UpdateState(AnimatorStateInfo stateInfo);
        // 이 상태에서 "떠나는 순간" 한 번 호출된다.
        public virtual void Exit() { }

        // # 함수의 선언부에 사용하는 virtual 키워드와 abstract 키워드
        // ## virtual
        // - 메서드(함수), 프로퍼티, 이벤트 선언 등에 사용되어 자식 클래스에서
        //  해당 멤버의 구현을 재정의(override)할 수 있도록 허용하는 권한을
        //  부여하는 키워드 입니다. 객체 지향에서 다형성을 구현하는 요소 중
        //  하나입니다.
        // - 보통, 부모 클래스의 함수도 사용 해야할 때 사용합니다.

        // ## abstract
        // - 선언만 있고 구현부(Body)가 없는 메서드 입니다. 자식클래스에서
        // - override 키워드를 이용(오버라이딩)하여 구체적인 로직을 구현하는
        // "강제적 계약"을 부여합니다.

        // ## 두 키워드의 차이
        // - virtual 키워드는 구현부가 있어서, 자식클래스에서 재정의하더라도 
        //  부모클래스의 구현부를 사용할 수 있음.
        // - absract 키워드는 구현부가 없기 때문에 무조건 자식 클래스에서
        //  재정의를 해야함
        // - 자식클래스에서 구현을 강제해야 할 때 사용합니다.
    }

}

