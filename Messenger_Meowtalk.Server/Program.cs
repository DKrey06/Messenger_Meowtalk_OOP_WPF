using Messenger_Meowtalk.Server.Data;
using Messenger_Meowtalk.Server.Services;
using Messenger_Meowtalk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger_MeowtalkServer
{
    class Program
    {
        private static List<WebSocket> _clients = new List<WebSocket>();
        private static List<Message> _messageHistory = new List<Message>();
        private static List<User> _connectedUsers = new List<User>();
        private static ChatDbContext _dbContext;
        private static MessageService _messageService;

        static async Task Main(string[] args)
        {
            Batteries_V2.Init();

            _dbContext = new ChatDbContext();
            var encryptionService = new EncryptionService();
            _messageService = new MessageService(_dbContext, encryptionService);

            await _dbContext.Database.EnsureCreatedAsync();

            await LoadConnectedUsersFromDatabase();
            await EnsureUserChatRelationships();

            var server = new HttpListener();
            var localIP = GetLocalIPAddress();

            server.Prefixes.Add("http://localhost:8000/");
            server.Prefixes.Add($"http://{localIP}:8000/");

            try
            {
                server.Start();
                Console.WriteLine("WebSocket сервер запущен!");
                Console.WriteLine($"IP адрес: {localIP}");
                Console.WriteLine("Ожидание подключений...");

                while (true)
                {
                    var context = await server.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        var webSocketContext = await context.AcceptWebSocketAsync(null);
                        var webSocket = webSocketContext.WebSocket;
                        _clients.Add(webSocket);

                        var clientIP = context.Request.RemoteEndPoint?.Address?.ToString();
                        Console.WriteLine($"Новое подключение от {clientIP}. Всего клиентов: {_clients.Count}");

                        _ = Task.Run(() => HandleClient(webSocket));
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сервера: {ex.Message}");
                Console.ReadKey();
            }
            finally
            {
                _dbContext?.Dispose();
            }
        }

        private static async Task LoadConnectedUsersFromDatabase()
        {
            try
            {
                var usersFromDb = await _dbContext.Users.ToListAsync();
                _connectedUsers.Clear();
                _connectedUsers.AddRange(usersFromDb);
                Console.WriteLine($"Загружено {_connectedUsers.Count} пользователей из базы данных");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки пользователей из базы: {ex.Message}");
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
                            if (message.Type == Message.MessageType.System && message.Content.Contains("создал чат"))
                            {
                                await HandleChatCreation(message.ChatId, message.Sender);
                            }

                            if (string.IsNullOrEmpty(message.ChatId))
                            {
                                message.ChatId = "general";
                            }

                            var chatExists = await _dbContext.Chats.AnyAsync(c => c.ChatId == message.ChatId);
                            if (!chatExists)
                            {
                                var newChat = new Chat { ChatId = message.ChatId, Name = "Новый чат" };
                                _dbContext.Chats.Add(newChat);
                                await _dbContext.SaveChangesAsync();
                                await CreateUserChatRelationshipsForNewChat(message.ChatId);
                            }

                            if (message.Type == Message.MessageType.System)
                            {
                                var userId = await SaveUserToDatabase(message.Sender);

                                var user = _connectedUsers.FirstOrDefault(u => u.Username == message.Sender);
                                if (user == null && message.Content.Contains("присоединился"))
                                {
                                    var newUser = new User
                                    {
                                        UserId = userId,
                                        Username = message.Sender,
                                        IsOnline = true,
                                        Status = "В сети"
                                    };
                                    _connectedUsers.Add(newUser);
                                    user = newUser;
                                }

                                if (message.Content.Contains("присоединился") && user != null)
                                {
                                    await Task.Delay(500);

                                    var userChats = await _dbContext.UserChats
                                        .Where(uc => uc.UserId == user.UserId)
                                        .Select(uc => uc.ChatId)
                                        .ToListAsync();

                                    foreach (var chatId in userChats)
                                    {
                                        await SendChatHistory(webSocket, chatId, user.UserId);
                                    }
                                }
                            }
                            else
                            {
                                if (message.Type == Message.MessageType.Text)
                                {
                                    try
                                    {
                                        await SyncConnectedUsersWithDatabase();

                                        var participantIds = _connectedUsers
                                            .Where(u => u != null && !string.IsNullOrEmpty(u.UserId))
                                            .Select(u => u.UserId)
                                            .ToList();

                                        if (!participantIds.Any())
                                        {
                                            _dbContext.Messages.Add(message);
                                            await _dbContext.SaveChangesAsync();
                                        }
                                        else
                                        {
                                            await _messageService.SaveMessageAsync(message, participantIds);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Ошибка сохранения в БД: {ex.Message}");
                                    }
                                }
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
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocket ошибка: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            finally
            {
                _clients.Remove(webSocket);
            }
        }

        private static async Task HandleChatCreation(string chatId, string creatorUsername)
        {
            try
            {
                var existingChat = await _dbContext.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
                if (existingChat == null)
                {
                    var newChat = new Chat
                    {
                        ChatId = chatId,
                        Name = $"Чат {chatId}"
                    };
                    _dbContext.Chats.Add(newChat);
                    await _dbContext.SaveChangesAsync();
                }

                var creator = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == creatorUsername);
                if (creator == null)
                {
                    creator = new User
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Username = creatorUsername,
                        IsOnline = true,
                        Status = "В сети"
                    };
                    _dbContext.Users.Add(creator);
                    await _dbContext.SaveChangesAsync();
                }

                var existingUserChat = await _dbContext.UserChats
                    .FirstOrDefaultAsync(uc => uc.UserId == creator.UserId && uc.ChatId == chatId);

                if (existingUserChat == null)
                {
                    var userChat = new UserChat
                    {
                        UserId = creator.UserId,
                        ChatId = chatId,
                        JoinedAt = DateTime.Now
                    };
                    _dbContext.UserChats.Add(userChat);
                    await _dbContext.SaveChangesAsync();
                }

                var connectedUser = _connectedUsers.FirstOrDefault(u => u.Username == creatorUsername);
                if (connectedUser == null)
                {
                    _connectedUsers.Add(creator);
                }

                var creatorWebSocket = _clients.FirstOrDefault();
                if (creatorWebSocket != null && creatorWebSocket.State == WebSocketState.Open)
                {
                    await SendChatHistory(creatorWebSocket, chatId, creator.UserId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания чата: {ex.Message}");
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

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "localhost";
        }

        private static async Task<string> SaveUserToDatabase(string username)
        {
            try
            {
                var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (existingUser == null)
                {
                    var newUser = new User
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Username = username,
                        IsOnline = true,
                        Status = "В сети"
                    };
                    _dbContext.Users.Add(newUser);
                    await _dbContext.SaveChangesAsync();
                    return newUser.UserId;
                }
                else
                {
                    return existingUser.UserId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения пользователя: {ex.Message}");
                return null;
            }
        }

        private static async Task EnsureUserChatRelationships()
        {
            try
            {
                var users = await _dbContext.Users.ToListAsync();
                var chats = await _dbContext.Chats.ToListAsync();

                int createdCount = 0;

                foreach (var user in users)
                {
                    foreach (var chat in chats)
                    {
                        if (user != null && chat != null && !string.IsNullOrEmpty(user.UserId) && !string.IsNullOrEmpty(chat.ChatId))
                        {
                            var existingUserChat = await _dbContext.UserChats
                                .FirstOrDefaultAsync(uc => uc.UserId == user.UserId && uc.ChatId == chat.ChatId);

                            if (existingUserChat == null)
                            {
                                var userChat = new UserChat
                                {
                                    UserId = user.UserId,
                                    ChatId = chat.ChatId,
                                    JoinedAt = DateTime.Now
                                };

                                _dbContext.UserChats.Add(userChat);
                                createdCount++;
                            }
                        }
                    }
                }

                if (createdCount > 0)
                {
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания связей UserChat: {ex.Message}");
            }
        }

        private static async Task SendChatHistory(WebSocket webSocket, string chatId, string userId)
        {
            try
            {
                var messages = await _messageService.GetUserMessagesAsync(userId, chatId);

                foreach (var message in messages)
                {
                    var messageJson = JsonSerializer.Serialize(message);
                    var buffer = Encoding.UTF8.GetBytes(messageJson);

                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки истории: {ex.Message}");
            }
        }

        private static async Task CreateUserChatRelationshipsForNewChat(string chatId)
        {
            try
            {
                var users = await _dbContext.Users.ToListAsync();
                var chat = await _dbContext.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);

                if (chat == null) return;

                int createdCount = 0;

                foreach (var user in users)
                {
                    if (user != null && !string.IsNullOrEmpty(user.UserId))
                    {
                        var existingUserChat = await _dbContext.UserChats
                            .FirstOrDefaultAsync(uc => uc.UserId == user.UserId && uc.ChatId == chatId);

                        if (existingUserChat == null)
                        {
                            var userChat = new UserChat
                            {
                                UserId = user.UserId,
                                ChatId = chatId,
                                JoinedAt = DateTime.Now
                            };

                            _dbContext.UserChats.Add(userChat);
                            createdCount++;
                        }
                    }
                }

                if (createdCount > 0)
                {
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания связей для чата {chatId}: {ex.Message}");
            }
        }

        private static async Task SyncConnectedUsersWithDatabase()
        {
            try
            {
                var usersInDb = await _dbContext.Users.ToListAsync();
                var usersToRemove = new List<User>();

                foreach (var connectedUser in _connectedUsers)
                {
                    var userInDb = usersInDb.FirstOrDefault(u => u.Username == connectedUser.Username);
                    if (userInDb != null)
                    {
                        if (connectedUser.UserId != userInDb.UserId)
                        {
                            connectedUser.UserId = userInDb.UserId;
                        }
                    }
                    else
                    {
                        usersToRemove.Add(connectedUser);
                    }
                }

                foreach (var userToRemove in usersToRemove)
                {
                    _connectedUsers.Remove(userToRemove);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка синхронизации: {ex.Message}");
            }
        }
    }
}