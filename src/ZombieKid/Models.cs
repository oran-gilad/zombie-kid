using System.Text.Json.Serialization;

namespace ZombieKid;

public sealed class AppSettings
{
    public string DailyLimit { get; set; } = "00:02:00";
    public int PollIntervalSeconds { get; set; } = 5;
    public int AlmostOverMinutes { get; set; } = 1;
    public List<string> ProcessNames { get; set; } = ["notepad.exe", "notepad++.exe"];
    public string DataDirectory { get; set; } = @"C:\Users\oran\OneDrive\Documents\zombie-kid\data";
    public GitSyncSettings GitSync { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
}

public sealed class GitSyncSettings
{
    public bool Enabled { get; set; } = true;
    public string RepositoryDirectory { get; set; } = @"C:\Users\oran\OneDrive\Documents\zombie-kid";
    public int SyncIntervalMinutes { get; set; } = 10;
}

public sealed class EmailSettings
{
    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = "smtp.example.com";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "zombie-kid@example.com";
    public List<string> Recipients { get; set; } = [];
    public List<string> SummaryEmailTimes { get; set; } = ["17:00", "21:00"];
}

public sealed class DailyActivity
{
    public string Date { get; set; } = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
    public string LastUpdatedLocal { get; set; } = DateTime.Now.ToString("O");
    public string Limit { get; set; } = "00:02:00";
    public string TotalTime { get; set; } = "00:00:00";
    public string Remaining { get; set; } = "00:02:00";
    public long TotalSeconds { get; set; }
    public long LimitSeconds { get; set; } = 120;
    public long RemainingSeconds { get; set; } = 120;
    public bool AlmostOverNotified { get; set; }
    public bool ThresholdEmailSent { get; set; }
    public Dictionary<string, bool> SummaryEmailsSent { get; set; } = [];
    public Dictionary<string, ProcessActivity> Processes { get; set; } = [];
    public List<ActivityEvent> Events { get; set; } = [];
}

public sealed class ProcessActivity
{
    public string Time { get; set; } = "00:00:00";
    public long Seconds { get; set; }
}

public sealed class ActivityEvent
{
    public string TimeLocal { get; set; } = DateTime.Now.ToString("O");
    public string Type { get; set; } = "info";
    public string Message { get; set; } = "";
    public List<string> Processes { get; set; } = [];
}

public sealed class ActivityIndex
{
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = [];
}
