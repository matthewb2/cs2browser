using System.Runtime.CompilerServices;
using AngleSharp.Dom;

namespace CS2Browser.Engine;

/// <summary>
/// 원본 DOM 위에 놓이는 하이레벨 캐시 DOM.
/// 자바스크립트는 캐시 문서(<see cref="Document"/>)만 조작하고, 스크립트 작업(task)이 끝날 때마다
/// <see cref="Commit"/> 이 누적된 변경(MutationRecord)만 원본 문서(<see cref="Original"/>)에 반영한다.
/// 레이아웃/렌더링은 항상 원본 문서를 기준으로 하므로 스크립트 실행 중의 중간 상태는 화면에 노출되지 않는다.
/// (NetSurf 가 DOM 변경을 모아 두었다가 예약된 reformat 에서 박스 트리를 다시 만드는 흐름과 같다.)
/// </summary>
public sealed class DomCache : IDisposable
{
    private static readonly ConditionalWeakTable<IDocument, DomCache> ByDocument = new();

    private readonly ConditionalWeakTable<INode, INode> _cacheToOriginal = new();
    private readonly ConditionalWeakTable<INode, INode> _originalToCache = new();
    private readonly MutationObserver _observer;
    private readonly List<IMutationRecord> _pending = new();
    private readonly object _gate = new();
    private bool _committing;
    private bool _disposed;

    private DomCache(IDocument original)
    {
        Original = original;
        Document = (IDocument)original.Clone(true);
        MapTree(Document, Original, notify: false);

        _observer = new MutationObserver((records, _) =>
        {
            lock (_gate)
            {
                _pending.AddRange(records);
            }
        });
        _observer.Connect(Document, childList: true, subtree: true, attributes: true, characterData: true);

        ByDocument.AddOrUpdate(Original, this);
        ByDocument.AddOrUpdate(Document, this);
    }

    /// <summary>레이아웃/렌더링이 사용하는 원본 문서.</summary>
    public IDocument Original { get; }

    /// <summary>자바스크립트가 조작하는 캐시 문서.</summary>
    public IDocument Document { get; }

    /// <summary>커밋으로 원본 문서가 실제로 바뀌었을 때 발생한다.</summary>
    public event EventHandler? Committed;

    /// <summary>캐시 노드와 원본 노드가 새로 연결되었을 때 발생한다(캐시 노드, 원본 노드).</summary>
    public event Action<INode, INode>? NodeMapped;

    /// <summary>원본 문서에 대한 캐시를 만든다. 이미 있으면 기존 캐시를 반환한다.</summary>
    public static DomCache GetOrCreate(IDocument original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (ByDocument.TryGetValue(original, out var existing) && !existing._disposed
            && ReferenceEquals(existing.Original, original))
        {
            return existing;
        }

        return new DomCache(original);
    }

    /// <summary>원본 문서 또는 캐시 문서로 캐시를 찾는다.</summary>
    public static bool TryGet(IDocument document, out DomCache cache)
    {
        if (ByDocument.TryGetValue(document, out var found) && !found._disposed)
        {
            cache = found;
            return true;
        }

        cache = null!;
        return false;
    }

    public INode? GetOriginal(INode cacheNode)
        => _cacheToOriginal.TryGetValue(cacheNode, out var original) ? original : null;

    public INode? GetCached(INode originalNode)
        => _originalToCache.TryGetValue(originalNode, out var cached) ? cached : null;

    /// <summary>
    /// 캐시 문서의 변경을 원본 문서에 동기화한다. 변경이 없으면 아무것도 하지 않는다.
    /// </summary>
    /// <returns>원본 문서가 바뀌었으면 true.</returns>
    public bool Commit()
    {
        if (_disposed || _committing)
        {
            return false;
        }

        IMutationRecord[] records;
        lock (_gate)
        {
            _pending.AddRange(_observer.Flush());
            if (_pending.Count == 0)
            {
                return false;
            }

            records = _pending.ToArray();
            _pending.Clear();
        }

        _committing = true;
        var changed = false;
        try
        {
            // 1) 캐시 트리에서 빠진 노드는 연결을 끊는다. 나중에 다시 삽입되면 새로 복제된다.
            foreach (var record in records)
            {
                if (record.Removed is not { } removed)
                {
                    continue;
                }

                foreach (var node in removed)
                {
                    if (!IsInCacheTree(node))
                    {
                        UnmapTree(node);
                    }
                }
            }

            // 2) 변경 대상 노드(속성/텍스트/자식 목록)만 원본에 반영한다.
            var seen = new HashSet<INode>(ReferenceEqualityComparer.Instance);
            foreach (var record in records)
            {
                if (!seen.Add(record.Target))
                {
                    continue;
                }

                try
                {
                    changed |= Reconcile(record.Target);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DomCache] 동기화 실패: {ex.Message}");
                }
            }
        }
        finally
        {
            _committing = false;
        }

        if (changed)
        {
            Committed?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _observer.Disconnect();
        ByDocument.Remove(Original);
        ByDocument.Remove(Document);
    }

    private bool IsInCacheTree(INode node) => Document.Contains(node);

    private bool Reconcile(INode cacheNode)
    {
        if (!IsInCacheTree(cacheNode) || GetOriginal(cacheNode) is not { } original)
        {
            return false;
        }

        var changed = cacheNode switch
        {
            IElement element when original is IElement originalElement => SyncAttributes(element, originalElement),
            ICharacterData data when original is ICharacterData originalData => SyncData(data, originalData),
            _ => false
        };

        return SyncChildren(cacheNode, original) | changed;
    }

    private static bool SyncData(ICharacterData cached, ICharacterData original)
    {
        if (string.Equals(cached.Data, original.Data, StringComparison.Ordinal))
        {
            return false;
        }

        original.Data = cached.Data;
        return true;
    }

    private static bool SyncAttributes(IElement cached, IElement original)
    {
        var changed = false;
        foreach (var attr in original.Attributes.ToArray())
        {
            if (!cached.HasAttribute(attr.NamespaceUri, attr.LocalName))
            {
                original.RemoveAttribute(attr.NamespaceUri, attr.LocalName);
                changed = true;
            }
        }

        foreach (var attr in cached.Attributes.ToArray())
        {
            var current = original.GetAttribute(attr.NamespaceUri, attr.LocalName);
            if (string.Equals(current, attr.Value, StringComparison.Ordinal))
            {
                continue;
            }

            if (attr.NamespaceUri is null)
            {
                original.SetAttribute(attr.Name, attr.Value);
            }
            else
            {
                original.SetAttribute(attr.NamespaceUri, attr.Name, attr.Value);
            }

            changed = true;
        }

        return changed;
    }

    private bool SyncChildren(INode cacheParent, INode originalParent)
    {
        var desired = new List<INode>(cacheParent.ChildNodes.Length);
        foreach (var child in cacheParent.ChildNodes)
        {
            desired.Add(GetOriginal(child) ?? ImportIntoOriginal(child));
        }

        var changed = false;
        for (var i = 0; i < desired.Count; i++)
        {
            var current = i < originalParent.ChildNodes.Length ? originalParent.ChildNodes[i] : null;
            if (!ReferenceEquals(current, desired[i]))
            {
                originalParent.InsertBefore(desired[i], current);
                changed = true;
            }
        }

        while (originalParent.ChildNodes.Length > desired.Count)
        {
            originalParent.RemoveChild(originalParent.LastChild!);
            changed = true;
        }

        return changed;
    }

    private INode ImportIntoOriginal(INode cacheNode)
    {
        var imported = Original.Import(cacheNode, true);
        MapTree(cacheNode, imported, notify: true);
        return imported;
    }

    private void MapTree(INode cacheNode, INode originalNode, bool notify)
    {
        _cacheToOriginal.AddOrUpdate(cacheNode, originalNode);
        _originalToCache.AddOrUpdate(originalNode, cacheNode);
        if (notify)
        {
            NodeMapped?.Invoke(cacheNode, originalNode);
        }

        var cacheChildren = cacheNode.ChildNodes;
        var originalChildren = originalNode.ChildNodes;
        var count = Math.Min(cacheChildren.Length, originalChildren.Length);
        for (var i = 0; i < count; i++)
        {
            MapTree(cacheChildren[i], originalChildren[i], notify);
        }
    }

    private void UnmapTree(INode cacheNode)
    {
        if (_cacheToOriginal.TryGetValue(cacheNode, out var original))
        {
            _originalToCache.Remove(original);
            _cacheToOriginal.Remove(cacheNode);
        }

        foreach (var child in cacheNode.ChildNodes)
        {
            UnmapTree(child);
        }
    }
}
