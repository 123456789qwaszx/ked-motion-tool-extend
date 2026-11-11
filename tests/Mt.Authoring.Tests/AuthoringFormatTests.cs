using Ked.Presentation.Core;

namespace Mt.Authoring.Tests;

public class AuthoringFormatTests
{
    [Fact]
    public void 저작_원본의_스키마와_확장자()
    {
        Assert.Equal("motion-authoring/2", AuthoringFormat.AuthoringSchema);
        Assert.Equal("motion-authoring/1", AuthoringFormat.AuthoringSchemaV1); // 옛 판 — 읽을 수 있다
        Assert.EndsWith(".json", AuthoringFormat.AuthoringExtension);
    }

    [Fact]
    public void 반입_코어의_컷_재생이_보이고_산출물은_컷_파일이다()
    {
        // Mt.Authoring → Ked.Presentation.Core 참조가 살아 있는지. 산출물의 형식 이름은 코어가 든다 — 저작 원본의 스키마와 다르다.
        Assert.Equal("cut/1", CutPlayer.Format);
        Assert.NotEqual(AuthoringFormat.AuthoringSchema, CutPlayer.Format);
    }
}
