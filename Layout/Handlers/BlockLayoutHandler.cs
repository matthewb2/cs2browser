namespace CS2Browser.Layout;

/// <summary>
/// display:block 계열 컨테이너용 레이아웃 핸들러.
/// 자식 블록/이미지는 세로로 쌓고, 인라인 내용은 <see cref="InlineFlow"/> 로 흘려보낸다.
/// </summary>
internal sealed class BlockLayoutHandler : ContainerLayoutHandler
{
    public static readonly BlockLayoutHandler Instance = new();
}
