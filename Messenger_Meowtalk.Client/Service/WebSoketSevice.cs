using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Client.Services
{
    public class WebSocketService
    {
        private ClientWebSocket _webSocket;
        private readonly string _serverUrl = "ws://localhost:8080/";
        public event Action<Message> MessageReceived;
        public event Action<string> ConnectionStatusChanged;

        public async Task ConnectAsync(string username)
        {
            try
            {
                _webSocket = new ClientWebSocket();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                await _webSocket.ConnectAsync(new Uri(_serverUrl), cts.Token);
                ConnectionStatusChanged?.Invoke("✅ Подключено к серверу");

                var connectMessage = new Message
                {
                    Sender = username,
                    Content = $"{username} присоединился к чату",
                    Type = Message.MessageType.System
                };
                await SendMessageAsync(connectMessage);

                _ = Task.Run(ReceiveMessages);
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke($"❌ Ошибка подключения: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(Message message)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var json = JsonSerializer.Serialize(message);
                    var buffer = Encoding.UTF8.GetBytes(json);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    ConnectionStatusChanged?.Invoke($"❌ Ошибка отправки: {ex.Message}");
                }
            }
            else
            {
                ConnectionStatusChanged?.Invoke("❌ WebSocket не подключен");
            }
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[4096];
            try
            {
                while (_webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var message = JsonSerializer.Deserialize<Message>(messageJson);

                        if (message != null)
                        {
                            MessageReceived?.Invoke(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "", CancellationToken.None);
                        ConnectionStatusChanged?.Invoke("🔌 Отключено от сервера");
                    }
                }
            }
            catch (WebSocketException ex)
            {
                ConnectionStatusChanged?.Invoke($"❌ WebSocket ошибка: {ex.Message}");
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke($"❌ Ошибка получения: {ex.Message}");
            }
        }

        public async Task DisconnectAsync(string username)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    // Отправляем системное сообщение об отключении
                    var disconnectMessage = new Message
                    {
                        Sender = username,
                        Content = $"{username} покинул чат",
                        Type = Message.MessageType.System
                    };
                    await SendMessageAsync(disconnectMessage);

                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    ConnectionStatusChanged?.Invoke($"❌ Ошибка отключения: {ex.Message}");
                }
            }
        }
    }
}