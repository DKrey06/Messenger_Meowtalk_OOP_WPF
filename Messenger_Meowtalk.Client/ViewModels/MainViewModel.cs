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
        private bool _isEmojiPanelOpen;
        private readonly ChatService _chatService;

        public User CurrentUser { get; }
        public ObservableCollection<Chat> Chats { get; }
        public ObservableCollection<EmojiItem> Emojis { get; } = new();
        public ObservableCollection<EmojiItem> Stickers { get; } = new();
        private readonly HashSet<string> _discoveredUsers = new();

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
        public bool IsEmojiPanelOpen
        {
            get => _isEmojiPanelOpen;
            set => SetProperty(ref _isEmojiPanelOpen, value);
        }

        public ICommand SendMessageCommand { get; }
        public ICommand StartNewChatCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ToggleEmojiPanelCommand { get; }
        public ICommand InsertEmojiCommand { get; }

        public MainViewModel(User currentUser)
        {
            CurrentUser = currentUser;
            Chats = new ObservableCollection<Chat>();
            _chatService = new ChatService();
            ConnectionStatus = "Подключение...";

            _chatService.MessageReceived += OnMessageReceivedFromService;
            _chatService.ConnectionStatusChanged += OnConnectionStatusChangedFromService;

            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync(), CanSendMessage);
            StartNewChatCommand = new RelayCommand(StartNewChat);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync());
            ToggleEmojiPanelCommand = new RelayCommand(ToggleEmojiPanel);
            InsertEmojiCommand = new RelayCommand<EmojiItem>(InsertEmoji);

            _ = InitializeConnectionAsync();
            InitializeTestChats();
            InitializeEmojis();
            InitializeStickers();
            InitializeTestUsers();
        }
        private void InitializeTestUsers()
        {
            // Добавляем пользователей из существующих тестовых чатов
            var initialUsers = new[] { "Марина", "Иван", "Алексей" };
            foreach (var user in initialUsers)
            {
                _discoveredUsers.Add(user);
            }
            UpdateAvailableUsers();
        }
        // Метод для создания избранного чата
        public void CreateFavoriteChat()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var chatName = "Избранное";
                var favoriteChatId = $"favorite_{CurrentUser.Username}";

                // Проверяем, нет ли уже такого чата
                var existingChat = Chats.FirstOrDefault(c => c.ChatId == favoriteChatId);
                if (existingChat != null)
                {
                    SelectedChat = existingChat;
                    return;
                }

                var favoriteChat = new Chat
                {
                    ChatId = favoriteChatId,
                    Name = chatName,
                    Type = ChatType.Private
                };

                favoriteChat.Messages.Add(new Message
                {
                    Sender = "System",
                    Content = "Это ваш личный чат для заметок и важных сообщений",
                    Type = Message.MessageType.System,
                    Timestamp = DateTime.Now,
                    IsMyMessage = false
                });

                favoriteChat.RefreshLastMessageProperties();
                Chats.Insert(0, favoriteChat);
                SelectedChat = favoriteChat;

                Debug.WriteLine($"Создан избранный чат для {CurrentUser.Username}");
            });
        }
        private async Task InitializeConnectionAsync()
        {

            await _chatService.ConnectAsync(CurrentUser.Username);
            
        }

        private void OnMessageReceivedFromService(Message message)
        {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessIncomingMessage(message);
                });
          
        }

        private void ProcessIncomingMessage(Message message)
        {
            // Автоматически обнаруживаем пользователей из входящих сообщений
            if (!string.IsNullOrEmpty(message.Sender) &&
                message.Sender != CurrentUser.Username &&
                message.Sender != "System")
            {
                if (_discoveredUsers.Add(message.Sender))
                {
                    // Новый пользователь обнаружен
                    UpdateAvailableUsers();
                    Debug.WriteLine($"Обнаружен новый пользователь: {message.Sender}");
                }
            }

            var chat = FindOrCreateChatForMessage(message);
            chat.Messages.Add(message);
            chat.RefreshLastMessageProperties();

            if (chat == SelectedChat)
            {
                OnPropertyChanged(nameof(SelectedChat));
                MessageReceived?.Invoke(this, EventArgs.Empty);
            }
            MoveChatToTop(chat);
        }
        private void UpdateAvailableUsers()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableUsers.Clear();
                foreach (var user in _discoveredUsers.Where(u => u != CurrentUser.Username))
                {
                    AvailableUsers.Add(user);
                }

                Debug.WriteLine($"Доступные пользователи: {string.Join(", ", AvailableUsers)}");
            });
        }

        private Chat FindOrCreateChatForMessage(Message message)
        {
            var chat = Chats.FirstOrDefault(c => c.ChatId == message.ChatId);

            if (chat == null)
            {
                // Определяем тип чата по ChatId
                var isGroupChat = message.ChatId?.StartsWith("group_") == true;

                chat = new Chat
                {
                    ChatId = message.ChatId ?? $"private_{message.Sender}",
                    Name = message.Sender == CurrentUser.Username ? "Избранное" : message.Sender,
                    Type = isGroupChat ? ChatType.Group : ChatType.Private
                };

                Chats.Insert(0, chat);
            }

            return chat;
        }
        // Добавляем метод для обновления списка доступных пользователей
        public void UpdateAvailableUsers(string[] users)
        {
            AvailableUsers.Clear();
            foreach (var user in users.Where(u => u != CurrentUser.Username))
            {
                AvailableUsers.Add(user);
            }
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
                var messageContent = MessageText.Trim();

                MessageText = string.Empty;
                await _chatService.SendMessageAsync(messageContent, SelectedChat.ChatId);    
        }

        private void ToggleEmojiPanel()
        {
            IsEmojiPanelOpen = !IsEmojiPanelOpen;
        }

        private void InsertEmoji(EmojiItem emoji)
        {
            if (emoji == null) return;

            if (emoji.IsSticker)
            {
                // Для стикеров отправляем как специальное сообщение
                _ = SendStickerAsync(emoji.Code);
            }
            else
            {
                // Для эмодзи добавляем в текстовое поле
                MessageText += emoji.Code;

                // Сохраняем фокус на текстовом поле после добавления эмодзи
                MessageTextBox_Focus();

                // Немного задерживаем вызов, чтобы WPF успел обновить UI
                Task.Delay(50).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Устанавливаем курсор в конец текста
                        var window = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                        window?.FocusMessageTextBoxAndSetCursorToEnd();
                    });
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }

            // Закрываем панель после выбора
            IsEmojiPanelOpen = false;
        }

        private async Task SendStickerAsync(string stickerCode)
        {
            if (SelectedChat == null) return;

            // Отправляем стикер как текстовое сообщение с префиксом
            await _chatService.SendMessageAsync($"[STICKER]{stickerCode}", SelectedChat.ChatId);

            // Восстанавливаем фокус после отправки стикера
            MessageTextBox_Focus();
        }

        private void InitializeEmojis()
        {
            // Популярные эмодзи
            var popularEmojis = new[]
            {
                "😊", "😂", "🥰", "😍", "🤔", "😎", "🥺", "😭", "😡", "👍",
                "❤️", "🔥", "✨", "🎉", "🙏", "💯", "🤝", "👏", "🐱", "🌟"
            };
            foreach (var emoji in popularEmojis)
            {
                Emojis.Add(new EmojiItem { Code = emoji, Description = "Эмодзи" });
            }
        }

        private void InitializeStickers()
        {
            // Простые текстовые стикеры (можно заменить на картинки)
            var stickers = new[]
            {
                "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "╰(▔∀▔)╯", "(～￣▽￣)～", "ヽ(•‿•)ノ",
                "(´･ω･`)", "( ° ʖ °)", "¯\\_(ツ)_/¯", "(>^_^)>",
                "<(^_^<)", "(¬‿¬)", "(づ￣ ³￣)づ", "ヾ(⌐■_■)ノ♪"
            };

            foreach (var sticker in stickers)
            {
                Stickers.Add(new EmojiItem { Code = sticker, Description = "Стикер", IsSticker = true });
            }
        }



        private ObservableCollection<string> _availableUsers = new();
        public ObservableCollection<string> AvailableUsers
        {
            get => _availableUsers;
            set => SetProperty(ref _availableUsers, value);
        }

        // Заменяем метод StartNewChat:
        private void StartNewChat()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Если нет других пользователей, предлагаем создать избранный чат
                if (AvailableUsers.Count == 0)
                {
                    var result = MessageBox.Show(
                        "Пока нет других пользователей для чата.\n\n" +
                        "Хотите создать личный избранный чат для заметок?",
                        "Нет пользователей",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        CreateFavoriteChat();
                    }
                    return;
                }

                var newChatWindow = new NewChatWindow(CurrentUser.Username, AvailableUsers.ToArray());

                if (newChatWindow.ShowDialog() == true)
                {
                    Chat newChat;

                    if (newChatWindow.IsGroupChat)
                    {
                        // Создаем групповой чат
                        newChat = new Chat
                        {
                            ChatId = $"group_{DateTime.Now.Ticks}",
                            Name = newChatWindow.ChatName,
                            Type = ChatType.Group
                        };

                        // Добавляем участников
                        newChat.Participants.Add(new User { Username = CurrentUser.Username });
                        foreach (var username in newChatWindow.SelectedUsers)
                        {
                            newChat.Participants.Add(new User { Username = username });
                        }

                        newChat.Messages.Add(new Message
                        {
                            Sender = "System",
                            Content = $"Создан групповой чат",
                            Type = Message.MessageType.System,
                            Timestamp = DateTime.Now,
                            IsMyMessage = false
                        });
                    }
                    else
                    {
                        // Создаем личный чат
                        var otherUser = newChatWindow.SelectedUsers.First();
                        newChat = new Chat
                        {
                            ChatId = $"private_{CurrentUser.Username}_{otherUser}",
                            Name = otherUser,
                            Type = ChatType.Private
                        };

                        newChat.Participants.Add(new User { Username = CurrentUser.Username });
                        newChat.Participants.Add(new User { Username = otherUser });

                        newChat.Messages.Add(new Message
                        {
                            Sender = "System",
                            Content = "Начало переписки",
                            Type = Message.MessageType.System,
                            Timestamp = DateTime.Now,
                            IsMyMessage = false
                        });
                    }

                    newChat.RefreshLastMessageProperties();
                    Chats.Insert(0, newChat);
                    SelectedChat = newChat;
                    MessageTextBox_Focus();
                }
            });
        }

        private void MessageTextBox_Focus()
        {
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
                await _chatService.DisconnectAsync();
                ConnectionStatus = "Отключено вручную"; 
        }

        private void InitializeTestChats()
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
                chat1.RefreshLastMessageProperties(); 
                Chats.Add(chat1);
               

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
              
                if (Chats.Count > 0)
                {
                    SelectedChat = Chats[0];
                    
                }
            }    
        

        public async void Cleanup()
        {
 
               
                await DisconnectAsync();

                if (_chatService != null)
                {
                    _chatService.MessageReceived -= OnMessageReceivedFromService;
                    _chatService.ConnectionStatusChanged -= OnConnectionStatusChangedFromService;
                }
                Chats.Clear();

            
        }
    }
}