using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using AngleSharp.Scripting;
using CS2Browser.Scripting;

namespace CS2Browser.Engine;

public sealed class V8ScriptingService : IScriptingService
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IDocument, V8DomContext> Contexts = new();

    public static V8DomContext GetOrCreateContext(IDocument document)
    {
        return Contexts.GetValue(document, doc => new V8DomContext(doc));
    }

    public bool SupportsType(string type)
    {
        return string.IsNullOrEmpty(type) ||
               type.Equals("text/javascript", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("application/ecmascript", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("module", StringComparison.OrdinalIgnoreCase);
    }

    public Task EvaluateScriptAsync(IResponse response, ScriptOptions options, CancellationToken cancellationToken)
    {
        var document = options.Document;
        if (document is null)
        {
            return Task.CompletedTask;
        }

        string source = string.Empty;
        if (options.Element is IHtmlScriptElement scriptEl)
        {
            source = scriptEl.TextContent;
        }

        if (string.IsNullOrEmpty(source) && response?.Content != null)
        {
            using var reader = new StreamReader(response.Content);
            source = reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return Task.CompletedTask;
        }

        var v8 = GetOrCreateContext(document);
        try
        {
            v8.Execute(source);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[V8ScriptingService] Exception in V8 execution: {ex}");
            throw;
        }

        return Task.CompletedTask;
    }
}
