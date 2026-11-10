using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;

namespace Mt.Authoring.Tests;

/// <summary>
/// 예시 문서 <c>examples/transitions/station/station.motion-authoring.json</c> — 화면 이동 연출 하나를 세트로 지은 것(<c>PLAN-set.md</c> S6).
/// 층 표(<c>art/layers.tsv</c>)를 세트로 들이고, 등장·머묾·퇴장 애니메이션을 놓고, 화면에 서는 연출로 묶는다. 편집기의 명령으로만 짓는다.
/// </summary>
internal static class TransitionExamples
{
    public const string Station = "station";
    public static readonly Vec2 Stage = new(1920f, 1080f);

    private static KeyAsset K(float t, float v, TangentMode tangent = TangentMode.Auto) => new() { T = t, V = v, Tangent = tangent };

    private static KeyAsset F(float t, float v) => K(t, v, TangentMode.Flat);

    private static KeyAsset L(float t, float v) => K(t, v, TangentMode.Linear);

    private static void Curve(MotionEditor editor, string motion, string node, MotionChannelKind kind, Axis axis, params KeyAsset[] keys)
    {
        if (editor.Document.Find(motion)!.Channels.All(c => c.Node != node || c.Kind != kind))
        {
            editor.AddChannel(motion, node, kind);
        }

        editor.SetKeys(motion, node, kind, axis, keys);
    }

    private static void Scale(MotionEditor editor, string motion, string node, params KeyAsset[] keys)
    {
        Curve(editor, motion, node, MotionChannelKind.Scale, Axis.X, keys);
        Curve(editor, motion, node, MotionChannelKind.Scale, Axis.Y, keys.Select(k => K(k.T, k.V, k.Tangent)).ToArray());
    }

    /// <summary>길이 안에서 [from, to] 동안 a → b, 그 앞뒤로는 가만히 — 층마다 시차를 두고 들어오게.</summary>
    private static KeyAsset[] Ramp(float duration, float from, float to, float a, float b)
    {
        var keys = new List<KeyAsset>();

        if (from > 0f)
        {
            keys.Add(F(0f, a));
        }

        keys.Add(F(from, a));
        keys.Add(F(to, b));

        if (to < duration)
        {
            keys.Add(F(duration, b));
        }

        return keys.ToArray();
    }

    public static MotionEditor BuildStation(string layersPath)
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(new RigAsset { Kind = Station });
        editor.ImportLayers(Station, LayerSheet.Load(layersPath), "art", Station, Stage);

        const MotionChannelMode Rel = MotionChannelMode.Relative;

        // ── 등장(1.2초) — 바탕색이 먼저 화면을 덮고("covered"에서 대본이 장면을 바꾼다), 층들이 시차를 두고 제자리로 들어온다.
        // 전부 상대값이다: 가만히 선 자세(배치 모드에서 놓은 자리)에서 얼마나 떨어져 있는가. 끝에서 0이라 제자리에 선다.
        const string In = "station_in";
        const float D = 1.2f;
        editor.AddMotion(In, Station, D, MotionEnd.Commit, Rel);
        Curve(editor, In, MotionEditor.BackdropNode, MotionChannelKind.Alpha, Axis.X, L(0f, 0f), L(0.25f, 1f), L(D, 1f));
        editor.AddMotionEvent(In, 0.25f, "covered");

        Scale(editor, In, "arches", Ramp(D, 0.15f, 0.75f, 0.55f, 1f));
        Curve(editor, In, "arches", MotionChannelKind.Alpha, Axis.X, Ramp(D, 0.15f, 0.45f, 0f, 1f));
        Curve(editor, In, "floor", MotionChannelKind.Position, Axis.Y, Ramp(D, 0.2f, 0.7f, -520f, 0f));
        Curve(editor, In, "pillars_left", MotionChannelKind.Position, Axis.X, Ramp(D, 0.3f, 0.9f, -820f, 0f));
        Curve(editor, In, "pillars_right", MotionChannelKind.Position, Axis.X, Ramp(D, 0.3f, 0.9f, 820f, 0f));
        Curve(editor, In, "train", MotionChannelKind.Position, Axis.X, Ramp(D, 0.5f, 1.1f, -900f, 0f));
        Curve(editor, In, "sign", MotionChannelKind.Position, Axis.Y, F(0f, 360f), F(0.6f, 360f), K(0.95f, -18f), F(1.1f, 0f), F(D, 0f)); // 내려와 살짝 출렁
        Scale(editor, In, "light", Ramp(D, 0.5f, 1.15f, 0.3f, 1f));
        Curve(editor, In, "light", MotionChannelKind.Alpha, Axis.X, Ramp(D, 0.5f, 0.9f, 0f, 1f));

        (string Node, float At)[] microbes = { ("microbe_sun", 0.8f), ("microbe_leaf", 0.9f), ("microbe_cell", 1.0f) };

        foreach ((string node, float at) in microbes)
        {
            // 모든 커브는 길이의 끝에 키가 있어야 한다(코어 규칙) — 일찍 끝나는 층도 끝까지 값을 든다.
            var pop = new List<KeyAsset> { F(0f, 0f), F(at, 0f), K(at + 0.12f, 1.2f), F(at + 0.2f, 1f) };

            if (at + 0.2f < D - 1e-4f)
            {
                pop.Add(F(D, 1f));
            }

            Scale(editor, In, node, pop.ToArray());
            Curve(editor, In, node, MotionChannelKind.Alpha, Axis.X, Ramp(D, at, at + 0.1f, 0f, 1f));
        }

        // ── 머묾(2초, 되풀이) — 미생물이 떠다니고 끝의 빛이 숨쉰다.
        const string Hold = "station_hold";
        editor.AddMotion(Hold, Station, 2f, MotionEnd.Restore, Rel);
        Curve(editor, Hold, "microbe_sun", MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(1f, 16f), K(2f, 0f));
        Curve(editor, Hold, "microbe_sun", MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(1f, 20f), K(2f, 0f));
        Curve(editor, Hold, "microbe_leaf", MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(0.7f, -12f), K(1.4f, 8f), K(2f, 0f));
        Curve(editor, Hold, "microbe_leaf", MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(1f, -7f), K(2f, 0f));
        Curve(editor, Hold, "microbe_cell", MotionChannelKind.Position, Axis.X, K(0f, 0f), K(1f, 26f), K(2f, 0f));
        Scale(editor, Hold, "light", K(0f, 1f), K(1f, 1.07f), K(2f, 1f));

        // ── 퇴장(0.9초) — 층들이 들어온 길로 물러나고 바탕색이 걷힌다.
        const string Out = "station_out";
        const float E = 0.9f;
        editor.AddMotion(Out, Station, E, MotionEnd.Commit, Rel);

        foreach ((string node, _) in microbes)
        {
            Curve(editor, Out, node, MotionChannelKind.Alpha, Axis.X, Ramp(E, 0f, 0.2f, 1f, 0f));
        }

        Curve(editor, Out, "sign", MotionChannelKind.Position, Axis.Y, Ramp(E, 0f, 0.35f, 0f, 360f));
        Curve(editor, Out, "train", MotionChannelKind.Position, Axis.X, Ramp(E, 0.05f, 0.5f, 0f, -900f));
        Curve(editor, Out, "pillars_left", MotionChannelKind.Position, Axis.X, Ramp(E, 0.15f, 0.65f, 0f, -820f));
        Curve(editor, Out, "pillars_right", MotionChannelKind.Position, Axis.X, Ramp(E, 0.15f, 0.65f, 0f, 820f));
        Curve(editor, Out, "floor", MotionChannelKind.Position, Axis.Y, Ramp(E, 0.25f, 0.7f, 0f, -520f));
        Scale(editor, Out, "arches", Ramp(E, 0.2f, 0.8f, 1f, 1.7f));
        Curve(editor, Out, "arches", MotionChannelKind.Alpha, Axis.X, Ramp(E, 0.45f, 0.8f, 1f, 0f));
        Scale(editor, Out, "light", Ramp(E, 0.2f, 0.8f, 1f, 2.4f));
        Curve(editor, Out, "light", MotionChannelKind.Alpha, Axis.X, Ramp(E, 0.5f, 0.85f, 1f, 0f));
        Curve(editor, Out, MotionEditor.BackdropNode, MotionChannelKind.Alpha, Axis.X, L(0f, 1f), L(0.5f, 1f), L(E, 0f));

        // ── 연출 — 화면에 선다. 1초 머물고 걷힌다.
        editor.AddEmoji(Station, Station);
        editor.UpdateEmoji(Station, new EmojiAsset { Skin = Station, Enter = In, Hold = Hold, Exit = Out, HoldSeconds = 1f, Screen = true });
        return editor;
    }
}
