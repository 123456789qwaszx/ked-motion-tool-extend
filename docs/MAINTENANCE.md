# ked-motion-tool 유지보수

코드를 고치는 사람을 위한 문서 — 정해진 것, 빌드·CI, 코드 지도, 깨면 안 되는 불변식, 자주 하는 작업, 테스트 요령, 함정.
쓰는 법은 [`USER-GUIDE.md`](USER-GUIDE.md), 지금 어디인가는 [`STATUS.md`](STATUS.md), 일하는 관습은 [`CLAUDE.md`](../CLAUDE.md). 문서와 코드가 다르면 코드와 테스트가 이긴다.

## 1. 정해진 것 (다시 논하지 않는다)

| 결정 | 뜻 | 근거 |
|---|---|---|
| 독립된 툴 | 게임 런타임과의 관계는 지금 다루지 않는다. 코어는 이 저장소의 것이고 런타임을 따라가지 않는다(소유자, 2026-11-11) | `PLAN-foundation.md` §1 · `src/Ked.Presentation.Core/vendoring.md` |
| 진실은 저작 원본 | `*.cut-authoring.json`이 주인이고 컷 파일은 거기서 굽는 산출물이다. 다른 판은 읽지 않는다 — 마이그레이션을 두지 않는다 | `PLAN-foundation.md` F2 |
| 구조는 툴이 정의한다 | 앱이 아는 것은 문서가 정의한 컷과 세트뿐이다(Spine처럼 툴이 원본) | `PLAN-standalone.md` §6 |
| 보이는 것 = 재생하는 것 | 컷의 미리보기와 컷 파일의 재생은 코어 `Cut/CutPlayer`의 같은 계산이다 | `PLAN-standalone.md` §7 |
| 조작 하나 = 되돌리기 한 칸 | 키 없는 포즈도 이력에 든다. 무변경 조작은 기록하지 않는다 | §4의 2 |
| 단축키는 마야 | 전역 단축키 · 그래프 조작 · 타임 슬라이더 전부 마야 관습. 그림자 버튼은 두지 않는다. 오른쪽 패널은 탭 셋(채널 박스 · 오브젝트 · 타임라인)이고 설정 · 단축키는 창 | `USER-GUIDE.md` |
| 안 쓰는 기능은 흔적 없이 | 모델 필드 · 편집기 명령 · 직렬화 키 · 화면 · 테스트 · 문서를 한 커밋에서 다 뺀다 | `CLAUDE.md` |

## 2. 빌드 · 테스트 · CI

```bash
dotnet build KedMotionTool.sln -c Release
```

```bash
dotnet test KedMotionTool.sln -c Release --no-build
```

- SDK는 `global.json`(10.0.100, latestFeature). 패키지 판은 `Directory.Packages.props` 한 곳(Avalonia 12.1.1).
- `Directory.Build.props`: ImplicitUsings · Nullable · **TreatWarningsAsErrors** · LangVersion latest. 코어만 자기 csproj에서 LangVersion 9 · netstandard2.1 · ImplicitUsings/Nullable 끔으로 되돌린다.
- CI: `.github/workflows/test.yml` — `main` · `dev` 푸시와 PR, ubuntu-latest, restore → build → test. Windows 패키지 스모크는 없다.
- `dotnet test`의 요약 줄이 **셋**(세 프로젝트) 나오는지 센다 — 빌드에 실패한 프로젝트는 조용히 건너뛴다.
- 화면을 그림으로 뽑는다(헤드리스가 Skia로 실제 렌더): `MT_RENDER_DIR=<폴더> dotnet test tests/Mt.App.Tests -c Release --filter CutShots`.

## 3. 코드 지도 — 어디를 열면 되나

| 하고 싶은 것 | 자리 |
|---|---|
| 컷 파일의 모양 · 이 프레임의 모습 · 깊이와 카메라의 계산 | 코어 `Cut/CutFileDto.cs` · `CutPlayer.cs` · `CutMath.cs` |
| 사각형 계층과 좌표 · 곡선 · 깊이의 눈금과 커브 | 코어 `State/RectNodeTree.cs` · `Transforms/` · `Ease/CurveFunctions.cs` · `Cut/DepthLevelLabels.cs` · `Cut/CutMath.cs`(깊이 커브는 상수) |
| 문서 · 타임라인 · 채널 · 키, 컷 · 세트 · 오브젝트 · 스킨 | `Mt.Authoring/Model/AuthoringDocument.cs` · `CutAsset.cs` · `SetAsset.cs` · `SkinAsset.cs` |
| 편집 명령 전부(되돌리기 포함) | `Mt.Authoring/Editing/DocumentEditor.cs` — `Mutate` · `Batch` · `RecordExternal` · `Undo`/`Redo`. 영역별로 `DocumentEditor.*.cs`(컷 · 세트 · 스킨 · 재생 블록 · 그림 키 · 이벤트 · 빼기 · 층 표 · 시차 · 화면 밖 · 숨기기) |
| 저작 원본 · 컷 파일 읽고 쓰기 | `Serialization/AuthoringDocumentJson.cs` · `CutFileJson.cs` |
| 규칙 · 알림 · 돕는 손 | `Validation/DocumentValidator.cs` · `ValidationFixes.cs` |
| 문서의 세트·컷을 세운 한 벌 · 무대 크기 | `Sets/SetBuilder.cs` · `BuiltSets.cs` · `BaseResolutionFile.cs`. 세트를 펼친 오브젝트(`ExpandedObject`)와 트리 세우기는 `Model/SetAsset.cs`의 `SetCodec` |
| 컷을 무대에 세우기(오브젝트 → 포즈 → 카메라와 깊이) | `Preview/CutStage.cs` |
| 시각의 포즈 · 재생 블록 굽기 · 포즈 → 키 값 | `Preview/TimelinePose.cs` · `TimelineFlatten.cs` · `PoseEdit.cs` |
| 그림 키 · 이벤트 키 읽기 · 재생 시계 · 사각형의 네 모서리 | `Preview/ImageKeys.cs` · `EventKeys.cs` · `PlaybackClock.cs` · `StageQuads.cs` |
| 그래프 · 도프 시트 · 타임 슬라이더의 순수 산수 | `Graph/` — `GraphCamera` · `GraphHitTest` · `TangentMath` · `HandleMath` · `KeyOps` · `KeySelection` · `KeyClipboard` · `TimeSnap` · `TimeText` · `TimeSliderOps` · `DopeSheetRows` · `CurveSampler` · `RelativeInput` |
| 설정 파일 | `Settings/ToolSettings.cs` |
| 셸(목록 · 하이어아키 · 인스펙터 탭 · 툴바 · 전역 단축키 · 문제 칸) | `Mt.App/MainWindow.axaml.cs`(큰 파일 — 영역 주석으로 나뉜다)와 `MainWindow.*.cs`(컷 · 세트 · 스킨 · 재생 블록 · 그림 키 · 이벤트 · 불투명도) |
| 뷰포트 · 그래프 에디터 · 도프 시트 · 타임 슬라이더 · 레인지 | `Mt.App/Views/` |
| 문서 경로 · 세운 한 벌 · 저장 · 내보내기 · 자동 저장 | `Mt.App/Services/DocumentSession.cs` |
| 명령줄 | `Mt.Cli/MtCli.cs` |

## 4. 깨면 안 되는 불변식

1. **편집은 `DocumentEditor`만 지난다.** 뷰 · 세션은 모델을 직접 바꾸지 않는다. 새 편집 기능은 `DocumentEditor`에 메서드를 더하고(`Mutate` 또는 `Batch`), 뷰는 그것을 부른다. 드래그 중간값은 렌더만, 놓는 순간이 편집 하나.
2. **조작 하나 = 되돌리기 한 칸.** 여러 명령이 한 조작이면 `Batch`로 감싼다. 문서 밖 상태(키 없는 포즈 등)의 변화는 `RecordExternal(undo, redo)`로 같은 줄에 올린다. 무변경 조작은 기록하지 않는다(`Commit`이 JSON 전후를 비교). 던지는 명령은 흔적이 없다. 이력은 200칸.
   - 칸 = 문서 스냅샷(JSON) + 문서 밖 손(undo/redo 델리게이트). **키 없는 포즈**의 변화(끌기 · 값 입력으로 생김, Esc · 스크럽 · 선택 변경 · 재생으로 사라짐)도 전부 칸이고, S로 키한 것을 되돌리면 키 확정 직전의 포즈로 돌아온다.
   - 되돌리기 단축키는 셸 한 곳(`OnShellKeyDown`, 터널 · `PhysicalKey`)이 받는다 — 어디에 포커스가 있든 한 번만. 툴바 버튼도 같은 명령.
   - 글 칸은 한 번만 확정한다(`OnCommit`). 세운 한 벌(세트 · 컷의 구조, 스킨의 그림)이 바뀌는 되돌리기는 `DocumentSession`이 지문을 견줘 다시 세운다.
3. **보이는 것 = 재생하는 것.** 컷의 미리보기는 코어 `CutPlayer`와 같은 모습을 내야 한다 — `CutTests.툴의_미리보기와_코어의_컷_재생이_모든_프레임에서_같은_모습을_낸다`가 심판이다. 화면용 근사 수식을 두지 않는다.
4. **순서 규약.** 컷은 오브젝트를 세우고 → 타임라인의 포즈를 잡은 뒤 → **카메라와 깊이**를 넣는다(`CutStage.Apply`는 한 번만).
5. **세트만인가, 세트나 컷인가.** 컷의 오브젝트 계층도 세트와 같은 그릇(`CutAsset.AsSet`)이다. 둘을 아우르는 것은 이름에 드러낸다 — `FindSetOrCut` · `AllSetsAndCuts` · `RequireSetOrCut`. 세트만은 `Sets` · `RequireSet`.

## 5. 자주 하는 작업

- **라벨 · 문구 바꾸기** — 채널 종류의 화면 이름은 `Model/ChannelKindLabel.cs`. 바꾸면 `USER-GUIDE.md`도 같이.
- **전역 단축키 추가** — `MainWindow.OnShellKeyDown`(tunnel 핸들러, `PhysicalKey` 기준). 글 입력 칸(`e.Source is TextBox`)은 건너뛴다. 그래프 안에서 그래프 도구가 먼저인 키(Q/W/R)는 `InsideGraph` 검사. 단축키 창(`OpenShortcuts`)의 표와 `USER-GUIDE.md`를 같이 고친다.
- **기능을 지울 때** — §1의 "흔적 없이". 지운 뒤 `grep`으로 식별자와 문서 언급을 한 번 더 훑는다.
- **저작 원본의 모양을 바꿀 때** — 판(`AuthoringFormat.AuthoringSchema`)을 올리고 옛 판은 읽지 않는다. 컷 파일(`cut/1`)의 모양을 바꾸면 `PLAN-standalone.md` §7을 같이 고친다.

## 6. 테스트 요령

- **Authoring**(xunit): 견본은 `Fixtures.ActorSet()` · `ActorDocument()` · `PartsSetFixture`. 편집기로 지은 견본은 `CanUndo`가 이미 참이다(처음 기록이 비어 있다고 가정하지 말 것).
- **App**(Avalonia.Headless): `HeadlessUi.Run(() => …)`, 견본은 `TestDocuments.Nod()` · `OpenActor(window)`. 입력은 `window.MouseDown/Move/Up` · `KeyPressQwerty` · `KeyTextInput`, 좌표는 `view.TranslatePoint(local, window)`. 포즈 · 기즈모는 `view.GizmoCenter()` · `ShownQuad()` · `CurrentPose()` 손잡이로 읽는다. 화면의 기능에는 `internal … FromForm()` 손잡이를 붙인다.
- **헤드리스의 한계**: 접힌 Expander · 안 고른 탭 안의 컨트롤은 포커스를 못 받는다(늘 보이는 칸을 쓴다) · 두 번째 창(설정 · 단축키)은 세션이 멈춘다(열지 않는다) · 재생 시계는 실제 시간이라 `Assert.InRange`로 느슨하게.
- **되돌리기 테스트**는 `editor.Revision` · `CanUndo` · `HasPending`로 칸 수를 센다. "조작 하나 = 한 칸"을 깨는 변경은 `MainWindowTests.복합_조작은_되돌리기_한_칸이다_…`가 잡는다.

## 7. 함정

| 함정 | 메모 |
|---|---|
| `CutStage` 순서 | `Build` → 포즈 → `Apply`(카메라 · 깊이). `Apply`를 두 번 넣으면 깊이의 배율이 두 번 곱해진다 |
| 원점이 그림 밖인 오브젝트 | 뷰포트 `GizmoAnchor`가 손잡이를 그림 가운데로 옮긴다 |
| 그래프 팬은 RMB만 | MMB 드래그는 선택 키 이동(마야). 뷰포트는 MMB · RMB 둘 다 팬 |
| 시간 스냅 기본 1/24 | 테스트에는 `SnapStep`을 1/60으로 두고 기대값을 쓰는 곳이 있다 |
| 세운 한 벌의 동기화 | `DocumentSession`은 편집기 Changed에서 지문(문서의 세트 · 컷의 구조, 스킨의 그림)이 바뀐 때만 다시 세운다. 키 편집은 다시 세우지 않는다 |
| 글 칸 확정 | `OnCommit`: Enter는 확정 + 포커스를 뷰포트로, LostFocus는 글이 바뀐 때만 확정. 같은 값 재확정은 기록이 아니다 |
| 노드 키 | 뷰포트의 트리에서 오브젝트의 키는 `set/이름`(`ViewportView.SetObjectId`), 품은 세트의 안쪽은 `인스턴스.이름` |
