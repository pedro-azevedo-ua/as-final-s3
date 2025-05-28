using Piranha.AttributeBuilder;
using Piranha.Models;
using MvcWeb.Interfaces;
using Piranha.Extend;
using Piranha.Extend.Fields;

namespace MvcWeb.Models;

[PostType(Title = "Standard post")]
public class StandardPost : Post<StandardPost>, IEventPublishable
{
    /// <summary>
    /// Gets/sets the available comments if these
    /// have been loaded from the database.
    /// </summary>
    public IEnumerable<Comment> Comments { get; set; } = new List<Comment>();
    
    [Region(Title = "Publish on Save", Description = "⚠️ ATTENTION: If enabled, this article will be automatically sent to external systems after publication. Disable it if you do not intend to synchronize with external integrations.")]
    public CheckBoxField PublishEvents { get; set; }
}
