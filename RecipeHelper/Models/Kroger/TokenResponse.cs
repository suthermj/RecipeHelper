using Newtonsoft.Json;

namespace RecipeHelper.Models.Kroger
{
    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string Token {  get; set; }
    }
}
