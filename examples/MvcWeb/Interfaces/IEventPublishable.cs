using Piranha.Extend.Fields;

namespace MvcWeb.Interfaces
{
    public interface IEventPublishable
    {
        CheckBoxField PublishEvents { get; set; }
    }
}
