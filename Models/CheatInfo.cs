using System;

namespace CheatFinderRust.Models
{
    /// <summary>
    /// Информация о найденном чите
    /// </summary>
    public class CheatInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CheatType Type { get; set; }
        public DateTime FoundAt { get; set; }
        public long Size { get; set; }

        public CheatInfo()
        {
            FoundAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Тип чита
    /// </summary>
    public enum CheatType
    {
        RustPirate,
        RustOfficial,
        CommonCheat
    }
}

