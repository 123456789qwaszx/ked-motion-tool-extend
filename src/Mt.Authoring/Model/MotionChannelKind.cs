namespace Mt.Authoring.Model;

/// <summary>채널이 움직이는 오브젝트의 속성.</summary>
public enum MotionChannelKind
{
    /// <summary>자리(X·Y). 부모 안의 px.</summary>
    Position,

    /// <summary>배율(X·Y).</summary>
    Scale,

    /// <summary>회전(X·Y·Z). 도.</summary>
    Euler,

    /// <summary>불투명도(X 하나, 0~1).</summary>
    Alpha,
}
