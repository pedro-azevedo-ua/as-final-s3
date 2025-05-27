namespace ContentsRUs.Eventing.Shared.Models
{
    public class SecureContentEvent
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public ContentData Content { get; set; }
        public AuthorData Author { get; set; }
        public string HashedUserId { get; set; }
        public string Signature { get; set; }
    }

    public class ContentData
    {
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Type { get; set; }
        public IDictionary<string, object> Regions { get; set; }
    }

    public class AuthorData
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}