using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoxQuotaUpdate
{
    internal class BoxCollection<T>
    {
        public List<T> Entries { get; set; }
        [JsonProperty("total_count")]
        public int TotalCount { get; set; }
    }
}