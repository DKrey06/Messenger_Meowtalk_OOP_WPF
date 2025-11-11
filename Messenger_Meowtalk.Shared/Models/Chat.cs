using Messenger_Meowtalk.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Models
{
    public class Chat
    {
        public string ChatId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<Message> Messages { get; set; } = new();
    }
}