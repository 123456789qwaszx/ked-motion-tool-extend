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

    /// <param name="assemble">false면 조립으로 옮기기 전의 한 덩어리 — 옮긴 뒤와 재생이 같은지 맞춰 보는 데 쓴다.</param>
    public static MotionEditor BuildStation(string layersPath, bool assemble = true)
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
// ── 조립으로 옮긴다 — 한 덩어리로 다 지은 뒤에, 미생물 셋을 화면의 메뉴가 하는 그대로 옮긴다:
        // "새 세트로 빼기 ▸ 여기서 microbe_cell까지"(이름은 첫 오브젝트에서) → 세트 이름 칸에서 microbes로.
        // 키가 새 세트의 애니메이션(microbes_in · _hold · _out)으로 가고 station의 애니메이션에는 그것을 트는 재생 블록이 선다 — 재생은 그대로다.
        if (assemble)
        {
            editor.ExtractRigNodes(Station, new[] { "microbe_sun", "microbe_leaf", "microbe_cell" }, "microbe_sun");
            editor.RenameRig("microbe_sun", "microbes");
        }

        return editor;
    }

    public const string Pillar = "pillar";

    public const string Sun = "sun";

    /// <summary>
    /// 기울어진 기둥과 노을 — station과 다른 결로 짓는다: 층들을 **같은 타이밍으로 놓은 뒤 시차(<see cref="MotionEditor.StaggerNodes"/>)로 민다**.
    /// 지평선의 것들(노을 · 도시 둘 · 언덕)이 아래에서 차례로 솟고, 해가 떠오르고, 기둥이 넘어지듯 들어와 선다.
    ///
    /// **조립으로 짓는다**(<c>PLAN-set.md</c> S9·S10): 해는 따로 세트(<see cref="Sun"/>)이고 제 애니메이션(뜬다 · 숨쉰다 · 진다)을 가진다.
    /// pillar는 그 세트를 품고, 제 애니메이션에서 **재생 블록**으로 그것들을 튼다 — 해의 키는 pillar에 하나도 없다.
    /// </summary>
    public static MotionEditor BuildPillar(string layersPath)
    {
        var editor = new MotionEditor(new MotionDocument());
        const MotionChannelMode Rel = MotionChannelMode.Relative;

        // 층 표를 둘로 가른다 — 해의 층 둘은 해 세트로, 나머지는 pillar로.
        string[] lines = File.ReadAllLines(layersPath);
        LayerSheet sunSheet = LayerSheet.Parse(string.Join('\n', lines.Where(l => !l.StartsWith('#') && l.Contains("_sun", StringComparison.Ordinal))));
        LayerSheet rest = LayerSheet.Parse(string.Join('\n', lines.Where(l => l.StartsWith('#') || !l.Contains("_sun", StringComparison.Ordinal))));

        // ── 해 세트 — 해의 가운데가 세트의 가운데가 되게 들인다(어디에 품겨도 그 자리가 해의 자리다).
        LayerEntry disc = sunSheet.Layers.Single(l => l.File.EndsWith("_sun.png", StringComparison.Ordinal));
        var center = new Vec2(disc.Left + disc.Width / 2f, disc.Top + disc.Height / 2f);
        editor.AddRig(new RigAsset { Kind = Sun });
        editor.ImportLayers(Sun, sunSheet, "art", Sun, new Vec2(center.X * 2f, center.Y * 2f));
        editor.RenameRigNode(Sun, "sun_glow", "glow");
        editor.RenameRigNode(Sun, "sun", "disc");

        // 뜬다(0.8초, 유지) — 지평선 아래에서 떠오르고 빛이 번진다.
        editor.AddMotion("sun_rise", Sun, 0.8f, MotionEnd.Commit, Rel);
        Curve(editor, "sun_rise", "disc", MotionChannelKind.Position, Axis.Y, Ramp(0.8f, 0f, 0.7f, -300f, 0f));
        Curve(editor, "sun_rise", "glow", MotionChannelKind.Alpha, Axis.X, Ramp(0.8f, 0.1f, 0.8f, 0f, 1f));
        Scale(editor, "sun_rise", "glow", Ramp(0.8f, 0.1f, 0.8f, 0.5f, 1f));

        // 숨쉰다(1.5초, 복원) — 빛이 커졌다 돌아온다. 되풀이해서 튼다.
        editor.AddMotion("sun_pulse", Sun, 1.5f, MotionEnd.Restore, Rel);
        Scale(editor, "sun_pulse", "glow", K(0f, 1f), K(0.75f, 1.08f), K(1.5f, 1f));

        // 진다(0.5초, 유지) — 빛이 꺼지고 해가 가라앉으며 사라진다.
        editor.AddMotion("sun_set", Sun, 0.5f, MotionEnd.Commit, Rel);
        Curve(editor, "sun_set", "disc", MotionChannelKind.Position, Axis.Y, Ramp(0.5f, 0f, 0.5f, 0f, -300f));
        Curve(editor, "sun_set", "disc", MotionChannelKind.Alpha, Axis.X, Ramp(0.5f, 0.3f, 0.5f, 1f, 0f));
        Curve(editor, "sun_set", "glow", MotionChannelKind.Alpha, Axis.X, Ramp(0.5f, 0f, 0.4f, 1f, 0f));

        // ── pillar 세트 — 나머지 층 + 해 세트를 품는다(노을 바로 앞 = 도시 뒤, 원래 층의 자리).
        editor.AddRig(new RigAsset { Kind = Pillar });
        editor.ImportLayers(Pillar, rest, "art", Pillar, Stage);
        editor.AddRigInstance(Pillar, Sun, Sun);
        editor.UpdateRigNode(Pillar, Sun, new RigNodeAsset { X = center.X - Stage.X / 2f, Y = Stage.Y / 2f - center.Y, Alpha = true });
        RigAsset pillarRig = editor.Document.FindRig(Pillar)!;

        while (pillarRig.Nodes.FindIndex(n => n.Id == Sun) > pillarRig.Nodes.FindIndex(n => n.Id == "haze") + 1)
        {
            editor.MoveRigNode(Pillar, Sun, -1);
        }

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

        // 해 — 품은 세트의 "뜬다"를 0.6초에 튼다. 제 차례 전에는 인스턴스를 통째로 숨긴다(가만히 선 자세는 "다 보이는" 것이라).
        editor.AddMotionPlay(In, Sun, "sun_rise", 0.6f);
        editor.HideUntilTurn(In, Sun);

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
        editor.AddMotionPlay(Hold, Sun, "sun_pulse", 0f, repeat: 2); // 해가 두 번 숨쉰다

        // ── 퇴장(1초) — 해가 지고, 지평선이 가라앉고(시차 — 앞의 것부터), 기둥이 기울어 나가고, 바탕색이 걷힌다.
        const string Out = "pillar_out";
        const float E = 1f;
        editor.AddMotion(Out, Pillar, E, MotionEnd.Commit, Rel);

        foreach (string petal in petals)
        {
            Curve(editor, Out, petal, MotionChannelKind.Alpha, Axis.X, Ramp(E, 0f, 0.2f, 1f, 0f));
        }

        editor.AddMotionPlay(Out, Sun, "sun_set", 0f);
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

    // ── 나머지 세 장면 — 처음부터 조립으로: 층 표를 통째로 들인 뒤 "새 세트로 빼기"로 가르고, 뺀 세트의 애니메이션을 재생 블록으로 튼다 ──

    public const string Lapis = "lapis";
    public const string Heredity = "heredity";
    public const string Mercury = "mercury";

    private static MotionEditor Begin(string kind, string layersPath)
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(new RigAsset { Kind = kind });
        editor.ImportLayers(kind, LayerSheet.Load(layersPath), "art", kind, Stage);
        return editor;
    }

    /// <summary>등장의 머리 — 바탕색이 0.25초에 화면을 덮고 거기서 <c>covered</c>가 울린다.</summary>
    private static void Cover(MotionEditor editor, string motion, float duration)
    {
        Curve(editor, motion, MotionEditor.BackdropNode, MotionChannelKind.Alpha, Axis.X, L(0f, 0f), L(0.25f, 1f), L(duration, 1f));
        editor.AddMotionEvent(motion, 0.25f, "covered");
    }

    /// <summary>퇴장의 꼬리 — 바탕색이 끝에서 걷힌다.</summary>
    private static void Uncover(MotionEditor editor, string motion, float duration)
        => Curve(editor, motion, MotionEditor.BackdropNode, MotionChannelKind.Alpha, Axis.X, L(0f, 1f), L(duration * 0.55f, 1f), L(duration, 0f));

    private static void Fade(MotionEditor editor, string motion, string node, float duration, float from, float to, float a, float b)
        => Curve(editor, motion, node, MotionChannelKind.Alpha, Axis.X, Ramp(duration, from, to, a, b));

    private static void Slide(MotionEditor editor, string motion, string node, Axis axis, float duration, float from, float to, float a, float b)
        => Curve(editor, motion, node, MotionChannelKind.Position, axis, Ramp(duration, from, to, a, b));

    private static void Cue(MotionEditor editor, string kind, float holdSeconds)
    {
        editor.AddEmoji(kind, kind);
        editor.UpdateEmoji(kind, new EmojiAsset { Skin = kind, Enter = kind + "_in", Hold = kind + "_hold", Exit = kind + "_out", HoldSeconds = holdSeconds, Screen = true });
    }

    /// <summary>
    /// 바닷속 기둥들 — 바닥과 기둥이 아래에서 차례로 솟고, 양옆의 나무줄기가 닫히듯 들어오고, 물고기 떼와 가오리가 헤엄쳐 들어온다.
    /// 빛나는 구슬은 따로 세트(<c>orb</c>: 톡 나타난다 · 둥실거린다 · 사그라든다)로 빼 재생 블록으로 튼다.
    /// </summary>
    public static MotionEditor BuildLapis(string layersPath)
    {
        MotionEditor editor = Begin(Lapis, layersPath);
        const MotionChannelMode Rel = MotionChannelMode.Relative;

        // 구슬을 새 세트로 뺀다 — 자리와 그림을 데리고 가고 그 자리에 인스턴스 orb가 선다.
        editor.ExtractRigNodes(Lapis, new[] { "orb" }, "orb");
        editor.AddMotion("orb_pop", "orb", 0.5f, MotionEnd.Commit, Rel);
        Scale(editor, "orb_pop", "orb", K(0f, 0.2f), K(0.32f, 1.15f), F(0.5f, 1f));
        editor.AddMotion("orb_bob", "orb", 2f, MotionEnd.Restore, Rel);
        Curve(editor, "orb_bob", "orb", MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(1f, 22f), K(2f, 0f));
        Scale(editor, "orb_bob", "orb", K(0f, 1f), K(1f, 1.06f), K(2f, 1f));
        editor.AddMotion("orb_fade", "orb", 0.3f, MotionEnd.Commit, Rel);
        Scale(editor, "orb_fade", "orb", F(0f, 1f), F(0.3f, 0.5f));
        Fade(editor, "orb_fade", "orb", 0.3f, 0f, 0.3f, 1f, 0f);

        // ── 등장(1.4초)
        const string In = "lapis_in";
        const float D = 1.4f;
        editor.AddMotion(In, Lapis, D, MotionEnd.Commit, Rel);
        Cover(editor, In, D);
        Fade(editor, In, "light_band", D, 0.2f, 0.9f, 0f, 1f);
        string[] rising = { "seabed_far", "pillars_far", "pillars_mid", "seabed_near" };

        foreach (string layer in rising)
        {
            Slide(editor, In, layer, Axis.Y, D, 0.2f, 0.75f, -760f, 0f);
        }

        editor.StaggerNodes(In, rising, 0.1f);
        Slide(editor, In, "trunk_left", Axis.X, D, 0.5f, 1.0f, -420f, 0f);
        Slide(editor, In, "trunk_right", Axis.X, D, 0.55f, 1.05f, 420f, 0f);
        Slide(editor, In, "fish_school", Axis.X, D, 0.6f, 1.3f, 900f, 0f);
        Slide(editor, In, "ray", Axis.X, D, 0.7f, 1.35f, -900f, 0f);
        editor.HideUntilTurn(In, "ray"); // 화면 밖까지 못 나가는 자리라 제 차례 전에는 숨긴다
        Fade(editor, In, "stars", D, 0.8f, 1.3f, 0f, 1f);
        editor.AddMotionPlay(In, "orb", "orb_pop", 0.85f);
        editor.HideUntilTurn(In, "orb");

        // ── 머묾(2초, 되풀이)
        const string Hold = "lapis_hold";
        editor.AddMotion(Hold, Lapis, 2f, MotionEnd.Restore, Rel);
        Curve(editor, Hold, "fish_school", MotionChannelKind.Position, Axis.X, K(0f, 0f), K(1f, -46f), K(2f, 0f));
        Curve(editor, Hold, "ray", MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(1f, 20f), K(2f, 0f));
        Curve(editor, Hold, "stars", MotionChannelKind.Alpha, Axis.X, K(0f, 1f), K(1f, 0.55f), K(2f, 1f));
        editor.AddMotionPlay(Hold, "orb", "orb_bob", 0f);

        // ── 퇴장(1초)
        const string Out = "lapis_out";
        const float E = 1f;
        editor.AddMotion(Out, Lapis, E, MotionEnd.Commit, Rel);
        editor.AddMotionPlay(Out, "orb", "orb_fade", 0f);
        Fade(editor, Out, "stars", E, 0f, 0.3f, 1f, 0f);
        Slide(editor, Out, "fish_school", Axis.X, E, 0f, 0.5f, 0f, -1900f);
        Slide(editor, Out, "ray", Axis.X, E, 0f, 0.5f, 0f, 1300f);
        Slide(editor, Out, "trunk_left", Axis.X, E, 0.1f, 0.55f, 0f, -420f);
        Slide(editor, Out, "trunk_right", Axis.X, E, 0.1f, 0.55f, 0f, 420f);
        string[] sinking = { "seabed_near", "pillars_mid", "pillars_far", "seabed_far" };

        foreach (string layer in sinking)
        {
            Slide(editor, Out, layer, Axis.Y, E, 0.15f, 0.55f, 0f, -760f);
        }

        editor.StaggerNodes(Out, sinking, 0.07f);
        Fade(editor, Out, "light_band", E, 0.3f, 0.7f, 1f, 0f);
        Uncover(editor, Out, E);
        Cue(editor, Lapis, 1.5f);
        return editor;
    }

    /// <summary>
    /// 달과 떠 있는 섬 — 달이 커지며 떠오르고 소용돌이가 돌아 들어오고, 섬들이 솟고 나뭇가지가 위에서 내려온다.
    /// 씨앗(구슬 + 그 안의 소용돌이)과 수정(수정 + 빛)은 따로 세트로 빼 제 애니메이션을 재생 블록으로 튼다 — 한 연출에 품은 세트 둘.
    /// </summary>
    public static MotionEditor BuildHeredity(string layersPath)
    {
        MotionEditor editor = Begin(Heredity, layersPath);
        const MotionChannelMode Rel = MotionChannelMode.Relative;

        // 씨앗 — 구슬과 그 안의 소용돌이.
        editor.ExtractRigNodes(Heredity, new[] { "orb", "orb_swirl" }, "seed");
        editor.AddMotion("seed_in", "seed", 0.6f, MotionEnd.Commit, Rel);
        Scale(editor, "seed_in", "orb", K(0f, 0.3f), K(0.4f, 1.1f), F(0.6f, 1f));
        Scale(editor, "seed_in", "orb_swirl", F(0f, 0.3f), F(0.6f, 1f));
        Curve(editor, "seed_in", "orb_swirl", MotionChannelKind.Euler, Axis.Z, F(0f, -160f), F(0.6f, 0f));
        editor.AddMotion("seed_sway", "seed", 1.5f, MotionEnd.Restore, Rel);
        Curve(editor, "seed_sway", "orb_swirl", MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(0.75f, 28f), K(1.5f, 0f));
        Scale(editor, "seed_sway", "orb", K(0f, 1f), K(0.75f, 1.05f), K(1.5f, 1f));

        // 수정 — 수정과 그 빛.
        editor.ExtractRigNodes(Heredity, new[] { "crystals", "crystals_light" }, "crystal");
        editor.AddMotion("crystal_in", "crystal", 0.6f, MotionEnd.Commit, Rel);
        Slide(editor, "crystal_in", "crystals", Axis.Y, 0.6f, 0f, 0.45f, -260f, 0f);
        Slide(editor, "crystal_in", "crystals_light", Axis.Y, 0.6f, 0f, 0.45f, -260f, 0f);
        Fade(editor, "crystal_in", "crystals_light", 0.6f, 0.3f, 0.6f, 0f, 1f);
        editor.AddMotion("crystal_glint", "crystal", 1.5f, MotionEnd.Restore, Rel);
        Curve(editor, "crystal_glint", "crystals_light", MotionChannelKind.Alpha, Axis.X, K(0f, 1f), K(0.75f, 0.45f), K(1.5f, 1f));

        // ── 등장(1.5초)
        const string In = "heredity_in";
        const float D = 1.5f;
        editor.AddMotion(In, Heredity, D, MotionEnd.Commit, Rel);
        Cover(editor, In, D);
        Fade(editor, In, "moon", D, 0.2f, 0.8f, 0f, 1f);
        Scale(editor, In, "moon", Ramp(D, 0.2f, 1.0f, 0.6f, 1f));
        Fade(editor, In, "spiral", D, 0.3f, 1.0f, 0f, 1f);
        Curve(editor, In, "spiral", MotionChannelKind.Euler, Axis.Z, Ramp(D, 0.3f, 1.2f, -40f, 0f));
        string[] islands = { "island", "islets" };

        foreach (string layer in islands)
        {
            Slide(editor, In, layer, Axis.Y, D, 0.35f, 0.9f, -760f, 0f);
        }

        editor.StaggerNodes(In, islands, 0.15f);
        Slide(editor, In, "canopy", Axis.Y, D, 0.5f, 1.0f, 520f, 0f);
        editor.AddMotionPlay(In, "crystal", "crystal_in", 0.65f);
        editor.HideUntilTurn(In, "crystal");
        editor.AddMotionPlay(In, "seed", "seed_in", 0.8f);
        editor.HideUntilTurn(In, "seed");
        string[] leaves = { "leaf_1", "leaf_2", "leaf_3" };

        foreach (string leaf in leaves)
        {
            Fade(editor, In, leaf, D, 1.0f, 1.2f, 0f, 1f);
            Slide(editor, In, leaf, Axis.Y, D, 1.0f, 1.3f, 120f, 0f);
        }

        editor.StaggerNodes(In, leaves, 0.07f);

        // ── 머묾(3초, 되풀이) — 씨앗이 흔들리고 수정이 반짝인다(각각 두 번).
        const string Hold = "heredity_hold";
        editor.AddMotion(Hold, Heredity, 3f, MotionEnd.Restore, Rel);
        editor.AddMotionPlay(Hold, "seed", "seed_sway", 0f, repeat: 2);
        editor.AddMotionPlay(Hold, "crystal", "crystal_glint", 0f, repeat: 2);

        for (int i = 0; i < leaves.Length; i++)
        {
            Curve(editor, Hold, leaves[i], MotionChannelKind.Position, Axis.X, K(0f, 0f), K(1.5f, 24f + i * 8f), K(3f, 0f));
            Curve(editor, Hold, leaves[i], MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(1.5f, i % 2 == 0 ? -30f : 30f), K(3f, 0f));
        }

        Curve(editor, Hold, "spiral", MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(1.5f, 6f), K(3f, 0f));

        // ── 퇴장(1초) — 품은 세트는 인스턴스째로 사그라든다(제 키).
        const string Out = "heredity_out";
        const float E = 1f;
        editor.AddMotion(Out, Heredity, E, MotionEnd.Commit, Rel);

        foreach (string leaf in leaves)
        {
            Fade(editor, Out, leaf, E, 0f, 0.2f, 1f, 0f);
        }

        Fade(editor, Out, "seed", E, 0f, 0.3f, 1f, 0f);
        Scale(editor, Out, "seed", Ramp(E, 0f, 0.3f, 1f, 0.5f));
        Fade(editor, Out, "crystal", E, 0.05f, 0.35f, 1f, 0f);
        Slide(editor, Out, "canopy", Axis.Y, E, 0.1f, 0.5f, 0f, 520f);
        Slide(editor, Out, "islets", Axis.Y, E, 0.15f, 0.55f, 0f, -760f);
        Slide(editor, Out, "island", Axis.Y, E, 0.22f, 0.62f, 0f, -760f);
        Fade(editor, Out, "spiral", E, 0.2f, 0.6f, 1f, 0f);
        Fade(editor, Out, "moon", E, 0.3f, 0.7f, 1f, 0f);
        Uncover(editor, Out, E);
        Cue(editor, Heredity, 1.5f);
        return editor;
    }

    /// <summary>
    /// 행성과 고래 — 대기가 번지고 행성이 아래에서 솟고, 고래 둘과 물고기 떼가 가로질러 들어온다.
    /// 별(빛 + 별)은 따로 세트 <c>star</c>로 빼 켜진다 · 깜빡인다 · 꺼진다를 재생 블록으로 튼다.
    /// </summary>
    public static MotionEditor BuildMercury(string layersPath)
    {
        MotionEditor editor = Begin(Mercury, layersPath);
        const MotionChannelMode Rel = MotionChannelMode.Relative;

        editor.ExtractRigNodes(Mercury, new[] { "star_glow", "star" }, "star");
        editor.AddMotion("star_on", "star", 0.6f, MotionEnd.Commit, Rel);
        Scale(editor, "star_on", "star", K(0f, 0.1f), K(0.35f, 1.25f), F(0.6f, 1f));
        Scale(editor, "star_on", "star_glow", Ramp(0.6f, 0.1f, 0.6f, 0.4f, 1f));
        Fade(editor, "star_on", "star_glow", 0.6f, 0.1f, 0.6f, 0f, 1f);
        editor.AddMotion("star_twinkle", "star", 1.25f, MotionEnd.Restore, Rel);
        Scale(editor, "star_twinkle", "star_glow", K(0f, 1f), K(0.6f, 1.12f), K(1.25f, 1f));
        Curve(editor, "star_twinkle", "star", MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(0.6f, 20f), K(1.25f, 0f));
        editor.AddMotion("star_off", "star", 0.4f, MotionEnd.Commit, Rel);
        Scale(editor, "star_off", "star", F(0f, 1f), F(0.4f, 0.1f));
        Fade(editor, "star_off", "star_glow", 0.4f, 0f, 0.3f, 1f, 0f);
        Fade(editor, "star_off", "star", 0.4f, 0.2f, 0.4f, 1f, 0f);

        // ── 등장(1.5초)
        const string In = "mercury_in";
        const float D = 1.5f;
        editor.AddMotion(In, Mercury, D, MotionEnd.Commit, Rel);
        Cover(editor, In, D);
        string[] air = { "atmosphere", "atmosphere_inner" };

        foreach (string layer in air)
        {
            Fade(editor, In, layer, D, 0.2f, 0.8f, 0f, 1f);
            Scale(editor, In, layer, Ramp(D, 0.2f, 0.9f, 1.2f, 1f));
        }

        editor.StaggerNodes(In, air, 0.12f);
        Slide(editor, In, "planet", Axis.Y, D, 0.3f, 0.95f, -820f, 0f);
        Slide(editor, In, "planet_bands", Axis.Y, D, 0.3f, 0.95f, -820f, 0f);
        Slide(editor, In, "clouds", Axis.X, D, 0.45f, 1.15f, -620f, 0f);
        Fade(editor, In, "clouds", D, 0.45f, 0.8f, 0f, 1f);
        Fade(editor, In, "stars", D, 0.6f, 1.2f, 0f, 1f);
        editor.AddMotionPlay(In, "star", "star_on", 0.7f);
        editor.HideUntilTurn(In, "star");
        Slide(editor, In, "whale_big", Axis.X, D, 0.5f, 1.4f, 1700f, 0f);
        Slide(editor, In, "whale_small", Axis.X, D, 0.7f, 1.45f, 900f, 0f);
        Slide(editor, In, "fish_school", Axis.X, D, 0.8f, 1.45f, 900f, 0f);

        // ── 머묾(2.5초, 되풀이) — 고래가 느리게 헤엄치고 별이 두 번 깜빡인다.
        const string Hold = "mercury_hold";
        editor.AddMotion(Hold, Mercury, 2.5f, MotionEnd.Restore, Rel);
        Curve(editor, Hold, "whale_big", MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(1.25f, 26f), K(2.5f, 0f));
        Curve(editor, Hold, "whale_big", MotionChannelKind.Euler, Axis.Z, K(0f, 0f), K(1.25f, -3f), K(2.5f, 0f));
        Curve(editor, Hold, "whale_small", MotionChannelKind.Position, Axis.Y, K(0f, 0f), K(1.25f, -18f), K(2.5f, 0f));
        Curve(editor, Hold, "fish_school", MotionChannelKind.Position, Axis.X, K(0f, 0f), K(1.25f, -36f), K(2.5f, 0f));
        editor.AddMotionPlay(Hold, "star", "star_twinkle", 0f, repeat: 2);

        // ── 퇴장(1초)
        const string Out = "mercury_out";
        const float E = 1f;
        editor.AddMotion(Out, Mercury, E, MotionEnd.Commit, Rel);
        editor.AddMotionPlay(Out, "star", "star_off", 0f);
        Slide(editor, Out, "whale_big", Axis.X, E, 0f, 0.6f, 0f, -1500f);
        Slide(editor, Out, "whale_small", Axis.X, E, 0f, 0.5f, 0f, -2000f);
        Slide(editor, Out, "fish_school", Axis.X, E, 0f, 0.45f, 0f, -1900f);
        Fade(editor, Out, "stars", E, 0.1f, 0.4f, 1f, 0f);
        Fade(editor, Out, "clouds", E, 0.15f, 0.5f, 1f, 0f);
        Slide(editor, Out, "planet", Axis.Y, E, 0.2f, 0.65f, 0f, -820f);
        Slide(editor, Out, "planet_bands", Axis.Y, E, 0.2f, 0.65f, 0f, -820f);
        Fade(editor, Out, "atmosphere_inner", E, 0.3f, 0.65f, 1f, 0f);
        Fade(editor, Out, "atmosphere", E, 0.35f, 0.7f, 1f, 0f);
        Uncover(editor, Out, E);
        Cue(editor, Mercury, 1.5f);
        return editor;
    }
}
