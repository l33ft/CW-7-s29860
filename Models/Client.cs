namespace TravelAgencyAPI.Models
{
    public class Client
    {
        public int IdClient { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Telephone { get; set; }
        public string? Pesel { get; set; }
    }
}