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
        private Message _editingMessage;
        private bool _isEditingMode;
        private readonly ChatService _chatService;
        private readonly NotificationService _notificationService;
        private readonly StickerService _stickerService;

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
                    (ClearChatHistoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        public Message EditingMessage
        {
            get => _editingMessage;
            set => SetProperty(ref _editingMessage, value);
        }

        public bool IsEditingMode
        {
            get => _isEditingMode;
            set => SetProperty(ref _isEditingMode, value);
        }

        public ICommand SendMessageCommand { get; }
        public ICommand StartNewChatCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ToggleEmojiPanelCommand { get; }
        public ICommand InsertEmojiCommand { get; }
        public ICommand ClearChatHistoryCommand { get; }
        public ICommand EditMessageCommand { get; }
        public ICommand DeleteMessageCommand { get; }
        public ICommand CancelEditCommand { get; }

        public MainViewModel(User currentUser)
        {
            CurrentUser = currentUser;
            Chats = new ObservableCollection<Chat>();
            _chatService = new ChatService();
            _notificationService = new NotificationService();
            ConnectionStatus = "Подключение...";

            _stickerService = new StickerService();
            foreach (var sticker in _stickerService.GraphicStickers)
            {
                Stickers.Add(sticker);
            }

            _chatService.MessageReceived += OnMessageReceivedFromService;
            _chatService.ConnectionStatusChanged += OnConnectionStatusChangedFromService;

            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync(), CanSendMessage);
            StartNewChatCommand = new RelayCommand(StartNewChat);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync());
            ToggleEmojiPanelCommand = new RelayCommand(ToggleEmojiPanel);
            InsertEmojiCommand = new RelayCommand<EmojiItem>(InsertEmoji);
            ClearChatHistoryCommand = new RelayCommand(async () => await ClearChatHistoryAsync(), CanClearChatHistory);
            EditMessageCommand = new RelayCommand<Message>(EditMessage);
            DeleteMessageCommand = new RelayCommand<Message>(async (message) => await DeleteMessageAsync(message));
            CancelEditCommand = new RelayCommand(CancelEdit);

            _ = InitializeConnectionAsync();
            InitializeTestChats();
            InitializeEmojis();
            _ = Task.Run(async () => await LoadChatHistoryAsync());
        }

        private async Task LoadChatHistoryAsync()
        {
            try
            {
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки истории: {ex.Message}");
            }
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
            if (message.Type == Message.MessageType.System && message.Content.StartsWith("sync_edit_message:"))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HandleSyncEditMessage(message);
                });
                return;
            }

            if (message.Type == Message.MessageType.System && message.Content.StartsWith("sync_delete_message:"))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HandleSyncDeleteMessage(message);
                });
                return;
            }

            if (message.Type == Message.MessageType.System && message.Content == "sync_clear_chat_history")
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HandleSyncClearChatHistory(message);
                });
                return;
            }

            if (message.Type == Message.MessageType.Text &&
                !string.IsNullOrEmpty(message.MediaType) &&
                message.MediaType == "image")
            {
                message.Type = Message.MessageType.Sticker;
            }
            else if (message.Type == Message.MessageType.Text &&
                !string.IsNullOrEmpty(message.Content) &&
                (message.Content.StartsWith("[STICKER]") || IsImagePath(message.Content)))
            {
                message.Type = Message.MessageType.Sticker;
            }

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

                if (!message.IsMyMessage)
                {
                    string notificationContent = message.Content;

                    if (message.Type == Message.MessageType.Sticker)
                    {
                        notificationContent = "[Стикер]";
                    }

                    _notificationService.ShowMessageNotification(message.Sender, notificationContent, chat.Name);
                }
            }

            chat.RefreshLastMessageProperties();

            if (chat == SelectedChat)
            {
                OnPropertyChanged(nameof(SelectedChat));
                MessageReceived?.Invoke(this, EventArgs.Empty);
                (ClearChatHistoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            MoveChatToTop(chat);
        }

        private void HandleSyncEditMessage(Message syncMessage)
        {
            try
            {
                var messageId = syncMessage.Content.Replace("sync_edit_message:", "");
                var chat = Chats.FirstOrDefault(c => c.ChatId == syncMessage.ChatId);
                if (chat == null) return;

                var messageToUpdate = chat.Messages.FirstOrDefault(m => m.Id == messageId);
                if (messageToUpdate != null)
                {
                    var oldMessage = messageToUpdate;

                    var updatedMessage = new Message
                    {
                        Id = oldMessage.Id,
                        Sender = oldMessage.Sender,
                        Content = syncMessage.OriginalContent,
                        ChatId = oldMessage.ChatId,
                        Timestamp = oldMessage.Timestamp,
                        Type = oldMessage.Type,
                        IsMyMessage = oldMessage.IsMyMessage,
                        IsEdited = syncMessage.IsEdited,
                        EditedTimestamp = syncMessage.EditedTimestamp,
                        OriginalContent = oldMessage.OriginalContent,
                        MediaType = oldMessage.MediaType
                    };

                    var index = chat.Messages.IndexOf(oldMessage);
                    if (index >= 0)
                    {
                        chat.Messages[index] = updatedMessage;
                    }

                    OnPropertyChanged(nameof(SelectedChat));

                    chat.RefreshLastMessageProperties();

                    if (!syncMessage.IsMyMessage)
                    {
                        _notificationService.ShowMessageNotification("System",
                            $"{syncMessage.Sender} отредактировал сообщение",
                            chat.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обработки синхронизации редактирования: {ex.Message}");
            }
        }

        private void HandleSyncDeleteMessage(Message syncMessage)
        {
            try
            {
                var messageId = syncMessage.Content.Replace("sync_delete_message:", "");
                var chat = Chats.FirstOrDefault(c => c.ChatId == syncMessage.ChatId);
                if (chat == null) return;

                var messageToDelete = chat.Messages.FirstOrDefault(m => m.Id == messageId);
                if (messageToDelete != null)
                {
                    chat.Messages.Remove(messageToDelete);
                    chat.RefreshLastMessageProperties();
                    OnPropertyChanged(nameof(SelectedChat));

                    if (!syncMessage.IsMyMessage)
                    {
                        _notificationService.ShowMessageNotification("System",
                            $"{syncMessage.Sender} удалил сообщение",
                            chat.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обработки синхронизации удаления: {ex.Message}");
            }
        }

        private void HandleSyncClearChatHistory(Message message)
        {
            var chat = Chats.FirstOrDefault(c => c.ChatId == message.ChatId);
            if (chat == null) return;

            chat.Messages.Clear();

            chat.Messages.Add(new Message
            {
                Sender = "System",
                Content = $"{message.Sender} очистил историю чата",
                Type = Message.MessageType.System,
                Timestamp = DateTime.Now,
                IsMyMessage = false,
                ChatId = message.ChatId
            });

            chat.RefreshLastMessageProperties();

            if (chat == SelectedChat)
            {
                OnPropertyChanged(nameof(SelectedChat));
                MessageReceived?.Invoke(this, EventArgs.Empty);
            }

            _notificationService.ShowMessageNotification("System",
                $"{message.Sender} очистил историю чата",
                chat.Name);
        }

        private bool IsImagePath(string content)
        {
            return content.ToLower().EndsWith(".png") ||
                   content.ToLower().EndsWith(".jpg") ||
                   content.ToLower().EndsWith(".jpeg");
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
            if (IsEditingMode)
            {
                return !string.IsNullOrWhiteSpace(MessageText) &&
                       EditingMessage != null &&
                       _chatService != null;
            }
            else
            {
                return !string.IsNullOrWhiteSpace(MessageText) &&
                       SelectedChat != null &&
                       _chatService != null;
            }
        }

        private async Task SendMessageAsync()
        {
            if (!CanSendMessage()) return;

            if (IsEditingMode && EditingMessage != null)
            {
                await EditCurrentMessageAsync();
            }
            else
            {
                await SendNewMessageAsync();
            }
        }

        private async Task EditCurrentMessageAsync()
        {
            var newContent = MessageText.Trim();
            if (string.IsNullOrEmpty(newContent) || newContent == EditingMessage.Content)
            {
                CancelEdit();
                return;
            }

            try
            {
                if (!EditingMessage.IsEdited)
                {
                    EditingMessage.OriginalContent = EditingMessage.Content;
                }

                EditingMessage.Content = newContent;
                EditingMessage.IsEdited = true;
                EditingMessage.EditedTimestamp = DateTime.Now;

                await _chatService.EditMessageAsync(EditingMessage);

                MessageText = string.Empty;
                IsEditingMode = false;
                EditingMessage = null;

                OnPropertyChanged(nameof(SelectedChat));
                MessageReceived?.Invoke(this, EventArgs.Empty);

                MessageTextBox_Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании сообщения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendNewMessageAsync()
        {
            var messageContent = MessageText.Trim();
            MessageText = string.Empty;
            await _chatService.SendMessageAsync(messageContent, SelectedChat.ChatId);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageTextBox_Focus();
            }));
        }

        private void EditMessage(Message message)
        {
            if (message == null || message.Type != Message.MessageType.Text) return;

            EditingMessage = message;
            MessageText = message.Content;
            IsEditingMode = true;

            MessageTextBox_Focus();
        }

        private async Task DeleteMessageAsync(Message message)
        {
            if (message == null) return;

            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить это сообщение?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _chatService.DeleteMessageAsync(message);

                    var chat = Chats.FirstOrDefault(c => c.ChatId == message.ChatId);
                    if (chat != null)
                    {
                        chat.Messages.Remove(message);
                        chat.RefreshLastMessageProperties();
                        OnPropertyChanged(nameof(SelectedChat));

                        MessageReceived?.Invoke(this, EventArgs.Empty);
                    }

                    MessageBox.Show(
                        "Сообщение успешно удалено",
                        "Удаление завершено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при удалении сообщения: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void CancelEdit()
        {
            EditingMessage = null;
            MessageText = string.Empty;
            IsEditingMode = false;
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
                if (!string.IsNullOrEmpty(emoji.ImagePath))
                {
                    _ = SendGraphicStickerAsync(emoji);
                }
                else
                {
                    _ = SendStickerAsync(emoji.Code);
                }
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

        private async Task SendGraphicStickerAsync(EmojiItem sticker)
        {
            if (SelectedChat == null) return;

            var message = new Message
            {
                Sender = CurrentUser.Username,
                Content = sticker.ImagePath,
                ChatId = SelectedChat.ChatId,
                Timestamp = DateTime.Now,
                Type = Message.MessageType.Sticker,
                MediaType = "image",
                IsMyMessage = true
            };

            await _chatService.SendMessageAsync(message);
            MessageTextBox_Focus();
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

        public void UpdateWindowFocusState(bool isFocused)
        {
            _notificationService.SetWindowFocusState(isFocused);
        }

        private bool CanClearChatHistory()
        {
            return SelectedChat != null && SelectedChat.Messages.Any();
        }

        private async Task ClearChatHistoryAsync()
        {
            if (SelectedChat == null) return;

            var result = MessageBox.Show(
                $"Вы уверены, что хотите очистить историю чата \"{SelectedChat.Name}\"?\n\nЭто действие нельзя отменить!",
                "Подтверждение очистки",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _chatService.ClearChatHistoryAsync(SelectedChat.ChatId);

                    var syncMessage = new Message
                    {
                        Sender = CurrentUser.Username,
                        Content = "sync_clear_chat_history",
                        Type = Message.MessageType.System,
                        Timestamp = DateTime.Now,
                        ChatId = SelectedChat.ChatId,
                        IsMyMessage = true
                    };

                    await _chatService.SendMessageAsync(syncMessage);

                    SelectedChat.Messages.Clear();

                    SelectedChat.Messages.Add(new Message
                    {
                        Sender = "System",
                        Content = $"{CurrentUser.Username} очистил историю чата",
                        Type = Message.MessageType.System,
                        Timestamp = DateTime.Now,
                        IsMyMessage = false,
                        ChatId = SelectedChat.ChatId
                    });

                    SelectedChat.RefreshLastMessageProperties();
                    OnPropertyChanged(nameof(SelectedChat));

                    MessageReceived?.Invoke(this, EventArgs.Empty);

                    MessageBox.Show(
                        "История чата успешно очищена",
                        "Очистка завершена",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при очистке истории: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public void RefreshCommands()
        {
            (ClearChatHistoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}