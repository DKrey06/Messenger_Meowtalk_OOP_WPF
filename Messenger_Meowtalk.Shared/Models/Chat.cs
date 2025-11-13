using Messenger_Meowtalk.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Messenger_Meowtalk.Shared.Models
{
    public class Chat : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        [Key]
        [JsonPropertyName("chatId")]
        public string ChatId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
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

        public virtual ObservableCollection<Message> Messages { get; set; } = new ObservableCollection<Message>();
        public virtual ICollection<UserChat> UserChats { get; set; } = new List<UserChat>();

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

        public event PropertyChangedEventHandler PropertyChanged;

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
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}