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
                var messages = await _context.Messages
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var result = new List<Message>();

                foreach (var message in messages)
                {
                    var decryptedMessage = new Message
                    {
                        Id = message.Id, // Важно: используем ID из базы данных
                        Sender = message.Sender,
                        ChatId = message.ChatId,
                        Timestamp = message.Timestamp,
                        Type = message.Type,
                        IsEdited = message.IsEdited,
                        EditedTimestamp = message.EditedTimestamp,
                        OriginalContent = message.OriginalContent,
                        MediaType = message.MediaType
                    };

                    // Для системных сообщений не нужно расшифровывать
                    if (message.Type == Message.MessageType.System)
                    {
                        decryptedMessage.Content = message.Content;
                        decryptedMessage.IsMyMessage = message.Sender == userId;
                    }
                    else
                    {
                        // Для обычных сообщений расшифровываем
                        var encryptedMessage = await _context.EncryptedMessages
                            .FirstOrDefaultAsync(em => em.MessageId == message.Id && em.UserId == userId);

                        if (encryptedMessage != null && _encryptionService.HasKeyForUser(userId))
                        {
                            try
                            {
                                decryptedMessage.Content = _encryptionService.DecryptMessage(
                                    encryptedMessage.EncryptedContent,
                                    encryptedMessage.IV,
                                    userId);
                                decryptedMessage.IsMyMessage = message.Sender == userId;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка расшифровки сообщения {message.Id} для пользователя {userId}: {ex.Message}");
                                decryptedMessage.Content = "[Не удалось расшифровать]";
                            }
                        }
                        else
                        {
                            decryptedMessage.Content = message.Content; // Используем содержимое из Messages как fallback
                        }
                    }

                    result.Add(decryptedMessage);
                }

                Console.WriteLine($"Загружено {result.Count} сообщений для пользователя {userId} в чате {chatId}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения сообщений: {ex.Message}");
                return new List<Message>();
            }
        }
        public async Task<bool> ClearChatHistoryAsync(string chatId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Удаляем зашифрованные сообщения (кроме системных)
                var messagesToDelete = await _context.Messages
                    .Where(m => m.ChatId == chatId && m.Type != Message.MessageType.System)
                    .Select(m => m.Id)
                    .ToListAsync();

                var encryptedMessages = await _context.EncryptedMessages
                    .Where(em => messagesToDelete.Contains(em.MessageId))
                    .ToListAsync();

                _context.EncryptedMessages.RemoveRange(encryptedMessages);
                await _context.SaveChangesAsync();

                // Удаляем сами сообщения (кроме системных)
                var messages = await _context.Messages
                    .Where(m => m.ChatId == chatId && m.Type != Message.MessageType.System)
                    .ToListAsync();

                _context.Messages.RemoveRange(messages);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Ошибка очистки истории чата {chatId}: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> UpdateMessageAsync(Message message, List<string> participantIds)
        {
            try
            {
                // Ищем сообщение по ID или по содержимому и времени
                var existingMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == message.Id)
                    ?? await _context.Messages
                        .FirstOrDefaultAsync(m =>
                            m.Sender == message.Sender &&
                            m.Content == message.OriginalContent &&
                            m.Timestamp.Date == message.Timestamp.Date &&
                            m.ChatId == message.ChatId);

                if (existingMessage == null)
                {
                    Console.WriteLine($"Сообщение для обновления не найдено. ID: {message.Id}, Sender: {message.Sender}, Chat: {message.ChatId}");
                    return false;
                }

                // Обновляем ID сообщения на найденный в базе
                message.Id = existingMessage.Id;

                // Шифруем новое содержимое для каждого участника
                foreach (var participantId in participantIds)
                {
                    if (_encryptionService.HasKeyForUser(participantId))
                    {
                        // Шифруем новое содержимое
                        var (encryptedData, iv) = _encryptionService.EncryptMessage(message.Content, participantId);

                        // Находим или создаем запись в EncryptedMessages
                        var encryptedMessage = await _context.EncryptedMessages
                            .FirstOrDefaultAsync(em => em.MessageId == existingMessage.Id && em.UserId == participantId);

                        if (encryptedMessage != null)
                        {
                            // Обновляем существующую запись
                            encryptedMessage.EncryptedContent = encryptedData;
                            encryptedMessage.IV = iv;
                        }
                        else
                        {
                            // Создаем новую запись
                            encryptedMessage = new EncryptedMessage
                            {
                                MessageId = existingMessage.Id,
                                UserId = participantId,
                                EncryptedContent = encryptedData,
                                IV = iv
                            };
                            _context.EncryptedMessages.Add(encryptedMessage);
                        }
                    }
                }

                // Обновляем основные поля сообщения
                existingMessage.IsEdited = message.IsEdited;
                existingMessage.EditedTimestamp = message.EditedTimestamp;

                // Если нужно сохранить оригинальное содержимое, зашифруем его
                if (!string.IsNullOrEmpty(message.OriginalContent))
                {
                    // Шифруем оригинальное содержимое для первого участника (можно адаптировать для всех)
                    var firstParticipant = participantIds.FirstOrDefault();
                    if (firstParticipant != null && _encryptionService.HasKeyForUser(firstParticipant))
                    {
                        var (encryptedOriginal, ivOriginal) = _encryptionService.EncryptMessage(message.OriginalContent, firstParticipant);
                        // Сохраняем в какое-то поле или создаем отдельную логику
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"Сообщение {existingMessage.Id} успешно обновлено");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления сообщения: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(string messageId)
        {
            try
            {
                // Находим сообщение в базе
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null) return false;

                // Удаляем все связанные записи в EncryptedMessages
                var encryptedMessages = await _context.EncryptedMessages
                    .Where(em => em.MessageId == messageId)
                    .ToListAsync();

                _context.EncryptedMessages.RemoveRange(encryptedMessages);

                // Удаляем само сообщение
                _context.Messages.Remove(message);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка удаления сообщения: {ex.Message}");
                return false;
            }
        }
    }
}