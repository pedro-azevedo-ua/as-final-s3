using Piranha.AttributeBuilder;
using Piranha.Models;
using MvcWeb.Interfaces;
using Piranha.Extend;
using Piranha.Extend.Fields;

namespace MvcWeb.Models;

[PageType(Title = "Standard page")]
public class StandardPage : Page<StandardPage>, IEventPublishable
{
    
    [Region(Title = "Publish on Save", Description = "⚠️ ATTENTION: If enabled, this article will be automatically sent to external systems after publication. Disable it if you do not intend to synchronize with external integrations.")]
    public CheckBoxField PublishEvents { get; set; }
}
