using Messenger_Meowtalk.Client.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
            try
            {
                string stickersRoot = "Assets/Stickers/";

                if (!Directory.Exists(stickersRoot))
                {
                    Directory.CreateDirectory(stickersRoot);
                    return;
                }

                var imageFiles = Directory.GetFiles(stickersRoot, "*.*", SearchOption.AllDirectories)
                    .Where(file => file.ToLower().EndsWith(".png") ||
                                   file.ToLower().EndsWith(".jpg") ||
                                   file.ToLower().EndsWith(".jpeg"));

                foreach (var filePath in imageFiles)
                {
                    // Конвертируем в относительный путь для pack URI
                    var relativePath = filePath.Replace("\\", "/");
                    if (relativePath.StartsWith("Assets/"))
                    {
                        relativePath = "/" + relativePath; // Делаем абсолютным в сборке
                    }

                    var sticker = new EmojiItem
                    {
                        Code = Path.GetFileNameWithoutExtension(filePath),
                        Description = "Стикер",
                        IsSticker = true,
                        ImagePath = relativePath // Используем относительный путь
                    };
                    GraphicStickers.Add(sticker);
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки стикеров: {ex.Message}");
            }
        }
    }
}