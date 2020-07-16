using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using NLog.Config;
using NLog.Targets;
using NLog.Common;

namespace NLog.AzureLog
{
    [Target("Azure")]
    public sealed class Azure : TargetWithLayout
    {

        public Azure()
        {
        }

        private static TaskQueue _taskQueue = new TaskQueue(2, 20000);
        [RequiredParameter]
        public string CustomerId { get; set; }
        public string SharedKey { get; set; }
        public string LogName { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            string logMessage = this.Layout.Render(logEvent);

            if (_taskQueue.Queue(() => PostData(logMessage)))
            {
                _taskQueue.ProcessBackground();
            }

        }

        public string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        private string Base64(object p)
        {
            throw new NotImplementedException();
        }

        public async Task PostData(string logMessage)
        {
            try
            {
                //See code from https://docs.microsoft.com/en-us/azure/azure-monitor/platform/data-collector-api
                // Create a hash for the API signature
                var datestring = DateTime.UtcNow.ToString("r");
                var jsonBytes = Encoding.UTF8.GetBytes(logMessage);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, this.SharedKey);
                string signature = "SharedKey " + this.CustomerId + ":" + hashedString;

                string url = "https://" + CustomerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", LogName);
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", datestring);
                //client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                System.Net.Http.HttpContent httpContent = new StringContent(logMessage, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
            }
            catch (Exception excep)
            {
                InternalLogger.Error("API Post Exception: " + excep.Message);
            }
        }
    }
}
