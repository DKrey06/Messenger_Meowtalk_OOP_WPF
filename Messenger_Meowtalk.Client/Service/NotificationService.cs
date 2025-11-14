using Messenger_Meowtalk.Shared.Models;
using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Messenger_Meowtalk.Client.Services
{
    public class NotificationService
    {
        private bool _isWindowFocused = true;
        private Window _notificationWindow;

        public NotificationService()
        {
        }

        public void SetWindowFocusState(bool isFocused)
        {
            _isWindowFocused = isFocused;
        }

        public void ShowMessageNotification(string sender, string message, string chatName = null, Message.MessageType messageType = Message.MessageType.Text)
        {
            if (_isWindowFocused && Application.Current.MainWindow?.WindowState != WindowState.Minimized)
                return;

            // РАЗНЫЕ ТЕКСТЫ ДЛЯ РАЗНЫХ ТИПОВ СООБЩЕНИЙ
            var notificationMessage = messageType switch
            {
                Message.MessageType.Sticker => "📎 Стикер",
                Message.MessageType.Image => "🖼️ Изображение",
                Message.MessageType.File => "📎 Файл",
                _ => message.Length > 50 ? message.Substring(0, 50) + "..." : message
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowCustomNotification(sender, notificationMessage, chatName);
            });

            PlayNotificationSound();
        }

        private void ShowCustomNotification(string sender, string message, string chatName)
        {
            //Закрываем предыдущее уведомление если есть
            _notificationWindow?.Close();

            _notificationWindow = new Window
            {
                Width = 350,
                Height = 120,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Left = SystemParameters.WorkArea.Right - 370,
                Top = SystemParameters.WorkArea.Bottom - 140
            };

            var border = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(220, 45, 45, 48)),
                CornerRadius = new CornerRadius(10),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    Opacity = 0.5,
                    BlurRadius = 10
                }
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(15)
            };

            var titleText = new TextBlock
            {
                Text = string.IsNullOrEmpty(chatName) ? $"✉️ Новое сообщение от {sender}" : $"✉️ {chatName}",
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14
            };

            var messageText = new TextBlock
            {
                Text = message.Length > 50 ? message.Substring(0, 50) + "..." : message,
                Foreground = System.Windows.Media.Brushes.LightGray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(messageText);
            border.Child = stackPanel;
            _notificationWindow.Content = border;

            //Анимация появления
            _notificationWindow.Opacity = 0;
            _notificationWindow.Show();

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1,
                TimeSpan.FromSeconds(0.3));
            _notificationWindow.BeginAnimation(Window.OpacityProperty, fadeIn);

            //Автоматическое закрытие через 4 секунды
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, e) =>
            {
                CloseNotification();
                timer.Stop();
            };
            timer.Start();

            //Закрытие по клику
            _notificationWindow.MouseDown += (s, e) =>
            {
                CloseNotification();
                //Активируем главное окно при клике на уведомление
                Application.Current.MainWindow?.Activate();
                if (Application.Current.MainWindow?.WindowState == WindowState.Minimized)
                {
                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                }
            };
        }

        private void CloseNotification()
        {
            if (_notificationWindow != null)
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0,
                    TimeSpan.FromSeconds(0.3));
                fadeOut.Completed += (s, e) => _notificationWindow.Close();
                _notificationWindow.BeginAnimation(Window.OpacityProperty, fadeOut);
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                using (var soundPlayer = new SoundPlayer("Assets/Sounds/notification.wav"))
                {
                    soundPlayer.Play();
                }
            }
            catch
            {
                try
                {
                    SystemSounds.Beep.Play();
                }
                catch
                {
                }
            }
        }

        public void ShowSystemNotification(string title, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowCustomNotification(title, message, "Система");
            });
        }
    }
}