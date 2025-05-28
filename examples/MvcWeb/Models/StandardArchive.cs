using Piranha.AttributeBuilder;
using Piranha.Models;
using Piranha.Extend;
using Piranha.Extend.Fields;
using MvcWeb.Interfaces;

namespace MvcWeb.Models;

[PageType(Title = "Standard archive", IsArchive = true)]
public class StandardArchive : Page<StandardArchive>, IEventPublishable
{
    /// <summary>
    /// The currently loaded post archive.
    /// </summary>
    public PostArchive<PostInfo> Archive { get; set; }

    [Region(Title = "Publish on Save", Description = "⚠️ ATTENTION: If enabled, this article will be automatically sent to external systems after publication. Disable it if you do not intend to synchronize with external integrations.")]
    public CheckBoxField PublishEvents { get; set; }
}
