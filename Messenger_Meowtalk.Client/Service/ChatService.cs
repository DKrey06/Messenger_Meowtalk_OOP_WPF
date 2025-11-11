using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Messenger_Meowtalk.Shared.Models;

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
            // Находим чат для сообщения или создаем новый
            var chat = FindOrCreateChat(message.ChatId, message.Sender);

            // Устанавливаем флаг принадлежности сообщения
            message.IsMyMessage = message.Sender == _currentUser.Username;

            MessageReceived?.Invoke(message);
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

        public async Task DisconnectAsync()
        {
            await _webSocketService.DisconnectAsync(_currentUser.Username);
        }
    }
}