using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Messenger_Meowtalk.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string messageText = MessageTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(messageText) && messageText != "Напишите сообщение...")
            {
                //Заглушка для отправки сообщения
                MessageBox.Show($"Сообщение отправлено: {messageText}", "MeowTalk",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                MessageTextBox.Text = "Напишите сообщение...";
            }
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            //Заглушка для смайликов
            MessageBox.Show("Панель смайликов будет добавлена позже", "MeowTalk",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            //Заглушка для нового чата
            MessageBox.Show("Функция создания нового чата будет добавлена позже", "MeowTalk",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            //Заглушка для настроек пользователя
            MessageBox.Show("Настройки пользователя будут добавлены позже", "MeowTalk",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}