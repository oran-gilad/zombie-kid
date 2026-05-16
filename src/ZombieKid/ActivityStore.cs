using System.Text.Json;

namespace ZombieKid;

public sealed class ActivityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _dataDirectory;

    public ActivityStore(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        Directory.CreateDirectory(_dataDirectory);
    }

    public DailyActivity LoadToday(TimeSpan limit)
    {
        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        var path = DailyPath(today);
        DailyActivity activity;

        if (File.Exists(path))
        {
            activity = JsonSerializer.Deserialize<DailyActivity>(File.ReadAllText(path), JsonOptions) ?? new DailyActivity { Date = today };
        }
        else
        {
            activity = new DailyActivity { Date = today };
        }

        UpdateComputedFields(activity, limit);
        Save(activity);
        return activity;
    }

    public void Save(DailyActivity activity)
    {
        File.WriteAllText(DailyPath(activity.Date), JsonSerializer.Serialize(activity, JsonOptions));
        UpdateIndex();
    }

    public void AddSeconds(DailyActivity activity, IEnumerable<string> runningProcesses, int seconds, TimeSpan limit)
    {
        var running = runningProcesses.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (running.Count == 0 || seconds <= 0)
        {
            UpdateComputedFields(activity, limit);
            Save(activity);
            return;
        }

        activity.TotalSeconds += seconds;
        foreach (var processName in running)
        {
            if (!activity.Processes.TryGetValue(processName, out var processActivity))
            {
                processActivity = new ProcessActivity();
                activity.Processes[processName] = processActivity;
            }

            processActivity.Seconds += seconds;
            processActivity.Time = FormatSeconds(processActivity.Seconds);
        }

        UpdateComputedFields(activity, limit);
        Save(activity);
    }

    public void AddEvent(DailyActivity activity, string type, string message, IEnumerable<string>? processes, TimeSpan limit)
    {
        activity.Events.Add(new ActivityEvent
        {
            Type = type,
            Message = message,
            Processes = processes?.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList() ?? []
        });
        UpdateComputedFields(activity, limit);
        Save(activity);
    }

    private void UpdateIndex()
    {
        var files = Directory.EnumerateFiles(_dataDirectory, "????-??-??.json")
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        File.WriteAllText(Path.Combine(_dataDirectory, "index.json"), JsonSerializer.Serialize(new ActivityIndex { Files = files }, JsonOptions));
    }

    private string DailyPath(string date) => Path.Combine(_dataDirectory, date + ".json");

    private static void UpdateComputedFields(DailyActivity activity, TimeSpan limit)
    {
        activity.LastUpdatedLocal = DateTime.Now.ToString("O");
        activity.LimitSeconds = (long)limit.TotalSeconds;
        activity.Limit = FormatSeconds(activity.LimitSeconds);
        activity.TotalTime = FormatSeconds(activity.TotalSeconds);
        activity.RemainingSeconds = Math.Max(0, activity.LimitSeconds - activity.TotalSeconds);
        activity.Remaining = FormatSeconds(activity.RemainingSeconds);
    }

    public static string FormatSeconds(long seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
        var hours = (long)value.TotalHours;
        return $"{hours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
