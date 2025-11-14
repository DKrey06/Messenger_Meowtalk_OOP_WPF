using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger_Meowtalk.Shared.Models
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public MessageType Type { get; set; }
        public string ChatId { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public bool IsEdited { get; set; }
        public DateTime? EditedTimestamp { get; set; }
        public string OriginalContent { get; set; }

        [NotMapped]
        public bool IsMyMessage { get; set; }

        [NotMapped]
        public bool ShowSender { get; set; } = true;

        public enum MessageType
        {
            Text,
            Sticker,
            System
        }
    }
}