﻿using System;
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
            if (message == null)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
        }

        public static string GetMessageText(this QueueMessage message, bool base64Decode)
        {
            if (message == null)
            {
                return string.Empty;
            }

            if (base64Decode)
            {
                return message.GetDecodedMessageText();
            }
            return message.MessageText;
        }
    }
}
