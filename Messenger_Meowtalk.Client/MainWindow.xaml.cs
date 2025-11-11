using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Messenger_Meowtalk.Shared.Models;
using Messenger_Meowtalk.Client.ViewModels;

namespace Messenger_Meowtalk.Client
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(User user)
        {
            InitializeComponent();
            _viewModel = new MainViewModel(user);
            DataContext = _viewModel;

            // Подписываемся на события для автоматической прокрутки
            _viewModel.MessageReceived += OnMessageReceived;
            _viewModel.ChatSelected += OnChatSelected;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MessageTextBox.Focus();

            // Прокручиваем к нижней части при загрузке
            ScrollToBottom();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel?.Cleanup();

            // Отписываемся от событий
            if (_viewModel != null)
            {
                _viewModel.MessageReceived -= OnMessageReceived;
                _viewModel.ChatSelected -= OnChatSelected;
            }
        }

        private void ChatItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Chat chat)
            {
                _viewModel.SelectedChat = chat;
                ScrollToBottom();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SendMessageCommand?.CanExecute(null) == true)
            {
                _viewModel.SendMessageCommand.Execute(null);
                ScrollToBottom();
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.SendMessageCommand?.CanExecute(null) == true)
            {
                _viewModel.SendMessageCommand.Execute(null);
                e.Handled = true;
                ScrollToBottom();
            }
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StartNewChatCommand?.Execute(null);
            ScrollToBottom();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenSettingsCommand?.Execute(null);
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.MessageText += "😊 ";
            MessageTextBox.Focus();
        }

        // Обработчик получения нового сообщения через WebSocket
        private void OnMessageReceived(object sender, System.EventArgs e)
        {
            ScrollToBottom();
        }

        // Обработчик смены выбранного чата
        private void OnChatSelected(object sender, System.EventArgs e)
        {
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new System.Action(() =>
                {
                    try
                    {
                        if (MessagesScrollViewer != null)
                        {
                            MessagesScrollViewer.ScrollToEnd();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка прокрутки: {ex.Message}");
                    }
                }));
        }

        // Дополнительные методы для работы с WebSocket

        // Обработчик двойного клика по чату для переименования
        private void ChatItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Chat chat)
            {
                // Можно добавить функционал переименования чата
                var newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Введите новое имя для чата:",
                    "Переименовать чат",
                    chat.Name);

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    chat.Name = newName;
                }
            }
        }

        public void FocusMessageTextBox()
        {
            MessageTextBox?.Focus();
        }
        // Обработчик контекстного меню для чатов
        private void ChatItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is Border border && border.DataContext is Chat chat)
            {
                var contextMenu = new ContextMenu();

                var renameItem = new MenuItem { Header = "Переименовать" };
                renameItem.Click += (s, args) =>
                {
                    var newName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Введите новое имя для чата:",
                        "Переименовать чат",
                        chat.Name);

                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        chat.Name = newName;
                    }
                };

                var deleteItem = new MenuItem { Header = "Удалить чат" };
                deleteItem.Click += (s, args) =>
                {
                    var result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить чат '{chat.Name}'?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _viewModel.Chats.Remove(chat);
                        if (_viewModel.SelectedChat == chat)
                        {
                            _viewModel.SelectedChat = _viewModel.Chats.Count > 0 ? _viewModel.Chats[0] : null;
                        }
                    }
                };

                contextMenu.Items.Add(renameItem);
                contextMenu.Items.Add(deleteItem);

                border.ContextMenu = contextMenu;
            }
        }
    }
}