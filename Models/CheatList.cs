using System.Collections.Generic;

namespace CheatFinderRust.Models
{
    /// <summary>
    /// Модель списка читов из JSON файла
    /// </summary>
    public class CheatList
    {
        public List<string> Cheats { get; set; } = new List<string>();
    }
}

