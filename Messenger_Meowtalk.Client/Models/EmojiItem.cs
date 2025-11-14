using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Client.Models
{
    public class EmojiItem
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSticker { get; set; }
        public string ImagePath { get; set; } = string.Empty;
    }
}
