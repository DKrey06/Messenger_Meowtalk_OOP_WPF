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
                    ChatSelected?.Invoke(this, EventArgs.Empty);
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
            if (message.Type == Message.MessageType.System && message.Content.Contains("создал чат"))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HandleChatCreationMessage(message);
                });
                return;
            }

            var chat = FindOrCreateChatForMessage(message);

            if (!chat.Messages.Any(m => m.Id == message.Id && message.Id != "0"))
            {
                chat.Messages.Add(message);
            }

            chat.RefreshLastMessageProperties();

            if (chat == SelectedChat)
            {
                OnPropertyChanged(nameof(SelectedChat));
                MessageReceived?.Invoke(this, EventArgs.Empty);
            }
            MoveChatToTop(chat);
        }

        private void HandleChatCreationMessage(Message message)
        {
            var chatId = ExtractChatIdFromCreationMessage(message.Content);
            if (string.IsNullOrEmpty(chatId)) return;

            var existingChat = Chats.FirstOrDefault(c => c.ChatId == chatId);
            if (existingChat != null) return;

            var newChat = new Chat
            {
                ChatId = chatId,
                Name = $"Чат {chatId.Replace("private_", "")}"
            };

            newChat.Messages.Add(new Message
            {
                Sender = "System",
                Content = "Чат создан. Начните общение!",
                Type = Message.MessageType.System,
                Timestamp = DateTime.Now,
                IsMyMessage = false
            });

            newChat.RefreshLastMessageProperties();
            Chats.Insert(0, newChat);

            if (message.Sender == CurrentUser.Username)
            {
                SelectedChat = newChat;
            }
        }

        private string ExtractChatIdFromCreationMessage(string content)
        {
            try
            {
                var parts = content.Split(new[] { "создал чат" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    return parts[1].Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка извлечения chatId: {ex.Message}");
            }
            return null;
        }

        private Chat FindOrCreateChatForMessage(Message message)
        {
            var chat = Chats.FirstOrDefault(c => c.ChatId == message.ChatId);

            if (chat == null)
            {
                chat = new Chat
                {
                    ChatId = message.ChatId,
                    Name = GetChatDisplayName(message.ChatId, message.Sender)
                };

                Chats.Insert(0, chat);
            }

            return chat;
        }

        private string GetChatDisplayName(string chatId, string sender)
        {
            if (chatId == "general")
                return "Общий чат";

            if (chatId.StartsWith("private_"))
                return $"Приватный чат {chatId.Replace("private_", "").Substring(0, 6)}...";

            return $"Чат {chatId}";
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

            // Фокусируемся обратно на поле ввода
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageTextBox_Focus();
            }));
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
                _ = SendStickerAsync(emoji.Code);
            }
            else
            {
                MessageText += emoji.Code;
                MessageTextBox_Focus();

                Task.Delay(50).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var window = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                        window?.FocusMessageTextBoxAndSetCursorToEnd();
                    });
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }

            IsEmojiPanelOpen = false;
        }

        private async Task SendStickerAsync(string stickerCode)
        {
            if (SelectedChat == null) return;
            await _chatService.SendMessageAsync($"[STICKER]{stickerCode}", SelectedChat.ChatId);
            MessageTextBox_Focus();
        }

        private void InitializeEmojis()
        {
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

        private async void StartNewChat()
        {
            var newChatId = $"private_{DateTime.Now.Ticks}";
            var newChat = new Chat
            {
                ChatId = newChatId,
                Name = $"Новый чат {DateTime.Now:HH:mm}"
            };

            Chats.Insert(0, newChat);
            SelectedChat = newChat;

            var systemMessage = new Message
            {
                Sender = CurrentUser.Username,
                Content = $"{CurrentUser.Username} создал чат {newChatId}",
                Type = Message.MessageType.System,
                ChatId = newChatId,
                Timestamp = DateTime.Now
            };

            await _chatService.SendMessageAsync(systemMessage);

            newChat.Messages.Add(new Message
            {
                Sender = "System",
                Content = "Это новый чат. Начните общение!",
                Type = Message.MessageType.System,
                Timestamp = DateTime.Now,
                IsMyMessage = false
            });

            newChat.RefreshLastMessageProperties();

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
                $"Имя: {CurrentUser.Username}\n" +
                $"Статус: {CurrentUser.Status}\n" +
                $"ID: {CurrentUser.UserId}\n\n" +
                $"Подключение: {ConnectionStatus}",
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
            var generalChat = new Chat
            {
                Name = "Общий чат",
                ChatId = "general"
            };
            Chats.Add(generalChat);

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