using Newtonsoft.Json;

namespace BoxQuotaUpdate
{
    internal class BoxUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Login { get; set; }
        [JsonProperty("space_amount")]
        public long SpaceAmount { get; set; }
        public string Status { get; set; }
    }

    internal class BoxAccessToken
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
    }
}