using System.Windows;
using System.Windows.Controls;
using Minark.Client.ViewModels.Controls;

namespace Minark.Client.Controls;

/// <summary>
///     Picker d'emojis complet avec catégories.
///     Expose l'événement EmojiSelected quand l'utilisateur clique un emoji.
/// </summary>
public partial class EmojiPickerControl
{
    public EmojiPickerControl()
    {
        InitializeComponent();
        DataContext = new EmojiPickerViewModel();
    }

    public event Action<string>? EmojiSelected;

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
        {
            return;
        }

        if (DataContext is not EmojiPickerViewModel vm)
        {
            return;
        }

        var category = btn.Tag as EmojiCategory;
        if (category is null)
        {
            return;
        }

        var idx = Array.IndexOf(EmojiPickerViewModel.Categories, category);
        if (idx >= 0)
        {
            vm.SelectedCategoryIndex = idx;
        }
    }

    private void EmojiItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
        {
            return;
        }

        var emoji = btn.Tag as string;
        if (!string.IsNullOrEmpty(emoji))
        {
            EmojiSelected?.Invoke(emoji);
        }
    }
}