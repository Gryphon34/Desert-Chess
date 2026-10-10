using System;
using UnityEngine;

namespace Study.Utilities
{
    // 타겟을 부드럽게(Lerp) 따라가는 범용 추적 카메라

    public class SimpleFollowCamera : MonoBehaviour
    {
        public enum States
        {
            Stopped,
            Holding,
            Following
        }

        [field:SerializeField] public Transform Target { get;  set; }
        [SerializeField] private float lerpSpeed; // 따라가는 속도, Damping 값
        [SerializeField] private Vector3 offset;

        private Camera Cam { get; set; }
        private States State { get; set; }

        private Bounds limitBounds;
        private bool hasBounds = false;

        // 시작할 때 Target이 지정돼 있으면 바로 추적을 시작할지 여부입니다.
        //
        // 왜 필요한가:
        // States의 기본값은 0번인 Stopped이고, Stopped는 매 프레임 Target을 null로
        // 지웁니다. 즉 누군가 ChangeState(Following)를 호출해주지 않으면 이 카메라는
        // 영원히 움직이지 않습니다. 지금까지는 테스트용 SimpleFollowCameraTester(F3)가
        // 그 역할을 했는데, 실제 게임 씬에는 그게 없어서 카메라가 멈춰 있었습니다.
        [SerializeField] private bool followOnStart = true;

        private void Start()
        {
            // 현재 Scene의 MainCamera(Tag)를 가져옵니다.
            // ps : MainCamera Tag가 달려있는 녀석을 가져옵니다.
            Cam = Camera.main;

            if (followOnStart && Target != null)
            {
                ChangeState(States.Following);
            }
        }

        private void Update()
        {
            switch (State)
            {
                case States.Stopped:
                    // 멈춤 상태가 되면 타겟을 비워버립니다.
                    Target = null;
                    break;
                case States.Holding:
                    // Holding은 Target은 존재하지만 카메라를 이동하지 않는 상태
                    break;
                case States.Following:
                    //  Target의 위치를 적절히 보간하여 적용합니다.
                    // ps : lerpSpeed는 댐핑값이라고 해서 속도에 대한 보정값입니다.
                    // ================
                    // 아래의 보간 내용은 (transform.position)을 (Target.position+offset)으로 초당 (lerpSpeed) 속도로
                    // 계속 가깝게 만드는 코드입니다. Target.position이 멀다면 더 빨리, 가깝다면 느리게
                    // lerpSpeed가 0 이하이면 보간 없이 바로 붙습니다.
                    // (Game.unity에 0으로 저장돼 있어서, 보간만 하면 카메라가 전혀 움직이지 않습니다)
                    Vector3 lerpPos = lerpSpeed > 0f
                        ? Vector3.Lerp(transform.position, Target.position+offset, lerpSpeed * Time.deltaTime)
                        : Target.position + offset;
                    lerpPos.z = transform.position.z; // 2D의 경우 z는 바뀌지 않습니다.
                    transform.position = ClampToBounds(lerpPos);
                    break;
            }
        }

        /// <summary>
        /// 카메라가 이 범위 밖을 비추지 않도록 제한합니다(맵 테두리 등).
        /// </summary>
        public void SetBounds(Bounds bounds)
        {
            limitBounds = bounds;
            hasBounds = true;
        }

        public void ClearBounds()
        {
            hasBounds = false;
        }

        /// <summary>
        /// 맵 전환 직후처럼 보간 없이 바로 Target 위치로 옮길 때 씁니다.
        /// </summary>
        public void SnapToTarget()
        {
            if (Target == null) return;

            Vector3 pos = Target.position + offset;
            pos.z = transform.position.z;
            transform.position = ClampToBounds(pos);
        }

        // 화면 절반 크기만큼 안쪽으로 줄인 범위에 카메라 중심을 가둡니다.
        // 맵이 화면보다 작은 축은 맵 가운데에 고정합니다.
        private Vector3 ClampToBounds(Vector3 pos)
        {
            if (hasBounds == false) return pos;

            if (Cam == null) Cam = Camera.main;
            if (Cam == null || Cam.orthographic == false) return pos;

            float halfHeight = Cam.orthographicSize;
            float halfWidth = halfHeight * Cam.aspect;

            pos.x = ClampAxis(pos.x, limitBounds.min.x + halfWidth, limitBounds.max.x - halfWidth, limitBounds.center.x);
            pos.y = ClampAxis(pos.y, limitBounds.min.y + halfHeight, limitBounds.max.y - halfHeight, limitBounds.center.y);
            return pos;
        }

        private static float ClampAxis(float value, float min, float max, float center)
        {
            if (min > max) return center;
            return Mathf.Clamp(value, min, max);
        }

        public void ChangeState(States state)
        {
            if (state == States.Following && Target == null)
            {
                Debug.LogWarning($"SimpleFollowCamera :: Target이 없어 Following 상태로 전환할 수 없습니다.");
                return;
            }

            State = state;
        }

    }
}