namespace API.Models
{
    public class DatabaseSettings
    {
        public string RotationStrategy { get; set; } = "SizeBased";
        public int MaxDatabaseSizeMB { get; set; } = 500;
        public string CurrentDatabase { get; set; } = "Database1";
        public bool NotifyOnRotation { get; set; } = true;
    }

    public class CleanupSettings
    {
        public bool Enabled { get; set; } = true;
        public int IntervalHours { get; set; } = 120; // 5 days
        public int RetentionDays { get; set; } = 5;
        public bool NotifyOnCleanup { get; set; } = true;
    }
}
