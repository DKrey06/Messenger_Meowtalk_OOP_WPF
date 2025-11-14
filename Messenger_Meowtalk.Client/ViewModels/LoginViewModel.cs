using System;
using Messenger_Meowtalk.Shared.Models;

namespace Messenger_Meowtalk.Client.ViewModels
{
    public class LoginViewModel
    {
        public User CurrentUser { get; private set; }
        public string Username { get; set; } = string.Empty;

        public bool Login()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                return false;
            }

            if (Username.Length < 2)
            {
                return false;
            }

            CurrentUser = new User
            {
                Username = Username.Trim(),
                UserId = Guid.NewGuid().ToString(),
                IsOnline = true,
                Status = "В сети"
            };

            return true;
        }
    }
}