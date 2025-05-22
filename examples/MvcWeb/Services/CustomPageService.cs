using Piranha.Models;
using Piranha.Services;
using ContentsRUs.Eventing.Publisher;
using MvcWeb.Models;
using ContentsRUs.Eventing.Events;

namespace MvcWeb.Services;

public class CustomPageService : IPageService
{
    private readonly IPageService _inner;
    private readonly IPiranhaEventPublisher _eventPublisher;
    private readonly ILogger<CustomPageService> _logger;

    public CustomPageService(
        IPageService inner,
        IPiranhaEventPublisher eventPublisher,
        ILogger<CustomPageService> logger)
    {
        _inner = inner;
        _eventPublisher = eventPublisher;
        _logger = logger;

        Console.WriteLine("CustomPageService initialized!");
    }

    public async Task SaveAsync<T>(T model) where T : PageBase
    {
        await _inner.SaveAsync(model);

        _logger.LogInformation("Model type: {Type}, PublishEvents: {Events}",
            model.GetType().Name,
            (model as ArticlePage)?.PublishEvents?.Value);

        if (model is ArticlePage article && article.PublishEvents?.Value == true)
        {
            var evt = new ArticlePublishedEvent
            {
                Id = article.Id,
                Title = article.Title,
                Slug = article.Slug,
                SiteId = model.SiteId,
                Published = article.Published ?? DateTime.UtcNow
            };

            _logger.LogInformation("Publishing event for article: {Title}", article.Title);
            await _eventPublisher.PublishAsync(evt, routingKey: "content.article.published");
        }
    }

    public Task<IEnumerable<DynamicPage>> GetAllAsync(Guid? siteId = null) => _inner.GetAllAsync(siteId);
    public Task<IEnumerable<T>> GetAllAsync<T>(Guid? siteId = null) where T : PageBase => _inner.GetAllAsync<T>(siteId);
    public Task<IEnumerable<DynamicPage>> GetAllBlogsAsync(Guid? siteId = null) => _inner.GetAllBlogsAsync(siteId);
    public Task<IEnumerable<T>> GetAllBlogsAsync<T>(Guid? siteId = null) where T : PageBase => _inner.GetAllBlogsAsync<T>(siteId);
    public Task<IEnumerable<Guid>> GetAllDraftsAsync(Guid? siteId = null) => _inner.GetAllDraftsAsync(siteId);
    public Task<IEnumerable<Comment>> GetAllCommentsAsync(Guid? pageId = null, bool onlyApproved = true, int? page = null, int? pageSize = null)
        => _inner.GetAllCommentsAsync(pageId, onlyApproved, page, pageSize);
    public Task<IEnumerable<Comment>> GetAllPendingCommentsAsync(Guid? pageId = null, int? page = null, int? pageSize = null)
        => _inner.GetAllPendingCommentsAsync(pageId, page, pageSize);
    public Task<DynamicPage> GetStartpageAsync(Guid? siteId = null) => _inner.GetStartpageAsync(siteId);
    public Task<T> GetStartpageAsync<T>(Guid? siteId = null) where T : PageBase => _inner.GetStartpageAsync<T>(siteId);
    public Task<DynamicPage> GetByIdAsync(Guid id) => _inner.GetByIdAsync(id);
    public Task<IEnumerable<T>> GetByIdsAsync<T>(params Guid[] ids) where T : PageBase => _inner.GetByIdsAsync<T>(ids);
    public Task<T> GetByIdAsync<T>(Guid id) where T : PageBase => _inner.GetByIdAsync<T>(id);
    public Task<DynamicPage> GetBySlugAsync(string slug, Guid? siteId = null) => _inner.GetBySlugAsync(slug, siteId);
    public Task<T> GetBySlugAsync<T>(string slug, Guid? siteId = null) where T : PageBase => _inner.GetBySlugAsync<T>(slug, siteId);
    public Task<Guid?> GetIdBySlugAsync(string slug, Guid? siteId = null) => _inner.GetIdBySlugAsync(slug, siteId);
    public Task<DynamicPage> GetDraftByIdAsync(Guid id) => _inner.GetDraftByIdAsync(id);
    public Task<T> GetDraftByIdAsync<T>(Guid id) where T : PageBase => _inner.GetDraftByIdAsync<T>(id);
    public Task<Comment> GetCommentByIdAsync(Guid id) => _inner.GetCommentByIdAsync(id);
    public Task SaveDraftAsync<T>(T model) where T : PageBase => _inner.SaveDraftAsync(model);
    public Task SaveCommentAsync(Guid pageId, PageComment model) => _inner.SaveCommentAsync(pageId, model);
    public Task SaveCommentAndVerifyAsync(Guid pageId, PageComment model) => _inner.SaveCommentAndVerifyAsync(pageId, model);
    public Task DeleteAsync(Guid id) => _inner.DeleteAsync(id);
    public Task DeleteAsync<T>(T model) where T : PageBase => _inner.DeleteAsync(model);
    public Task DeleteCommentAsync(Guid id) => _inner.DeleteCommentAsync(id);
    public Task DeleteCommentAsync(Comment model) => _inner.DeleteCommentAsync(model);
    public Task MoveAsync<T>(T model, Guid? parentId, int sortOrder) where T : PageBase => _inner.MoveAsync(model, parentId, sortOrder);
    public Task<T> CreateAsync<T>(string typeId = null) where T : PageBase => _inner.CreateAsync<T>(typeId);
    public Task<T> CopyAsync<T>(T originalPage) where T : PageBase => _inner.CopyAsync(originalPage);
    public Task DetachAsync<T>(T model) where T : PageBase => _inner.DetachAsync(model);
}
