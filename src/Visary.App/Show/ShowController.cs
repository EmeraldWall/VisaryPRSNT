using Microsoft.UI.Dispatching;
using Visary.App.Shell;
using Visary.App.Transitions;
using Visary.Core.Model;
using Visary.Core.Themes;
using Visary.Core.Transitions;

namespace Visary.App.Show;

/// <summary>Slide show navigation: slides, object animation steps, timings and looping.</summary>
public sealed class ShowController
{
    readonly Presentation _pres;
    readonly List<int> _visible;
    readonly DispatcherQueueTimer _auto;
    readonly string _accent;
    List<BuildStep> _steps = [];
    bool _busy;

    public TransitionStage Stage { get; }
    public int Index { get; private set; }
    public int Step { get; private set; }
    public int StepCount => _steps.Count;
    public bool AtEnd { get; private set; }
    public Presentation Presentation => _pres;
    public DateTime StartedAt { get; private set; } = DateTime.Now;

    public event Action? StateChanged;
    public event Action? EndRequested;

    public ShowController(Presentation pres, int startIndex)
    {
        _pres = pres;
        _accent = SlideThemes.Get(pres.ThemeId).Accents[0];
        _visible = Enumerable.Range(0, pres.Slides.Count).Where(i => !pres.Slides[i].Hidden || i == startIndex).ToList();
        if (_visible.Count == 0) _visible.Add(0);
        Index = _visible.Contains(startIndex) ? startIndex : _visible[0];
        Stage = new TransitionStage(pres) { Prepare = BuildAnimator.ApplyInitialState };
        _auto = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _auto.IsRepeating = false;
        _auto.Tick += (_, _) => Next();
    }

    Slide Slide => _pres.Slides[Index];

    public async Task StartAsync()
    {
        StartedAt = DateTime.Now;
        await EnterAsync(Index, withTransition: true, builtUp: false);
    }

    async Task EnterAsync(int index, bool withTransition, bool builtUp)
    {
        _auto.Stop();
        AtEnd = false;
        Index = index;
        _steps = BuildTimeline.Steps(Slide.Builds);
        Step = 0;
        StateChanged?.Invoke();
        _busy = true;
        try
        {
            await Stage.ShowAsync(Slide, withTransition ? Slide.Transition : null, _accent, BuildAnimator.VisibleScene);
            if (builtUp)
            {
                foreach (var s in _steps) await BuildAnimator.PlayStepAsync(Stage.Current!, s, instant: true);
                Step = _steps.Count;
            }
            else if (_steps.Count > 0 && _steps[0].Automatic)
            {
                Step = 1;
                StateChanged?.Invoke();
                await BuildAnimator.PlayStepAsync(Stage.Current!, _steps[0]);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Entering slide", ex);
        }
        finally
        {
            _busy = false;
        }
        StateChanged?.Invoke();
        ScheduleAdvance();
    }

    void ScheduleAdvance()
    {
        if (Slide.Transition.AdvanceAfterSeconds is { } s && s >= 0)
        {
            _auto.Interval = TimeSpan.FromSeconds(Math.Max(.05, s));
            _auto.Start();
        }
    }

    public bool ClickAdvances => Slide.Transition.AdvanceOnClick;

    public async void Next()
    {
        try
        {
            if (Stage.IsPlaying) { Stage.Skip(); return; }
            if (_busy) return;
            if (AtEnd) { EndRequested?.Invoke(); return; }
            _auto.Stop();
            if (Step < _steps.Count)
            {
                var step = _steps[Step++];
                StateChanged?.Invoke();
                _busy = true;
                try { await BuildAnimator.PlayStepAsync(Stage.Current!, step); }
                finally { _busy = false; }
                ScheduleAdvance();
                return;
            }
            var pos = _visible.IndexOf(Index);
            if (pos >= 0 && pos < _visible.Count - 1) await EnterAsync(_visible[pos + 1], true, false);
            else if (_pres.Loop) await EnterAsync(_visible[0], true, false);
            else
            {
                AtEnd = true;
                StateChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log.Error("Next", ex);
        }
    }

    public async void Previous()
    {
        try
        {
            if (Stage.IsPlaying) Stage.Skip();
            if (_busy) return;
            if (AtEnd) { AtEnd = false; StateChanged?.Invoke(); return; }
            var minStep = _steps.Count > 0 && _steps[0].Automatic ? 1 : 0;
            if (Step > minStep)
            {
                var target = Step - 1;
                await EnterAsync(Index, false, false);
                foreach (var s in _steps.Take(target)) await BuildAnimator.PlayStepAsync(Stage.Current!, s, instant: true);
                Step = target;
                StateChanged?.Invoke();
                return;
            }
            var pos = _visible.IndexOf(Index);
            if (pos > 0) await EnterAsync(_visible[pos - 1], false, true);
            else if (_pres.Loop && _visible.Count > 1) await EnterAsync(_visible[^1], false, true);
        }
        catch (Exception ex)
        {
            Log.Error("Previous", ex);
        }
    }

    public async void GoTo(int index)
    {
        if (index < 0 || index >= _pres.Slides.Count) return;
        if (Stage.IsPlaying) Stage.Skip();
        await EnterAsync(index, true, false);
    }

    public int NextSlideIndex()
    {
        var pos = _visible.IndexOf(Index);
        return pos >= 0 && pos < _visible.Count - 1 ? _visible[pos + 1] : -1;
    }

    public void Stop() => _auto.Stop();
}
