using System;
using System.Windows;
using System.Windows.Input;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Client
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => UsernameTextBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }

        private void UsernameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryLogin();
            }
        }

        private void TryLogin()
        {
            var username = UsernameTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(username) || username.Length < 2)
            {
                MessageBox.Show("Введите имя пользователя (минимум 2 символа)!",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            var user = new User
            {
                Username = username,
                UserId = Guid.NewGuid().ToString(),
                IsOnline = true,
                Status = "В сети"
            };

            var mainWindow = new MainWindow(user);
            mainWindow.Show();
            this.Close();
        }
    }
}