using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BoxQuotaUpdate
{
    internal class BoxClient
    {
        private static readonly List<string> ConsumedAccessTokens = new List<string>();
        private static readonly object Locker = new object(); 
        private string _accessToken;
        private readonly string _id;
        private readonly string _secret;
        private string _refreshToken;
        private const string UserFields = "name,id,login,space_amount,status";
        private const string BoxApiUrlBase = "https://api.box.com/2.0";

        public BoxClient(string accessToken, string refreshToken, string id, string secret)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _id = id;
            _secret = secret;
        }

        public async Task<BoxUser> UpdateUser<T>(string id, long quotaInBytes)
        {
            return await PostAsync<BoxUser>(String.Format("/users/{0}?fields={1}",id,UserFields), new {id, space_amount = quotaInBytes});
        }

        public async Task<BoxCollection<BoxUser>> GetUsers(int offset)
        {
            return await GetAsync<BoxCollection<BoxUser>>(String.Format("/users?limit=1000&offset={0}&fields={1}", offset, UserFields));
        }

        private async Task<T> GetAsync<T>(string request)
        {
            return await GetResult<T>(async client => await client.GetAsync(BoxApiUrlBase + request));
        }

        private async Task<T> PostAsync<T>(string request, object body)
        {
            return await GetResult<T>(async client => await client.PutAsync(BoxApiUrlBase + request, new StringContent(JsonConvert.SerializeObject(body))));
        }

        private async Task<T> GetResult<T>(Func<HttpClient, Task<HttpResponseMessage>> getResult)
        {
            var result = await getResult(HttpClient());
            if (result.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (await TryRefreshAccessToken(_accessToken))
                {
                    result = await getResult(HttpClient());
                }
                else
                {
                    throw new BoxAuthorizationException();;
                }
            }
            result.EnsureSuccessStatusCode();
            var content = await result.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(content);
        }

        private async Task<bool> TryRefreshAccessToken(string accessToken)
        {
            lock (Locker)
            {
                if (string.IsNullOrWhiteSpace(_refreshToken) || string.IsNullOrWhiteSpace(_id) || string.IsNullOrWhiteSpace(_secret)) return false;
                if (ConsumedAccessTokens.Contains(accessToken)) return true;
                ConsumedAccessTokens.Add(accessToken);
                RefreshAccessToken();
                return true;
            }
        }

        private void RefreshAccessToken()
        {
            var httpClient = new HttpClient();

            var body = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", _refreshToken),
                new KeyValuePair<string, string>("client_id", _id),
                new KeyValuePair<string, string>("client_secret", _secret),
            };

            var result = httpClient.PostAsync("https://app.box.com/api/oauth2/token", new FormUrlEncodedContent(body)).Result;
            result.EnsureSuccessStatusCode();
            var content = result.Content.ReadAsStringAsync().Result;
            var token = JsonConvert.DeserializeObject<BoxAccessToken>(content);
            _accessToken = token.AccessToken;
            _refreshToken = token.RefreshToken;
        }

        private HttpClient HttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            return httpClient;
        }
    }

    public class BoxAuthorizationException : Exception
    {
        public BoxAuthorizationException()
            : base("Box authorization token is not valid -- perhaps it expired?")
        {
        }
    }
}