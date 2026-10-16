namespace CS2Browser.Layout;

/// <summary>
/// display:inline 계열 요소용 레이아웃 핸들러.
/// 순수 인라인 텍스트는 부모의 <see cref="InlineFlow"/> 에서 곧바로 흐르므로,
/// 이 핸들러는 이미지 등을 품은 인라인 요소가 독립 박스로 배치될 때 선택된다.
/// 내용 배치는 블록과 동일한 <see cref="ContainerLayoutHandler"/> 규칙을 따른다.
/// </summary>
internal sealed class InlineLayoutHandler : ContainerLayoutHandler
{
    public static readonly InlineLayoutHandler Instance = new();
}
