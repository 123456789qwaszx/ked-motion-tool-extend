using System.Text.Json.Nodes;

namespace Mt.Authoring.Tests;

/// <summary>
/// 시험용 조각 리그 — 자리 › 묶음 › 조각(<c>Badge_Anchor › Badge_Body › Badge_Part1…N</c>). 캐릭터가 아닌 작은 리그의 견본이다:
/// 문서가 정의하는 세트로(<see cref="Rig"/>), 또는 내장 덤프에 더한 파일로(<see cref="WriteDump"/>) 쓴다.
/// </summary>
internal static class PartsRigFixture
{
    public const string RigKind = "badge";
    public const float PartSize = 160f;

    /// <summary>문서가 정의하는 조각 리그 — 묶음과 조각은 불투명도를 받고, 조각은 그림 노드다.</summary>
    public static Mt.Authoring.Model.RigAsset Rig(string kind = RigKind, int parts = 4, float partSize = PartSize)
    {
        var rig = new Mt.Authoring.Model.RigAsset
        {
            Kind = kind,
            Nodes =
            {
                new Mt.Authoring.Model.RigNodeAsset { Id = "Badge_Anchor" },
                new Mt.Authoring.Model.RigNodeAsset { Id = "Badge_Body", Parent = "Badge_Anchor", Alpha = true },
            },
        };

        for (int i = 1; i <= parts; i++)
        {
            rig.Nodes.Add(new Mt.Authoring.Model.RigNodeAsset { Id = $"Badge_Part{i}", Parent = "Badge_Body", Width = partSize, Height = partSize, Image = true, Alpha = true });
        }

        return rig;
    }

    /// <summary>조각 리그가 든 <c>rig-schemas.json</c>의 경로(옆에 presets는 없다 — 깊이·focus는 내장본이 쓰인다).</summary>
    public static string WriteDump(string sourceDumpPath)
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText(sourceDumpPath))!;
        var nodes = new JsonArray
        {
            Node("__root", "", stretch: true, size: 0f),
            Node("Badge_Anchor", "__root", stretch: false, size: 0f),
            Node("Badge_Body", "Badge_Anchor", stretch: false, size: 0f, canvas: true), // 묶음 전체의 불투명도
        };

        for (int i = 1; i <= 4; i++)
        {
            nodes.Add(Node($"Badge_Part{i}", "Badge_Body", stretch: false, size: PartSize, canvas: true)); // 조각마다 따로 흐려진다
        }

        root["rigs"]!.AsArray().Add(new JsonObject { ["rigKind"] = RigKind, ["sourcePrefab"] = "", ["nodes"] = nodes });

        string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"badge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "rig-schemas.json");
        File.WriteAllText(path, root.ToJsonString());
        return path;
    }

    private static JsonObject Node(string id, string parent, bool stretch, float size, bool canvas = false) => new()
    {
        ["id"] = id,
        ["parent"] = parent,
        ["anchoredPosition"] = V2(0f, 0f),
        ["anchorMin"] = stretch ? V2(0f, 0f) : V2(0.5f, 0.5f),
        ["anchorMax"] = stretch ? V2(1f, 1f) : V2(0.5f, 0.5f),
        ["pivot"] = V2(0.5f, 0.5f),
        ["sizeDelta"] = V2(size, size),
        ["localScale"] = new JsonObject { ["x"] = 1f, ["y"] = 1f, ["z"] = 1f },
        ["localEulerAngles"] = new JsonObject { ["x"] = 0f, ["y"] = 0f, ["z"] = 0f },
        ["measuredRectSize"] = stretch ? V2(1920f, 1080f) : V2(size, size),
        ["hasCanvasGroup"] = canvas,
        ["canvasGroupAlpha"] = 1f,
    };

    private static JsonObject V2(float x, float y) => new() { ["x"] = x, ["y"] = y };
}
