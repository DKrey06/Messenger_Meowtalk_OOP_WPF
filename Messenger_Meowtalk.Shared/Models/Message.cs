using System;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Messenger_Meowtalk.Shared.Models
{
    public class Message
    {
        [Key]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonPropertyName("chatId")]
        public string ChatId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public MessageType Type { get; set; } = MessageType.Text;

        public bool IsMyMessage { get; set; }
        public bool ShowSender { get; set; } = true;

        // Новые свойства для редактирования/удаления
        [JsonPropertyName("isEdited")]
        public bool IsEdited { get; set; }

        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; }

        [JsonPropertyName("editedAt")]
        public DateTime? EditedAt { get; set; }

        [JsonPropertyName("originalContent")]
        public string OriginalContent { get; set; } = string.Empty;

        public enum MessageType
        {
            Text,
            System,
            Edit,    // Для редактирования
            Delete   // Для удаления
        }
    }
}