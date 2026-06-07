using System.Windows;
using System.Windows.Controls;

namespace Minark.Client.Helpers;

public class ContentBlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is ContentBlock block)
        {
            return block.Type == ContentBlockType.Image ? ImageTemplate : TextTemplate;
        }

        return base.SelectTemplate(item, container);
    }
}