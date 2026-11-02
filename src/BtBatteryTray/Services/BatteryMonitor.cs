using BtBatteryTray.Models;
using Microsoft.UI.Dispatching;

namespace BtBatteryTray.Services;

/// <summary>Polls battery levels on the UI thread's dispatcher, so <see cref="Updated"/> is always raised on the UI thread.</summary>
public sealed class BatteryMonitor
{
    readonly DispatcherQueueTimer _timer;
    bool _refreshing;

    public BatteryMonitor(DispatcherQueue dispatcher, TimeSpan interval)
    {
        _timer = dispatcher.CreateTimer();
        _timer.Interval = interval;
        _timer.IsRepeating = true;
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public IReadOnlyList<BtBattery> Current { get; private set; } = [];
    public DateTime? LastRefresh { get; private set; }

    public event EventHandler? Updated;

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public void Start()
    {
        _timer.Start();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;
        try
        {
            Current = await BatteryReader.ReadAllAsync();
            LastRefresh = DateTime.Now;
            Log.Write("Poll: " + (Current.Count == 0 ? "(none)" : string.Join(", ", Current.Select(b => $"{b.Name}={b.Level}%"))));
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Write($"Poll failed: {ex}");
        }
        finally
        {
            _refreshing = false;
        }
    }
}
