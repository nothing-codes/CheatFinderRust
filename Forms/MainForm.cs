using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CheatFinderRust.Models;
using CheatFinderRust.Services;

namespace CheatFinderRust.Forms
{
    public partial class MainForm : Form
    {
        private CheatScanner _scanner;
        private List<CheatInfo> _foundCheats;
        private DataGridView _resultsGrid = null!;
        private Button _scanButton = null!;
        private Button _stopButton = null!;
        private Button _exportButton = null!;
        private Label _statusLabel = null!;
        private ProgressBar _progressBar = null!;
        private TextBox _logTextBox = null!;

        public MainForm()
        {
            InitializeComponent();
            _scanner = new CheatScanner();
            _foundCheats = new List<CheatInfo>();
            SetupEventHandlers();
        }

        private void InitializeComponent()
        {
            this.Text = "Cheat Finder Rust - Поиск читов";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // Панель управления
            var controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110
            };

            _scanButton = new Button
            {
                Text = "Начать сканирование",
                Location = new Point(12, 12),
                Size = new Size(150, 32),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };

            _stopButton = new Button
            {
                Text = "Остановить",
                Location = new Point(170, 12),
                Size = new Size(150, 32),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };

            _exportButton = new Button
            {
                Text = "Экспорт результатов",
                Location = new Point(328, 12),
                Size = new Size(150, 32),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };

            _statusLabel = new Label
            {
                Text = "Готов к сканированию",
                Location = new Point(12, 58),
                Size = new Size(970, 20),
                AutoSize = false
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(12, 82),
                Size = new Size(970, 20),
                Style = ProgressBarStyle.Continuous
            };

            controlPanel.Controls.AddRange(new Control[] 
            { 
                _scanButton, 
                _stopButton, 
                _exportButton, 
                _statusLabel, 
                _progressBar 
            });

            // Таблица результатов
            _resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = true,
                GridColor = SystemColors.Control,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = SystemColors.Control,
                    ForeColor = SystemColors.ControlText,
                    SelectionBackColor = SystemColors.Highlight,
                    SelectionForeColor = SystemColors.HighlightText,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9F),
                    BackColor = SystemColors.Window,
                    ForeColor = SystemColors.ControlText,
                    SelectionBackColor = SystemColors.Highlight,
                    SelectionForeColor = SystemColors.HighlightText
                },
                RowHeadersVisible = false
            };

            _resultsGrid.Columns.Add("Name", "Название");
            _resultsGrid.Columns.Add("Path", "Путь");
            _resultsGrid.Columns.Add("FoundAt", "Найдено");

            // Все колонки делятся поровну
            foreach (DataGridViewColumn column in _resultsGrid.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            // Лог - справа на всю высоту
            var logPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            var logLabel = new Label
            {
                Text = "Лог:",
                Dock = DockStyle.Top,
                Height = 20,
                Padding = new Padding(12, 4, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            _logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window
            };

            logPanel.Controls.Add(_logTextBox);
            logPanel.Controls.Add(logLabel);

            // Разделитель - логи справа на всю высоту
            var splitter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = (int)(this.Width * 0.6) // 60% для таблицы, 40% для логов
            };

            splitter.Panel1.Controls.Add(_resultsGrid);
            splitter.Panel2.Controls.Add(logPanel);

            this.Controls.Add(splitter);
            this.Controls.Add(controlPanel);

            _scanButton.Click += ScanButton_Click;
            _stopButton.Click += StopButton_Click;
            _exportButton.Click += ExportButton_Click;
        }

        private void SetupEventHandlers()
        {
            _scanner.CheatFound += Scanner_CheatFound;
            _scanner.StatusChanged += Scanner_StatusChanged;
        }

        private async void ScanButton_Click(object? sender, EventArgs? e)
        {
            _scanButton.Enabled = false;
            _stopButton.Enabled = true;
            _exportButton.Enabled = false;
            _foundCheats.Clear();
            _resultsGrid.Rows.Clear();
            _logTextBox.Clear();
            _progressBar.Value = 0;

            var progress = new Progress<int>(value =>
            {
                _progressBar.Value = value;
            });

            try
            {
                await Task.Run(async () =>
                {
                    var results = await _scanner.ScanAllDrivesAsync(progress);
                    this.Invoke(new Action(() =>
                    {
                        _foundCheats = results;
                        UpdateResults();
                        _scanButton.Enabled = true;
                        _stopButton.Enabled = false;
                        _exportButton.Enabled = _foundCheats.Count > 0;
                    }));
                });
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка: {ex.Message}");
                _scanButton.Enabled = true;
                _stopButton.Enabled = false;
            }
        }

        private void StopButton_Click(object? sender, EventArgs? e)
        {
            _scanner.StopScanning();
            _scanButton.Enabled = true;
            _stopButton.Enabled = false;
            AddLog("Сканирование остановлено пользователем");
        }

        private void ExportButton_Click(object? sender, EventArgs? e)
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                saveDialog.FileName = $"CheatScanResults_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var writer = new StreamWriter(saveDialog.FileName))
                        {
                            writer.WriteLine("Результаты сканирования читов Rust");
                            writer.WriteLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            writer.WriteLine($"Найдено читов: {_foundCheats.Count}");
                            writer.WriteLine(new string('=', 80));

                            foreach (var cheat in _foundCheats)
                            {
                                writer.WriteLine($"Название: {cheat.Name}");
                                writer.WriteLine($"Путь: {cheat.Path}");
                                writer.WriteLine($"Найдено: {cheat.FoundAt:yyyy-MM-dd HH:mm:ss}");
                                writer.WriteLine(new string('-', 80));
                            }
                        }

                        MessageBox.Show($"Результаты сохранены в файл:\n{saveDialog.FileName}", 
                            "Экспорт завершен", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", 
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void Scanner_CheatFound(object? sender, CheatInfo cheatInfo)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Scanner_CheatFound(sender, cheatInfo)));
                return;
            }

            _foundCheats.Add(cheatInfo);
            AddResultRow(cheatInfo);
            AddLog($"Найден чит: {cheatInfo.Name}");
        }

        private void Scanner_StatusChanged(object? sender, string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Scanner_StatusChanged(sender, status)));
                return;
            }

            _statusLabel.Text = status;
            AddLog(status);
        }

        private void AddResultRow(CheatInfo cheatInfo)
        {
            _resultsGrid.Rows.Add(
                cheatInfo.Name,
                cheatInfo.Path,
                cheatInfo.FoundAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        private void UpdateResults()
        {
            _resultsGrid.Rows.Clear();
            foreach (var cheat in _foundCheats)
            {
                AddResultRow(cheat);
            }
        }

        private void AddLog(string message)
        {
            _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            _logTextBox.SelectionStart = _logTextBox.Text.Length;
            _logTextBox.ScrollToCaret();
        }
    }
}

