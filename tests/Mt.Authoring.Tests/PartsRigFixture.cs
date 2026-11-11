namespace Mt.Authoring.Tests;

/// <summary>
/// 시험용 조각 세트 — 자리 › 묶음 › 조각(<c>Badge_Anchor › Badge_Body › Badge_Part1…N</c>). 문서가 정의하는 작은 세트의 견본이다(<see cref="Rig"/>).
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
}
