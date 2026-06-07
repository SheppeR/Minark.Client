using Microsoft.Win32;
using Minark.Client.Services.Interfaces;

namespace Minark.Client.Services;

public class DialogService : IDialogService
{
    public string? OpenFile(string title, string filter)
    {
        var dlg = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = false
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}