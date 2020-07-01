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
        private static HttpClient _httpClient;

        public Azure()
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
            }
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
        public async Task PostData(string logMessage)
        {
            try
            {
                // Create a hash for the API signature
                var datestring = DateTime.UtcNow.ToString("r");
                var jsonBytes = Encoding.UTF8.GetBytes(logMessage);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, this.SharedKey);
                string signature = "SharedKey " + this.CustomerId + ":" + hashedString;

                string url = "https://" + CustomerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
                using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    httpRequestMessage.Content = new StringContent
                        (logMessage, Encoding.UTF8, "application/json");
                    httpRequestMessage.Headers.Clear();
                    httpRequestMessage.Headers.Add("Accept", "application/json");
                    httpRequestMessage.Headers.Add("Log-Type", LogName);
                    httpRequestMessage.Headers.Add("Authorization", signature);
                    httpRequestMessage.Headers.Add("x-ms-date", datestring);
                    httpRequestMessage.Headers.Add("time-generated-field", "");
                    try
                    {

                        HttpResponseMessage response = await _httpClient.SendAsync(httpRequestMessage);
                        if (response != null && response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            InternalLogger.Error("API Post Failed status: " + response.StatusCode.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        InternalLogger.Error(e.Message);
                        throw;
                    }
                    httpRequestMessage.Dispose();
                };


            }
            catch (Exception excep)
            {
                InternalLogger.Error("API Post Exception: " + excep.Message);
            }
        }
    }
}
