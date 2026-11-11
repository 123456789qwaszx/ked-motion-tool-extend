using System;
using System.Collections.Generic;

namespace Ked.Presentation.Core
{
    /// <summary>컷의 한 프레임 — 오브젝트마다의 자리(트리)·불투명도·그림, 그리는 차례.</summary>
    public sealed class CutPose
    {
        /// <summary>오브젝트의 id가 곧 노드 키다. 무대의 가운데가 원점인 공간에 선다(카메라와 깊이가 들어간 모습).</summary>
        public RectNodeTree Tree;

        /// <summary>오브젝트 → 불투명도(0~1). 트랙이 없는 오브젝트는 들어 있지 않다(1).</summary>
        public Dictionary<string, float> Alpha = new Dictionary<string, float>();

        /// <summary>오브젝트 → 그 프레임에 그릴 그림. 그림이 없는 오브젝트는 들어 있지 않다.</summary>
        public Dictionary<string, string> Image = new Dictionary<string, string>();

        /// <summary>그리는 차례 — 먼 것부터(맨 위 오브젝트의 그 프레임의 깊이 순, 같은 깊이는 파일의 순서). 밑의 오브젝트는 제 맨 위 오브젝트를 따라간다.</summary>
        public List<string> DrawOrder = new List<string>();

        public CutCameraDto Camera;
    }

    // ─────────────────────────────────────────────────────────────────
    // 컷 재생 — "이 프레임에 각 오브젝트와 카메라가 어디 있는가".
    //
    // 저작 도구의 미리보기와 게임의 재생이 이 함수 하나를 쓴다 — 툴에서 본 것 = 게임에서 본 것.
    // 게임은 결과(CutPose)를 무대의 UI에 옮기기만 한다.
    //
    // 트리의 차례는 무대 뼈대와 같다: 당기기(전체 배율) › 밀기(전체 이동) › 오브젝트별 틀(층의 FramingTransform·FramingScale 자리) › 오브젝트.
    // 깊이의 크기는 오브젝트의 제 원점을 중심으로 곱한다(자리는 그대로 — 캐릭터 슬롯의 DepthScale과 같다).
    // ─────────────────────────────────────────────────────────────────
    public static class CutPlayer
    {
        public const string Format = "cut/1";

        /// <summary>카메라 트랙의 target.</summary>
        public const string CameraTarget = "@camera";

        // 트리 안의 틀 — 오브젝트의 id와 겹치지 않는다(id는 영문자로 시작한다).
        private const string ZoomKey = "~zoom";
        private const string PanKey = "~pan";
        private const string FramePrefix = "~frame/";

        /// <summary>그 프레임의 모습. depth는 깊이 커브(없으면 깊이에 따른 크기를 주지 않는다).</summary>
        public static CutPose Evaluate(CutFileDto cut, float frame, DepthLevelTuningDto depth)
        {
            if (cut == null)
                throw new ArgumentNullException(nameof(cut));

            if (cut.format != Format)
                throw new ArgumentException($"모르는 컷 형식이다 ('{cut.format}') — 이 재생기는 {Format}을 읽는다.", nameof(cut));

            if (cut.stage == null || cut.objects == null)
                throw new ArgumentException("컷에 stage나 objects가 없다. 파일이 비었거나 역직렬화가 실패했다.", nameof(cut));

            var pose = new CutPose();
            var tree = new RectNodeTree(new RectSpace(new Vec2(cut.stage.width, cut.stage.height), Vec2.Half));
            pose.Tree = tree;

            // 카메라 — 가만히 선 값 위에 트랙.
            var camera = new CutCameraDto();
            camera.panX = Value(cut, CameraTarget, "panX", frame, cut.camera != null ? cut.camera.panX : 0f);
            camera.panY = Value(cut, CameraTarget, "panY", frame, cut.camera != null ? cut.camera.panY : 0f);
            camera.zoom = Value(cut, CameraTarget, "zoom", frame, cut.camera != null ? cut.camera.zoom : 0f);
            pose.Camera = camera;
            float scale = CutMath.CameraScale(camera.zoom);

            tree.Add(ZoomKey, null, RectNodeState.StretchFull.WithLocalScale(new Vec3(scale, scale, 1f)));
            tree.Add(PanKey, ZoomKey, RectNodeState.StretchFull.WithAnchoredPosition(new Vec2(camera.panX, camera.panY)));

            var top = new Dictionary<string, string>();   // 오브젝트 → 그 맨 위 조상
            var depthOf = new Dictionary<string, float>(); // 맨 위 오브젝트 → 그 프레임의 깊이

            for (int n = 0; n < cut.objects.Count; n++)
            {
                CutObjectDto o = cut.objects[n];

                if (o == null || string.IsNullOrEmpty(o.id))
                    throw new ArgumentException($"컷 '{cut.name}'의 오브젝트 [{n}]에 id가 없다.", nameof(cut));

                bool isTop = string.IsNullOrEmpty(o.parent);
                float sx = Value(cut, o.id, "scaleX", frame, o.scaleX);
                float sy = Value(cut, o.id, "scaleY", frame, o.scaleY);
                string parentKey;

                if (isTop)
                {
                    float level = Value(cut, o.id, "depth", frame, o.depth);
                    depthOf[o.id] = level;
                    top[o.id] = o.id;

                    // 틀 — 그 깊이가 카메라에 더 따르는 만큼.
                    CutMath.ResponseAt(level, out float panResponse, out float zoomResponse);
                    float framing = (float)Math.Pow(scale, zoomResponse);
                    parentKey = FramePrefix + o.id;
                    tree.Add(parentKey, PanKey, RectNodeState.StretchFull
                        .WithAnchoredPosition(new Vec2(camera.panX * panResponse, 0f))
                        .WithLocalScale(new Vec3(framing, framing, 1f)));

                    // 깊이의 크기 — 제 원점을 중심으로.
                    float size = CutMath.SizeAt(level, depth);
                    sx *= size;
                    sy *= size;
                }
                else
                {
                    parentKey = o.parent;
                    top[o.id] = top.TryGetValue(o.parent, out string root) ? root : o.parent;
                }

                tree.Add(o.id, parentKey, new RectNodeState(
                    anchoredPosition: new Vec2(Value(cut, o.id, "x", frame, o.x), Value(cut, o.id, "y", frame, o.y)),
                    anchorMin: Vec2.Half,
                    anchorMax: Vec2.Half,
                    pivot: Vec2.Half,
                    sizeDelta: new Vec2(o.width, o.height),
                    localScale: new Vec3(sx, sy, 1f),
                    localEulerAngles: new Vec3(0f, 0f, Value(cut, o.id, "rotation", frame, o.rotation))));

                if (Find(cut, o.id, "alpha") != null)
                    pose.Alpha[o.id] = Value(cut, o.id, "alpha", frame, 1f);

                string image = ImageAt(cut, o.id, frame, o.image);

                if (!string.IsNullOrEmpty(image))
                    pose.Image[o.id] = image;
            }

            // 그리는 차례 — 깊이 순(안정 정렬: 같은 깊이는 파일의 순서).
            var order = new List<int>(cut.objects.Count);

            for (int n = 0; n < cut.objects.Count; n++)
                order.Add(n);

            order.Sort((a, b) =>
            {
                float da = depthOf.TryGetValue(top[cut.objects[a].id], out float x) ? x : CutMath.DefaultDepth;
                float db = depthOf.TryGetValue(top[cut.objects[b].id], out float y) ? y : CutMath.DefaultDepth;
                int byDepth = da.CompareTo(db);
                return byDepth != 0 ? byDepth : a.CompareTo(b);
            });

            for (int n = 0; n < order.Count; n++)
                pose.DrawOrder.Add(cut.objects[order[n]].id);

            return pose;
        }

        /// <summary>구간 (after, until] 프레임에 울리는 이벤트들.</summary>
        public static List<CutEventDto> EventsBetween(CutFileDto cut, float after, float until)
        {
            var fired = new List<CutEventDto>();

            if (cut == null || cut.events == null)
                return fired;

            for (int n = 0; n < cut.events.Count; n++)
            {
                CutEventDto e = cut.events[n];

                if (e != null && e.f > after && e.f <= until)
                    fired.Add(e);
            }

            return fired;
        }

        private static CutTrackDto Find(CutFileDto cut, string target, string what)
        {
            if (cut.tracks == null)
                return null;

            for (int n = 0; n < cut.tracks.Count; n++)
            {
                CutTrackDto track = cut.tracks[n];

                if (track != null && track.target == target && track.what == what)
                    return track;
            }

            return null;
        }

        /// <summary>트랙의 그 프레임의 값 — 트랙이 없으면 가만히 선 값. 키 앞뒤로는 끝의 키 값이 이어진다(CurveFunctions의 클램프).</summary>
        private static float Value(CutFileDto cut, string target, string what, float frame, float rest)
        {
            CutTrackDto track = Find(cut, target, what);

            if (track == null || track.keys == null || track.keys.Count == 0)
                return rest;

            var keys = new CurveKey[track.keys.Count];

            for (int n = 0; n < keys.Length; n++)
            {
                CutKeyDto key = track.keys[n];
                keys[n] = new CurveKey(key.f, key.v, key.i, key.o);
            }

            return CurveFunctions.Evaluate(keys, frame);
        }

        /// <summary>계단 키 — 그 프레임 이전의 마지막 그림 키. 없으면 오브젝트에 적힌 그림.</summary>
        private static string ImageAt(CutFileDto cut, string target, float frame, string rest)
        {
            if (cut.images == null)
                return rest;

            string image = rest;

            for (int n = 0; n < cut.images.Count; n++)
            {
                CutImageTrackDto track = cut.images[n];

                if (track == null || track.target != target || track.keys == null)
                    continue;

                for (int k = 0; k < track.keys.Count; k++)
                {
                    if (track.keys[k].f > frame + 1e-4f)
                        break;

                    image = track.keys[k].v;
                }
            }

            return image;
        }
    }
}
