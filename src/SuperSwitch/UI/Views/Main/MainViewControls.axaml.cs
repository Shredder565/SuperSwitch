using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SuperSwitch.Ava.Common;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Ava.UI.Windows;
using System;

namespace SuperSwitch.Ava.UI.Views.Main
{
    public partial class MainViewControls : SuperSwitchControl<MainWindowViewModel>
    {
        public MainViewControls()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (TopLevel.GetTopLevel(this) is MainWindow window)
            {
                ViewModel = window.ViewModel;
            }
        }

        public void Sort_Checked(object sender, RoutedEventArgs args)
        {
            if (sender is RadioButton { IsChecked: true, Tag: string sortStrategy })
                ViewModel.Sort(Enum.Parse<ApplicationSort>(sortStrategy));
        }

        public void Order_Checked(object sender, RoutedEventArgs args)
        {
            if (sender is RadioButton { IsChecked: true, Tag: string sortOrder })
                ViewModel.Sort(sortOrder is not "Descending");
        }

        private void SearchBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            ViewModel.SearchText = SearchBox.Text;
        }
    }
}
