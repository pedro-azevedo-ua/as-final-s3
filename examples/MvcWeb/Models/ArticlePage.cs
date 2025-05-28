using Piranha.Extend;
using Piranha.Extend.Fields;
using Piranha.Models;
using Piranha.AttributeBuilder;
using MvcWeb.Interfaces;

namespace MvcWeb.Models 
{
    [PageType(Title = "Article Page")]
    public class ArticlePage : Page<ArticlePage>, IEventPublishable
    {
        [Region(Title = "Intro Title")]
        public StringField IntroTitle { get; set; }

        [Region(Title = "Body")]
        public HtmlField Body { get; set; }

        [Region(Title = "Publish on Save", Description = "⚠️ ATTENTION: If enabled, this article will be automatically sent to external systems after publication. Disable it if you do not intend to synchronize with external integrations.")]
        public CheckBoxField PublishEvents { get; set; }
    }
}
