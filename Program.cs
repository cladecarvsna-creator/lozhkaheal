using System;
using System.Windows.Forms;

namespace LozhkaHil
{
    /// <summary>
    /// Точка входа приложения «ложка хил».
    /// Аварийная утилита для диспетчеризации процессов, очистки автозагрузки
    /// и восстановления системы после сбоев или блокировок.
    ///
    /// Проект нацелен на .NET Framework 4.8, чтобы работать в среде восстановления
    /// Windows (Win RE), где присутствует классический рантайм .NET Framework.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Главный метод. Настраивает визуальные стили и запускает главную форму.
        /// Любое необработанное исключение перехватывается, чтобы утилита не
        /// «падала» без объяснения в аварийной среде.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // Глобальные перехватчики исключений — в Win RE не должно быть тихих падений.
            Application.ThreadException += (s, e) =>
                ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ShowFatal(e.ExceptionObject as Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new FormMain());
            }
            catch (Exception ex)
            {
                ShowFatal(ex);
            }
        }

        /// <summary>Показывает диалог с текстом фатальной ошибки.</summary>
        private static void ShowFatal(Exception ex)
        {
            MessageBox.Show(
                "Непредвиденная ошибка:\n\n" + (ex?.ToString() ?? "неизвестно"),
                "ложка хил — критическая ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
