using System;
using System.Collections.Generic;

namespace Ked.Presentation.Core
{
    // ─────────────────────────────────────────────────────────────────
    // 오브젝트 계층을 적는 모양 — 무대의 크기와, 이름 붙은 계층들.
    // 트리로 세우는 해석은 HierarchyLoader 한 곳이다.
    // ─────────────────────────────────────────────────────────────────

    [Serializable]
    public sealed class HierarchyFileDto
    {
        /// <summary>무대(계층이 딛고 서는 공간)의 크기.</summary>
        public Float2Dto stageSize;

        public List<HierarchyDto> hierarchies;
    }

    [Serializable]
    public sealed class HierarchyDto
    {
        public string name;

        /// <summary>부모가 항상 자식보다 먼저 온다 — 순서대로 넣으면 트리가 선다.</summary>
        public List<HierarchyNodeDto> nodes;
    }

    [Serializable]
    public sealed class HierarchyNodeDto
    {
        public string id;

        /// <summary>비어 있으면 루트(__root) 직속.</summary>
        public string parent;

        public Float2Dto anchoredPosition;
        public Float2Dto anchorMin;
        public Float2Dto anchorMax;
        public Float2Dto pivot;
        public Float2Dto sizeDelta;
        public Float3Dto localScale;
        public Float3Dto localEulerAngles;

        /// <summary>불투명도를 받는가.</summary>
        public bool hasAlpha;

        /// <summary>그림을 그리는 노드인가.</summary>
        public bool hasImage;

        /// <summary>단색 사각형의 색("#RRGGBB" 또는 "#RRGGBBAA") — 노드의 rect를 이 색으로 채운다. 없거나 비면 색 노드가 아니다.</summary>
        public string fillColor;
    }

    [Serializable]
    public sealed class Float2Dto
    {
        public float x;
        public float y;

        public Vec2 ToVec2() => new(x, y);
    }

    [Serializable]
    public sealed class Float3Dto
    {
        public float x;
        public float y;
        public float z;

        public Vec3 ToVec3() => new(x, y, z);
    }
}
