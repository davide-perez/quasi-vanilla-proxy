using System.Security.Cryptography;
using System.Text;


namespace DPE.QuasiVanillaProxy.Security
{
    // Utility class for encrypting and decrypting text leveraging DPAPI
    // https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection
    public class DataProtectionMgt
    {
        static readonly byte[] _entropy = Encoding.Unicode.GetBytes("aa4aa8c3-fa84-4b10-abe4-071ad169ebdb");
        static DataProtectionScope _scope = DataProtectionScope.CurrentUser;
        static readonly string _cypherPrefix = "CypherValue!"; // Will be prepended to encrypted values, and discarded on decryption


        public static string Encrypt(string clearText)
        {
            if (clearText == null)
                throw new ArgumentNullException(nameof(clearText));

            byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
            byte[] encryptedBytes = ProtectedData.Protect(clearBytes, _entropy, _scope);

            return Convert.ToBase64String(encryptedBytes);
        }


        public static string Decrypt(string encryptedText)
        {
            if (encryptedText == null)
                throw new ArgumentNullException(nameof(encryptedText));

            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] clearBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, _scope);

            return Encoding.UTF8.GetString(clearBytes);
        }


        public static bool IsEncrypted(string text) =>
             text.StartsWith(_cypherPrefix, StringComparison.OrdinalIgnoreCase);


        public static string EncryptSettingValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            if (IsEncrypted(value))
            {
                return value;
            }
            string encryptedValue = _cypherPrefix + Encrypt(Convert.ToString(value));

            return encryptedValue;
        }


        public static string DecryptSettingValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            if (!IsEncrypted(value))
            {
                return value;
            }
            value = value.Substring(_cypherPrefix.Length, value.Length - _cypherPrefix.Length);

            return Decrypt(value);
        }
    }
}
