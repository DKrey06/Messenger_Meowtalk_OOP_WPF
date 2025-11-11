using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger_Meowtalk.Shared.Models
{
    public class User
    {
        public string Username { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}