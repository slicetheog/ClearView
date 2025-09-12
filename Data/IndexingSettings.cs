using System;

namespace SpotlightClean.Data
{
    public enum IndexingSchedule
    {
        Manual,
        OnStartup,
        Interval
    }

    public class IndexingSettings
    {
        public IndexingSchedule Schedule { get; set; } = IndexingSchedule.OnStartup;
        public int IntervalValue { get; set; } = 24;
        public string IntervalUnit { get; set; } = "Hours";
        public DateTime LastIndexedUtc { get; set; } = DateTime.MinValue;
    }
}