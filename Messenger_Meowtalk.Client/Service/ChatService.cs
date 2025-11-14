using Messenger_Meowtalk.Shared.Models;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Client.Services
{
    public class ChatService
    {
        private ClientWebSocket _webSocket;
        private string _currentUser;
        private readonly Uri _serverUri;

        public event Action<Message> MessageReceived;
        public event Action<string> ConnectionStatusChanged;

        public ChatService()
        {
            _serverUri = new Uri("ws://localhost:8000/");
        }

        public async Task ConnectAsync(string username)
        {
            try
            {
                _currentUser = username;
                _webSocket = new ClientWebSocket();

                await _webSocket.ConnectAsync(_serverUri, CancellationToken.None);
                ConnectionStatusChanged?.Invoke("Подключено");

                var joinMessage = new Message
                {
                    Sender = username,
                    Content = $"{username} присоединился",
                    Type = Message.MessageType.System,
                    Timestamp = DateTime.Now,
                    ChatId = "general"
                };

                await SendMessageAsync(joinMessage);
                _ = Task.Run(ListenForMessages);
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke($"Ошибка подключения: {ex.Message}");
                Debug.WriteLine($"Ошибка подключения: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(string content, string chatId)
        {
            var message = new Message
            {
                Sender = _currentUser,
                Content = content,
                ChatId = chatId,
                Timestamp = DateTime.Now,
                Type = Message.MessageType.Text,
                IsMyMessage = true
            };

            await SendMessageAsync(message);
        }

        public async Task SendMessageAsync(Message message)
        {
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    var messageJson = JsonSerializer.Serialize(message);
                    var buffer = Encoding.UTF8.GetBytes(messageJson);

                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отправки сообщения: {ex.Message}");
            }
        }

        public async Task EditMessageAsync(Message message)
        {
            try
            {
                var editMessage = new Message
                {
                    Type = Message.MessageType.System,
                    Content = $"sync_edit_message:{message.Id}",
                    ChatId = message.ChatId,
                    Sender = _currentUser,
                    Timestamp = DateTime.Now,
                    OriginalContent = message.Content,
                    IsEdited = message.IsEdited,
                    EditedTimestamp = message.EditedTimestamp
                };

                await SendMessageAsync(editMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отправки редактирования: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteMessageAsync(Message message)
        {
            try
            {
                var deleteMessage = new Message
                {
                    Type = Message.MessageType.System,
                    Content = $"sync_delete_message:{message.Id}",
                    ChatId = message.ChatId,
                    Sender = _currentUser,
                    Timestamp = DateTime.Now
                };

                await SendMessageAsync(deleteMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отправки удаления: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ClearChatHistoryAsync(string chatId)
        {
            try
            {
                var clearMessage = new Message
                {
                    Type = Message.MessageType.System,
                    Content = "clear_chat_history",
                    ChatId = chatId,
                    Sender = _currentUser,
                    Timestamp = DateTime.Now
                };

                await SendMessageAsync(clearMessage);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отправки запроса на очистку истории: {ex.Message}");
                return false;
            }
        }

        private async Task ListenForMessages()
        {
            var buffer = new byte[4096];

            try
            {
                while (_webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var message = JsonSerializer.Deserialize<Message>(messageJson);

                        if (message != null)
                        {
                            message.IsMyMessage = message.Sender == _currentUser;
                            MessageReceived?.Invoke(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None);
                        ConnectionStatusChanged?.Invoke("Отключено");
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke($"Ошибка соединения: {ex.Message}");
                Debug.WriteLine($"Ошибка прослушивания: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    var leaveMessage = new Message
                    {
                        Sender = _currentUser,
                        Content = $"{_currentUser} покинул чат",
                        Type = Message.MessageType.System,
                        Timestamp = DateTime.Now,
                        ChatId = "general"
                    };

                    await SendMessageAsync(leaveMessage);

                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "",
                        CancellationToken.None);
                }

                _webSocket?.Dispose();
                _webSocket = null;
                ConnectionStatusChanged?.Invoke("Отключено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отключения: {ex.Message}");
            }
        }
    }
}