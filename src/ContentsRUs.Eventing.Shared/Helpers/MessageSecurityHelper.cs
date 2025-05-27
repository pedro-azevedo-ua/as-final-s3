using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using ContentsRUs.Eventing.Shared.Models;

namespace ContentsRUs.Eventing.Shared.Helpers
{
    public static class MessageSecurityHelper
    {
        // Hash a user ID for privacy
        public static string HashUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return string.Empty;

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(userId));
            return Convert.ToBase64String(bytes).Substring(0, 10);
        }

        public static string ComputeHmacSignature(SecureContentEvent message, string secretKey)
        {
            // Create a shallow copy with Signature set to null
            var clone = new SecureContentEvent
            {
                Id = message.Id,
                Name = message.Name,
                CreatedAt = message.CreatedAt,
                Content = message.Content,
                Author = message.Author,
                HashedUserId = message.HashedUserId,
                Signature = null // exclude signature
            };

            var json = JsonConvert.SerializeObject(clone, new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            });

            Console.WriteLine("[C# JSON to sign]: " + json);


            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); // hex encoding

        }

        public static bool VerifyHmacSignature(SecureContentEvent message, string signature, string secretKey)
        {
            var expected = ComputeHmacSignature(message, secretKey);
            return signature == expected;
        }


        public static bool ValidateSecureContentEvent(SecureContentEvent evt, out string validationError)
        {
            if (evt == null)
            {
                validationError = "Event is null.";
                return false;
            }
            if (evt.Id == Guid.Empty)
            {
                validationError = "Event Id is missing.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(evt.Name))
            {
                validationError = "Event Name is missing.";
                return false;
            }
            if (evt.Content == null)
            {
                validationError = "Content is missing.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(evt.Content.Title))
            {
                validationError = "Content Title is missing.";
                return false;
            }
            if (evt.Author == null)
            {
                validationError = "Author is missing.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(evt.HashedUserId))
            {
                validationError = "HashedUserId is missing.";
                return false;
            }
            // Add other checks as needed
            validationError = null;
            return true;
        }

    }
}
