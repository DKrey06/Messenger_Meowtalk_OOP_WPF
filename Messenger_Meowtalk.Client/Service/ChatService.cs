using Messenger_Meowtalk.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger_Meowtalk.Client.Services
{
    public class ChatService
    {
        private readonly WebSocketService _webSocketService;
        private User _currentUser;

        public ObservableCollection<Chat> Chats { get; } = new();
        public event Action<Message> MessageReceived;
        public event Action<string> ConnectionStatusChanged;

        public ChatService()
        {
            _webSocketService = new WebSocketService();
            _webSocketService.MessageReceived += OnMessageReceived;
            _webSocketService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        public async Task ConnectAsync(string username)
        {
            _currentUser = new User
            {
                Username = username,
                UserId = Guid.NewGuid().ToString(),
                IsOnline = true,
                Status = "В сети"
            };

            await _webSocketService.ConnectAsync(username);
        }

        private void OnMessageReceived(Message message)
        {
            // Обрабатываем сообщения редактирования
            if (message.Type == Message.MessageType.Edit && message.Content.StartsWith("EDIT:"))
            {
                HandleEditMessage(message);
                return;
            }

            // Обрабатываем сообщения удаления
            if (message.Type == Message.MessageType.Delete && message.Content.StartsWith("DELETE:"))
            {
                HandleDeleteMessage(message);
                return;
            }

            // Стандартная обработка сообщений
            var chat = FindOrCreateChat(message.ChatId, message.Sender);
            message.IsMyMessage = message.Sender == _currentUser.Username;

            // Добавляем в историю чата
            Application.Current.Dispatcher.Invoke(() =>
            {
                chat.Messages.Add(message);
                chat.RefreshLastMessageProperties();
            });

            MessageReceived?.Invoke(message);
        }
        private void HandleEditMessage(Message editMessage)
        {
            try
            {
                var parts = editMessage.Content.Split(':');
                if (parts.Length >= 3)
                {
                    var messageId = parts[1];
                    var newContent = string.Join(":", parts, 2, parts.Length - 2);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var chat in Chats)
                        {
                            var messageToEdit = chat.Messages.FirstOrDefault(m => m.Id == messageId);
                            if (messageToEdit != null)
                            {
                                if (!messageToEdit.IsEdited)
                                {
                                    messageToEdit.OriginalContent = messageToEdit.Content;
                                }

                                messageToEdit.Content = newContent;
                                messageToEdit.IsEdited = true;
                                messageToEdit.EditedAt = DateTime.Now;
                                break;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обработки редактирования: {ex.Message}");
            }
        }

        private void HandleDeleteMessage(Message deleteMessage)
        {
            try
            {
                var parts = deleteMessage.Content.Split(':');
                if (parts.Length >= 2)
                {
                    var messageId = parts[1];

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var chat in Chats)
                        {
                            var messageToDelete = chat.Messages.FirstOrDefault(m => m.Id == messageId);
                            if (messageToDelete != null)
                            {
                                messageToDelete.IsDeleted = true;
                                messageToDelete.Content = "Сообщение удалено";
                                break;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обработки удаления: {ex.Message}");
            }
        }
        private Chat FindOrCreateChat(string chatId, string senderName)
        {
            var chat = Chats.FirstOrDefault(c => c.ChatId == chatId);
            if (chat == null && !string.IsNullOrEmpty(senderName))
            {
                chat = new Chat
                {
                    ChatId = chatId ?? $"private_{senderName}",
                    Name = senderName == _currentUser.Username ? "Избранное" : senderName
                };

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Chats.Add(chat);
                });
            }
            return chat;
        }

        private void OnConnectionStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(status);
        }

        public async Task SendMessageAsync(string content, string chatId = "general")
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var message = new Message
            {
                Sender = _currentUser.Username,
                Content = content.Trim(),
                Timestamp = DateTime.Now,
                ChatId = chatId,
                Type = Message.MessageType.Text
            };

            await _webSocketService.SendMessageAsync(message);
        }

        public async Task SendMessageAsync(Message message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Content)) return;
            await _webSocketService.SendMessageAsync(message);
        }

        public async Task DisconnectAsync()
        {
            await _webSocketService.DisconnectAsync(_currentUser.Username);
        }
    }
}