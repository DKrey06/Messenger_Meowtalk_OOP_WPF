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

            _viewModel.MessageReceived += OnMessageReceived;
            _viewModel.ChatSelected += OnChatSelected;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MessageTextBox.Focus();
            ScrollToBottom();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel?.Cleanup();

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
                e.Handled = true; // Это предотвращает звуковой сигнал системы
                ScrollToBottom();

                // Убедимся, что фокус остается в TextBox
                MessageTextBox.Focus();
                MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
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

        //private void EmojiButton_Click(object sender, RoutedEventArgs e)
        //{
        //    _viewModel.MessageText += "😊 ";
        //    MessageTextBox.Focus();
        //}
        public void FocusMessageTextBoxAndSetCursorToEnd()
        {
            MessageTextBox?.Focus();

            // Устанавливаем курсор в конец текста
            if (MessageTextBox != null)
            {
                MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
            }
        }

        private void OnMessageReceived(object sender, System.EventArgs e)
        {
            ScrollToBottom();
        }

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

        public void FocusMessageTextBox()
        {
            MessageTextBox?.Focus();

            // Устанавливаем курсор в конец текста
            if (MessageTextBox != null)
            {
                MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
            }
        }
    }
}