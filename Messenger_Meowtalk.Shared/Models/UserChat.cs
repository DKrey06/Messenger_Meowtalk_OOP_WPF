using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Messenger_Meowtalk.Shared.Models
{
    public class UserChat
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonIgnore]
        public User User { get; set; }

        [JsonPropertyName("chatId")]
        public string ChatId { get; set; }

        [JsonIgnore]
        public Chat Chat { get; set; }

        [JsonPropertyName("joinedAt")]
        public DateTime JoinedAt { get; set; } = DateTime.Now;
    }
}