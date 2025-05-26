/*
 * Copyright (c) .NET Foundation and Contributors
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file for details.
 *
 * https://github.com/piranhacms/piranha.core
 *
 */

using System.ComponentModel.DataAnnotations;
using ContentsRUs.Eventing.Publisher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Piranha.Manager.Models;
using Piranha.Manager.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Piranha.Manager.Controllers;

/// <summary>
/// Api controller for page management.
/// </summary>
[Area("Manager")]
[Route("manager/api/page")]
[Authorize(Policy = Permission.Admin)]
[ApiController]
[AutoValidateAntiforgeryToken]
public class PageApiController : Controller
{
    private readonly PageService _service;
    private readonly IApi _api;
    private readonly ManagerLocalizer _localizer;
    private readonly IHubContext<Hubs.PreviewHub> _hub;
    private readonly IAuthorizationService _auth;

    private readonly ILogger<PageApiController> _systemLogger;
    private readonly ILogger _eventLogger;
    private readonly ILogger _securityLogger;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public PageApiController(PageService service, IApi api, ManagerLocalizer localizer, IHubContext<Hubs.PreviewHub> hub, IAuthorizationService auth,
         ILogger<PageApiController> systemLogger,
        ILoggerFactory loggerFactory)
    {
        _service = service;
        _api = api;
        _localizer = localizer;
        _hub = hub;
        _auth = auth;
        _systemLogger = systemLogger;
        _eventLogger = loggerFactory.CreateLogger("Eventing.RabbitMQ");
        _securityLogger = loggerFactory.CreateLogger("Security.Audit");
    }

    /// <summary>
    /// Gets the list model.
    /// </summary>
    /// <returns>The list model</returns>
    [Route("list")]
    [HttpGet]
    [Authorize(Policy = Permission.Pages)]
    public async Task<PageListModel> List()
    {
        var model = await _service.GetList();

        return model;
    }

    /// <summary>
    /// Gets the sitemap model.
    /// </summary>
    /// <returns>The list model</returns>
    [Route("sitemap/{siteId?}")]
    [HttpGet]
    public async Task<SiteListModel> Sitemap(Guid? siteId = null)
    {
        if (!siteId.HasValue || siteId == Guid.Empty)
        {
            siteId = (await _api.Sites.GetDefaultAsync()).Id;
        }
        return await _service.GetSiteList(siteId.Value);
    }

    /// <summary>
    /// Gets the archive model.
    /// </summary>
    /// <returns>The list model</returns>
    [Route("archives/{siteId?}")]
    [HttpGet]
    public async Task<SiteListModel> Archives(Guid? siteId = null)
    {
        if (!siteId.HasValue || siteId == Guid.Empty)
        {
            siteId = (await _api.Sites.GetDefaultAsync()).Id;
        }
        return await _service.GetArchiveList(siteId.Value);
    }

    /// <summary>
    /// Gets the page with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    /// <returns>The page edit model</returns>
    [Route("{id:Guid}")]
    [HttpGet]
    [Authorize(Policy = Permission.PagesEdit)]
    public async Task<PageEditModel> Get(Guid id)
    {
        return await _service.GetById(id);
    }

    /// <summary>
    /// Gets the info model for the page with the
    /// given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    /// <returns>The page info model</returns>
    [Route("info/{id}")]
    [HttpGet]
    [Authorize(Policy = Permission.Pages)]
    public async Task<Piranha.Models.PageInfo> GetInfo(Guid id)
    {
        return await _api.Pages.GetByIdAsync<Piranha.Models.PageInfo>(id);
    }

    /// <summary>
    /// Creates a new page of the specified type.
    /// </summary>
    /// <param name="siteId">The site id</param>
    /// <param name="typeId">The type id</param>
    /// <returns>The page edit model</returns>
    [Route("create/{siteId}/{typeId}")]
    [HttpGet]
    [Authorize(Policy = Permission.PagesAdd)]
    public async Task<PageEditModel> Create(Guid siteId, string typeId)
    {
        return await _service.Create(siteId, typeId);
    }

    /// <summary>
    /// Creates a new page of the specified type.
    /// </summary>
    /// <param name="pageId">The page the new page should be position relative to</param>
    /// <param name="typeId">The type id</param>
    /// <param name="after">If the new page should be positioned after the existing page</param>
    /// <returns>The page edit model</returns>
    [Route("createrelative/{pageId}/{typeId}/{after}")]
    [HttpGet]
    [Authorize(Policy = Permission.PagesAdd)]
    public async Task<PageEditModel> CreateRelative(Guid pageId, string typeId, bool after)
    {
        return await _service.CreateRelative(pageId, typeId, after);
    }

    /// <summary>
    /// Creates a new page by copying the specified page.
    /// </summary>
    /// <param name="sourceId">The page that should be copied</param>
    /// <param name="siteId">The site id</param>
    /// <returns>The page edit model</returns>
    [Route("copy/{sourceId}/{siteId}")]
    [HttpGet]
    [Authorize(Policy = Permission.PagesAdd)]
    public async Task<PageEditModel> CopyRelative(Guid sourceId, Guid siteId)
    {
        return await _service.Copy(sourceId, siteId);
    }

    /// <summary>
    /// Creates a new page in the specified position by copying the specified page.
    /// </summary>
    /// <param name="sourceId">The page that should be copied</param>
    /// <param name="pageId">The page the new page should be position relative to</param>
    /// <param name="after">If the new page should be positioned after the existing page</param>
    /// <returns>The page edit model</returns>
    [Route("copyrelative/{sourceId}/{pageId}/{after}")]
    [HttpGet]
    [Authorize(Policy = Permission.PagesAdd)]
    public async Task<PageEditModel> CopyRelative(Guid sourceId, Guid pageId, bool after)
    {
        return await _service.CopyRelative(sourceId, pageId, after);
    }

    /// <summary>
    /// Detaches the given copy into a unique page instance.
    /// </summary>
    /// <param name="pageId">The page id</param>
    /// <returns>The page edit model</returns>
    [Route("detach")]
    [HttpPost]
    [Authorize(Policy = Permission.PagesEdit)]
    public async Task<PageEditModel> Detach([FromBody] Guid pageId)
    {
        var model = await _service.Detach(pageId);

        if (model != null)
        {
            model.Status = new StatusMessage
            {
                Type = StatusMessage.Success,
                Body = _localizer.Page["The page was successfully detached from the original"]
            };
            return model;
        }
        return null;
    }

    /// <summary>
    /// Saves the given model
    /// </summary>
    /// <param name="model">The model</param>
    /// <returns>The result of the operation</returns>
    [Route("save")]
    [HttpPost]
    [Authorize(Policy = Permission.PagesPublish)]
    public async Task<PageEditModel> Save(PageEditModel model)
    {


        // Ensure that we have a published date
        if (string.IsNullOrEmpty(model.Published))
        {
            model.Published = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }

        Console.WriteLine($"public async Task<PageEditModel> Save(PageEditModel model)");

        var ret = await Save(model, false);

        await _hub?.Clients.All.SendAsync("Update", model.Id);

        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            string hostName = config["RabbitMQ:HostName"];
            int port = int.Parse(config["RabbitMQ:Port"]);
            string username = config["RabbitMQ:UserName"];
            string password = config["RabbitMQ:Password"];
            _eventLogger.LogInformation("Initiating content update event for page {PageId}", model.Id);
            //Log.Information("Connecting to RabbitMQ at {Host}:{Port}", hostName, port);

            await using var publisher = new PiranhaEventPublisher(
                hostName: hostName,
                port: port,
                user: username,
                pass: password);

            var page = await _api.Pages.GetByIdAsync(model.Id);

            /*
            var testEvent = new
            {
                Id = Guid.NewGuid(),
                Name = "Test Content Update",
                CreatedAt = DateTime.UtcNow,
                Content = new
                {
                    Title = "Sample Article",
                    Slug = "sample-article",
                    Excerpt = "This is a test excerpt from Piranha CMS",
                    Body = "This is the full body of the test article from Piranha CMS",
                    LastModified = DateTime.UtcNow,
                    Categories = new[] { "News", "Technology" }
                },
                Author = new
                {
                    Id = 1,
                    Name = "John Doe",
                    Email = "john@example.com"
                }
            };*/

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity?.Name;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;


            var eventData = new
            {
                Id = page.Id,
                Name = "Content Update",
                CreatedAt = DateTime.UtcNow,
                Content = new
                {
                    Title = page.Title,
                    Slug = page.Slug,
                    Type = page.TypeId,
                    Regions = ((IDictionary<string, object>)page.Regions).ToDictionary(r => r.Key, r => r.Value)
                },
                Author = new
                {
                    Id = userId, // Author ID
                    Name = userName,
                    Email = email
                }
            };


            string routingKey = "content.test";
            await publisher.PublishAsync(
                @event: eventData,
                routingKey: routingKey);

            // write in the console the result
            Console.WriteLine($"Message Sent");
            _securityLogger.LogInformation("User {UserId} published page {PageId}", User.FindFirst(ClaimTypes.NameIdentifier), model.Id);



        }
        catch (Exception ex)
        {
            _eventLogger.LogError(ex, "Failed to publish event for page {PageId}", model.Id);
            _systemLogger.LogCritical("Event publishing failure: {Error}", ex.Message);
            ret.Status = new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = _localizer.Page["An error occurred while publishing the page"]
            };
        }
        finally
        {
        }
        return ret;
    }

    /// <summary>
    /// Saves the given model
    /// </summary>
    /// <param name="model">The model</param>
    /// <returns>The result of the operation</returns>
    [Route("save/draft")]
    [HttpPost]
    [Authorize(Policy = Permission.PagesSave)]
    public async Task<PageEditModel> SaveDraft(PageEditModel model)
    {
        var ret = await Save(model, true);
        await _hub?.Clients.All.SendAsync("Update", model.Id);

        return ret;
    }

    /// <summary>
    /// Saves the given model and unpublishes it
    /// </summary>
    /// <param name="model">The model</param>
    /// <returns>The result of the operation</returns>
    [Route("save/unpublish")]
    [HttpPost]
    [Authorize(Policy = Permission.PagesPublish)]
    public async Task<PageEditModel> SaveUnpublish(PageEditModel model)
    {

        // Remove published date
        model.Published = null;

        var ret = await Save(model, false);
        await _hub?.Clients.All.SendAsync("Update", model.Id);
        _eventLogger.LogInformation("Initiating content update event for page {PageId}", model.Id);

        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            string hostName = config["RabbitMQ:HostName"];
            int port = int.Parse(config["RabbitMQ:Port"]);
            string username = config["RabbitMQ:UserName"];
            string password = config["RabbitMQ:Password"];

            //Log.Information("Connecting to RabbitMQ at {Host}:{Port}", hostName, port);

            await using var publisher = new PiranhaEventPublisher(
                hostName: hostName,
                port: port,
                user: username,
                pass: password);

            var testEvent = new
            {
                Id = Guid.NewGuid(),
                Name = "Test Content Update",
                CreatedAt = DateTime.UtcNow,
                Content = new
                {
                    Title = "Sample Article",
                    Slug = "sample-article",
                    Excerpt = "This is a test excerpt from Piranha CMS",
                    Body = "This is the full body of the test article from Piranha CMS",
                    LastModified = DateTime.UtcNow,
                    Categories = new[] { "News", "Technology" }
                },
                Author = new
                {
                    Id = 1,
                    Name = "John Doe",
                    Email = "john@example.com"
                }
            };


            string routingKey = "content.test";
            await publisher.PublishAsync(
                @event: testEvent,
                routingKey: routingKey);

            // write in the console the result
            Console.WriteLine($"Event published");
            _securityLogger.LogInformation("User {UserId} published page {PageId}", User.FindFirst(ClaimTypes.NameIdentifier), model.Id);


        }
        catch (Exception ex)
        {
            _eventLogger.LogError(ex, "Failed to publish event for page {PageId}", model.Id);
            _systemLogger.LogCritical("Event publishing failure: {Error}", ex.Message);
            ret.Status = new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = _localizer.Page["An error occurred while publishing the page"]
            };
        }
        finally
        {
        }

        return ret;
    }

    [Route("revert")]
    [HttpPost]
    [Authorize(Policy = Permission.PagesSave)]
    public async Task<PageEditModel> Revert([FromBody] Guid id)
    {
        var page = await _service.GetById(id, false);

        if (page != null)
        {
            await _service.Save(page, false);

            page = await _service.GetById(id);
        }

        page.Status = new StatusMessage
        {
            Type = StatusMessage.Success,
            Body = _localizer.Page["The page was successfully reverted to its previous state"]
        };

        await _hub?.Clients.All.SendAsync("Update", id);

        return page;
    }

    /// <summary>
    /// Deletes the page with the given id.
    /// </summary>
    /// <param name="id">The unique id</param>
    /// <returns>The result of the operation</returns>
    [Route("delete")]
    [HttpDelete]
    [Authorize(Policy = Permission.PagesDelete)]
    public async Task<StatusMessage> Delete([FromBody] Guid id)
    {
        try
        {

            var page = await _api.Pages.GetByIdAsync(id);

            _securityLogger.LogInformation("User {UserId} is attempting to delete page {PageId}", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier), id);
            _eventLogger.LogInformation("Initiating content delete event for page {PageId}", id);

            await _service.Delete(id);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity?.Name;
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            string hostName = config["RabbitMQ:HostName"];
            int port = int.Parse(config["RabbitMQ:Port"]);
            string username = config["RabbitMQ:UserName"];
            string password = config["RabbitMQ:Password"];

            await using var publisher = new PiranhaEventPublisher(

            hostName: hostName,
            port: port,
            user: username,
            pass: password);

            var deleteEvent = new
            {
                Id = Guid.NewGuid(),
                Type = "ContentDeleted",
                DeletedAt = DateTime.UtcNow,
                Page = new
                {
                    PageId = id,
                    Title = page?.Title,
                    Slug = page?.Slug
                },
                Author = new
                {
                    Id = userId,
                    Name = userName,
                    Email = userEmail
                }
            };

            await publisher.PublishAsync(
                @event: deleteEvent,
                routingKey: "content.deleted");

            Console.WriteLine($"Delete event published for page {id} with title '{page?.Title}'");
            _securityLogger.LogInformation("User {UserId} deleted page {PageId}", userId, id);
            return new StatusMessage
            {
                Type = StatusMessage.Success,
                Body = _localizer.Page["The page was successfully deleted"]
            };
        }
        catch (ValidationException e)
        {
            // Validation did not succeed
            return new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = e.Message
            };
        }
        catch (Exception ex)
        {
            _eventLogger.LogError(ex, "Failed to delete page {PageId}", id);
            _systemLogger.LogCritical("Event publishing failure: {Error}", ex.Message);
            return new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = _localizer.Page["An error occured while deleting the page"]
            };
        }
    }

    [Route("move")]
    [HttpPost]
    [Authorize(Policy = Permission.PagesEdit)]
    public async Task<PageListModel> Move([FromBody] StructureModel model)
    {
        if (await _service.MovePages(model))
        {
            var list = await List();

            list.Status = new StatusMessage
            {
                Type = StatusMessage.Success,
                Body = _localizer.Page["The sitemap was successfully updated"]
            };
            return list;
        }
        return new PageListModel
        {
            Status = new StatusMessage
            {
                Type = StatusMessage.Warning,
                Body = _localizer.Page["No pages changed position"]
            }
        };
    }

    /// <summary>
    /// Saves the given model
    /// </summary>
    /// <param name="model">The model</param>
    /// <param name="draft">If the page should be saved as a draft</param>
    /// <returns>The result of the operation</returns>
    private async Task<PageEditModel> Save(PageEditModel model, bool draft = false)
    {
        try
        {
            await _service.Save(model, draft);
        }
        catch (ValidationException e)
        {
            model.Status = new StatusMessage
            {
                Type = StatusMessage.Error,
                Body = e.Message
            };

            return model;
        }

        var ret = await _service.GetById(model.Id);
        ret.Status = new StatusMessage
        {
            Type = StatusMessage.Success,
            Body = draft ? _localizer.Page["The page was successfully saved"]
                : string.IsNullOrEmpty(model.Published) ? _localizer.Page["The page was successfully unpublished"] : _localizer.Page["The page was successfully published"]
        };

        return ret;
    }
}
