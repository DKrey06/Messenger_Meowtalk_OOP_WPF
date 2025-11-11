using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Models
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ChatId { get; set; } = string.Empty;
        public MessageType Type { get; set; } = MessageType.Text;

        public enum MessageType
        {
            Text,
            System
        }
    }
}