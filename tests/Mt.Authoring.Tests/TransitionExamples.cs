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

    public const string Pillar = "pillar";

    /// <summary>
    /// 기울어진 기둥과 노을 — station과 다른 결로 짓는다: 층들을 **같은 타이밍으로 놓은 뒤 시차(<see cref="MotionEditor.StaggerNodes"/>)로 민다**.
    /// 지평선의 것들(노을 · 도시 둘 · 언덕)이 아래에서 차례로 솟고, 해가 떠오르고, 기둥이 넘어지듯 들어와 선다.
    /// </summary>
    public static MotionEditor BuildPillar(string layersPath)
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(new RigAsset { Kind = Pillar });
        editor.ImportLayers(Pillar, LayerSheet.Load(layersPath), "art", Pillar, Stage);

        const MotionChannelMode Rel = MotionChannelMode.Relative;
        string[] horizon = { "haze", "city_far", "city_near", "hill" };
        string[] petals = { "petal_1", "petal_2", "petal_3" };

        // ── 등장(1.5초) ──
        const string In = "pillar_in";
        const float D = 1.5f;
        editor.AddMotion(In, Pillar, D, MotionEnd.Commit, Rel);
        Curve(editor, In, MotionEditor.BackdropNode, MotionChannelKind.Alpha, Axis.X, L(0f, 0f), L(0.25f, 1f), L(D, 1f));
        editor.AddMotionEvent(In, 0.25f, "covered");

        // 지평선의 층 넷 — 전부 0.2~0.7초에 아래에서 솟게 놓고, 시차로 0.12초씩 민다(하나씩 키를 옮기지 않는다).
        foreach (string layer in horizon)
        {
            Curve(editor, In, layer, MotionChannelKind.Position, Axis.Y, Ramp(D, 0.2f, 0.7f, -620f, 0f));
        }

        editor.StaggerNodes(In, horizon, 0.12f);

        Curve(editor, In, "clouds_far", MotionChannelKind.Position, Axis.X, Ramp(D, 0.15f, 1.0f, 520f, 0f));
        Curve(editor, In, "clouds_far", MotionChannelKind.Alpha, Axis.X, Ramp(D, 0.15f, 0.5f, 0f, 1f));
        Curve(editor, In, "clouds_near", MotionChannelKind.Position, Axis.X, Ramp(D, 0.25f, 1.1f, -640f, 0f));
        Curve(editor, In, "clouds_near", MotionChannelKind.Alpha, Axis.X, Ramp(D, 0.25f, 0.6f, 0f, 1f));

        // 해가 지평선 아래에서 떠오르고 빛이 번진다.
        Curve(editor, In, "sun", MotionChannelKind.Position, Axis.Y, Ramp(D, 0.6f, 1.3f, -300f, 0f));
        // 가만히 선 자세는 "다 보이는" 것이라, 제 차례 전에는 숨겨 둬야 한다 — 지평선에 가려지지 않는 층은 불투명도로.
        Curve(editor, In, "sun", MotionChannelKind.Alpha, Axis.X, Ramp(D, 0.6f, 0.75f, 0f, 1f));
        Curve(editor, In, "sun_glow", MotionChannelKind.Alpha, Axis.X, Ramp(D, 0.7f, 1.4f, 0f, 1f));
        Scale(editor, In, "sun_glow", Ramp(D, 0.7f, 1.4f, 0.5f, 1f));

        // 기둥 — 왼쪽 밖에서 넘어지듯 들어와 살짝 넘쳤다가 선다.
        Curve(editor, In, "pillar", MotionChannelKind.Position, Axis.X, Ramp(D, 0.55f, 1.05f, -640f, 0f));
        Curve(editor, In, "pillar", MotionChannelKind.Euler, Axis.Z, F(0f, 28f), F(0.55f, 28f), K(1.05f, -4f), F(1.25f, 0f), F(D, 0f));

        // 꽃잎 — 같은 타이밍으로 놓고 시차로.
        foreach (string petal in petals)
        {
            Curve(editor, In, petal, MotionChannelKind.Alpha, Axis.X, Ramp(D, 1.0f, 1.2f, 0f, 1f));
            Curve(editor, In, petal, MotionChannelKind.Position, Axis.X, Ramp(D, 1.0f, 1.3f, 180f, 0f));
        }

        editor.StaggerNodes(In, petals, 0.08f);

        // ── 머묾(3초, 되풀이) — 꽃잎이 흔들리고 구름이 천천히 숨쉰다.
        const string Hold = "pillar_hold";
        editor.AddMotion(Hold, Pillar, 3f, MotionEnd.Restore, Rel);

        for (int i = 0; i < petals.Length; i++)
        {
            float sway = 26f + i * 10f;
            Curve(editor, Hold, petals[i], MotionChannelKind.Position, Axis.X, K(0f, 0f), K(1.5f, -sway), K(3f, 0f));
            Curve(editor, Hold, petals[i], MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(0.75f, 14f), K(2.25f, -14f), K(3f, 0f));
            Curve(editor, Hold, petals[i], MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(1.5f, i % 2 == 0 ? 40f : -40f), K(3f, 0f));
        }

        Curve(editor, Hold, "clouds_near", MotionChannelKind.Position, Axis.X, K(0f, 0f), K(1.5f, 24f), K(3f, 0f));
        Scale(editor, Hold, "sun_glow", K(0f, 1f), K(1.5f, 1.08f), K(3f, 1f));

        // ── 퇴장(1초) — 해가 지고, 지평선이 가라앉고(시차 — 앞의 것부터), 기둥이 기울어 나가고, 바탕색이 걷힌다.
        const string Out = "pillar_out";
        const float E = 1f;
        editor.AddMotion(Out, Pillar, E, MotionEnd.Commit, Rel);

        foreach (string petal in petals)
        {
            Curve(editor, Out, petal, MotionChannelKind.Alpha, Axis.X, Ramp(E, 0f, 0.2f, 1f, 0f));
        }

        Curve(editor, Out, "sun", MotionChannelKind.Position, Axis.Y, Ramp(E, 0f, 0.5f, 0f, -300f));
        Curve(editor, Out, "sun", MotionChannelKind.Alpha, Axis.X, Ramp(E, 0.3f, 0.5f, 1f, 0f));
        Curve(editor, Out, "sun_glow", MotionChannelKind.Alpha, Axis.X, Ramp(E, 0f, 0.4f, 1f, 0f));
        Curve(editor, Out, "pillar", MotionChannelKind.Position, Axis.X, Ramp(E, 0.1f, 0.6f, 0f, -640f));
        Curve(editor, Out, "pillar", MotionChannelKind.Euler, Axis.Z, Ramp(E, 0.1f, 0.6f, 0f, 28f));
        string[] sinking = { "hill", "city_near", "city_far", "haze" };

        foreach (string layer in sinking)
        {
            Curve(editor, Out, layer, MotionChannelKind.Position, Axis.Y, Ramp(E, 0.15f, 0.55f, 0f, -620f));
        }

        editor.StaggerNodes(Out, sinking, 0.08f);
        Curve(editor, Out, "clouds_far", MotionChannelKind.Alpha, Axis.X, Ramp(E, 0.3f, 0.7f, 1f, 0f));
        Curve(editor, Out, "clouds_near", MotionChannelKind.Alpha, Axis.X, Ramp(E, 0.3f, 0.7f, 1f, 0f));
        Curve(editor, Out, MotionEditor.BackdropNode, MotionChannelKind.Alpha, Axis.X, L(0f, 1f), L(0.55f, 1f), L(E, 0f));

        editor.AddEmoji(Pillar, Pillar);
        editor.UpdateEmoji(Pillar, new EmojiAsset { Skin = Pillar, Enter = In, Hold = Hold, Exit = Out, HoldSeconds = 1.5f, Screen = true });
        return editor;
    }
}
