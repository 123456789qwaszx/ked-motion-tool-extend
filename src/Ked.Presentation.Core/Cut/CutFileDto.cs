using System;
using System.Collections.Generic;

namespace Ked.Presentation.Core
{
    // ─────────────────────────────────────────────────────────────────
    // 컷 파일(cut/1) — 저작 도구가 형태를 정하고 게임이 읽는다.
    //
    // 컷 = 씬(계층 구조의 오브젝트들) + 카메라 + 정해진 길이의 타임라인. 캐릭터·배경·소품이 전부 오브젝트이고
    // 그 기간의 움직임이 키프레임으로 다 잡혀 있다. 컷마다 새로 시작한다(앞 컷의 끝을 이어받지 않는다).
    //
    // 파일은 **다 펼쳐 구운 것**이다: 묶음(세트)은 오브젝트로 펼쳐져 있고(이름이 a.b), 묶음의 움직임은 키로 구워져 있다 —
    // 읽는 쪽은 오브젝트와 트랙만 알면 된다. 시각은 프레임(fps는 파일에 있다)이고 기울기도 프레임당 값이다.
    //
    // 모양은 유니티 JsonUtility가 그대로 읽게 public 필드 + [Serializable]이다(RigSchemaDto와 같은 규약).
    // 이 프레임의 모습을 내는 계산은 CutPlayer — 도구의 미리보기와 게임의 재생이 그 함수 하나를 쓴다.
    // ─────────────────────────────────────────────────────────────────

    [Serializable]
    public sealed class CutFileDto
    {
        /// <summary>형식 번호 — <see cref="CutPlayer.Format"/>. 모르는 번호면 읽는 쪽이 거절한다.</summary>
        public string format;

        public string name;

        /// <summary>길이(프레임).</summary>
        public int frames;

        public int fps;

        public CutStageDto stage;

        /// <summary>카메라의 가만히 선 값 — 카메라 트랙이 없는 동안의 값.</summary>
        public CutCameraDto camera;

        /// <summary>오브젝트들 — 부모가 자식보다 먼저 온다. 같은 깊이 안에서는 뒤의 것이 위에 그려진다.</summary>
        public List<CutObjectDto> objects;

        /// <summary>수의 트랙들 — 오브젝트의 자리·회전·배율·불투명도·깊이, 카메라의 밀기·당기기.</summary>
        public List<CutTrackDto> tracks;

        /// <summary>그림 트랙들 — 계단 키(그 프레임부터 그 그림).</summary>
        public List<CutImageTrackDto> images;

        public List<CutEventDto> events;
    }

    [Serializable]
    public sealed class CutStageDto
    {
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class CutCameraDto
    {
        public float panX;
        public float panY;
        public float zoom;
    }

    [Serializable]
    public sealed class CutObjectDto
    {
        public string id;

        /// <summary>부모 오브젝트의 id. 비면 맨 위 오브젝트다.</summary>
        public string parent;

        /// <summary>깊이(depthLevel) — 맨 위 오브젝트만 뜻이 있다. 밑의 오브젝트는 부모를 따른다.</summary>
        public float depth;

        /// <summary>가만히 선 자세 — 부모의 가운데에서의 자리(px, y는 위가 +) · 회전(도) · 배율 · 크기(px).</summary>
        public float x;
        public float y;
        public float rotation;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float width;
        public float height;

        /// <summary>그림(컷 파일 기준 경로). 비면 그림을 그리지 않는다.</summary>
        public string image;

        /// <summary>단색("#RRGGBB" 또는 "#RRGGBBAA"). 비면 색 사각형이 아니다. 그림과 함께 있으면 그림 뒤에 깔린다.</summary>
        public string color;
    }

    [Serializable]
    public sealed class CutTrackDto
    {
        /// <summary>오브젝트의 id. 카메라는 <see cref="CutPlayer.CameraTarget"/>.</summary>
        public string target;

        /// <summary>무엇을 — x · y · rotation · scaleX · scaleY · alpha · depth, 카메라는 panX · panY · zoom.</summary>
        public string what;

        /// <summary>프레임 오름차순. 첫 키 앞과 마지막 키 뒤로는 그 키의 값이 이어진다. 하나뿐이어도 된다.</summary>
        public List<CutKeyDto> keys;
    }

    [Serializable]
    public sealed class CutKeyDto
    {
        /// <summary>프레임.</summary>
        public float f;

        public float v;

        /// <summary>들어오는 · 나가는 기울기(프레임당 값) — 3차 Hermite. CurveFunctions의 규칙 그대로다.</summary>
        public float i;
        public float o;
    }

    [Serializable]
    public sealed class CutImageTrackDto
    {
        public string target;

        /// <summary>프레임 오름차순 — 그 프레임부터 그 그림. 첫 키 앞은 오브젝트에 적힌 그림이다.</summary>
        public List<CutImageKeyDto> keys;
    }

    [Serializable]
    public sealed class CutImageKeyDto
    {
        public float f;
        public string v;
    }

    [Serializable]
    public sealed class CutEventDto
    {
        public float f;
        public string name;
        public string value;
    }
}
