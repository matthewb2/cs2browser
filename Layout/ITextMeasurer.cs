namespace CS2Browser.Layout;

/// <summary>
/// 텍스트 폭을 측정하는 추상화. 레이아웃은 이 측정값으로 단어 위치와 줄바꿈을 계산한다.
/// 실제 렌더러(DirectWrite)를 주입하면 렌더링과 동일한 글꼴 메트릭을 사용할 수 있다.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>주어진 텍스트의 렌더링 폭(px)을 반환한다.</summary>
    float MeasureWidth(string text, float fontSize, string fontFamily, bool bold, bool italic);
}
