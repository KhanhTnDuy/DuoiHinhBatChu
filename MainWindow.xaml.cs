using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

public partial class MainWindow : Window
{
    private readonly GameViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new GameViewModel();
        DataContext = _vm;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, CancelEventArgs e) => _vm.Save();
}
