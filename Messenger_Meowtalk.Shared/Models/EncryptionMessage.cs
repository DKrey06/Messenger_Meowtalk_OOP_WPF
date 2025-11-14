using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Messenger_Meowtalk.Shared.Models
{
    public class EncryptedMessage
    {
        [Key]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("messageId")]
        public string MessageId { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("encryptedContent")]
        public byte[] EncryptedContent { get; set; }

        [JsonPropertyName("iv")]
        public byte[] IV { get; set; }

        [JsonPropertyName("encryptedAt")]
        public DateTime EncryptedAt { get; set; } = DateTime.Now;
    }
}