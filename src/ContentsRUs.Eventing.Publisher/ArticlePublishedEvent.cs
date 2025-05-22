namespace ContentsRUs.Eventing.Events
{
    public class ArticlePublishedEvent
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public Guid? SiteId { get; set; }
        public DateTime Published { get; set; }
    }
}
