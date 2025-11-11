using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Messenger_Meowtalk.Models
{
    public class Chat
    {
        public string ContactName { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public string LastMessageTime { get; set; } = "";
        public ObservableCollection<Message> Messages { get; set; } = new ObservableCollection<Message>();
    }
}