namespace RecipeHelper.Models.Kroger
{
    public class KrogerAuthResult
    {
        public bool IsAuthorized { get; set; }

        public string? KrogerProfileId { get; set; }
        public string? AccessToken { get; set; }

        // If !IsAuthorized, this tells the controller where to redirect
        public string? RedirectUrl { get; set; }
    }
}
