using Piranha.Extend;
using Piranha.Extend.Fields;
using Piranha.Models;
using Piranha.AttributeBuilder;

namespace MvcWeb.Models 
{
    [PageType(Title = "Article Page")]
    public class ArticlePage : Page<ArticlePage>
    {
        [Region(Title = "Intro Title")]
        public StringField IntroTitle { get; set; }

        [Region(Title = "Body")]
        public HtmlField Body { get; set; }

        [Region(Title = "Publish on Save", Description = "If checked, the article will be published immediately after saving.")]
        public CheckBoxField PublishEvents { get; set; }
    }
}
