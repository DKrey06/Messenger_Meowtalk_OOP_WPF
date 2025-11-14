using Messenger_Meowtalk.Server.Data;
using Messenger_Meowtalk.Server.Services;
using Messenger_Meowtalk.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Messenger_Meowtalk.Server.Services
{
    public class MessageService
    {
        private readonly ChatDbContext _context;
        private readonly EncryptionService _encryptionService;

        public MessageService(ChatDbContext context, EncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task SaveMessageAsync(Message message, List<string> participantUserIds)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.ChatId == message.ChatId);

                if (chat == null)
                {
                    chat = new Chat { ChatId = message.ChatId, Name = $"Чат {message.ChatId}" };
                    _context.Chats.Add(chat);
                    await _context.SaveChangesAsync();
                }

                var validUsers = new List<string>();
                foreach (var userId in participantUserIds)
                {
                    var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
                    if (userExists)
                    {
                        validUsers.Add(userId);
                    }
                }

                if (validUsers.Any())
                {
                    foreach (var userId in validUsers)
                    {
                        var existingUserChat = await _context.UserChats
                            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChatId == message.ChatId);

                        if (existingUserChat == null)
                        {
                            var userChat = new UserChat
                            {
                                UserId = userId,
                                ChatId = message.ChatId,
                                JoinedAt = DateTime.Now
                            };

                            _context.UserChats.Add(userChat);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                if (validUsers.Any())
                {
                    foreach (var userId in validUsers)
                    {
                        try
                        {
                            var userKey = _encryptionService.GenerateAndStoreUserKey(userId, "default_password_" + userId);
                            var encryptedData = _encryptionService.EncryptMessage(message.Content, userId);

                            var encryptedMessage = new EncryptedMessage
                            {
                                MessageId = message.Id,
                                UserId = userId,
                                EncryptedContent = encryptedData.encryptedData,
                                IV = encryptedData.iv
                            };

                            _context.EncryptedMessages.Add(encryptedMessage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка шифрования для {userId}: {ex.Message}");
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Message>> GetUserMessagesAsync(string userId, string chatId)
        {
            try
            {
                var messages = new List<Message>();

                var allMessages = await _context.Messages
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var encryptedMessages = await _context.EncryptedMessages
                    .Where(em => em.UserId == userId && em.MessageId != null)
                    .ToDictionaryAsync(em => em.MessageId, em => em);

                _encryptionService.GenerateAndStoreUserKey(userId, "default_password_" + userId);

                foreach (var message in allMessages)
                {
                    if (encryptedMessages.TryGetValue(message.Id, out var encryptedMessage))
                    {
                        try
                        {
                            var decryptedContent = _encryptionService.DecryptMessage(
                                encryptedMessage.EncryptedContent,
                                encryptedMessage.IV,
                                userId);

                            message.Content = decryptedContent;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка дешифровки сообщения {message.Id}: {ex.Message}");
                        }
                    }

                    messages.Add(message);
                }

                _encryptionService.RemoveUserKey(userId);
                return messages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения сообщений: {ex.Message}");
                throw;
            }
        }
    }
}