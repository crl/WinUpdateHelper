using System;
using System.Windows;

namespace WinUpdateHelper
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            MessageBox.Show($"error:{ex.Message}\r\n stackTrace:{ex.StackTrace}\r\n {e.IsTerminating}");
        }
    }
}
