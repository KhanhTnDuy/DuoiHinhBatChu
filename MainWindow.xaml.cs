using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DuoiHinhBatChu;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // --- Kiểm tra tạm Phần 1: đọc câu đố. Sẽ xóa ở Phần 2. ---
        var puzzles = new Services.PuzzleRepository().LoadAll();
        MessageBox.Show(
            $"Đọc được {puzzles.Count} câu.\n" +
            $"Câu đầu (dễ nhất): {puzzles[0].Answer}\n" +
            $"Câu cuối (khó nhất): {puzzles[^1].Answer}",
            "Test PuzzleRepository");
    }
}