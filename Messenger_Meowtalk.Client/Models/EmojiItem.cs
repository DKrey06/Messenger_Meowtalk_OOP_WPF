using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Client.Models
{
    public class EmojiItem
    {
        public string Code { get; set; } = string.Empty; // Unicode эмодзи или код стикера
        public string Description { get; set; } = string.Empty;
        public bool IsSticker { get; set; } // true для стикеров, false для эмодзи
    }
}
