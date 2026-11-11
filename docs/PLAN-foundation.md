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
| F3 | 문서를 지금의 툴에 맞춘다 | 다음 |
| F4 | 구조를 훑어 불필요한 것을 찾고, 승인받은 것을 걷는다 | |

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
