using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Messenger_Meowtalk.Shared.Models; 
namespace Messenger_MeowtalkServer
{
    class Program
    {
        private static List<WebSocket> _clients = new List<WebSocket>();
        private static List<Message> _messageHistory = new List<Message>();

        static async Task Main(string[] args)
        {
            var server = new HttpListener();
            server.Prefixes.Add("http://localhost:8080/");
            server.Start();
            Console.WriteLine("WebSocket сервер запущен на http://localhost:8080/");

            while (true)
            {
                var context = await server.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    var webSocket = webSocketContext.WebSocket;
                    _clients.Add(webSocket);
                    Console.WriteLine($"Новое подключение. Всего клиентов: {_clients.Count}");

                    _ = Task.Run(() => HandleClient(webSocket));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private static async Task HandleClient(WebSocket webSocket)
        {
            var buffer = new byte[4096];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        var message = JsonSerializer.Deserialize<Message>(messageJson);

                        if (message != null)
                        {
                            if (message.Type == Message.MessageType.System)
                            {
                                Console.WriteLine($"Системное сообщение: {message.Content}");
                            }
                            else
                            {
                                Console.WriteLine($"Получено сообщение от {message.Sender}: {message.Content}");
                            }
                            _messageHistory.Add(message);

                            await BroadcastMessage(messageJson);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            finally
            {
                _clients.Remove(webSocket);
                Console.WriteLine($"Клиент отключен. Всего клиентов: {_clients.Count}");
            }
        }

        private static async Task BroadcastMessage(string messageJson)
        {
            var buffer = Encoding.UTF8.GetBytes(messageJson);
            var tasks = new List<Task>();

            foreach (var client in _clients)
            {
                if (client.State == WebSocketState.Open)
                {
                    tasks.Add(client.SendAsync(new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks);
        }
    }
}