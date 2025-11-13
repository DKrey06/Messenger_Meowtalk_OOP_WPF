using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Client
{
    public partial class NewChatWindow : Window
    {
        public ObservableCollection<SelectableUser> AvailableUsers { get; } = new();
        public bool IsGroupChat => GroupChatRadio?.IsChecked == true;
        public string ChatName => IsGroupChat ? GroupNameTextBox.Text : string.Empty;
        public string[] SelectedUsers => AvailableUsers
            .Where(u => u.IsSelected)
            .Select(u => u.Username)
            .ToArray();

        public NewChatWindow(string currentUsername, string[] availableUsers)
        {
            InitializeComponent();

            // Заполняем список пользователей (исключая текущего)
            foreach (var username in availableUsers.Where(u => u != currentUsername))
            {
                AvailableUsers.Add(new SelectableUser { Username = username });
            }

            DataContext = this;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsGroupChat)
            {
                // Для группового чата проверяем, что выбраны участники и указано название
                if (string.IsNullOrWhiteSpace(ChatName))
                {
                    MessageBox.Show("Введите название группового чата", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedUsers.Length == 0)
                {
                    MessageBox.Show("Выберите участников для группового чата", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                // Для личного чата должен быть выбран ровно один пользователь
                if (SelectedUsers.Length != 1)
                {
                    MessageBox.Show("Для личного чата выберите одного пользователя", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // Вспомогательный класс для выбора пользователей
    public class SelectableUser
    {
        public string Username { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}