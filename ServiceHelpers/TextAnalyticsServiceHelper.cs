using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace ServiceHelpers
{
    public class TextAnalyticsServiceHelper
    {
        public class ResponsePayload
        {
            public List<Document> documents;
        }

        public class Document
        {
            public string id;
            public string score;
        }

        private static string apiKey;
        public static string ApiKey
        {
            get { return apiKey; }
            set
            {
                var changed = apiKey != value;
                apiKey = value;
                if (changed)
                {
                    InitializeClient();
                }
            }
        }

        private static HttpClient _client;

        private static void InitializeClient()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ApiKey);
        }

        public static async Task<float> GetSentimentAsync(string text)
        {
            var response = await _client.PostAsync("https://westus.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment", 
                new StringContent("{ \"documents\": [ { \"language\":\"en\",\"id\":\"1\",\"text\":\"" + text + "\" } ] }", Encoding.UTF8, "application/json"));

            Debug.WriteLine("TA response: " + response.StatusCode + " " + response.Headers.ToString() + " " + await response.Content.ReadAsStringAsync());

            var serializer = new DataContractJsonSerializer(typeof(ResponsePayload));
            var payload = serializer.ReadObject(await response.Content.ReadAsStreamAsync()) as ResponsePayload;

            double result;
            if (!Double.TryParse(payload.documents.First().score, out result))
            {
                throw new Exception("Failed to parse sentiment score");
            }
            return (float)result;
        }
    }
}
