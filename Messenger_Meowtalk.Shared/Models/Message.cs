using System;
using System.Text.Json.Serialization;

namespace Messenger_Meowtalk.Shared.Models
{
    public class Message
    {
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

        public enum MessageType
        {
            Text,
            System
        }
    }
}