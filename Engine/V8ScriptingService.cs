using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using AngleSharp.Scripting;
using CS2Browser.Scripting;

namespace CS2Browser.Engine;

/// <summary>
/// AngleSharp 스크립팅 서비스. 스크립트는 항상 <see cref="DomCache"/> 의 캐시 문서에서 실행된다.
/// <list type="bullet">
/// <item>파싱 중(캐시가 아직 없음): 스크립트를 큐에 쌓아 두고, 파싱이 끝나면 <see cref="RunPendingScripts"/> 가 순서대로 실행한다.</item>
/// <item>캐시 문서에 동적으로 삽입된 스크립트: 즉시 실행한다.</item>
/// <item>원본 문서(캐시의 미러): 실행하지 않는다. 동기화로 삽입된 스크립트가 다시 실행되는 것을 막는다.</item>
/// </list>
/// </summary>
public sealed class V8ScriptingService : IScriptingService
{
    private static readonly ConditionalWeakTable<IDocument, V8DomContext> Contexts = new();
    private static readonly ConditionalWeakTable<IDocument, List<string>> PendingScripts = new();

    /// <summary>
    /// 문서의 V8 컨텍스트를 반환한다. 원본 문서를 넘겨도 캐시가 있으면 캐시 문서의 컨텍스트를 돌려준다.
    /// </summary>
    public static V8DomContext GetOrCreateContext(IDocument document)
    {
        var target = DomCache.TryGet(document, out var cache) ? cache.Document : document;
        return Contexts.GetValue(target, doc => new V8DomContext(doc));
    }

    /// <summary>문서의 V8 컨텍스트를 정리한다(타이머 취소 포함).</summary>
    public static void ReleaseContext(IDocument document)
    {
        if (Contexts.TryGetValue(document, out var context))
        {
            Contexts.Remove(document);
            context.Dispose();
        }
    }

    /// <summary>파싱 중 큐에 쌓인 스크립트를 캐시 문서 컨텍스트에서 순서대로 실행한다.</summary>
    public static void RunPendingScripts(IDocument document)
    {
        List<string> scripts;
        lock (PendingScripts)
        {
            if (!PendingScripts.TryGetValue(document, out var queued))
            {
                return;
            }

            PendingScripts.Remove(document);
            scripts = queued;
        }

        var context = GetOrCreateContext(document);
        foreach (var source in scripts)
        {
            try
            {
                context.Execute(source);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[V8ScriptingService] Exception in V8 execution: {ex}");
            }
        }
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

        var hasCache = DomCache.TryGet(document, out var cache);
        if (hasCache && ReferenceEquals(document, cache.Original))
        {
            return Task.CompletedTask;
        }

        var source = ReadSource(response, options);
        if (string.IsNullOrWhiteSpace(source))
        {
            return Task.CompletedTask;
        }

        if (!hasCache)
        {
            lock (PendingScripts)
            {
                PendingScripts.GetOrCreateValue(document).Add(source);
            }

            return Task.CompletedTask;
        }

        try
        {
            GetOrCreateContext(document).Execute(source);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[V8ScriptingService] Exception in V8 execution: {ex}");
            throw;
        }

        return Task.CompletedTask;
    }

    private static string ReadSource(IResponse? response, ScriptOptions options)
    {
        var source = string.Empty;
        if (options.Element is IHtmlScriptElement scriptEl)
        {
            source = scriptEl.TextContent;
        }

        if (string.IsNullOrEmpty(source) && response?.Content != null)
        {
            using var reader = new StreamReader(response.Content);
            source = reader.ReadToEnd();
        }

        return source;
    }
}
