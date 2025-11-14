using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Messenger_Meowtalk.Client.Models;

namespace Messenger_Meowtalk.Client.Services
{
    public class StickerService
    {
        public ObservableCollection<EmojiItem> GraphicStickers { get; } = new();

        public StickerService()
        {
            LoadGraphicStickers();
        }

        private void LoadGraphicStickers()
        {
            var stickersFolder = "Assets/Stickers/default_cats";

            if (!Directory.Exists(stickersFolder))
            {
                Directory.CreateDirectory(stickersFolder);
                return;
            }

            var imageFiles = Directory.GetFiles(stickersFolder, "*.png")
                                   .Concat(Directory.GetFiles(stickersFolder, "*.jpg"))
                                   .Concat(Directory.GetFiles(stickersFolder, "*.jpeg"));

            foreach (var filePath in imageFiles)
            {
                var sticker = new EmojiItem
                {
                    Code = Path.GetFileNameWithoutExtension(filePath),
                    Description = "Стикер",
                    IsSticker = true,
                    ImagePath = filePath
                };
                GraphicStickers.Add(sticker);
            }
        }
    }
}