using System.Security.Cryptography;
using System.Text;

namespace Messenger_Meowtalk.Server.Services
{
    public class EncryptionService
    {
        private readonly Dictionary<string, byte[]> _userKeys = new();

        public byte[] GenerateAndStoreUserKey(string userId, string password)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(userId), 10000);
            var key = deriveBytes.GetBytes(32);
            _userKeys[userId] = key;
            return key;
        }

        public (byte[] encryptedData, byte[] iv) EncryptMessage(string message, string userId)
        {
            if (!_userKeys.ContainsKey(userId))
                throw new InvalidOperationException($"Key not found for user {userId}");

            using var aes = Aes.Create();
            aes.Key = _userKeys[userId];
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                cs.Write(messageBytes);
            }

            return (ms.ToArray(), aes.IV);
        }

        public string DecryptMessage(byte[] encryptedData, byte[] iv, string userId)
        {
            if (!_userKeys.ContainsKey(userId))
                throw new InvalidOperationException($"Key not found for user {userId}");

            using var aes = Aes.Create();
            aes.Key = _userKeys[userId];
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);

            return reader.ReadToEnd();
        }

        public bool HasKeyForUser(string userId) => _userKeys.ContainsKey(userId);
        public void RemoveUserKey(string userId) => _userKeys.Remove(userId);
    }
}