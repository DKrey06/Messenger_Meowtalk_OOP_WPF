using Messenger_Meowtalk.Client.Models;
using Messenger_Meowtalk.Client.Services;
using Messenger_Meowtalk.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Messenger_Meowtalk.Client.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private Chat _selectedChat;
        private string _messageText;
        private string _connectionStatus;
        private readonly ChatService _chatService;

        public User CurrentUser { get; }
        public ObservableCollection<Chat> Chats { get; }

        // События для UI
        public event EventHandler MessageReceived;
        public event EventHandler ChatSelected;
        public event EventHandler<string> ConnectionStatusChanged;

        public Chat SelectedChat
        {
            get => _selectedChat;
            set
            {
                if (SetProperty(ref _selectedChat, value))
                {
                    Debug.WriteLine($"Выбран чат: {_selectedChat?.Name ?? "null"}");
                    ChatSelected?.Invoke(this, EventArgs.Empty);

                    // Обновляем команду отправки сообщения
                    (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                if (SetProperty(ref _messageText, value))
                {
                    // Обновляем состояние команды отправки при изменении текста
                    (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public ICommand SendMessageCommand { get; }
        public ICommand StartNewChatCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand DisconnectCommand { get; }

        public MainViewModel(User currentUser)
        {
            CurrentUser = currentUser;
            Chats = new ObservableCollection<Chat>();
            _chatService = new ChatService();
            ConnectionStatus = "Подключение...";

            // Подписываемся на события чат-сервиса
            _chatService.MessageReceived += OnMessageReceivedFromService;
            _chatService.ConnectionStatusChanged += OnConnectionStatusChangedFromService;

            // Инициализация команд
            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync(), CanSendMessage);
            StartNewChatCommand = new RelayCommand(StartNewChat);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync());

            // Подключаемся к серверу
            _ = InitializeConnectionAsync();

            InitializeTestChats();
        }

        private async Task InitializeConnectionAsync()
        {
            try
            {
                Debug.WriteLine("🔄 Подключение к WebSocket серверу...");
                await _chatService.ConnectAsync(CurrentUser.Username);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка подключения: {ex.Message}");
                ConnectionStatus = $"❌ Ошибка подключения: {ex.Message}";

                MessageBox.Show($"Ошибка подключения к серверу: {ex.Message}\n\n" +
                              "Убедитесь, что сервер запущен на localhost:8080",
                              "Ошибка подключения",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnMessageReceivedFromService(Message message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessIncomingMessage(message);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка обработки сообщения: {ex.Message}");
            }
        }

        private void ProcessIncomingMessage(Message message)
        {
            try
            {
                Debug.WriteLine($"📨 Получено сообщение от {message.Sender}: {message.Content}");

                // Находим или создаем чат для сообщения
                var chat = FindOrCreateChatForMessage(message);

                // Добавляем сообщение в чат
                chat.Messages.Add(message);

                // ОБНОВЛЕНО: Вызываем метод обновления свойств последнего сообщения
                chat.RefreshLastMessageProperties();

                // Если это текущий выбранный чат, уведомляем UI
                if (chat == SelectedChat)
                {
                    OnPropertyChanged(nameof(SelectedChat));
                    MessageReceived?.Invoke(this, EventArgs.Empty);
                }

                // Перемещаем чат вверх списка (новейшие сверху)
                MoveChatToTop(chat);

                Debug.WriteLine($"✅ Сообщение добавлено в чат: {chat.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка обработки входящего сообщения: {ex.Message}");
            }
        }

        private Chat FindOrCreateChatForMessage(Message message)
        {
            var chat = Chats.FirstOrDefault(c => c.ChatId == message.ChatId);

            if (chat == null)
            {
                // Создаем новый чат
                chat = new Chat
                {
                    ChatId = message.ChatId ?? $"private_{message.Sender}",
                    Name = message.Sender == CurrentUser.Username ? "Избранное" : message.Sender
                };

                Chats.Insert(0, chat); // Добавляем в начало списка
                Debug.WriteLine($"✅ Создан новый чат: {chat.Name}");
            }

            return chat;
        }

        private void MoveChatToTop(Chat chat)
        {
            if (Chats.Contains(chat) && Chats.IndexOf(chat) != 0)
            {
                Chats.Remove(chat);
                Chats.Insert(0, chat);
            }
        }

        private void OnConnectionStatusChangedFromService(string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectionStatus = status;
                ConnectionStatusChanged?.Invoke(this, status);
                Debug.WriteLine($"🔌 Статус подключения: {status}");
            });
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(MessageText) &&
                   SelectedChat != null &&
                   _chatService != null;
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage()) return;

            try
            {
                Debug.WriteLine($"📤 Отправка сообщения в чат: {SelectedChat.Name}");

                // Сохраняем текст сообщения перед очисткой
                var messageContent = MessageText.Trim();

                // Очищаем поле ввода сразу для лучшего UX
                MessageText = string.Empty;

                // Отправляем через WebSocket
                await _chatService.SendMessageAsync(messageContent, SelectedChat.ChatId);

                Debug.WriteLine("✅ Сообщение отправлено через WebSocket");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка отправки: {ex.Message}");
                MessageText = MessageText;

                MessageBox.Show($"Ошибка отправки сообщения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
     

        private void StartNewChat()
        {
            try
            {
                Debug.WriteLine("Создание нового чата...");

                var newChat = new Chat
                {
                    ChatId = $"private_{DateTime.Now.Ticks}",
                    Name = "Новый чат"
                };

                // Добавляем приветственное сообщение
                newChat.Messages.Add(new Message
                {
                    Sender = "System",
                    Content = "Это новый чат. Начните общение!",
                    Type = Message.MessageType.System,
                    Timestamp = DateTime.Now,
                    IsMyMessage = false
                });

                // ОБНОВЛЕНО: Вызываем обновление свойств
                newChat.RefreshLastMessageProperties();

                // Добавляем в начало списка
                Chats.Insert(0, newChat);
                SelectedChat = newChat;

                Debug.WriteLine($"✅ Создан новый чат: {newChat.Name}");

                // Фокус на поле ввода сообщения
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageTextBox_Focus();
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка создания чата: {ex.Message}");
                MessageBox.Show($"Ошибка создания чата: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MessageTextBox_Focus()
        {
            // Этот метод будет вызван из UI
            var window = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            window?.FocusMessageTextBox();
        }

        private void OpenSettings()
        {
            MessageBox.Show(
                $"Настройки пользователя:\n\n" +
                $"👤 Имя: {CurrentUser.Username}\n" +
                $"🟢 Статус: {CurrentUser.Status}\n" +
                $"🆔 ID: {CurrentUser.UserId}\n\n" +
                $"🌐 Подключение: {ConnectionStatus}",
                "Настройки",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task DisconnectAsync()
        {
            try
            {
                await _chatService.DisconnectAsync();
                ConnectionStatus = "🔌 Отключено вручную";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка отключения: {ex.Message}");
                ConnectionStatus = $"❌ Ошибка отключения: {ex.Message}";
            }
        }

        private void InitializeTestChats()
        {
            try
            {
                // Тестовый чат 1
                var chat1 = new Chat
                {
                    Name = "Марина",
                    ChatId = "chat1"
                };
                chat1.Messages.Add(new Message
                {
                    Sender = "Марина",
                    Content = "Привет! Как дела?",
                    Timestamp = DateTime.Now.AddMinutes(-10),
                    IsMyMessage = false
                });
                // ОБНОВЛЕНО: Вызываем обновление свойств
                chat1.RefreshLastMessageProperties(); 
                Chats.Add(chat1);
                Debug.WriteLine($"✅ Добавлен тестовый чат: {chat1.Name}");

                // Тестовый чат 2
                var chat2 = new Chat
                {
                    Name = "Иван",
                    ChatId = "chat2"
                };
                chat2.Messages.Add(new Message
                {
                    Sender = "Иван",
                    Content = "Жду твоего ответа",
                    Timestamp = DateTime.Now.AddMinutes(-30),
                    IsMyMessage = false
                });
                chat2.RefreshLastMessageProperties();
                Chats.Add(chat2);
                Debug.WriteLine($"✅ Добавлен тестовый чат: {chat2.Name}");

                // Тестовый чат 3
                var chat3 = new Chat
                {
                    Name = "Алексей",
                    ChatId = "chat3"
                };
                chat3.Messages.Add(new Message
                {
                    Sender = "Алексей",
                    Content = "Завтра встреча в 15:00",
                    Timestamp = DateTime.Now.AddHours(-1),
                    IsMyMessage = false
                });
                chat3.RefreshLastMessageProperties(); 
                Chats.Add(chat3);
                Debug.WriteLine($"✅ Добавлен тестовый чат: {chat3.Name}");

                // Выбираем первый чат по умолчанию
                if (Chats.Count > 0)
                {
                    SelectedChat = Chats[0];
                    Debug.WriteLine($"✅ Выбран чат по умолчанию: {SelectedChat.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка инициализации тестовых чатов: {ex.Message}");
            }
        }

        public async void Cleanup()
        {
            try
            {
                Debug.WriteLine("🧹 Очистка ресурсов MainViewModel...");

                // Отключаемся от сервера
                await DisconnectAsync();

                // Отписываемся от событий
                if (_chatService != null)
                {
                    _chatService.MessageReceived -= OnMessageReceivedFromService;
                    _chatService.ConnectionStatusChanged -= OnConnectionStatusChangedFromService;
                }

                // Очищаем коллекции
                Chats.Clear();

                Debug.WriteLine("✅ Ресурсы MainViewModel очищены");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка при очистке: {ex.Message}");
            }
        }

        // Вспомогательные методы для работы с чатами
        public void DeleteChat(Chat chat)
        {
            if (chat == null) return;

            try
            {
                Chats.Remove(chat);

                if (SelectedChat == chat)
                {
                    SelectedChat = Chats.FirstOrDefault();
                }

                Debug.WriteLine($"✅ Чат удален: {chat.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка удаления чата: {ex.Message}");
            }
        }

        public void RenameChat(Chat chat, string newName)
        {
            if (chat == null || string.IsNullOrWhiteSpace(newName)) return;

            try
            {
                chat.Name = newName.Trim();
                Debug.WriteLine($"✅ Чат переименован: {chat.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка переименования чата: {ex.Message}");
            }
        }
    }
}