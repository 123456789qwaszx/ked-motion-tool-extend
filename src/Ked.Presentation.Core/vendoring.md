# Ked.Presentation.Core — 이 저장소의 코어 (출발점의 기록)

컷을 읽어 "이 프레임에 각 오브젝트와 카메라가 어디 있는가"를 내는 순수 C# 계산이다. **이 저장소의 것이다** — 필요하면 고친다.
게임 런타임의 코어를 따라가지 않고, 그쪽의 뒤 변경을 반입하지 않는다(소유자 결정, 2026-11-11). 이 문서는 동기화 절차가 아니라 **어디서 출발했는가**의 기록이다.

## 출발점

| 항목 | 값 |
|---|---|
| 원본 | `ked-stage-runtime` `Assets/Ked.Presentation/Core/` — `dev` @ `dc690c1` |
| 가져온 날 | 2026-10-29 |
| 그 뒤 | 툴이 컷(`Cut/`) · 색 사각형(`fillColor`) · 그림 노드 표시(`hasImage`)를 더했고, 2026-11-11에 컷이 쓰는 것만 남기고 줄였다(독립 C7 다-2) |

## 지금 있는 것

| 폴더 | 무엇 |
|---|---|
| `Cut/` | 컷 파일(`cut/1`)의 모양(`CutFileDto`) · 깊이와 카메라의 계산(`CutMath` — 깊이 커브는 여기의 상수다) · 깊이의 눈금(`DepthLevelLabels`) · 그 프레임의 모습(`CutPlayer`). 툴의 미리보기와 재생이 이 함수를 쓴다 |
| `Ease/` | 곡선의 키와 평가(`CurveKey` · `CurveFunctions`) |
| `Primitives/` | `Vec2` · `Vec3` |
| `State/` · `Transforms/` | 사각형 계층(`RectNodeTree` · `RectNodeState` · `RectSpace` · `RectChainMath`) — 유니티 RectTransform과 같은 규약의 좌표 계산 |

## 걷어 낸 것 (2026-11-11)

대본 커맨드를 무대 상태로 접던 것(`Reduce/` · `State`의 무대 상태 · `Tokens/`) · 모션 클립과 그 규칙(`Motion/`) · 초상 · 자리 · 프리셋의 튜닝 ·
이름 붙은 이징과 진동(`EaseFunctions` · `OscillationFunctions`) · `Rgba` · `SettledFocusMath` · `Documentation~/`, 그리고 그것들을 보던 테스트. 컷은 이것들을 쓰지 않는다.

그 뒤(같은 날, 기반 F4): 깊이 커브의 DTO(`DepthTuningDto` — 커브는 `CutMath`의 상수가 됐다) · 계층을 적는 DTO와 그 로더(`HierarchyDto` · `HierarchyLoader` · `Float2Dto`/`Float3Dto` — 세트는 도메인의 `ExpandedObject`에서 바로 트리를 세운다) · `Tuning/` 폴더.

## 컴파일 수준

csproj가 netstandard2.1 · C# 9 · 외부 의존 0을 지킨다 — 유니티에서도 컴파일되는 수준이다. 게임이 이 코드를 어떻게 가져갈지는 지금 다루지 않는다.
