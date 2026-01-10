using Avalonia.Controls;
using Avalonia.Threading;
using UltimateEnd.Services;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Desktop.Services
{
    public class UiBehavior : IUiBehavior
    {
        public void BeginDescriptionEdit(GameListViewModel vm, Control root)
        {
            vm.SelectedGame!.TempDescription = vm.SelectedGame.Description;
            vm.SelectedGame.IsEditingDescription = true;

            Dispatcher.UIThread.Post(() =>
            {
                var textBox = root.FindControl<TextBox>("DescriptionTextBox");
                textBox?.Focus();
                textBox?.SelectAll();
            }, DispatcherPriority.Render);
        }
    }
}