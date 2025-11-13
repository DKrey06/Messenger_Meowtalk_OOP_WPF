using Messenger_Meowtalk.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Messenger_Meowtalk.Shared.Models
{
    public class Chat : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string ChatId { get; set; } = Guid.NewGuid().ToString();

        // Добавляем тип чата
        public ChatType Type { get; set; } = ChatType.Private;

        // Список участников чата
        public ObservableCollection<User> Participants { get; set; } = new();

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Message> Messages { get; set; } = new();

        public string LastMessage
        {
            get
            {
                if (Messages.Count == 0) return "Нет сообщений";
                return Messages.Last().Content;
            }
        }

        public string LastMessageTime
        {
            get
            {
                if (Messages.Count == 0) return "";
                return Messages.Last().Timestamp.ToString("HH:mm");
            }
        }

        public DateTime LastMessageTimestamp
        {
            get
            {
                if (Messages.Count == 0) return DateTime.MinValue;
                return Messages.Last().Timestamp;
            }
        }

        // Иконка чата в зависимости от типа
        public string ChatIcon
        {
            get
            {
                return Type == ChatType.Group ? "👥" : "👤";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Chat()
        {
            Messages.CollectionChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(LastMessage));
                OnPropertyChanged(nameof(LastMessageTime));
                OnPropertyChanged(nameof(LastMessageTimestamp));
            };
        }

        public void RefreshLastMessageProperties()
        {
            OnPropertyChanged(nameof(LastMessage));
            OnPropertyChanged(nameof(LastMessageTime));
            OnPropertyChanged(nameof(LastMessageTimestamp));
            OnPropertyChanged(nameof(ChatIcon));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Тип чата
    public enum ChatType
    {
        Private,    // Личный чат
        Group       // Групповой чат
    }
}