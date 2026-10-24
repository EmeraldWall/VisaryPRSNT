using System.Text.Json;
using Visary.Core.Model;

namespace Visary.Core.Documents;

/// <summary>
/// Snapshot based undo and redo. Each step stores the slides as JSON; media is append only,
/// so it is shared between snapshots instead of being copied.
/// </summary>
public sealed class History
{
    readonly List<string> _undo = [];
    readonly List<string> _redo = [];
    string? _gestureBase;
    string? _coalesceKey;
    DateTime _coalesceTime;
    const int Limit = 150;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;

    static string Snap(Presentation p) => JsonSerializer.Serialize(p, Json.Options);

    /// <summary>Call before a change. Changes with the same key inside a short window merge into one step.</summary>
    public void Record(Presentation p, string? coalesceKey = null)
    {
        if (_gestureBase != null) return;
        var now = DateTime.UtcNow;
        if (coalesceKey != null && coalesceKey == _coalesceKey && now - _coalesceTime < TimeSpan.FromSeconds(1.2))
        {
            _coalesceTime = now;
            return;
        }
        Push(_undo, Snap(p));
        _redo.Clear();
        _coalesceKey = coalesceKey;
        _coalesceTime = now;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts a drag or resize. Only the state before the gesture becomes an undo step.</summary>
    public void BeginGesture(Presentation p) => _gestureBase ??= Snap(p);

    public void EndGesture(Presentation p)
    {
        if (_gestureBase is null) return;
        var now = Snap(p);
        if (now != _gestureBase)
        {
            Push(_undo, _gestureBase);
            _redo.Clear();
        }
        _gestureBase = null;
        _coalesceKey = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Presentation? Undo(Presentation current) => Step(_undo, _redo, current);
    public Presentation? Redo(Presentation current) => Step(_redo, _undo, current);

    Presentation? Step(List<string> from, List<string> to, Presentation current)
    {
        if (from.Count == 0) return null;
        var json = from[^1];
        from.RemoveAt(from.Count - 1);
        Push(to, Snap(current));
        var p = JsonSerializer.Deserialize<Presentation>(json, Json.Options)!;
        p.Media = current.Media;
        _coalesceKey = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return p;
    }

    static void Push(List<string> list, string s)
    {
        list.Add(s);
        if (list.Count > Limit) list.RemoveAt(0);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _gestureBase = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
