using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Messenger_Meowtalk.Client.Models;

namespace Messenger_Meowtalk.Client
{
    public partial class NewChatWindow : Window
    {
        public ObservableCollection<SelectableUser> AvailableUsers { get; } = new();

        public bool IsGroupChat => GroupChatRadio?.IsChecked == true;
        public string ChatName => IsGroupChat ? GroupNameTextBox?.Text : string.Empty;

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

            // Управляем видимостью элементов в зависимости от наличия пользователей
            if (AvailableUsers.Count == 0)
            {
                UsersInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                UsersInfoText.Visibility = Visibility.Collapsed;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsGroupChat)
            {
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
                // Для личного чата проверяем, что выбран ровно один пользователь
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
}