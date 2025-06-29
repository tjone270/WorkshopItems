namespace WorkshopItems {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // Enable visual styles for modern Windows appearance
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Set high DPI mode for better display scaling
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Set up global exception handlers
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Run the application
            try {
                Application.Run(new MainForm());
            } catch (Exception ex) {
                ShowFatalError(ex);
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e) {
            ShowError(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            if (e.ExceptionObject is Exception ex) {
                ShowFatalError(ex);
            }
        }

        private static void ShowError(Exception ex) {
            var message = $"An unexpected error occurred:\n\n{ex.Message}\n\n" +
                         "The application may continue running, but some features may not work correctly.";

            MessageBox.Show(
                message,
                $"{Application.ProductName} - Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void ShowFatalError(Exception ex) {
            var message = $"A fatal error occurred:\n\n{ex.Message}\n\n" +
                         "The application will now close.";

            MessageBox.Show(
                message,
                $"{Application.ProductName} - Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            Environment.Exit(1);
        }
    }
}