using System.Diagnostics;

namespace ZombieKid;

public sealed class ActivityMonitor
{
    private readonly AppSettings _settings;
    private readonly ActivityStore _store;
    private readonly EmailNotifier _emailNotifier;
    private readonly GitSyncService _gitSyncService;
    private readonly GitHubApiSyncService _gitHubApiSyncService;
    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _cancellation = new();
    private DailyActivity _activity;
    private DateOnly _activityDate;

    public ActivityMonitor(AppSettings settings, NotifyIcon notifyIcon)
    {
        _settings = settings;
        _notifyIcon = notifyIcon;
        _store = new ActivityStore(settings.DataDirectory);
        _emailNotifier = new EmailNotifier(settings.Email);
        _gitSyncService = new GitSyncService(settings.GitSync);
        _gitHubApiSyncService = new GitHubApiSyncService(settings.GitHubApiSync, settings.DataDirectory);
        Limit = ParseLimit(settings.DailyLimit);
        _activity = _store.LoadToday(Limit);
        _activityDate = DateOnly.FromDateTime(DateTime.Now);
    }

    public TimeSpan Limit { get; }

    public void Start() => _ = RunAsync(_cancellation.Token);

    public void Stop() => _cancellation.Cancel();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                EnsureTodayActivity();
                var runningProcesses = GetRunningConfiguredProcesses();
                var pollSeconds = Math.Max(1, _settings.PollIntervalSeconds);
                _store.AddSeconds(_activity, runningProcesses, runningProcesses.Count > 0 ? pollSeconds : 0, Limit);

                await MaybeNotifyAlmostOverAsync(cancellationToken);
                await MaybeEnforceLimitAsync(cancellationToken);
                await MaybeSendSummariesAsync(cancellationToken);
                await _gitHubApiSyncService.TrySyncAsync(cancellationToken);
                await _gitSyncService.TrySyncAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _store.AddEvent(_activity, "error", ex.Message, null, Limit);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _settings.PollIntervalSeconds)), cancellationToken);
        }
    }

    private void EnsureTodayActivity()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today == _activityDate)
        {
            return;
        }

        _activityDate = today;
        _activity = _store.LoadToday(Limit);
    }

    private List<string> GetRunningConfiguredProcesses()
    {
        var configured = _settings.ProcessNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Process.GetProcesses()
            .Select(static process =>
            {
                try
                {
                    return SettingsLoader.NormalizeProcessName(process.ProcessName);
                }
                catch
                {
                    return string.Empty;
                }
            })
            .Where(configured.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task MaybeNotifyAlmostOverAsync(CancellationToken cancellationToken)
    {
        if (_activity.AlmostOverNotified)
        {
            return;
        }

        var thresholdSeconds = _settings.AlmostOverMinutes * 60;
        if (_activity.RemainingSeconds <= thresholdSeconds && _activity.TotalSeconds < _activity.LimitSeconds)
        {
            _activity.AlmostOverNotified = true;
            var message = $"Playing limit almost over ({_settings.AlmostOverMinutes} minutes more)";
            ShowBalloon("ZombieKid", message, ToolTipIcon.Warning);
            _store.AddEvent(_activity, "almost-over", message, null, Limit);
            await _gitHubApiSyncService.TrySyncAsync(cancellationToken, force: true);
            await _gitSyncService.TrySyncAsync(cancellationToken);
        }
    }

    private async Task MaybeEnforceLimitAsync(CancellationToken cancellationToken)
    {
        if (_activity.TotalSeconds < _activity.LimitSeconds)
        {
            return;
        }

        var closed = CloseConfiguredProcesses();
        if (closed.Count == 0)
        {
            return;
        }

        var message = "Daily playing limit reached. Configured game processes were closed.";
        ShowBalloon("ZombieKid", message, ToolTipIcon.Error);
        _store.AddEvent(_activity, "limit-reached", message, closed, Limit);

        if (!_activity.ThresholdEmailSent)
        {
            try
            {
                await _emailNotifier.SendThresholdAsync(_activity, closed, cancellationToken);
                _activity.ThresholdEmailSent = true;
                _store.Save(_activity);
            }
            catch (Exception ex)
            {
                _store.AddEvent(_activity, "email-error", ex.Message, null, Limit);
            }
        }

        await _gitHubApiSyncService.TrySyncAsync(cancellationToken, force: true);
    }

    private List<string> CloseConfiguredProcesses()
    {
        var closed = new List<string>();
        var configured = _settings.ProcessNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processName = SettingsLoader.NormalizeProcessName(process.ProcessName);
                if (!configured.Contains(processName))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                closed.Add(processName);
            }
            catch
            {
                // Process may exit between enumeration and kill, or access may be denied.
            }
            finally
            {
                process.Dispose();
            }
        }

        return closed.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task MaybeSendSummariesAsync(CancellationToken cancellationToken)
    {
        foreach (var summaryTime in _settings.Email.SummaryEmailTimes)
        {
            if (_activity.SummaryEmailsSent.ContainsKey(summaryTime) || !TimeOnly.TryParse(summaryTime, out var time))
            {
                continue;
            }

            var now = TimeOnly.FromDateTime(DateTime.Now);
            if (now.Hour == time.Hour && now.Minute >= time.Minute)
            {
                try
                {
                    await _emailNotifier.SendSummaryAsync(_activity, cancellationToken);
                    _activity.SummaryEmailsSent[summaryTime] = true;
                    _store.Save(_activity);
                }
                catch (Exception ex)
                {
                    _store.AddEvent(_activity, "email-error", ex.Message, null, Limit);
                }
            }
        }
    }

    private void ShowBalloon(string title, string message, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private static TimeSpan ParseLimit(string value)
    {
        return TimeSpan.TryParse(value, out var parsed) ? parsed : TimeSpan.FromMinutes(2);
    }
}
