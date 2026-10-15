namespace CS2Browser.Tests;

/// <summary>테스트에서 공용으로 쓰는 HTML 문서들.</summary>
internal static class TestDocuments
{
    public const string Simple =
        """
        <!doctype html>
        <html>
          <head><title>Simple Page</title></head>
          <body style="margin: 8px">
            <div id="a" style="background-color: #ff0000">A</div>
          </body>
        </html>
        """;

    public const string Flex =
        """
        <!doctype html>
        <html>
          <body style="margin: 10px">
            <div style="display: flex; flex-direction: row;">
              <div style="font-size: 20px">One</div>
              <div style="font-size: 20px">Two</div>
              <div style="font-size: 20px">Three</div>
            </div>
          </body>
        </html>
        """;

    public const string WithScript =
        """
        <!doctype html>
        <html>
          <body>
            <div id="target">before</div>
            <script>document.getElementById('target').textContent = 'after';</script>
          </body>
        </html>
        """;
}
