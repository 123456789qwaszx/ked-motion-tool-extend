# ked-motion-tool 유지보수 핸드오프

코드를 고치는 사람을 위한 문서. 빌드·테스트·CI, 코드 지도, 지켜야 할 불변식, 자주 하는 작업의
절차, 테스트 작성 요령, 함정 목록. 사용법은 [`USER-GUIDE.md`](USER-GUIDE.md), 현황과 열린 항목은
[`HANDOFF.md`](HANDOFF.md).

## 1. 빌드 · 테스트 · CI

```
dotnet restore KedMotionTool.sln
dotnet build   KedMotionTool.sln --configuration Release      # TreatWarningsAsErrors — 경고 하나가 빌드를 깬다
dotnet test    KedMotionTool.sln --configuration Release --logger "console;verbosity=normal"
dotnet run     --project src/Mt.App/Mt.App.csproj
```

- SDK는 `global.json`(10.0.100, latestFeature). 패키지 판은 `Directory.Packages.props` 한 곳(Avalonia 12.1.1 — VnTool과 같은 판).
- `Directory.Build.props`: ImplicitUsings·Nullable·TreatWarningsAsErrors·LangVersion latest. 반입 코어만 자기 csproj에서 LangVersion 9 · netstandard2.1 · ImplicitUsings/Nullable 끔으로 되돌린다(유니티 호환).
- CI: `.github/workflows/test.yml` — `main`·`dev` 푸시와 PR, ubuntu-latest, restore → build → test. 테스트 프로젝트 셋(Core 414 · Authoring 183 · App 66건, 2026-10-29). Windows 패키지 스모크는 아직 없다.
- 로컬에 SDK를 못 깔면 CI가 유일한 검증이다. `dotnet test`의 요약 줄이 **셋**(세 프로젝트) 나오는지 세고 "전부 통과"라고 말한다 — 빌드에 실패한 프로젝트는 조용히 건너뛴다.

## 2. 코드 지도 — 어디를 열면 되나

| 하고 싶은 것 | 자리 |
|---|---|
| 모션·채널·키 모델, 컷·세트·스킨 모델 | `Mt.Authoring/Model/MotionDocument.cs` · `CutAsset.cs` · `RigAsset.cs` · `SkinAsset.cs` |
| 편집 명령 전부(undo 포함) | `Mt.Authoring/Editing/MotionEditor.cs` — `Mutate`·`Batch`·`RecordExternal`·`Undo/Redo`·`Replay` |
| 저작 원본 읽고 쓰기 | `Serialization/MotionDocumentJson.cs` (`motion-authoring/2`, 옛 `/1` 읽기) |
| `motions.json` 읽고 쓰기 | `Serialization/MotionsJson.cs` (`motions/2` 정규 직렬화·관용 읽기, 옛 `/1`) |
| 규칙·경고·돕는 손 | `Validation/MotionValidator.cs`(코어 `MotionRules`) · `MotionFixes.cs` · `MotionExport.cs` |
| 세운 리그(문서의 세트·컷)·내장 깊이 커브·기준 해상도 | `Rig/RigAssets.cs` · `RigSchemaFile.cs` · `BuiltinTuning.cs` · `DepthTuningFile.cs` · `BaseResolutionFile.cs` |
| 컷을 무대에 세우기(오브젝트 → 포즈 → 카메라와 깊이) | `Preview/CutStage.cs`(계산은 코어 `Cut/CutMath`) · 컷 파일은 `Serialization/CutFileJson.cs` |
| 시각의 포즈, 포즈 → 키 값 | `Preview/MotionPreview.cs` · `PoseEdit.cs`(`RootDeltaToLocal`·`AngleDelta`·`ScaleAlong`·`KeyValueFor`) |
| 노드의 사각형·축·캔버스 좌표 | `Preview/StageQuads.cs` |
| 재생 시계 | `Preview/PlaybackClock.cs` |
| 그래프 수학(순수) | `Graph/` — `GraphCamera`(좌표) · `GraphHitTest` · `TangentMath` · `HandleMath` · `KeyOps` · `KeySelection` · `KeyClipboard` · `TimeSnap`(DefaultStep 1/24) · `TimeText` · `TimeSliderOps` · `DopeSheetRows` · `CurveSampler` · `RelativeInput`(`+=3`) |
| 설정 파일 | `Settings/ToolSettings.cs` |
| 셸(모션 목록·하이어아키·인스펙터 탭·툴바·전역 단축키·설정/단축키 창·문제 칸) | `Mt.App/MainWindow.axaml.cs`(큰 파일 — 영역 주석으로 나뉜다) |
| 뷰포트(그리기·매니퓰레이터·키 없는 포즈·보기 이동) | `Mt.App/Views/ViewportView.cs` |
| 그래프 에디터 | `Mt.App/Views/GraphEditorView.cs` |
| 도프 시트 · 타임 슬라이더 · 레인지 | `Views/DopeSheetView.cs` · `TimeSliderView.cs` · `RangeSliderView.cs` |
| 문서 경로·자산 동기화·저장·내보내기·자동 저장 | `Mt.App/Services/DocumentSession.cs` |
| 내장 데이터 | `Mt.Authoring/Defaults/ExportedTuning/presets/depth.json`(깊이 커브의 사본, 원본 커밋은 `BuiltinTuning.Source`) |

## 3. 깨면 안 되는 불변식

1. **편집은 `MotionEditor`만 지난다.** 뷰·세션은 모델을 직접 바꾸지 않는다. 새 편집 기능은 `MotionEditor`에 메서드를 더하고(`Mutate` 또는 `Batch`), 뷰는 그것을 부른다. 드래그 중간값은 렌더만(`_overrides`), 놓는 순간이 편집 하나.
2. **조작 하나 = 되돌리기 한 칸.** 여러 명령이 한 조작이면 `Batch`로 감싼다. 문서 밖 상태(키 없는 포즈 등)의 변화는 `RecordExternal(undo, redo)`로 같은 줄에 올린다. 무변경 조작은 기록하지 않는다(`Commit`이 JSON 전후를 비교). 던지는 명령은 흔적이 없다. 자세한 정책과 테스트는 `PLAN-undo.md`.
3. **Core 사본(`src/Ked.Presentation.Core`)은 고치지 않는다.** 소비자 쪽에서 적응한다. 동기화 절차는 `src/Ked.Presentation.Core/vendoring.md`.
4. **산출물 등가.** `MotionsJson` 쓰기는 정규 직렬화(§7)이고 유니티 `MotionClipExporter`와 바이트까지 같아야 한다. `MotionsJsonTests.nod_bow를_편집기로_지어_내보내면_Unity_산출물과_바이트까지_같다`가 심판이다.
5. **보이는 것 = 재생하는 것.** 뷰포트 포즈는 코어 `MotionMath.Compose`·`StageState` 폴드 함수로만 만든다. 화면용 근사 수식을 두지 않는다.
6. **툴은 Yarn·커맨드를 모른다.** `Mt.Authoring`에 그 타입을 들이지 않는다.
7. **순서 규약.** 컷은 오브젝트를 세우고 → 타임라인의 포즈를 잡은 뒤 → **카메라와 깊이**를 넣는다(`CutStage.Apply`는 한 번만).

## 4. 자주 하는 작업

### 4.1 코어 재반입 (`ked-stage-runtime`이 바뀌었을 때)

1. `vendoring.md`의 절차대로 `diff -r`로 갈래를 가르고 저쪽 본으로 교체(`Tests/`·`*.meta`·`*.asmdef` 제외).
2. 순수 테스트만 `tests/Ked.Presentation.Core.Tests/EditMode/`로 복사(유니티·DOTween·Newtonsoft 참조는 제외).
3. 컴파일 적응은 `Mt.Authoring`·`Mt.App`에서. 규약이 바뀌었으면 `MotionsJson`·`MotionDocumentJson`·`AuthoringFormat`의 스키마 문자열과 골든 픽스처(`tests/Mt.Authoring.Tests/Fixtures/`)를 갱신한다.
4. 전체 빌드·테스트. `vendoring.md` 표의 원본 커밋·반입일 갱신.

### 4.2 라벨·문구 바꾸기

채널 종류 표시는 `Model/ChannelKindLabel.cs`, 재생 기준은 `MotionModeLabel.cs`. 종료 처리·재생 기준 콤보는 `MainWindow`의 모션 탭. 바꾸면 README·USER-GUIDE의 표도 같이.

### 4.3 전역 단축키 추가

`MainWindow.OnShellKeyDown`(tunnel 핸들러, `PhysicalKey` 기준). 글 입력 칸(`e.Source is TextBox`)은 건너뛴다. 그래프 안에서 그래프 도구가 먼저인 키(Q/W/R)는 `InsideGraph` 검사. 단축키 창(`OpenShortcuts`)의 표와 README·USER-GUIDE를 같이 고친다.

### 4.4 기능을 지울 때

소유자 방침은 "흔적 없이": 모델 필드 · 편집기 명령 · 직렬화 키 · 화면 · 테스트 · 문서를 한 커밋에서 다 뺀다. 마이그레이션은 두지 않는다(옛 저작본이 없다). 지운 뒤 `grep`으로 식별자와 문서 언급을 한 번 더 훑는다.

### 4.5 무대 등가 견본 바꾸기

`tests/Mt.Authoring.Tests/Fixtures/stage-parity/`의 `motions.json`(툴 산출물)·`poses.json`(포즈 표)은 **이쪽이 원본**이고
무대 저장소 `Assets/Ked.Presentation/Core/Tests/EditMode/UnityParity/ToolExport~/`가 사본이다(코어와 반대 방향).
고치면 `StageParityTests`를 녹색으로 만든 뒤 두 파일을 그대로 무대 쪽에 복사하고, 그쪽 테스트 머리 주석의 출처 커밋을 고친다.
`motions.json`은 바이트 비교 대상이라 손으로 고치지 않는다 — `StageParityTests.ParityDocument()`를 고치고 내보낸 결과로 바꾼다.

## 5. 테스트 작성 요령

- **Authoring**: xunit. 편집기 테스트는 `TestDocuments.Nod()` 같은 견본으로 시작한다 — 견본은 편집기로 지어서 `CanUndo`가 이미 참이다(처음 기록이 비어 있다고 가정하지 말 것). 새 문서는 기본 씬에 `s1`이 이미 있다.
- **App(Avalonia.Headless)**: `HeadlessUi.Run(() => …)`, 입력은 `window.MouseDown/Move/Up`·`KeyPressQwerty`·`KeyTextInput`, 좌표는 `view.TranslatePoint(local, window)`. 포즈·기즈모는 `view.GizmoCenter()`·`ObjectQuad()`·`CurrentPose()` 손잡이로 읽는다.
- 헤드리스의 한계: PNG를 디코딩·인코딩하지 못한다(그림은 자리표시 사각형으로 검증) · 접힌 Expander·안 고른 탭 안의 컨트롤은 포커스를 못 받는다(늘 보이는 `RangeStartBox` 같은 칸을 쓴다) · 두 번째 창(설정·단축키)은 세션이 멈춘다(열지 않는다, 08df523) · 재생 시계는 실제 시간이라 `Assert.InRange`로 느슨하게.
- 되돌리기 테스트는 `editor.Revision`·`CanUndo`·`HasPending`로 칸 수를 센다. "조작 하나 = 한 칸"을 깨는 변경은 `MainWindowTests.복합_조작은_되돌리기_한_칸이다`가 잡는다.
- 무대 등가(`StageParityTests`)는 포즈 표를 `1e-3` 안에서 비교한다. 시작값은 표의 `start`(0이 아닌 값 — relative와 absolute가 갈리게)이고, 길이를 지난 표본이 리듀서의 끝 상태다.
- 산출물 테스트는 바이트 비교다. 직렬화 순서·소수 표기(`R`, 정수는 소수점 없이)·`-0 → 0`·LF·끝 개행 하나를 지킨다.

## 6. 함정과 메모

| 함정 | 메모 |
|---|---|
| `CutStage` 순서 | `Build` → 포즈 → `Apply`(카메라·깊이). `Apply`를 두 번 넣으면 깊이의 배율이 두 번 곱해진다 |
| 원점이 그림 밖인 오브젝트 | 발 기준으로 도는 묶음처럼 원점이 그림에서 떨어져 있으면 뷰포트 `GizmoAnchor`가 손잡이를 그림 가운데로 옮긴다 |
| 그래프 팬은 RMB만 | MMB 드래그는 선택 키 이동(마야). 뷰포트는 MMB·RMB 둘 다 팬 |
| 시간 스냅 기본 1/24 | 테스트는 기대값을 1/60으로 쓰는 곳이 있어 `Show()`에서 `SnapStep = 1.0 / 60`을 둔다 |
| 자산 동기화 | `DocumentSession`은 편집기 Changed에서 자산 지문(문서의 세트·컷의 구조, 스킨의 그림)이 바뀐 때만 다시 읽는다. 키 편집은 다시 읽지 않는다 |
| 글 칸 확정 | `OnCommit`: Enter는 확정 + 포커스를 뷰포트로, LostFocus는 글이 바뀐 때만 확정. 같은 값 재확정은 기록이 아니다 |
| 옛 파일 | `motion-authoring/1`·`motions/1`은 읽힌다(채널 `mode` 무시). 쓰기는 언제나 `/2` |
| 커밋 메시지 | 모델 식별자를 저장소 산출물에 넣지 않는다. 한국어, "무엇을 왜" |

## 7. 문서 지도

| 문서 | 내용 |
|---|---|
| `README.md` | 기능 전부의 정본 요약(화면별 조작 표) |
| `docs/HANDOFF.md` · `USER-GUIDE.md` · `MAINTENANCE.md` | 인수인계 · 사용법 · 이 문서 |
| `docs/PLAN.md` | 결정·범위·아키텍처·데이터·마일스톤·v2·위험 |
| `docs/PLAN-M5.md` · `PLAN-maya.md` · `maya-comparison.md` | 사용감 단계, 마야 관습 대조 |
| `docs/PLAN-controller.md` · `PLAN-scene.md` · `PLAN-inspector.md` · `PLAN-undo.md` | 컨트롤러 · 씬/무대 · 인스펙터 · 되돌리기 결정판 |
| `src/Ked.Presentation.Core/vendoring.md` | 코어 반입 절차 |
