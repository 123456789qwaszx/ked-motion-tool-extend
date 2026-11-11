# ked-motion-tool

**컷**을 만드는 저작 도구(.NET 10 + Avalonia 12). 컷 = 씬(계층 구조의 오브젝트들) + 카메라 + 정해진 길이의 타임라인이다.
캐릭터·배경·소품이 전부 오브젝트이고, 그 기간의 움직임을 마야식 그래프 에디터 · 도프 시트 · 타임 슬라이더로 키를 잡는다. 산출물은 컷 파일(`cut/1`)이다.

장기 목표로 다듬는 독립된 툴이다 — 지금은 기능을 더하지 않고 기반을 다듬는 때다(`docs/PLAN-foundation.md`).

## 낱말

| 화면 | 코드 | 뜻 |
|---|---|---|
| 컷 | `Cut` | 씬 + 카메라 + 타임라인 하나. 툴의 단위이자 내보내는 단위 |
| 오브젝트 | `Object` | 계층 안의 사각형 하나 — 그림 · 색 사각형 · 묶기만 하는 점 · 세트의 인스턴스 |
| 타임라인 / 애니메이션 | `Timeline` | 키를 담는 것. 컷에는 하나("타임라인"), 세트에는 이름 붙은 것이 여럿("애니메이션") |
| 세트 | `Set` | 묶어서 다시 쓰는 오브젝트 계층. 컷이나 다른 세트에 인스턴스로 놓고, 그 애니메이션을 재생 블록으로 튼다 |
| 스킨 | `Skin` | 그림 오브젝트마다 어떤 그림을 그릴지의 묶음 |

## 실행 · 빌드 · 테스트

```bash
dotnet run --project src/Mt.App/Mt.App.csproj
```

```bash
dotnet build KedMotionTool.sln -c Release
```

```bash
dotnet test KedMotionTool.sln -c Release --no-build
```

.NET 10 SDK(`global.json`). 패키지 판은 `Directory.Packages.props` 한 곳에서 관리한다. 쓰는 법은 [`docs/USER-GUIDE.md`](docs/USER-GUIDE.md).

## 구조

```
Ked.Presentation.Core  ──►  Mt.Authoring  ──►  Mt.App (Avalonia)
                                          └─►  Mt.Cli (mt)
```

| 프로젝트 | 역할 |
|---|---|
| `src/Ked.Presentation.Core` | 순수 C#(netstandard2.1 · C# 9). 컷 파일의 모양과 "이 프레임의 모습"을 내는 계산(`Cut/`) · 사각형 계층과 좌표 계산 · 곡선 · 깊이 커브. 툴의 미리보기와 컷 파일의 재생이 같은 함수를 쓴다. 출발점은 [`vendoring.md`](src/Ked.Presentation.Core/vendoring.md) |
| `src/Mt.Authoring` | 저작 도메인 — Avalonia를 모른다. `Model/`(저작 원본) · `Editing/DocumentEditor`(모든 편집의 통로, 스냅샷 되돌리기) · `Serialization/`(저작 원본 · 컷 파일) · `Validation/` · `Sets/`(문서의 세트·컷을 세운 한 벌) · `Preview/`(포즈 · 컷의 카메라와 깊이 · 재생 블록 굽기) · `Graph/`(그래프·도프 시트의 순수 산수) |
| `src/Mt.App` | 그리기와 입력. `MainWindow`(셸) · `Views/`(뷰포트 · 그래프 에디터 · 도프 시트 · 타임 슬라이더) · `Services/DocumentSession`(경로 · 세운 한 벌 · 저장 · 내보내기) |
| `src/Mt.Cli` | 명령줄 `mt` — 창 없이 검증 · 컷 내보내기 · 그 시각의 포즈 조회 |

테스트(전부 xunit): `tests/Ked.Presentation.Core.Tests`(코어) · `tests/Mt.Authoring.Tests` · `tests/Mt.App.Tests`(화면은 Avalonia.Headless).

## 파일

| 파일 | 형식 | 무엇 |
|---|---|---|
| `*.cut-authoring.json` | `cut-authoring/1` | 저작 원본 — 컷 · 세트 · 타임라인 · 스킨. 세트의 참조 · 재생 블록 · 탄젠트 모드처럼 컷 파일에 구워져 나가기 전의 것을 든다 |
| `*.cut.json` | `cut/1` | 컷 파일 — 컷 하나를 다 펼쳐 구운 한 덩어리(오브젝트 · 트랙 · 그림 트랙 · 이벤트, 시각은 프레임). 정의는 [`docs/PLAN-standalone.md`](docs/PLAN-standalone.md) §7 |

## 문서

| 문서 | 무엇 |
|---|---|
| [`docs/STATUS.md`](docs/STATUS.md) | 지금 어디까지 왔고 다음이 무엇인가 |
| [`docs/PLAN-foundation.md`](docs/PLAN-foundation.md) | 지금 하는 기반 다듬기의 단계와 기록 |
| [`docs/PLAN-standalone.md`](docs/PLAN-standalone.md) | 컷 도구의 방향 · 컷의 정의(§6) · 컷 파일(§7) |
| [`docs/USER-GUIDE.md`](docs/USER-GUIDE.md) | 쓰는 법 — 화면별 조작 전부 |
| [`docs/MAINTENANCE.md`](docs/MAINTENANCE.md) | 고치는 사람을 위한 것 — 불변식 · 어디에 무엇이 있나 · 자주 하는 작업 |
| [`CLAUDE.md`](CLAUDE.md) | 이 저장소에서 일하는 관습 |
