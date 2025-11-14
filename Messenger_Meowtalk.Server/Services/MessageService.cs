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

        public async Task SaveMessageAsync(Message message, List<string> participantIds)
        {
            try
            {
                // Сохраняем в основную таблицу Messages
                var messageToSave = new Message
                {
                    Id = message.Id ?? Guid.NewGuid().ToString(),
                    Sender = message.Sender,
                    Content = message.Content, // Сохраняем оригинальное содержимое
                    ChatId = message.ChatId,
                    Timestamp = message.Timestamp,
                    Type = message.Type,
                    IsEdited = message.IsEdited,
                    EditedTimestamp = message.EditedTimestamp,
                    OriginalContent = message.OriginalContent,
                    MediaType = message.MediaType
                };

                _context.Messages.Add(messageToSave);

                // ДОПОЛНИТЕЛЬНО сохраняем в EncryptedMessages для каждого участника
                foreach (var participantId in participantIds)
                {
                    if (_encryptionService.HasKeyForUser(participantId))
                    {
                        var (encryptedData, iv) = _encryptionService.EncryptMessage(message.Content, participantId);

                        var encryptedMessage = new EncryptedMessage
                        {
                            MessageId = messageToSave.Id,
                            UserId = participantId,
                            EncryptedContent = encryptedData,
                            IV = iv
                        };

                        _context.EncryptedMessages.Add(encryptedMessage);
                        Console.WriteLine($"Создана запись EncryptedMessage для пользователя {participantId}");
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"Сообщение {messageToSave.Id} сохранено в Messages и EncryptedMessages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения сообщения: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Message>> GetUserMessagesAsync(string userId, string chatId)
        {
            try
            {
                // ЗАГРУЖАЕМ ИЗ ОСНОВНОЙ ТАБЛИЦЫ Messages
                var messages = await _context.Messages
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var result = new List<Message>();

                foreach (var message in messages)
                {
                    var resultMessage = new Message
                    {
                        Id = message.Id,
                        Sender = message.Sender,
                        Content = message.Content, // Берем содержимое напрямую из Messages
                        ChatId = message.ChatId,
                        Timestamp = message.Timestamp,
                        Type = message.Type,
                        IsEdited = message.IsEdited,
                        EditedTimestamp = message.EditedTimestamp,
                        MediaType = message.MediaType,
                        IsMyMessage = message.Sender == userId
                    };

                    result.Add(resultMessage);
                }

                Console.WriteLine($"Загружено {result.Count} сообщений из Messages для пользователя {userId} в чате {chatId}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения сообщений из Messages: {ex.Message}");
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
                // Находим сообщение в базе
                var existingMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == message.Id)
                    ?? await _context.Messages
                        .FirstOrDefaultAsync(m =>
                            m.Sender == message.Sender &&
                            m.ChatId == message.ChatId &&
                            Math.Abs((m.Timestamp - message.Timestamp).TotalMinutes) < 1);

                if (existingMessage == null)
                {
                    Console.WriteLine($"Сообщение для обновления не найдено. ID: {message.Id}");
                    return false;
                }

                Console.WriteLine($"Обновление сообщения: ID={existingMessage.Id}, OldContent={existingMessage.Content}");

                // ОБНОВЛЯЕМ ОСНОВНУЮ ТАБЛИЦУ Messages
                existingMessage.Content = message.Content; // Обновляем содержимое напрямую
                existingMessage.IsEdited = message.IsEdited;
                existingMessage.EditedTimestamp = message.EditedTimestamp;

                // ОБНОВЛЯЕМ ТАБЛИЦУ EncryptedMessages ДЛЯ ВСЕХ УЧАСТНИКОВ
                foreach (var participantId in participantIds)
                {
                    if (_encryptionService.HasKeyForUser(participantId))
                    {
                        // Шифруем новое содержимое
                        var (encryptedData, iv) = _encryptionService.EncryptMessage(message.Content, participantId);

                        // Находим запись в EncryptedMessages
                        var encryptedMessage = await _context.EncryptedMessages
                            .FirstOrDefaultAsync(em => em.MessageId == existingMessage.Id && em.UserId == participantId);

                        if (encryptedMessage != null)
                        {
                            // Обновляем существующую запись
                            encryptedMessage.EncryptedContent = encryptedData;
                            encryptedMessage.IV = iv;
                            Console.WriteLine($"Обновлено EncryptedMessage для пользователя {participantId}");
                        }
                        else
                        {
                            // Создаем новую запись если не найдена
                            encryptedMessage = new EncryptedMessage
                            {
                                MessageId = existingMessage.Id,
                                UserId = participantId,
                                EncryptedContent = encryptedData,
                                IV = iv
                            };
                            _context.EncryptedMessages.Add(encryptedMessage);
                            Console.WriteLine($"Создано EncryptedMessage для пользователя {participantId}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Нет ключа для пользователя {participantId}, пропускаем обновление EncryptedMessages");
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"Сообщение {existingMessage.Id} успешно обновлено в Messages и EncryptedMessages. Новое содержание: {existingMessage.Content}");
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
        public async Task MigrateExistingMessagesToEncrypted()
        {
            try
            {
                // Получаем все сообщения, которые еще не имеют записей в EncryptedMessages
                var messagesWithoutEncryption = await _context.Messages
                    .Where(m => !_context.EncryptedMessages.Any(em => em.MessageId == m.Id))
                    .ToListAsync();

                Console.WriteLine($"Найдено {messagesWithoutEncryption.Count} сообщений для миграции в EncryptedMessages");

                foreach (var message in messagesWithoutEncryption)
                {
                    // Получаем участников чата
                    var participantIds = await _context.UserChats
                        .Where(uc => uc.ChatId == message.ChatId)
                        .Select(uc => uc.UserId)
                        .ToListAsync();

                    if (!participantIds.Any()) continue;

                    // Создаем записи EncryptedMessages для каждого участника
                    foreach (var participantId in participantIds)
                    {
                        if (!_encryptionService.HasKeyForUser(participantId))
                        {
                            _encryptionService.GenerateAndStoreUserKey(participantId, participantId + "_default_password");
                        }

                        var (encryptedData, iv) = _encryptionService.EncryptMessage(message.Content, participantId);

                        var encryptedMessage = new EncryptedMessage
                        {
                            MessageId = message.Id,
                            UserId = participantId,
                            EncryptedContent = encryptedData,
                            IV = iv
                        };

                        _context.EncryptedMessages.Add(encryptedMessage);
                    }

                    Console.WriteLine($"Мигрировано сообщение {message.Id} для {participantIds.Count} участников");
                }

                await _context.SaveChangesAsync();
                Console.WriteLine("Миграция завершена");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка миграции сообщений: {ex.Message}");
            }
        }
    }
}