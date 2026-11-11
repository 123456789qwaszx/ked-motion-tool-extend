# ked-motion-tool-extend

**컷**을 만드는 저작 도구(.NET 10 + Avalonia 12). 컷 = 씬(계층 구조의 오브젝트들) + 카메라 + 정해진 길이의 타임라인 — 캐릭터·배경·소품이 전부 오브젝트이고, 그 기간의 움직임을 키프레임으로 잡는다(마야의 한 컷처럼). 산출물은 컷 파일(`cut/1`)이다.

**방향(소유자, 2026-11-11)**: 장기 목표인 독립된 툴이다. 게임 런타임(`ked-presentation-runtime`)과의 관계는 지금 다루지 않는다 — 런타임을 따라가지도, 고치지도 않는다. 지금은 기능 개발이 아니라 기반 시설을 다듬고 불필요한 아키텍처를 걷어 내는 때다.

- **먼저 읽을 것**: `docs/STATUS.md`(지금 어디까지 왔고 다음이 무엇인가) → `docs/PLAN-foundation.md`(지금 하는 기반 다듬기의 단계와 완료 기준) → `docs/PLAN-standalone.md`(방향 · 컷의 정의 §6 · 컷 파일 §7 · 단계 §8 · 단계별 기록).
- 일은 `dev`에서 한다. PR은 요청받았을 때만.
- 형제 저장소 — 서로 맞물리지 않는다. 여기서는 그쪽의 일을 하지 않는다:
  - `ked-motion-tool-characterRig`: 캐릭터와 이모지의 애니메이션(유니티의 스키마를 따른다)
  - `ked-motion-tool-screenTransition`: 그림 여러 장을 움직이는 화면 전환(카메라·깊이 없이)

## 구조

`Ked.Presentation.Core`(코어 — 이 저장소의 것. 순수 C#, netstandard2.1 · C# 9. 컷을 읽는 계산 `Cut/`이 여기 있다. 출발점은 런타임의 코어였다 — `src/Ked.Presentation.Core/vendoring.md`) → `Mt.Authoring`(도메인) → `Mt.App`(Avalonia 화면) + `Mt.Cli`.
테스트: `tests/Mt.Authoring.Tests` · `tests/Mt.App.Tests`(xunit — 화면은 `HeadlessUi.Run`) · `tests/Ked.Presentation.Core.Tests`(NUnit — 코어의 테스트).

## 명령

```bash
dotnet build KedMotionTool.sln -c Release
```

```bash
dotnet test KedMotionTool.sln -c Release --no-build
```

화면을 그림으로 뽑는다(헤드리스가 Skia로 실제 렌더 — 고친 화면은 눈으로 확인한다):

```bash
MT_RENDER_DIR=<폴더> dotnet test tests/Mt.App.Tests -c Release --filter CutShots
```

## 관습

- 코드 주석 · 문서 · 커밋 메시지는 **한국어**, 대화는 존댓말. 커밋은 "무엇을 왜" 한 줄 + 본문.
- **한 번에 하나.** 단계마다 전체 테스트가 통과한 채로 커밋하고 푸시한다. 계획서에 결정과 상태를 적는다.
- `TreatWarningsAsErrors` — 안 쓰는 필드·using·인자는 빌드가 막는다.
- 편집은 `MotionEditor`의 명령으로만. **조작 하나 = 되돌리기 한 칸**, 같은 값은 기록하지 않는다. 테스트가 `editor.Revision`으로 이것을 본다.
- **안 쓰는 기능은 흔적 없이 지운다** — 코드 · 주석 · 문서 · 테스트까지.
- 화면의 기능에는 테스트 손잡이(`internal … FromForm()`)와 헤드리스 테스트를 붙인다. 고친 화면은 렌더로 뽑아 눈으로 확인한다(헤드리스가 Skia로 실제 렌더한다 — `window.CaptureRenderedFrame()`).
- **툴의 미리보기와 게임의 재생은 같은 계산**이다 — 그 계산은 코어(`Ked.Presentation.Core/Cut/`)에 순수 함수로 두고, 둘이 같은 모습을 내는지 프레임마다 맞추는 테스트를 둔다(`CutTests.툴의_미리보기와_코어의_컷_재생이_…`).
- 코어는 이 저장소의 것이다 — 필요하면 고친다. 런타임의 뒤 변경을 반입하지 않는다.
- 큰 걷어 내기는 범위와 완료 기준(빌드 0 오류 · 전체 테스트 통과 · 잔재 검색 0건)을 적어 에이전트에게 맡기고, 결과는 **직접 다시 돌려 확인한 뒤** 커밋한다. 에이전트가 작업 중인 트리는 커밋하지 않는다.
