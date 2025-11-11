using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Messenger_Meowtalk.Views;

namespace Messenger_Meowtalk
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}
