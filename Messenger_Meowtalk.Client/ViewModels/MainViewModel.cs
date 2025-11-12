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

        private Chat FindOrCreateChatForMessage(Message message)
        {
            var chat = Chats.FirstOrDefault(c => c.ChatId == message.ChatId);

            if (chat == null)
            {
                chat = new Chat
                {
                    ChatId = message.ChatId ?? $"private_{message.Sender}",
                    Name = message.Sender == CurrentUser.Username ? "Избранное" : message.Sender
                };

                Chats.Insert(0, chat); 
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



        private void StartNewChat()
        {

                var newChat = new Chat
                {
                    ChatId = $"private_{DateTime.Now.Ticks}",
                    Name = "Новый чат"
                };
                newChat.Messages.Add(new Message
                {
                    Sender = "System",
                    Content = "Это новый чат. Начните общение!",
                    Type = Message.MessageType.System,
                    Timestamp = DateTime.Now,
                    IsMyMessage = false
                });
                newChat.RefreshLastMessageProperties();

                Chats.Insert(0, newChat);
                SelectedChat = newChat;

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageTextBox_Focus();
                }));
            
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