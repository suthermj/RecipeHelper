using Newtonsoft.Json;

namespace RecipeHelper.Models.Kroger
{
    public class KrogerLocationsResponse
    {
        [JsonProperty("data")]
        public List<KrogerLocationData> Data { get; set; } = new();
    }

    public class KrogerLocationData
    {
        [JsonProperty("locationId")]
        public string LocationId { get; set; } = null!;

        [JsonProperty("chain")]
        public string Chain { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("phone")]
        public string Phone { get; set; } = "";

        [JsonProperty("address")]
        public KrogerLocationAddress Address { get; set; } = new();

        [JsonProperty("geolocation")]
        public KrogerGeolocation? Geolocation { get; set; }
    }

    public class KrogerGeolocation
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }

    public class KrogerLocationAddress
    {
        [JsonProperty("addressLine1")]
        public string AddressLine1 { get; set; } = "";

        [JsonProperty("city")]
        public string City { get; set; } = "";

        [JsonProperty("state")]
        public string State { get; set; } = "";

        [JsonProperty("zipCode")]
        public string ZipCode { get; set; } = "";
    }

    public class KrogerLocationDto
    {
        public string LocationId { get; set; } = null!;
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Chain { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
