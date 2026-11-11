namespace Mt.App.Tests;

/// <summary>
/// 시험용 조각 세트 — 자리 › 묶음 › 조각(<c>Badge_Anchor › Badge_Body › Badge_Part1…N</c>). 문서가 정의하는 작은 세트의 견본이다(<see cref="Set"/>).
/// </summary>
internal static class PartsSetFixture
{
    public const string SetName = "badge";
    public const float PartSize = 160f;

    /// <summary>문서가 정의하는 조각 리그 — 묶음과 조각은 불투명도를 받고, 조각은 그림 노드다.</summary>
    public static Mt.Authoring.Model.SetAsset Set(string kind = SetName, int parts = 4, float partSize = PartSize)
    {
        var set = new Mt.Authoring.Model.SetAsset
        {
            Name = kind,
            Objects =
            {
                new Mt.Authoring.Model.ObjectAsset { Id = "Badge_Anchor" },
                new Mt.Authoring.Model.ObjectAsset { Id = "Badge_Body", Parent = "Badge_Anchor", Alpha = true },
            },
        };

        for (int i = 1; i <= parts; i++)
        {
            set.Objects.Add(new Mt.Authoring.Model.ObjectAsset { Id = $"Badge_Part{i}", Parent = "Badge_Body", Width = partSize, Height = partSize, Image = true, Alpha = true });
        }

        return set;
    }
}
