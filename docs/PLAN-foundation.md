# 기반 다듬기 — 작업 계획 (2026-11-11)

근거: 소유자의 지시("`ked-motion-tool-extend` 세션에게 — 지시", 2026-11-11)와 "세 세션 공통 브리핑(2026-11-11 오후)".
이 문서는 **무엇을 어떤 차례로 하고, 각 단계가 언제 끝난 것인가**를 적는다. 단계가 끝날 때마다 결정과 상태를 여기에 적고, `STATUS.md`를 고친다.

## 1. 방향 (소유자)

- 이 툴은 장기 목표다. 최소 2주 이상 다듬으며, 그 기간에 출력이 `ked-presentation-runtime`에 반영될 일은 없다.
- 런타임과의 관계는 잊는다 — 완전히 독립된 툴로 뻗어 나갈 것을 상정한다.
- **기능 개발이 아니다.** 뒤에 의도와 목적이 확정되면 바로 일하기 편하도록 기반 시설을 다듬고 불필요한 아키텍처를 걷어 낸다.
- 출력물(컷 파일)은 구조만 관리한다.

## 2. 하지 않는 것

- 새 기능 — `STATUS.md` §6의 "알려진 불편"은 기록만 둔다(소유자가 열 때까지).
- 게임 쪽 일(`CutPose`를 유니티 UI에 옮기기 · `Cut/`을 런타임에 넣기 · 깊이 커브를 넘기는 약속) — 보류.
- 런타임 저장소를 고치거나 따라가는 것, 형제 저장소(`characterRig` · `screenTransition`)의 일.
- 보여 주기 위한 컷 예시 — 테스트의 발판으로 필요한 최소한(하나)까지만.

## 3. 단계

모든 단계의 공통 완료 기준: **빌드 경고·오류 0 · 전체 테스트 통과 · 걷거나 바꾼 이름의 잔재 검색 0건 · 이 문서와 `STATUS.md`에 기록 · 커밋과 푸시.**
화면을 고친 단계는 렌더(`CutShots`)를 뽑아 눈으로 확인한다. 한 번에 하나 — 소유자의 "진행."으로 다음 단계를 연다.

| 단계 | 내용 | 상태 |
|---|---|---|
| F0 | 문서에 방향을 적는다(지시서 4절의 여섯 곳) | 끝 — `8c9b912` |
| F1 | 코어를 컷이 쓰는 것만 남기고 줄인다(C7 다-2) | 끝 — `21cebdb` |
| F2 | 옛 이름을 고친다 | 끝 — `09c98c4`(코어) · `012d746`(식별자) · F2-3(문구 — 이 문서와 같은 커밋) |
| F3 | 문서를 지금의 툴에 맞춘다 | 끝 — `ffe95e9` · F3-3 · F3-4 |
| F4 | 구조를 훑어 불필요한 것을 찾고, 승인받은 것을 걷는다 | 끝 — `464f6eb` ~ 나6 |

### F2 — 옛 이름을 고친다

`Rig` · `Motion` 같은 캐릭터 리그 시절의 이름을 컷 · 오브젝트 · 타임라인 · 묶음으로 고친다(코드와 화면). 동작은 바꾸지 않는다 — 이름만.

범위(2026-11-11에 센 것): 이름에 `Rig`/`Motion`이 든 타입 40개 남짓, 파일 40개 남짓, 화면 문구 가운데 "모션"·"리그"가 든 것 30개 남짓.
도메인 7,500줄 · 화면 12,000줄 · 테스트 8,700줄에 걸친다.

한 커밋에 다 넣으면 검토할 수 없으니 넷으로 나눈다. 각각이 위의 완료 기준을 채운 커밋 하나다.

| | 내용 | 대표적인 것 |
|---|---|---|
| F2-0 | **대응표를 정한다** — 옛 이름과 새 이름을 표로 적어 소유자에게 보고한다. 승인받은 표가 F2-1~3의 기준이다 | 아래 "정해야 하는 것" |
| F2-1 | 코어 | `RigSchemaDto` · `RigSchemaLoader` · `RigSchema*Dto`, "덤프"라는 말 |
| F2-2 | 도메인(`Mt.Authoring`)과 그 테스트 | `MotionEditor` · `MotionDocument` · `MotionAsset` · `RigAsset` · `MotionFlatten` · `MotionValidator`, `Rig/` 폴더 |
| F2-3 | 화면(`Mt.App`) · 명령줄(`Mt.Cli`)과 그 테스트 — 보이는 문구까지 | "모션 추가" · "모션 없음" · "모션" 탭, 테스트 손잡이의 이름 |

F2-0에서 **정해야 하는 것**(권고를 함께 보고한다):

1. **낱말.** `Motion`(지금은 "컷이나 묶음에 얹히는 애니메이션") · `Rig`(지금은 "오브젝트의 계층") · `Set`/세트(묶음)를 각각 무엇으로 부를지. 화면의 우리말과 코드의 영어를 한 쌍으로 정한다.
2. **저작본의 키.** 저작본(JSON)의 키에도 옛 이름이 있다. 코드만 고치고 키는 둘지, 키도 고칠지. 키를 고치면 옛 저작본을 읽지 못한다 — 지금 지켜야 할 저작본이 있는지가 답을 정한다.
3. **프로젝트와 네임스페이스.** `Ked.Presentation.Core` · `Mt.*` · `KedMotionTool.sln` 같은 그릇의 이름까지 고칠지. 고치면 모든 파일의 `using`과 폴더가 움직인다.
4. **컷 파일(`cut/1`)의 키**는 건드리지 않는다(이미 컷의 낱말로 적혀 있다) — 확인만 한다.

하는 방법: 이름 하나씩 기계적으로 바꾸고(타입 → 파일 → 주석과 문구), 바꿀 때마다 빌드한다. 묶음이 크면 범위와 완료 기준을 적어 에이전트에게 맡기고, 결과는 직접 다시 돌려 확인한 뒤 커밋한다(`CLAUDE.md`의 관습).

#### F2-0 — 대응표 (2026-11-11, **승인됨** — 아래 "정해야 하는 것"은 모두 권고대로)

지금의 모델을 읽고 지은 표다. 승인받으면 F2-1~3의 기준이 된다. "쓰임"은 그 이름이 나오는 파일 수/횟수(코드와 테스트).

**낱말 넷** — 소유자의 말(컷 · 오브젝트 · 타임라인 · 묶음)을 코드의 영어와 한 쌍으로:

| 뜻 | 화면 | 코드 | 지금의 옛 이름 |
|---|---|---|---|
| 컷 | 컷 | `Cut` | (그대로) |
| 오브젝트 | 오브젝트 | `Object` | `RigNode` · `Node` · 화면의 "노드" |
| 타임라인(키를 담는 것 — 컷에는 하나, 묶음에는 여럿) | 컷은 "타임라인", 묶음의 것은 "애니메이션" | `Timeline` | `Motion` · 화면의 "모션" |
| 묶음(이름 붙은 오브젝트 계층 — 다른 컷·묶음에 인스턴스로 놓는다) | "세트" | `Set` | `Rig` · `rigKind` |

**도메인(`Mt.Authoring`)의 타입:**

| 옛 이름 | 새 이름 | 쓰임 | 덧붙임 |
|---|---|---|---|
| `MotionDocument` | `AuthoringDocument` | 28/99 | 저작본 한 벌 |
| `MotionDocumentJson` | `AuthoringDocumentJson` | 14/68 | |
| `MotionEditor`(+ 나뉜 파일 12개) | `DocumentEditor` | 49/193 | `MotionEditor.Rigs.cs` → `DocumentEditor.Sets.cs` |
| `MotionChangedEventArgs` · `MotionChangeKind` | `DocumentChangedEventArgs` · `DocumentChangeKind` | 6/9 · 18/93 | 멤버(`Structure` · `Keys` …)는 그대로 |
| `MotionAsset` | `TimelineAsset` | 46/215 | `.Rig` → `.Set`, 문서의 `Motions` → `Timelines` |
| `MotionPlayAsset` | `PlayBlockAsset` | 11/24 | 재생 블록. `.Motion` → `.Timeline` |
| `MotionSwapAsset` · `MotionSwaps` | `ImageKeyAsset` · `ImageKeys` | 9/15 · 5/16 | 그림 키. `Swaps` → `ImageKeys`, `MotionEditor.Swaps.cs` · `MainWindow.Swaps.cs`도 |
| `MotionEventAsset` · `MotionEvents` | `EventKeyAsset` · `EventKeys` | 10/21 · 5/9 | 이벤트 키 |
| `MotionChannelKind` | `ChannelKind` | 39/393 | `ChannelKindLabel`과 짝이 맞는다 |
| `MotionNames` | `NameRules` | 7/22 | 기본 이름 `"motion"` → `"timeline"` |
| `MotionFlatten` | `TimelineFlatten` | 6/8 | 재생 블록을 펼쳐 굽기 |
| `MotionPreview` | `TimelinePose` | 8/34 | 시각 t의 포즈 |
| `MotionValidator` · `MotionProblem` | `DocumentValidator` · `ValidationProblem` | 11/42 · 9/33 | |
| `MotionFix` · `MotionFixes` | `ValidationFix` · `ValidationFixes` | 4/16 · 4/14 | |
| `RigAsset` | `SetAsset` | 21/93 | `.Kind` → `.Name`, `.Nodes` → `.Objects`. 컷의 계층도 같은 그릇이다 — `CutAsset.Rig` → `CutAsset.AsSet` |
| `RigNodeAsset` | `ObjectAsset` | 30/152 | |
| `RigAssetCodec` | `SetCodec` | 13/33 | 묶음 → 코어의 계층 DTO |
| `RigAssets` | `SetBuilder` | 10/14 | 문서의 묶음·컷을 세운 한 벌로 |
| `RigSchemaFile` | `BuiltSets` | 18/57 | 세운 한 벌(파일이 아니다) |
| `RigParts` | `ObjectParts` | 5/19 | 그림 오브젝트와 색 사각형 |
| 문서의 `Rigs` · `FindRig` · `AllRigs` | `Sets` · `FindSet` · `AllSets` | | |
| 채널·블록·그림 키의 `.Node` | `.Object` | | |
| 폴더 `Mt.Authoring/Rig/`(네임스페이스 `Mt.Authoring.Rig`) | `Sets/`(`Mt.Authoring.Sets`) | | 깊이 커브 · 기준 해상도 · 층 표도 여기 있다 — 자리가 맞는지는 F4에서 본다 |
| `SkinAsset` · `SkinPartSetup` · `CutAsset` · `CutStage` · `ChannelAsset` · `KeyAsset` | (그대로) | | 옛 이름이 아니다 |

**코어:**

| 옛 이름 | 새 이름 | 쓰임 |
|---|---|---|
| `RigSchemasFileDto` | `HierarchyFileDto` | 4/13 |
| `RigSchemaRigDto`(`rigKind`) | `HierarchyDto`(`name`) | 12/42 |
| `RigSchemaNodeDto` | `HierarchyNodeDto` | 10/27 |
| `RigSchemaLoader`(`AddRigTo`) | `HierarchyLoader`(`AddTo`) | 10/42 |
| 주석의 "덤프" · "리그" | "계층" | |
| `RectNodeTree` · `RectNodeState` · `Node`라는 말 | (그대로) | 트리의 마디라는 일반어다 |

**화면 · 명령줄 · 테스트:**

| 옛 것 | 새 것 |
|---|---|
| 화면 문구의 "모션"(28곳) — "모션 추가" · "모션 없음" · "모션" 탭 · "먼저 모션을 고른다" … | 컷을 연 때는 "타임라인", 세트의 것은 "애니메이션" |
| 화면 문구의 "노드"(7곳) | "오브젝트" |
| `MainWindow.Swaps.cs` | `MainWindow.ImageKeys.cs` |
| 명령줄 `pose --motion` | `pose --timeline` |
| `PartsRigFixture` · `RigAssetTests` · `RigSchemaFileTests` · `Motion*Tests` | `PartsSetFixture` · `SetAssetTests` · `BuiltSetsTests` · 새 타입 이름을 따른다 |
| 테스트 손잡이 이름의 `Motion` · `Rig` | 위 표를 따른다 |

**정해야 하는 것과 권고:**

1. **화면의 낱말 — "세트"인가 "묶음"인가.** 지시서는 "묶음"이라 적었고, 화면은 지금 "세트"다(67곳, "묶음"은 4곳). **권고: 화면은 "세트" 그대로**, 코드는 `Set`. 이미 한결같이 쓰이고 있고 계획서(`PLAN-set.md`)도 그 말로 적혀 있다.
2. **화면의 "타임라인"과 "애니메이션".** 코드는 `Timeline` 하나지만 화면은 컷의 것을 "타임라인", 세트의 것을 "애니메이션"이라 부른다. **권고: 그대로 둘로 부른다** — 컷에는 하나뿐이라 이름 없이 붙고, 세트에는 이름 붙은 것이 여럿이라 쓰는 사람에게 다르게 보인다. "모션"만 없앤다.
3. **저작본의 키와 확장자.** 키(`motions` · `rigs` · `rig` · `kind` · `nodes` · `node` · `swaps` · `motion`)와 스키마(`motion-authoring/2`) · 확장자(`.motion-authoring.json`)에도 옛 이름이 있다.
   **권고: 함께 고친다** — 스키마 `cut-authoring/1`, 확장자 `.cut-authoring.json`, 키는 코드의 새 이름. 옛 판(`motion-authoring/1` · `/2`)을 읽는 길은 두지 않는다(호환 코드를 걷으라는 지시와 같은 방향이다).
   **단, 소유자가 손에 든 저작본이 있으면 읽지 못하게 된다** — 저장소에는 저작본이 하나도 없다. 지켜야 할 파일이 있으면 키와 확장자는 그대로 두고 코드만 고친다.
4. **그릇의 이름**(`Ked.Presentation.Core` · `Mt.*` · `KedMotionTool.sln` · 창 제목 `ked-motion-tool`). **권고: 지금은 두지 않는다** — 툴의 이름은 의도와 목적이 확정될 때 정할 일이고, 고치면 모든 파일의 `using`과 폴더가 움직인다. F4의 목록에 올려 둔다.
5. **컷 파일(`cut/1`)의 키**는 건드리지 않는다 — 이미 `objects` · `tracks` · `target` · `what`으로 적혀 있다.

**소유자의 답(2026-11-11):** 지켜야 할 저작본은 없다. 마이그레이션을 고려한 것을 만들지 않는다. 필요 없는 것은 과감하게 정리하며 간다.

조사하다 눈에 띈 것(F3 · F4의 몫 — 지금 고치지 않는다):

- `STATUS.md` §6의 "CLI의 컷 내보내기가 없다"는 낡았다 — `Mt.Cli`에 `cut` 명령이 있다.
- 묶음 → 코어의 계층 DTO → 트리의 세 겹(`RigAsset` → `RigSchemaRigDto` → `RectNodeTree`). 덤프를 읽지 않는 지금 가운데 겹이 필요한지.
- `MotionDocumentJson`이 옛 판(`motion-authoring/1`)을 읽어 준다(위 3번이 승인되면 F2에서 함께 나간다).

### F3 — 문서를 지금의 툴에 맞춘다

이름이 정해진 뒤에 한다(F2의 새 이름으로 적기 위해서다).

| | 문서 | 할 일 |
|---|---|---|
| F3-1 | `README.md` | 컷 도구의 것으로 다시 쓴다. 옛 절(`motions.json` · `motions/2·3` · 런타임 규약 · 리그 덤프 · 맨 아래 "진행" 표)을 걷는다 |
| F3-2 | `docs/USER-GUIDE.md` | 지금 화면의 조작만 남긴다 |
| F3-3 | `docs/HANDOFF.md` · `docs/MAINTENANCE.md` | 옛 불변식(산출물 등가 · 무대 등가 견본 · 코어 반입)과 옛 저장소 표를 걷고, 지금의 구조와 불변식만 남긴다. `STATUS.md` · `CLAUDE.md`와 겹치는 것은 한 곳으로 모은다 |
| F3-4 | 옛 계획서(`PLAN.md` · `PLAN-M5` · `-maya` · `-controller` · `-scene` · `-inspector` · `-parts` · `-set` · `-undo` · `maya-comparison.md`) | 지금의 툴에 남아 있는 결정과 사라진 기능의 기록을 갈라 목록으로 보고한다. 지울지 · 줄일지 · 둘지는 소유자가 정한다 |

완료 기준에 더하는 것: 문서 안의 파일 경로와 타입 이름이 실제로 있는지 검색으로 맞춘다.

### F4 — 구조를 훑는다

**걷기 전에 목록부터 보고한다.** 찾는 것:

- 걷어 낸 뒤에 남은 빈 층(넘겨주기만 하는 타입 · 폴더)
- 한 군데서만 쓰는 추상
- 쓰이지 않는 설정과 공개 멤버
- 옛 형식을 읽어 주는 호환 코드(예: 옛 문서의 속성을 읽을 때 무시하는 곳)
- 테스트만 부르는 코드

목록의 각 항목에는 어디에 있는지 · 왜 불필요해 보이는지 · 걷으면 무엇이 달라지는지 · 권고를 적는다. 승인받은 것을 한 번에 하나씩 걷는다(항목 하나에 커밋 하나).

이미 보이는 것:

- 코어의 `RectNodeState.Centered` · `WithAnchors` · `WithPivot` · `WithSizeDelta`, `RectNodeTree.BuildChainTo` · `TryGetState` — 테스트만 부른다.
- `.github/workflows/test.yml`과 `MAINTENANCE.md`의 CI 설명이 지금의 테스트 구성과 맞는지.
- `Mt.Cli` — 컷 내보내기가 없다(`STATUS.md` §6). 지금 무엇을 하는지, 남길 까닭이 있는지.

#### F4 목록 (2026-11-11, **승인됨** — 권고대로, 소유자 "진행.")

코드를 훑어 찾은 것. 각 항목은 승인받으면 커밋 하나로 걷는다(완료 기준은 §3 공통). "쓰임"은 정의 밖에서 부르는 곳.

**가. 안 쓰는 것 — 작고 확실하다(권고: 걷는다)**

| | 무엇 | 어디 | 걷으면 |
|---|---|---|---|
| 가1 | 어디서도 안 부르는 화면 손잡이 | `MainWindow`: `EventRowCount` · `HideObjectForTurnFromMenu` · `OpenSettingsFromMenu` · `OpenShortcutsFromMenu` · `RangeSlider` · `SettingsWindow` · `ShortcutsWindow` · `SharedFlags` / `DopeSheetView.IsCollapsed` · `GraphEditorView.CenterCurrentTime` · `ClearCurveSelection` | 기능은 그대로. 손잡이만 나간다(설정·단축키 창은 헤드리스에서 열 수 없어 테스트가 없다) |
| 가2 | 테스트가 없는 화면 기능의 손잡이 | `KeyCutDepthFromForm`(깊이의 키) · `AddEventKeyFromForm`(이벤트 추가) | **걷지 않고 헤드리스 테스트를 붙인다**(관습: 화면의 기능에는 손잡이와 테스트) |
| 가3 | 안 쓰는 도메인·코어 멤버 | `TimeText.ToSeconds` · `RectNodeTree.BuildChainTo`(쓰임 0) / `RectNodeState.Centered` · `WithAnchors` · `WithPivot` · `WithSizeDelta` · `RectNodeTree.TryGetState` · `DocumentEditor.RemoveKey` · `DopeSheetRows.Covers` · `TimeText.FromSeconds`(테스트만 부른다) | 그 멤버와 그것만 보는 테스트가 나간다 |
| 가4 | 호환 코드 | `PlaybackClock.Loop`("호환용" bool — `Mode`와 겹친다) · `AuthoringDocumentJsonTests.옛_문서의_걷어_낸_속성은_읽을_때_무시된다`(옛 판을 안 읽는 지금은 뜻이 없다) · 타임라인 목록의 "옛 문서" 주석 | 뷰포트는 `Mode`를 쓴다 |

**나. 구조 — 권고와 함께 정해 주셔야 하는 것**

| | 무엇 | 지금 | 권고 |
|---|---|---|---|
| 나1 | **깊이 커브를 파일에서 코드로** | 유니티 덤프 모양 그대로의 JSON(`{"MonoBehaviour": {"level": …}}`, 5.5KB)을 어셈블리에 싣고(`Defaults/ExportedTuning/presets/depth.json`) `BuiltinTuning` → `DepthTuningFile` → 코어 `DepthTuningDto`로 읽는다. 쓰는 것은 키 셋의 `scaleCurve` 하나(`yCurve` · 프리셋 · 보존 값은 안 쓴다). 재생기(`CutPlayer.Evaluate`)는 커브를 인자로 받고, 없으면 크기 1로 떨어지는 길이 곳곳에 있다 | **걷는다** — 커브 키를 코어 `CutMath`에 상수로 두고, 파일 · 리소스 · DTO · 로더 · 테스트 견본(`Fixtures/presets/depth.json`) · nullable 경로를 없앤다. 컷 파일의 형식은 그대로이고, 재생에 컷 파일 하나면 된다("깊이 커브를 넘기는 약속"이 필요 없어진다) |
| 나2 | **계층 DTO 층** | 세트(`SetAsset` — 가운데 기준의 x · y · 너비 · 높이 · 회전 · 배율) → `SetCodec.ToDto` → 코어 `HierarchyDto`(덤프 때의 모양: 앵커 · 피벗 · sizeDelta를 `Float2Dto`/`Float3Dto`로) → `HierarchyLoader` → `RectNodeTree`. 파일로 읽고 쓰는 일이 없는데 직렬화용 DTO가 메모리 속 중간 단계로 남았다. 뷰포트 · 컷 무대 · 검증 · 하이어아키가 이 DTO를 읽는다 | **걷는다 — 둘로 나눠서.** ① 코어의 `Hierarchy*` · `Float*Dto`를 도메인으로 내리고(코어는 `Cut/` · 사각형 계층 · 곡선만) ② DTO를 "펼친 오브젝트" 레코드로 바꿔 거기서 바로 트리를 세운다(`BuiltSets`가 그것을 든다). 크다 — 뷰포트와 컷 무대를 지난다 |
| 나3 | **코어 테스트의 NUnit** | 코어 테스트(60개)만 NUnit, 나머지는 xunit. 출발점이 유니티 테스트 러너였던 까닭 | **xunit으로 합친다** — 테스트 프레임워크 하나 |
| 나4 | **코어의 컴파일 제약**(netstandard2.1 · C# 9 · Nullable 끔) | 유니티가 가져갈 수 있게 둔 안전핀 | **둔다** — 비용이 작고, 컷 파일을 읽는 쪽을 언젠가 떼어 갈 때의 자리다. 런타임과의 관계를 다시 열 때 정한다 |
| 나5 | **돕는 손**(`ValidationFixes`) | 고칠 손이 하나(이름 고치기)이고, 문제의 문구 앞머리로 고른다 | **둔다** — 작다. 손이 늘면 그때 문제에 종류를 단다 |
| 나6 | **남은 옛 이름 · 자리** | 세트의 이름을 든 변수가 `kind`인 곳(채널 종류의 `kind`와 섞인다) · 견본 `Fixtures.BuiltOf` · `BaseResolutionFile`(파일이 아니라 상수 하나) · `Sets/` 폴더에 깊이 커브 · 무대 크기 · 층 표가 섞임 | **고친다** — 나1 · 나2 뒤에(그때 자리가 정해진다) |
| 나7 | 그릇의 이름(`Ked.Presentation.Core` · `Mt.*` · `ked-motion-tool`) · 큰 파일(`ViewportView` 2,581줄 · `MainWindow.axaml.cs` 2,528줄 · `GraphEditorView` 2,193줄) | — | **지금은 둔다** — 이름은 F2-0에서 정한 대로 툴의 의도가 확정될 때. 큰 파일 나누기는 걷어 내기가 아니라 고쳐 짓기다 |

**권고하는 차례**: 가1 → 가2 → 가3 → 가4 → 나1 → 나2① → 나2② → 나3 → 나6. 가는 한 커밋에 묶어도 된다(작다).

**진행.**
- [x] **가1 · 가2**(`464f6eb`) — 안 부르는 화면 손잡이와 연결된 곳 없는 그래프 기능(`CenterCurrentTime` · `ClearCurveSelection`)을 걷고, 테스트가 없던 화면 기능 셋(깊이의 키 · 이벤트 추가와 줄 · 제 차례 전까지 숨기기)에 헤드리스 테스트를 붙였다.
  `EventRowCount` · `HideObjectForTurnFromMenu`는 걷지 않고 가2로 옮겼다(테스트가 그것을 쓴다).
- [x] **가3 · 가4**(`796cf47`) — `RectNodeTree.TryGetState` · `BuildChainTo` · `DocumentEditor.RemoveKey`(테스트는 `RemoveKeys`로) · `DopeRow.Covers` · `TimeText.ToSeconds` · `FromSeconds` · `PlaybackClock.Loop`/`ViewportView.Loop`(호환용 — `Mode`를 쓴다)를 걷었다.
  - **철회**: `RectSpace.Centered` · `RectNodeState.WithAnchors` · `WithPivot` · `WithSizeDelta`는 남긴다 — 코어 테스트가 사각형 계산(앵커 · 피벗)을 검증하려고 값을 짓는 도구다. "테스트만 부른다"는 걷을 까닭이 못 된다.
  - 옛 속성 테스트(`옛_문서의_걷어_낸_속성은_…`)는 F2-2에서 옛 판 테스트와 함께 이미 나가 있었다.
- [x] **나1 깊이 커브를 코드로**(`2f1aa72`) — 크기 커브의 키 셋을 코어 `CutMath`의 상수로 두고, 내장 JSON(`Defaults/ExportedTuning/presets/depth.json`, 유니티 덤프 모양) ·
  `BuiltinTuning` · `DepthTuningFile` · 코어 `DepthTuningDto`(커브 DTO · 쓰지 않던 세로 커브) · 테스트 견본(`Fixtures/presets/depth.json`과 `Fixtures.PathOf`) · 뷰포트의 `SetDepth` · 세션의 `Depth`를 없앴다.
  `CutPlayer.Evaluate(cut, frame)` · `CutStage.Apply(tree, cut, prefix)` · `SizeAt(level)`은 커브를 인자로 받지 않는다 — "커브가 없으면 크기 1"인 길이 사라졌다. 앱이 그리는 모습은 그대로다(전부터 내장 커브를 썼다).
  컷 파일의 형식은 그대로이고, **재생에 컷 파일 하나면 된다**(STATUS §6의 "깊이 커브를 넘기는 약속"이 필요 없어졌다).
  카메라만 따로 보던 테스트는 "커브 없음"을 전제로 했다 — 기대값에 그 깊이의 크기를 곱하게 고쳤다.
- [x] **나2 계층 DTO 층**(`e2cc525`) — ①(코어에서 도메인으로 내리기)과 ②(펼친 오브젝트로 바꾸기)를 **한 번에** 했다: ②가 끝나면 코어의 DTO는 남지 않으므로 ①을 따로 거칠 까닭이 없었다.
  - 코어의 `HierarchyFileDto` · `HierarchyDto` · `HierarchyNodeDto` · `Float2Dto`/`Float3Dto` · `HierarchyLoader`와 그 테스트(13개)를 없앴다. `Tuning/`에는 깊이의 눈금만 남아 `Cut/`로 옮기고 폴더를 없앴다.
  - 도메인에 `ExpandedObject`(경로 이름 · 부모 · 자리 · 크기 · 배율 · 회전 · 불투명도 · 그림 · 색)를 두고, `SetCodec.Expand`(세트 → 펼친 오브젝트) · `WithPoints` · `BuildTree`(→ 코어 트리)로 바로 세운다.
    모든 오브젝트가 가운데 앵커 · 피벗이라 앵커 · 피벗 · `sizeDelta`를 따로 적을 까닭이 없다.
  - **루트(`__root`)는 목록에 없다** — 트리를 세울 때만 붙는다. 그래서 하이어아키 · 뷰포트 · 컷 파일 굽기 · 테스트에서 "루트는 건너뛴다"가 다 사라졌다.
  - `BuiltSets`는 `StageSize`와 이름별 펼친 오브젝트를 든다(`TryGetObjects`). 없는 이름의 `BuildTree`는 던진다.
  - 로더 테스트가 지키던 규칙 가운데 "부모가 앞에 없으면 세우지 못한다"는 `BuiltSetsTests`로 옮겼다. 화면은 렌더로 보아 바꾸기 전과 같다.
- [x] **나3 NUnit → xunit**(`ff6da55`) — 코어 테스트(46개) 셋을 xunit으로 옮겼다(단언은 변환기로 기계적으로 — `Assert.That(…, Is.EqualTo(…).Within(Eps))` → `Assert.Equal(…, …, Eps)` 등).
  테스트 프레임워크가 하나가 됐고 `Directory.Packages.props`에서 NUnit 판을 걷었다. 유니티 시절의 폴더 `EditMode/`도 걷었다. 코어 테스트 프로젝트는 Nullable만 끈다(코어를 null로 부르는 시험이 있다).
- [x] **나6 남은 옛 이름 · 자리**(이 커밋)
  - 세트의 이름을 든 `kind`(약 60곳)를 `setName`으로 — `string kind` 선언(매개변수 · 지역 · 패턴 변수)의 범위 안에서만 바꿨다. 채널 종류의 `kind`(`ChannelKind`)는 그대로다.
    한 메서드(`ThroughInstance`)에는 세트 이름이 둘이라 뒤의 것을 `extracted`로.
  - `BaseResolutionFile.Default`(파일이 아니라 상수 하나) → `Stage.Size`. 견본 `Fixtures.SetOf`(옛 `RigOf`) → `Fixtures.BuiltOf`.
  - `Sets/` 폴더는 이제 세운 한 벌 · 세우기 · 조각 · 층 표 · 무대 크기 — 깊이 커브가 나가서 자리가 맞는다. 코어의 `Tuning/`은 나2에서 없앴다.
- **나4 · 나5 · 나7은 권고대로 두었다**(코어의 컴파일 제약 · 돕는 손 · 그릇의 이름과 큰 파일).

**F4 끝(2026-11-11).** 테스트 299개(Core 46 · Authoring 170 · App 83). 코어는 `Cut/` · `Ease/` · `Primitives/` · `State/` · `Transforms/`만 남았다.

## 4. 차례의 까닭

- **이름(F2)이 문서(F3)보다 먼저** — 문서를 먼저 고치면 이름을 바꾼 뒤 한 번 더 고쳐야 한다.
- **구조 훑기(F4)가 맨 뒤** — 이름이 제 뜻을 가진 뒤라야 "이 층이 무엇을 하는가"가 보인다. 다만 F2 · F3을 하다 눈에 띈 것은 그때그때 F4의 목록에 적어 둔다(바로 걷지 않는다).

## 5. 보고

단계마다 **된 것 · 정한 것 · 아직 불편한 것 · 다음**을 짧게. 소유자가 이미 답한 것을 다시 묻지 않고, 권고가 틀렸으면 바로 철회해 여기에 적는다.

## 6. 기록

- **F0(2026-11-11, `8c9b912`)** — `STATUS.md` §1 · §2 · §6 · §7, `PLAN-standalone.md`의 "가져다 쓴다" 철회, `CLAUDE.md`의 "사본" 표현.
- **F1(2026-11-11, `21cebdb`)** — 코어 6,283줄 → 1,460줄, 코어 테스트 414 → 60개. 자세한 것은 `PLAN-standalone.md`의 C7 (다)-2.
  - 정한 것: 이름 붙은 이징(`EaseFunctions` 등)도 걷었다 — "남길 것"에 적혀 있었지만 어디서도 부르지 않았다. `MotionChannelKind`는 도메인으로 옮겼다.
- **F2(2026-11-11)** — 옛 이름을 고쳤다. 동작은 바꾸지 않았다. 테스트 310개(Core 60 · Authoring 169 · App 81).
  - F2-1 코어: `RigSchema*` → `Hierarchy*`. 덤프 때의 필드(`sourcePrefab` · `measuredRectSize` · `canvasGroupAlpha`)를 없앴다.
  - F2-2 도메인 · 화면 · 테스트의 식별자: 대응표대로. 저작본은 `cut-authoring/1` · `.cut-authoring.json`이고 키는 새 이름이다(`timelines` · `sets` · `objects` · `object` · `imageKeys`).
    옛 판(`motion-authoring/1`)을 읽던 길과 그 테스트, 상수만 보던 `AuthoringFormatTests`를 없앴다.
  - F2-3 화면 문구와 주석: "모션" → "타임라인"(세트의 것을 만들고 지우는 단추는 "애니메이션 추가/삭제"), 화면의 "노드" → "오브젝트", 주석의 "리그" → "세트"/"계층". 명령줄은 `pose --timeline`, 출력의 `nodes` → `objects`.
  - 대응표에서 달라진 것:
    - **"세트만"과 "세트나 컷"을 이름으로 갈랐다** — 옛 `Rig`는 둘을 아울렀고 옛 `Set`은 세트만이었다. `FindSetOrCut` · `AllSetsAndCuts` · `RequireSetOrCut`(아우르는 것)과 `Sets` · `RequireSet`(세트만).
    - 세션과 뷰포트가 드는 "세운 한 벌"은 `Built`(`DocumentSession.Built` · `ViewportView.SetBuilt`).
    - 화면 쪽의 `SetNode`(세트의 오브젝트)도 `Object`로(`SelectObject` · `RefreshObjectPanel` …).
    - 새 애니메이션의 기본 이름은 `timeline` · `timeline_2`….
  - 남은 것(F4의 목록으로): 세트의 이름을 든 변수가 아직 `kind`인 곳이 많다. 코어의 `HierarchyDto` 겹이 필요한지. 그릇의 이름(`Mt.*` · `ked-motion-tool`).
- **F3(2026-11-11)** — 문서를 지금의 툴에 맞췄다.
  - F3-1·2: README는 짧은 입구(무엇 · 낱말 · 실행 · 구조 · 파일 · 문서 지도)로, 화면 조작 전부는 `USER-GUIDE.md` 한 곳으로 모았다(둘이 같은 표를 따로 들고 있었다).
    지금 화면과 다른 것(재생 기준 · 종료 처리 · 상대값 · restore/commit · `motions/3` · 옛 문서 읽기 · 중립값 알림)을 고쳤다. 화면에 나가는 오류 문구의 "노드"도 "오브젝트"로(F2-3에서 도메인 쪽을 놓쳤다).
  - F3-3: `HANDOFF.md`를 걷었다 — 아직 맞는 결정은 `MAINTENANCE.md` §1로. `MAINTENANCE.md`는 지금 이름의 코드 지도와 지금의 불변식으로 다시 썼다.
  - F3-4: 옛 계획서는 **소유자의 "과감하게 정리하며 진행"(2026-11-11)에 따라 목록 보고 없이 걷었다** — 지금의 툴에 없는 것의 기록이다.
    - 걷은 것: `PLAN.md`(캐릭터 모션 툴 · `motions/2` · M0~M6) · `PLAN-M5.md` · `PLAN-maya.md` · `maya-comparison.md`(M4 시점의 대조표) · `PLAN-controller.md`(컨트롤러) · `PLAN-scene.md`(씬) ·
      `PLAN-inspector.md` · `PLAN-parts.md`(이모지 · 리그 덤프 · `motions/3`) · `PLAN-undo.md`(옛 이름으로 적힌 지난 수리 — 정책은 `MAINTENANCE.md` §4의 2로 옮겼다).
    - 남긴 것: `PLAN-standalone.md`(컷의 정의 · 컷 파일 · 단계별 기록)와 `PLAN-set.md`(세트의 단계 S1~S11 — 코드 주석이 가리킨다. 머리에 "기록"임과 바뀐 낱말을 적었다).
    - 코드 주석의 걷은 계획서 참조를 지웠고, 남은 "런타임의 …" 표현(깊이 · 카메라 · 이벤트)을 지금의 뜻으로 고쳤다. csproj의 옛 견본 설명(`motions.nod-bow.json` · `stage-parity/`)도.
  - 문서 안의 링크 · 경로 · 식별자가 실제로 있는지 검색으로 맞췄다(0건 어긋남).
  - F4의 목록에 더할 것(조사하다 본 것):
    - `AuthoringDocumentJsonTests.옛_문서의_걷어_낸_속성은_읽을_때_무시된다` — 옛 속성(`scenes` · `rigSchemas` …)이 든 문서를 읽는 호환 테스트. 옛 판을 안 읽는 지금은 뜻이 없다.
    - 내장 깊이 커브: `BuiltinTuning.Source`(런타임 커밋) · 폴더 `Defaults/ExportedTuning/` · 유니티 덤프 모양(`{"MonoBehaviour": {"level": …}}`)을 그대로 읽는 `DepthTuningFile`.
    - 세트의 이름을 든 변수가 `kind`인 곳 · 테스트 견본 `Fixtures.BuiltOf`(옛 `RigOf`).

