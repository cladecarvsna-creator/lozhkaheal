using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LozhkaHil
{
    /// <summary>
    /// Главная (и единственная) форма утилиты «ложка хил».
    ///
    /// Весь интерфейс строится в коде — без .Designer.cs и .resx, чтобы
    /// приложение оставалось одним файлом и легко собиралось/запускалось
    /// в среде восстановления Windows (Win RE).
    ///
    /// Форма содержит четыре модуля (вкладки):
    ///   1. Процессы            — диспетчер процессов;
    ///   2. Автозагрузка        — менеджер веток реестра Run/RunOnce;
    ///   3. Восстановление      — снятие системных блокировок, ремонт Winlogon;
    ///   4. Быстрые утилиты     — перезапуск проводника и запуск команд.
    /// </summary>
    public sealed class FormMain : Form
    {
        // ---- Палитра тёмной темы -------------------------------------------------
        private static readonly Color ColBackground = ColorTranslator.FromHtml("#0F172A");
        private static readonly Color ColSurface    = ColorTranslator.FromHtml("#1E293B");
        private static readonly Color ColSurfaceAlt = ColorTranslator.FromHtml("#334155");
        private static readonly Color ColText       = ColorTranslator.FromHtml("#E2E8F0");
        private static readonly Color ColTextMuted  = ColorTranslator.FromHtml("#94A3B8");
        private static readonly Color ColAmber      = ColorTranslator.FromHtml("#F59E0B");
        private static readonly Color ColBlue       = ColorTranslator.FromHtml("#3B82F6");
        private static readonly Color ColDanger     = ColorTranslator.FromHtml("#EF4444");
        private static readonly Color ColGrid       = ColorTranslator.FromHtml("#0B1220");

        // ---- Общие элементы управления ------------------------------------------
        private CheckBox _chkTopMost;
        private Label _statusLabel;

        // ---- Модуль 1: процессы --------------------------------------------------
        private DataGridView _gridProcesses;
        private TextBox _txtProcessFilter;
        private BindingList<ProcessRow> _processRows;
        private ContextMenuStrip _processMenu;

        // ---- Модуль 2: автозагрузка ---------------------------------------------
        private DataGridView _gridStartup;
        private BindingList<StartupRow> _startupRows;

        // ---- Модуль 4: быстрые утилиты ------------------------------------------
        private TextBox _txtRunCommand;

        /// <summary>Конструктор: настраивает окно и собирает интерфейс.</summary>
        public FormMain()
        {
            Text = "ложка хил — аварийная утилита восстановления";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1000, 680);
            MinimumSize = new Size(820, 560);
            BackColor = ColBackground;
            ForeColor = ColText;
            Font = new Font("Segoe UI", 9.5f);

            // TopMost = true по умолчанию, чтобы окно не перекрывалось баннерами.
            TopMost = true;

            BuildUi();

            // Первичная загрузка данных.
            RefreshProcesses();
            RefreshStartup();
        }

        #region Построение интерфейса

        /// <summary>Собирает верхнюю панель, вкладки и строку состояния.</summary>
        private void BuildUi()
        {
            var topBar = BuildTopBar();

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(190, 34),
                Padding = new Point(12, 6),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                BackColor = ColBackground
            };
            // Кастомная отрисовка вкладок под тёмную тему.
            tabs.DrawItem += Tabs_DrawItem;

            tabs.TabPages.Add(BuildProcessesTab());
            tabs.TabPages.Add(BuildStartupTab());
            tabs.TabPages.Add(BuildRestoreTab());
            tabs.TabPages.Add(BuildUtilitiesTab());

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = ColSurface,
                ForeColor = ColTextMuted,
                Text = "Готово."
            };

            // Порядок добавления важен для докинга (снизу вверх).
            Controls.Add(tabs);
            Controls.Add(_statusLabel);
            Controls.Add(topBar);
        }

        /// <summary>Верхняя панель: заголовок и чекбокс «Поверх всех окон».</summary>
        private Panel BuildTopBar()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = ColSurface,
                Padding = new Padding(14, 0, 14, 0)
            };

            var title = new Label
            {
                Text = "🥄  ложка хил",
                ForeColor = ColAmber,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 14)
            };

            _chkTopMost = new CheckBox
            {
                Text = "Поверх всех окон",
                Checked = true,
                ForeColor = ColText,
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(panel.Width - 180, 18)
            };
            _chkTopMost.CheckedChanged += (s, e) => TopMost = _chkTopMost.Checked;

            panel.Controls.Add(title);
            panel.Controls.Add(_chkTopMost);
            return panel;
        }

        /// <summary>Отрисовывает заголовки вкладок в стиле тёмной темы.</summary>
        private void Tabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabControl = (TabControl)sender;
            var page = tabControl.TabPages[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var back = new SolidBrush(selected ? ColSurfaceAlt : ColSurface))
                e.Graphics.FillRectangle(back, e.Bounds);

            // Акцентная полоса снизу у выбранной вкладки.
            if (selected)
                using (var accent = new SolidBrush(ColAmber))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Bottom - 3, e.Bounds.Width, 3);

            TextRenderer.DrawText(
                e.Graphics, page.Text, Font, e.Bounds,
                selected ? ColText : ColTextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        #endregion

        #region Общие фабрики элементов (тёмная тема)

        /// <summary>Создаёт кнопку в тёмном стиле с указанным акцентным цветом.</summary>
        private Button MakeButton(string text, Color accent, EventHandler onClick, int width = 0)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = width == 0,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = accent == ColAmber ? Color.Black : Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(4),
                Padding = new Padding(10, 0, 10, 0),
                UseVisualStyleBackColor = false
            };
            if (width > 0) btn.Width = width;
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        /// <summary>Создаёт настроенный под тёмную тему DataGridView.</summary>
        private DataGridView MakeGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = ColGrid,
                BorderStyle = BorderStyle.None,
                GridColor = ColSurfaceAlt,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32
            };

            grid.DefaultCellStyle.BackColor = ColGrid;
            grid.DefaultCellStyle.ForeColor = ColText;
            grid.DefaultCellStyle.SelectionBackColor = ColBlue;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = ColSurface;

            grid.ColumnHeadersDefaultCellStyle.BackColor = ColSurfaceAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ColText;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            return grid;
        }

        /// <summary>Создаёт текстовое поле в тёмном стиле.</summary>
        private TextBox MakeTextBox()
        {
            return new TextBox
            {
                BackColor = ColGrid,
                ForeColor = ColText,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        /// <summary>Обновляет строку состояния.</summary>
        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.Text = text;
        }

        #endregion

        #region Модуль 1 — Диспетчер процессов

        /// <summary>Строит вкладку «Процессы».</summary>
        private TabPage BuildProcessesTab()
        {
            var page = new TabPage("Процессы") { BackColor = ColBackground, Padding = new Padding(10) };

            // Панель фильтра и кнопок сверху.
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ColBackground
            };

            top.Controls.Add(new Label
            {
                Text = "Поиск:",
                ForeColor = ColTextMuted,
                AutoSize = true,
                Margin = new Padding(4, 9, 2, 0)
            });

            _txtProcessFilter = MakeTextBox();
            _txtProcessFilter.Width = 200;
            _txtProcessFilter.Margin = new Padding(4, 6, 12, 0);
            // Фильтрация в реальном времени по мере ввода.
            _txtProcessFilter.TextChanged += (s, e) => ApplyProcessFilter();
            top.Controls.Add(_txtProcessFilter);

            top.Controls.Add(MakeButton("Обновить", ColBlue, (s, e) => RefreshProcesses()));
            top.Controls.Add(MakeButton("Завершить процесс", ColAmber, (s, e) => KillSelected(false)));
            top.Controls.Add(MakeButton("Завершить дерево", ColAmber, (s, e) => KillSelected(true)));
            top.Controls.Add(MakeButton("Экстренная остановка несистемных", ColDanger, (s, e) => EmergencyStop()));

            // Таблица процессов.
            _gridProcesses = MakeGrid();
            _gridProcesses.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Название", DataPropertyName = nameof(ProcessRow.Name), FillWeight = 30, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
                new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = nameof(ProcessRow.Pid), Width = 70 },
                new DataGridViewTextBoxColumn { HeaderText = "Память (МБ)", DataPropertyName = nameof(ProcessRow.MemoryMb), Width = 100 },
                new DataGridViewTextBoxColumn { HeaderText = "Путь к .exe", DataPropertyName = nameof(ProcessRow.Path), FillWeight = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            // Контекстное меню: открыть папку / скопировать путь.
            _processMenu = new ContextMenuStrip { BackColor = ColSurface, ForeColor = ColText };
            var miOpenFolder = new ToolStripMenuItem("Открыть папку с файлом");
            miOpenFolder.Click += (s, e) => OpenContainingFolder();
            var miCopyPath = new ToolStripMenuItem("Копировать путь");
            miCopyPath.Click += (s, e) => CopyProcessPath();
            _processMenu.Items.Add(miOpenFolder);
            _processMenu.Items.Add(miCopyPath);
            _gridProcesses.ContextMenuStrip = _processMenu;
            // Выделять строку под курсором при вызове контекстного меню правой кнопкой.
            _gridProcesses.CellMouseDown += Grid_CellMouseDownSelect;

            page.Controls.Add(_gridProcesses);
            page.Controls.Add(top);
            return page;
        }

        /// <summary>Считывает список процессов и заполняет таблицу.</summary>
        private void RefreshProcesses()
        {
            var rows = new List<ProcessRow>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    long memMb = p.WorkingSet64 / (1024 * 1024);
                    rows.Add(new ProcessRow
                    {
                        Name = p.ProcessName,
                        Pid = p.Id,
                        MemoryMb = memMb,
                        Path = TryGetProcessPath(p)
                    });
                }
                catch (Win32Exception) { /* недостаточно прав для чтения — пропускаем */ }
                catch (InvalidOperationException) { /* процесс уже завершился */ }
                finally { p.Dispose(); }
            }

            _processRows = new BindingList<ProcessRow>(rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList());
            ApplyProcessFilter();
            SetStatus($"Процессов загружено: {rows.Count}.");
        }

        /// <summary>Пытается получить путь к исполняемому файлу процесса.</summary>
        private static string TryGetProcessPath(Process p)
        {
            try { return p.MainModule?.FileName ?? "(недоступно)"; }
            catch (Win32Exception) { return "(нет доступа)"; }       // системный/защищённый процесс
            catch (InvalidOperationException) { return "(нет данных)"; }
            catch (NotSupportedException) { return "(нет данных)"; }
        }

        /// <summary>Применяет строку фильтра к списку процессов.</summary>
        private void ApplyProcessFilter()
        {
            if (_processRows == null) return;
            string term = _txtProcessFilter.Text.Trim();
            if (term.Length == 0)
            {
                _gridProcesses.DataSource = _processRows;
            }
            else
            {
                var filtered = _processRows
                    .Where(r => r.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                _gridProcesses.DataSource = new BindingList<ProcessRow>(filtered);
            }
        }

        /// <summary>Возвращает выделенные строки процессов.</summary>
        private IEnumerable<ProcessRow> SelectedProcesses()
        {
            return _gridProcesses.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as ProcessRow)
                .Where(r => r != null);
        }

        /// <summary>Завершает выделенные процессы (по одному или деревом).</summary>
        private void KillSelected(bool wholeTree)
        {
            var targets = SelectedProcesses().ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("Выберите процесс в таблице.", "ложка хил",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string what = wholeTree ? "дерево процессов" : "процесс(ы)";
            if (MessageBox.Show(
                    $"Завершить {what}: {string.Join(", ", targets.Select(t => $"{t.Name} ({t.Pid})"))}?",
                    "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int killed = 0;
            foreach (var t in targets)
            {
                if (TryKill(t.Pid, wholeTree)) killed++;
            }
            SetStatus($"Завершено объектов: {killed} из {targets.Count}.");
            RefreshProcesses();
        }

        /// <summary>
        /// Завершает один процесс по PID. При wholeTree=true завершает всё дерево
        /// (на .NET Framework 4.8 Kill(bool) недоступен, поэтому дерево обходится вручную).
        /// </summary>
        private bool TryKill(int pid, bool wholeTree)
        {
            try
            {
                if (wholeTree) KillTree(pid);
                using (var p = Process.GetProcessById(pid))
                {
                    if (!p.HasExited) p.Kill();
                }
                return true;
            }
            catch (ArgumentException) { return false; }          // процесс уже завершён
            catch (InvalidOperationException) { return false; }  // уже завершался
            catch (Win32Exception ex)
            {
                // Отказ доступа — обычно системный/защищённый процесс либо нужны права администратора.
                SetStatus($"Не удалось завершить PID {pid}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Рекурсивно завершает дочерние процессы указанного PID через WMI-запрос
        /// (Win32_Process.ParentProcessId). Работает и в Win RE, где есть WMI.
        /// </summary>
        private void KillTree(int parentPid)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentPid}"))
                using (var results = searcher.Get())
                {
                    foreach (var mo in results)
                    {
                        int childPid = Convert.ToInt32(mo["ProcessId"]);
                        KillTree(childPid);          // сначала внуки
                        try
                        {
                            using (var child = Process.GetProcessById(childPid))
                                if (!child.HasExited) child.Kill();
                        }
                        catch (ArgumentException) { }
                        catch (InvalidOperationException) { }
                        catch (Win32Exception) { }
                        finally { mo.Dispose(); }
                    }
                }
            }
            catch (Exception)
            {
                // Если WMI недоступен, завершаем хотя бы сам родительский процесс (выполнится у вызывающего кода).
            }
        }

        /// <summary>
        /// Экстренная остановка: завершает все процессы, чьи .exe запущены НЕ из
        /// C:\Windows\System32. Защищённые/системные процессы, доступ к пути которых
        /// закрыт, пропускаются во избежание завершения критичных компонентов.
        /// </summary>
        private void EmergencyStop()
        {
            if (MessageBox.Show(
                    "Будут завершены ВСЕ процессы, запущенные не из C:\\Windows\\System32.\n" +
                    "Несохранённые данные в приложениях будут потеряны. Продолжить?",
                    "Экстренная остановка", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            string system32 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System)); // ...\System32
            int self = Process.GetCurrentProcess().Id;
            int killed = 0, skipped = 0;

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == self) { continue; }            // не завершаем саму утилиту
                    string path = TryGetProcessPath(p);

                    // Пропускаем всё, для чего путь неизвестен (обычно защищённые системные процессы).
                    if (path.StartsWith("(", StringComparison.Ordinal)) { skipped++; continue; }

                    // Оставляем процессы из System32.
                    if (path.StartsWith(system32, StringComparison.OrdinalIgnoreCase)) { continue; }

                    if (!p.HasExited) { p.Kill(); killed++; }
                }
                catch (Win32Exception) { skipped++; }
                catch (InvalidOperationException) { }
                finally { p.Dispose(); }
            }

            SetStatus($"Экстренная остановка: завершено {killed}, пропущено {skipped}.");
            RefreshProcesses();
        }

        /// <summary>Открывает папку с исполняемым файлом выбранного процесса.</summary>
        private void OpenContainingFolder()
        {
            var row = SelectedProcesses().FirstOrDefault();
            if (row == null || !File.Exists(row.Path))
            {
                SetStatus("Путь к файлу недоступен.");
                return;
            }
            try
            {
                // Открыть проводник и выделить файл.
                Process.Start("explorer.exe", $"/select,\"{row.Path}\"");
            }
            catch (Exception ex)
            {
                SetStatus("Не удалось открыть папку: " + ex.Message);
            }
        }

        /// <summary>Копирует путь выбранного процесса в буфер обмена.</summary>
        private void CopyProcessPath()
        {
            var row = SelectedProcesses().FirstOrDefault();
            if (row == null) return;
            try
            {
                // Clipboard.SetText не принимает пустую строку — очищаем буфер вручную.
                if (string.IsNullOrEmpty(row.Path)) Clipboard.Clear();
                else Clipboard.SetText(row.Path);
                SetStatus("Путь скопирован в буфер обмена.");
            }
            catch (Exception ex)
            {
                SetStatus("Не удалось скопировать: " + ex.Message);
            }
        }

        /// <summary>Выделяет строку под правой кнопкой мыши перед показом меню.</summary>
        private void Grid_CellMouseDownSelect(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                var grid = (DataGridView)sender;
                grid.ClearSelection();
                grid.Rows[e.RowIndex].Selected = true;
            }
        }

        #endregion

        #region Модуль 2 — Менеджер автозагрузки

        // Определения веток автозагрузки: корень куста, подраздел и человекочитаемое имя.
        private static readonly (RegistryHive Hive, string SubKey, string Label)[] StartupLocations =
        {
            (RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\Run",     @"HKCU\...\Run"),
            (RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\RunOnce",  @"HKCU\...\RunOnce"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run",     @"HKLM\...\Run"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce",  @"HKLM\...\RunOnce"),
        };

        /// <summary>Строит вкладку «Автозагрузка».</summary>
        private TabPage BuildStartupTab()
        {
            var page = new TabPage("Автозагрузка") { BackColor = ColBackground, Padding = new Padding(10) };

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                WrapContents = false,
                BackColor = ColBackground
            };
            top.Controls.Add(MakeButton("Обновить", ColBlue, (s, e) => RefreshStartup()));
            top.Controls.Add(MakeButton("Добавить в автозагрузку", ColBlue, (s, e) => AddStartupEntry()));
            top.Controls.Add(MakeButton("Удалить из автозагрузки", ColDanger, (s, e) => RemoveStartupEntry()));

            _gridStartup = MakeGrid();
            _gridStartup.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Имя", DataPropertyName = nameof(StartupRow.Name), FillWeight = 25, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
                new DataGridViewTextBoxColumn { HeaderText = "Путь / команда", DataPropertyName = nameof(StartupRow.Command), FillWeight = 55, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
                new DataGridViewTextBoxColumn { HeaderText = "Ветка реестра", DataPropertyName = nameof(StartupRow.Location), FillWeight = 20, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            page.Controls.Add(_gridStartup);
            page.Controls.Add(top);
            return page;
        }

        /// <summary>Читает все ветки Run/RunOnce (HKCU и HKLM) и заполняет таблицу.</summary>
        private void RefreshStartup()
        {
            var rows = new List<StartupRow>();
            foreach (var loc in StartupLocations)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(loc.Hive, RegistryView.Default))
                    using (var key = baseKey.OpenSubKey(loc.SubKey, writable: false))
                    {
                        if (key == null) continue;
                        foreach (var name in key.GetValueNames())
                        {
                            rows.Add(new StartupRow
                            {
                                Name = name,
                                Command = Convert.ToString(key.GetValue(name)) ?? string.Empty,
                                Location = loc.Label,
                                Hive = loc.Hive,
                                SubKey = loc.SubKey
                            });
                        }
                    }
                }
                catch (SecurityException) { /* нет прав на чтение ветки */ }
                catch (UnauthorizedAccessException) { /* нет прав на чтение ветки */ }
            }

            _startupRows = new BindingList<StartupRow>(rows);
            _gridStartup.DataSource = _startupRows;
            SetStatus($"Записей автозагрузки: {rows.Count}.");
        }

        /// <summary>Удаляет выбранное значение из соответствующей ветки реестра.</summary>
        private void RemoveStartupEntry()
        {
            var row = _gridStartup.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as StartupRow)
                .FirstOrDefault(r => r != null);

            if (row == null)
            {
                MessageBox.Show("Выберите запись автозагрузки.", "ложка хил",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Удалить «{row.Name}» из {row.Location}?", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(row.Hive, RegistryView.Default))
                using (var key = baseKey.OpenSubKey(row.SubKey, writable: true))
                {
                    if (key == null) { SetStatus("Ветка реестра не найдена."); return; }
                    key.DeleteValue(row.Name, throwOnMissingValue: false);
                }
                SetStatus($"Удалено: {row.Name}.");
                RefreshStartup();
            }
            catch (UnauthorizedAccessException)
            {
                ShowAccessDenied("удаления записи автозагрузки");
            }
            catch (SecurityException)
            {
                ShowAccessDenied("удаления записи автозагрузки");
            }
        }

        /// <summary>Диалог добавления нового значения в автозагрузку (HKCU\...\Run).</summary>
        private void AddStartupEntry()
        {
            using (var dlg = new StartupEntryDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    // По умолчанию пишем в HKCU\...\Run — не требует прав администратора.
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                    using (var key = baseKey.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        key.SetValue(dlg.EntryName, dlg.EntryCommand, RegistryValueKind.String);
                    }
                    SetStatus($"Добавлено в автозагрузку: {dlg.EntryName}.");
                    RefreshStartup();
                }
                catch (UnauthorizedAccessException) { ShowAccessDenied("добавления записи автозагрузки"); }
                catch (SecurityException) { ShowAccessDenied("добавления записи автозагрузки"); }
            }
        }

        #endregion

        #region Модуль 3 — Восстановление системы

        // Раздел политик, где вредоносные программы часто ставят блокировки.
        private const string PoliciesSystemKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";

        // Раздел Winlogon.
        private const string WinlogonKey = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";

        /// <summary>Строит вкладку «Восстановление реестра».</summary>
        private TabPage BuildRestoreTab()
        {
            var page = new TabPage("Восстановление реестра") { BackColor = ColBackground, Padding = new Padding(16), AutoScroll = true };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ColBackground
            };

            layout.Controls.Add(MakeSectionLabel("Снятие системных блокировок"));
            layout.Controls.Add(MakeHint(
                "Удаляет параметры DisableTaskMgr, DisableCMD и DisableRegistryTools из\n" +
                "HKCU\\...\\Policies\\System — восстанавливает доступ к Диспетчеру задач, CMD и Regedit."));
            layout.Controls.Add(MakeButton("Сбросить блокировки системных утилит", ColAmber, (s, e) => ResetSystemLocks()));

            layout.Controls.Add(MakeSpacer());
            layout.Controls.Add(MakeSectionLabel("Проверка Winlogon"));
            layout.Controls.Add(MakeHint(
                "Восстанавливает Shell = \"explorer.exe\" и\n" +
                "Userinit = \"C:\\Windows\\system32\\userinit.exe,\" — типичная цель подмены вредоносами."));
            layout.Controls.Add(MakeButton("Проверить и исправить Winlogon", ColAmber, (s, e) => FixWinlogon()));

            page.Controls.Add(layout);
            return page;
        }

        /// <summary>
        /// Снимает блокировки системных утилит: удаляет параметры DisableTaskMgr,
        /// DisableCMD и DisableRegistryTools из ветки политик HKCU.
        /// </summary>
        private void ResetSystemLocks()
        {
            string[] values = { "DisableTaskMgr", "DisableCMD", "DisableRegistryTools" };
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var key = baseKey.OpenSubKey(PoliciesSystemKey, writable: true))
                {
                    if (key == null)
                    {
                        SetStatus("Раздел политик отсутствует — блокировок нет.");
                        MessageBox.Show("Блокировки не найдены: раздел политик отсутствует.", "ложка хил",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int removed = 0;
                    foreach (var v in values)
                    {
                        if (key.GetValue(v) != null)
                        {
                            key.DeleteValue(v, throwOnMissingValue: false);
                            removed++;
                        }
                    }
                    SetStatus($"Снятие блокировок: удалено параметров {removed}.");
                    MessageBox.Show(
                        removed > 0 ? $"Блокировки сняты (удалено параметров: {removed})." : "Активных блокировок не обнаружено.",
                        "ложка хил", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (UnauthorizedAccessException) { ShowAccessDenied("снятия блокировок"); }
            catch (SecurityException) { ShowAccessDenied("снятия блокировок"); }
        }

        /// <summary>
        /// Проверяет и восстанавливает значения Winlogon:
        /// Shell = "explorer.exe" и Userinit = "C:\Windows\system32\userinit.exe,".
        /// Пишет в HKCU (per-user); для общесистемного HKLM нужны права администратора.
        /// </summary>
        private void FixWinlogon()
        {
            const string expectedShell = "explorer.exe";
            string expectedUserinit = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "userinit.exe") + ",";

            try
            {
                var changes = new List<string>();
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var key = baseKey.CreateSubKey(WinlogonKey))
                {
                    string shell = Convert.ToString(key.GetValue("Shell"));
                    if (!string.Equals(shell, expectedShell, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue("Shell", expectedShell, RegistryValueKind.String);
                        changes.Add($"Shell: «{shell}» → «{expectedShell}»");
                    }

                    string userinit = Convert.ToString(key.GetValue("Userinit"));
                    if (!string.Equals(userinit, expectedUserinit, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue("Userinit", expectedUserinit, RegistryValueKind.String);
                        changes.Add($"Userinit: «{userinit}» → «{expectedUserinit}»");
                    }
                }

                if (changes.Count == 0)
                {
                    SetStatus("Winlogon в порядке — изменений не требуется.");
                    MessageBox.Show("Параметры Winlogon корректны.", "ложка хил",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("Winlogon исправлен.");
                    MessageBox.Show("Восстановлено:\n\n" + string.Join("\n", changes), "ложка хил",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (UnauthorizedAccessException) { ShowAccessDenied("исправления Winlogon"); }
            catch (SecurityException) { ShowAccessDenied("исправления Winlogon"); }
        }

        #endregion

        #region Модуль 4 — Быстрые утилиты

        /// <summary>Строит вкладку «Быстрые утилиты».</summary>
        private TabPage BuildUtilitiesTab()
        {
            var page = new TabPage("Быстрые утилиты") { BackColor = ColBackground, Padding = new Padding(16), AutoScroll = true };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ColBackground
            };

            layout.Controls.Add(MakeSectionLabel("Проводник"));
            layout.Controls.Add(MakeHint("Завершает explorer.exe и запускает его заново — помогает при зависшем рабочем столе."));
            layout.Controls.Add(MakeButton("Перезапустить Проводник", ColBlue, (s, e) => RestartExplorer()));

            layout.Controls.Add(MakeSpacer());
            layout.Controls.Add(MakeSectionLabel("Выполнить команду (аналог Win+R)"));
            layout.Controls.Add(MakeHint("Введите команду или путь: cmd, powershell, regedit, taskmgr, notepad, control…"));

            var inputRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = ColBackground
            };
            _txtRunCommand = MakeTextBox();
            _txtRunCommand.Width = 360;
            _txtRunCommand.Margin = new Padding(0, 4, 8, 0);
            // Enter в поле = запуск команды.
            _txtRunCommand.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunCommand(); }
            };
            inputRow.Controls.Add(_txtRunCommand);
            inputRow.Controls.Add(MakeButton("Выполнить", ColAmber, (s, e) => RunCommand()));
            layout.Controls.Add(inputRow);

            // Кнопки быстрого доступа к типичным утилитам.
            layout.Controls.Add(MakeSpacer());
            layout.Controls.Add(MakeSectionLabel("Быстрый запуск"));
            var quick = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = ColBackground
            };
            quick.Controls.Add(MakeButton("Диспетчер задач", ColSurfaceAlt, (s, e) => LaunchTool("taskmgr.exe")));
            quick.Controls.Add(MakeButton("Командная строка", ColSurfaceAlt, (s, e) => LaunchTool("cmd.exe")));
            quick.Controls.Add(MakeButton("PowerShell", ColSurfaceAlt, (s, e) => LaunchTool("powershell.exe")));
            quick.Controls.Add(MakeButton("Редактор реестра", ColSurfaceAlt, (s, e) => LaunchTool("regedit.exe")));
            quick.Controls.Add(MakeButton("Панель управления", ColSurfaceAlt, (s, e) => LaunchTool("control.exe")));
            layout.Controls.Add(quick);

            page.Controls.Add(layout);
            return page;
        }

        /// <summary>Перезапускает Проводник Windows.</summary>
        private void RestartExplorer()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try { if (!p.HasExited) p.Kill(); }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                    finally { p.Dispose(); }
                }

                // Небольшая пауза, затем запуск заново.
                // (В большинстве сборок Windows explorer перезапускается сам,
                //  но запускаем явно на случай Win RE / отключённого автозапуска.)
                string explorerPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                Process.Start(explorerPath);

                SetStatus("Проводник перезапущен.");
            }
            catch (Exception ex)
            {
                SetStatus("Не удалось перезапустить Проводник: " + ex.Message);
            }
        }

        /// <summary>Выполняет команду из поля ввода (через оболочку, как Win+R).</summary>
        private void RunCommand()
        {
            string cmd = _txtRunCommand.Text.Trim();
            if (cmd.Length == 0) return;
            LaunchTool(cmd);
        }

        /// <summary>
        /// Запускает утилиту/команду через ShellExecute (UseShellExecute=true),
        /// что позволяет использовать имена вида «cmd», «regedit», пути и URL.
        /// </summary>
        private void LaunchTool(string command)
        {
            try
            {
                var psi = new ProcessStartInfo(command) { UseShellExecute = true };
                Process.Start(psi);
                SetStatus($"Запущено: {command}");
            }
            catch (Win32Exception ex)
            {
                // Например, файл не найден или отменён контроль учётных записей (UAC).
                SetStatus("Не удалось запустить: " + ex.Message);
                MessageBox.Show("Не удалось выполнить команду:\n" + ex.Message, "ложка хил",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка запуска: " + ex.Message);
            }
        }

        #endregion

        #region Вспомогательные элементы разметки и сообщения

        /// <summary>Заголовок раздела на вкладках восстановления/утилит.</summary>
        private Label MakeSectionLabel(string text) => new Label
        {
            Text = text,
            ForeColor = ColAmber,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 4)
        };

        /// <summary>Поясняющий текст под заголовком раздела.</summary>
        private Label MakeHint(string text) => new Label
        {
            Text = text,
            ForeColor = ColTextMuted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        /// <summary>Пустой отступ между разделами.</summary>
        private Control MakeSpacer() => new Panel { Height = 14, Width = 10, BackColor = ColBackground };

        /// <summary>Единое сообщение об отказе доступа.</summary>
        private void ShowAccessDenied(string action)
        {
            SetStatus($"Отказано в доступе для {action}. Запустите от имени администратора.");
            MessageBox.Show(
                $"Недостаточно прав для {action}.\n\n" +
                "Запустите «ложка хил» от имени администратора и повторите попытку.",
                "Отказано в доступе", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion

        #region Модели данных для таблиц

        /// <summary>Строка таблицы процессов.</summary>
        private sealed class ProcessRow
        {
            public string Name { get; set; }
            public int Pid { get; set; }
            public long MemoryMb { get; set; }
            public string Path { get; set; }
        }

        /// <summary>Строка таблицы автозагрузки.</summary>
        private sealed class StartupRow
        {
            public string Name { get; set; }
            public string Command { get; set; }
            public string Location { get; set; }
            // Служебные поля для операции удаления (в таблице не отображаются).
            public RegistryHive Hive { get; set; }
            public string SubKey { get; set; }
        }

        #endregion

        #region Диалог добавления записи автозагрузки

        /// <summary>Небольшой модальный диалог для ввода имени и команды новой записи автозагрузки.</summary>
        private sealed class StartupEntryDialog : Form
        {
            private readonly TextBox _name;
            private readonly TextBox _command;

            public string EntryName => _name.Text.Trim();
            public string EntryCommand => _command.Text.Trim();

            public StartupEntryDialog()
            {
                Text = "Добавить в автозагрузку (HKCU\\...\\Run)";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(460, 170);
                BackColor = ColBackground;
                ForeColor = ColText;
                Font = new Font("Segoe UI", 9.5f);

                var lblName = new Label { Text = "Имя параметра:", AutoSize = true, ForeColor = ColTextMuted, Location = new Point(14, 16) };
                _name = new TextBox { Location = new Point(14, 36), Width = 430, BackColor = ColGrid, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle };

                var lblCmd = new Label { Text = "Путь / команда:", AutoSize = true, ForeColor = ColTextMuted, Location = new Point(14, 70) };
                _command = new TextBox { Location = new Point(14, 90), Width = 430, BackColor = ColGrid, ForeColor = ColText, BorderStyle = BorderStyle.FixedSingle };

                var ok = new Button
                {
                    Text = "Добавить",
                    DialogResult = DialogResult.OK,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColAmber,
                    ForeColor = Color.Black,
                    Location = new Point(268, 128),
                    Width = 84
                };
                ok.FlatAppearance.BorderSize = 0;

                var cancel = new Button
                {
                    Text = "Отмена",
                    DialogResult = DialogResult.Cancel,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = ColSurfaceAlt,
                    ForeColor = ColText,
                    Location = new Point(360, 128),
                    Width = 84
                };
                cancel.FlatAppearance.BorderSize = 0;

                // Проверка ввода перед закрытием с OK.
                ok.Click += (s, e) =>
                {
                    if (EntryName.Length == 0 || EntryCommand.Length == 0)
                    {
                        MessageBox.Show("Заполните имя и команду.", "ложка хил",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.None; // не закрывать диалог
                    }
                };

                Controls.AddRange(new Control[] { lblName, _name, lblCmd, _command, ok, cancel });
                AcceptButton = ok;
                CancelButton = cancel;
            }
        }

        #endregion
    }
}
