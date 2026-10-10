# 라운드 맵 (LDtk)

게임의 라운드 맵 6개는 모두 `ReferenceLayout.ldtk` 한 파일에 들어 있습니다.
LDtk Unity 패키지가 이 파일을 직접 임포트하고, `Game.unity`에 배치된 맵을 `MapManager`가 라운드마다 켜고 끕니다.

## 파일

| 파일 | 내용 |
| --- | --- |
| `ReferenceLayout.ldtk` | 라운드 맵 원본. LDtk에서 열어 편집합니다 (레벨은 파일 안에 포함, 외부 `.ldtkl` 없음) |
| `ReferenceLayout/` | 타일셋 정의(`.ldtkt`)와 미리보기 이미지 |
| `Cavernas_by_Adam_Saltsman.png` | 타일셋 이미지 |
| `GroundTile.asset` | Ground IntGrid에 연결된 충돌 타일 |

격자는 16 px, Unity 배율은 16 pixels/unit입니다.

## 레벨 구성

| 레벨 | RoundIndex | DisplayName | IsBossRound |
| --- | --- | --- | --- |
| `ReferenceLayout` | 1 | ROUND 1 | false |
| `ReferenceLayout_02` | 2 | ROUND 2 | false |
| `ReferenceLayout_03` | 3 | ROUND 3 | false |
| `ReferenceLayout_04` | 4 | ROUND 4 | false |
| `ReferenceLayout_05` | 5 | ROUND 5 | false |
| `ReferenceLayout_06` | 6 | BOSS | true |

레벨들은 월드 좌표에서 겹치지 않게 가로로 놓여 있습니다. 맵 전환은 씬을 바꾸지 않고 해당 레벨만 활성화하는 방식입니다.

## 레이어 · 엔티티 · 필드

| 종류 | 이름 | 용도 |
| --- | --- | --- |
| 레이어 | `Ground` | 충돌 (IntGrid) |
| 레이어 | `BackGround` | 배경 타일 |
| 레이어 | `Entities` | 아래 엔티티 배치 |
| 엔티티 | `PlayerStart` | 플레이어 시작 위치 (레벨당 1개) |
| 엔티티 | `EnemySpawn` | 일반 몬스터 스폰 후보 (개수 제한 없음, 랜덤 선택) |
| 엔티티 | `BossSpawn` | 보스 스폰 위치 (보스 레벨만) |
| 레벨 필드 | `RoundIndex` | 라운드 번호 (없으면 배치 순서) |
| 레벨 필드 | `DisplayName` | HUD 맵 이름 (없으면 레벨 identifier) |
| 레벨 필드 | `IsBossRound` | 보스 라운드 여부 |

이 이름들은 `01. Script/03. Map/RoundMap.cs`의 상수와 일치해야 합니다. LDtk에서 이름을 바꾸면 코드도 함께 바꿔야 합니다.

## 수정 방법

| 하고 싶은 것 | 방법 |
| --- | --- |
| 스폰 위치 바꾸기 | LDtk에서 엔티티를 옮기고 저장 → Unity가 자동 재임포트 |
| 맵(라운드) 추가 | 레벨 추가 → `RoundIndex` 지정 → `PlayerStart` 1개 · `EnemySpawn` 배치 → `GameManager.roundCountToBoss`와 맵 개수 맞추기 |
| 보스 맵 지정 | 해당 레벨 `IsBossRound` 켜고 `BossSpawn` 배치 |

현재 맵에는 사다리 · 사슬 · 물 · 체크포인트 레이어가 없습니다. 이동은 발판(Ground)으로만 연결됩니다.
