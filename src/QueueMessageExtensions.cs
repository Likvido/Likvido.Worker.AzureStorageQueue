using System;
using System.Text;
using Azure.Storage.Queues.Models;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class QueueMessageExtensions
    {
        public static string EncocodeMessageText(this string message)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
        }

        public static string GetDecodedMessageText(this QueueMessage message)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
        }
    }
}
