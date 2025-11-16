using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CheatFinderRust.Models;
using Newtonsoft.Json;

namespace CheatFinderRust.Services
{
    /// <summary>
    /// Сервис для сканирования дисков и поиска читов
    /// </summary>
    public class CheatScanner
    {
        private CheatList? _cheatList;
        private List<CheatInfo> _foundCheats;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<CheatInfo>? CheatFound;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<int>? ProgressChanged;

        public CheatScanner()
        {
            _foundCheats = new List<CheatInfo>();
            LoadCheatList();
        }

        /// <summary>
        /// Загрузка списка читов из JSON файла
        /// </summary>
        private void LoadCheatList()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "cheats.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    _cheatList = JsonConvert.DeserializeObject<CheatList>(json) ?? new CheatList();
                }
                else
                {
                    // Создаем список по умолчанию
                    _cheatList = new CheatList
                    {
                        Cheats = new List<string> 
                        { 
                            "ALKAD", "alkad", "RustPirate", "rustpirate", "PirateCheat", "piratecheat",
                            "RustOfficial", "rustofficial", "RustSteam", "ruststeam", "RustCheat", "rustcheat",
                            "RustHack", "rusthack", "cheats", "hacks", "aimbot", "esp", "wallhack",
                            "rust.dll", "rust.exe", "cheat.dll", "hack.dll", "aimbot.dll", "esp.dll",
                            "wallhack.dll", "rustcheat.exe", "rusthack.exe"
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Ошибка загрузки списка читов: {ex.Message}");
                _cheatList = new CheatList();
            }
        }

        /// <summary>
        /// Начать сканирование всех дисков
        /// </summary>
        public async Task<List<CheatInfo>> ScanAllDrivesAsync(IProgress<int>? progress = null)
        {
            _foundCheats.Clear();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                int totalDrives = drives.Length;
                int currentDrive = 0;

                foreach (var drive in drives)
                {
                    if (token.IsCancellationRequested)
                        break;

                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        OnStatusChanged($"Сканирование диска: {drive.Name}");
                        await ScanDirectoryAsync(drive.RootDirectory.FullName, token, progress);
                        currentDrive++;
                        progress?.Report((currentDrive * 100) / totalDrives);
                    }
                }

                OnStatusChanged($"Сканирование завершено. Найдено читов: {_foundCheats.Count}");
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Ошибка при сканировании: {ex.Message}");
            }

            return _foundCheats;
        }

        /// <summary>
        /// Сканирование директории
        /// </summary>
        private async Task ScanDirectoryAsync(string directory, CancellationToken token, IProgress<int>? progress = null)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                // Проверяем имя папки
                string folderName = Path.GetFileName(directory);
                if (!string.IsNullOrEmpty(folderName))
                {
                    CheckFolderName(folderName, directory);
                }

                // Получаем файлы в текущей директории
                string[]? files = null;
                try
                {
                    files = Directory.GetFiles(directory);
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
                catch (Exception)
                {
                    return;
                }

                // Проверяем файлы
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested)
                        return;

                    try
                    {
                        CheckFileName(Path.GetFileName(file), file);
                    }
                    catch { }
                }

                // Рекурсивно сканируем поддиректории
                string[]? directories = null;
                try
                {
                    directories = Directory.GetDirectories(directory);
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
                catch (Exception)
                {
                    return;
                }

                foreach (var dir in directories)
                {
                    if (token.IsCancellationRequested)
                        return;

                    // Пропускаем системные папки для ускорения
                    string dirName = Path.GetFileName(dir);
                    if (ShouldSkipDirectory(dirName))
                        continue;

                    await ScanDirectoryAsync(dir, token, progress);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Игнорируем ошибки доступа
            }
            catch (Exception)
            {
                // Игнорируем другие ошибки
            }
        }

        /// <summary>
        /// Проверка имени папки на наличие читов
        /// </summary>
        private void CheckFolderName(string folderName, string fullPath)
        {
            if (_cheatList == null || string.IsNullOrEmpty(folderName)) return;

            // Проверяем только по точному названию папки
            var matchedCheat = _cheatList.Cheats.FirstOrDefault(cheat => 
                folderName.Equals(cheat, StringComparison.OrdinalIgnoreCase));

            if (matchedCheat != null)
            {
                // Определяем тип чита по содержимому
                CheatType type = DetermineCheatType(matchedCheat, folderName);
                AddCheat(fullPath, folderName, type);
            }
        }

        /// <summary>
        /// Проверка имени файла на наличие читов
        /// </summary>
        private void CheckFileName(string fileName, string fullPath)
        {
            if (_cheatList == null || string.IsNullOrEmpty(fileName)) return;

            // Проверяем только по точному названию файла
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            var matchedCheat = _cheatList.Cheats.FirstOrDefault(cheat => 
                // Точное совпадение имени файла
                fileName.Equals(cheat, StringComparison.OrdinalIgnoreCase) ||
                // Совпадение имени файла без расширения
                fileNameWithoutExt.Equals(cheat, StringComparison.OrdinalIgnoreCase));

            if (matchedCheat != null)
            {
                // Определяем тип чита по содержимому
                CheatType type = DetermineCheatType(matchedCheat, fileName);
                AddCheat(fullPath, fileName, type);
            }
        }

        /// <summary>
        /// Определение типа чита по названию
        /// </summary>
        private CheatType DetermineCheatType(string matchedCheat, string fullName)
        {
            string lowerCheat = matchedCheat.ToLowerInvariant();
            string lowerName = fullName.ToLowerInvariant();

            // Проверка на Rust Pirate (ALKAD)
            if (lowerCheat.Contains("alkad") || lowerCheat.Contains("pirate") || 
                lowerName.Contains("alkad") || lowerName.Contains("pirate"))
            {
                return CheatType.RustPirate;
            }

            // Проверка на Rust Official (STEAM)
            if (lowerCheat.Contains("official") || lowerCheat.Contains("steam") ||
                lowerName.Contains("official") || lowerName.Contains("steam"))
            {
                return CheatType.RustOfficial;
            }

            // Остальное - общий чит
            return CheatType.CommonCheat;
        }

        /// <summary>
        /// Добавление найденного чита
        /// </summary>
        private void AddCheat(string path, string name, CheatType type)
        {
            var cheatInfo = new CheatInfo
            {
                Path = path,
                Name = name,
                Type = type
            };

            try
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    cheatInfo.Size = fileInfo.Length;
                }
                else if (Directory.Exists(path))
                {
                    cheatInfo.Size = 0;
                }
            }
            catch { }

            _foundCheats.Add(cheatInfo);
            OnCheatFound(cheatInfo);
        }

        /// <summary>
        /// Проверка, нужно ли пропустить директорию
        /// </summary>
        private bool ShouldSkipDirectory(string dirName)
        {
            // Пропускаем системные папки для ускорения
            string[] skipDirs = { 
                "System Volume Information", 
                "$Recycle.Bin", 
                "Recovery",
                "Windows",
                "Program Files (x86)\\Windows Kits",
                "Program Files\\Windows Kits"
            };

            return skipDirs.Any(skip => dirName.Equals(skip, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Остановить сканирование
        /// </summary>
        public void StopScanning()
        {
            _cancellationTokenSource?.Cancel();
        }

        protected virtual void OnCheatFound(CheatInfo cheatInfo)
        {
            CheatFound?.Invoke(this, cheatInfo);
        }

        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        protected virtual void OnProgressChanged(int progress)
        {
            ProgressChanged?.Invoke(this, progress);
        }
    }
}

