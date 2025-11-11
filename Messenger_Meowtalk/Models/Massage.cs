using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Models
{
    public class Message
    {
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsOwnMessage { get; set; }
        public string Sender { get; set; } = "";
    }
}