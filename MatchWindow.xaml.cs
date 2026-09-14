using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DuoiHinhBatChu.Models;
using DuoiHinhBatChu.Services;
using DuoiHinhBatChu.ViewModels;

namespace DuoiHinhBatChu;

/// <summary>
/// Màn đấu nhiều người. Mở ra là vào thẳng phòng chờ; việc nối máy chủ do
/// view model tự làm lúc người chơi bấm tạo / vào phòng.
/// </summary>
public partial class MatchWindow : Window
{
    private readonly MatchViewModel _vm;

    /// <summary>Đang rời phòng dở thì đừng chạy lại lần nữa khi cửa sổ đóng thật.</summary>
    private bool _leaving;

    private readonly Account _account;
    private readonly AppSettings _settings;

    /// <summary>Đang quay về màn chế độ, nên lúc đóng KHÔNG được để app tắt.</summary>
    private bool _goingBack;

    public MatchWindow(Account account, AppSettings settings, LobbyMode mode)
    {
        InitializeComponent();

        _account = account;
        _settings = settings;

        _vm = new MatchViewModel(account, settings, mode);
        _vm.GoBack += GoBack;
        DataContext = _vm;

        // Vào ván thì kéo focus về cửa sổ: nếu con trỏ còn nằm trong ô "số câu"
        // hay ô mật khẩu của sảnh chờ (vừa bị ẩn đi), chữ gõ sẽ rơi vào ô đó
        // chứ không vào hàng đáp án
        _vm.PropertyChanged += (_, e) =>
        {
            // Cũng kéo về khi hộp kết quả / sảnh chờ đóng: nút vừa bấm trong
            // đó bị ẩn nhưng vẫn giữ focus, chữ gõ sẽ rơi vào nó
            if (e.PropertyName is nameof(MatchViewModel.IsPlaying)
                                or nameof(MatchViewModel.IsMatchOver)
                                or nameof(MatchViewModel.IsInLobby))
                Dispatcher.BeginInvoke(() => Keyboard.Focus(this));
        };
    }

    /// <summary>
    /// Về màn chọn chế độ: mở cửa sổ kia trước rồi mới đóng cửa sổ này, để app
    /// không tự thoát. Việc rời phòng vẫn do Window_Closing lo.
    /// </summary>
    private void GoBack()
    {
        if (_goingBack) return;
        _goingBack = true;

        var mode = new ModeWindow(_account, _settings);
        Application.Current.MainWindow = mode;
        mode.Show();
        Close();
    }

    /// <summary>
    /// Bàn phím vật lý trong ván: chữ cái điền vào ô, Backspace lấy ra, Enter
    /// gửi. Đấu là đua tốc độ nên gõ nhanh hơn hẳn bấm phím trên màn hình.
    ///
    /// Đang gõ trong một ô nhập (mã phòng, mật khẩu, số câu) thì không bắt —
    /// chữ đó là của ô nhập, không phải của ván.
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { GoBack(); return; }   // ESC = rời phòng, về màn chế độ

        if (!_vm.IsPlaying) return;
        if (Keyboard.FocusedElement is TextBox or PasswordBox) return;

        Key key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;   // bộ gõ tiếng Việt đang bật

        if (key is >= Key.A and <= Key.Z)
        {
            _vm.TypeLetter((char)('A' + (key - Key.A)));
            e.Handled = true;
        }
        else if (key == Key.Back)
        {
            _vm.EraseLast();
            e.Handled = true;
        }
        else if (key == Key.Enter)
        {
            _vm.Submit();
            e.Handled = true;
        }
    }

    /// <summary>
    /// "Rời phòng": về màn chọn chế độ, KHÔNG đóng thẳng. Cửa sổ này là cửa sổ
    /// duy nhất đang mở, đóng thẳng là app tắt luôn — QA 2026-09-15 bấm Rời
    /// phòng ở hộp kết quả và mất cả app.
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e) => GoBack();

    /// <summary>
    /// PasswordBox không cho ràng buộc dữ liệu vào Password, nên đẩy tay sang
    /// view model giống màn đăng nhập.
    /// </summary>
    private void RoomPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.RoomPassword = ((PasswordBox)sender).Password;

    /// <summary>
    /// Rời phòng là việc phải chờ máy chủ, mà Closing thì không chờ được. Nên
    /// hoãn đóng lại, báo cho máy chủ xong mới đóng thật.
    /// </summary>
    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_leaving) return;

        e.Cancel = true;
        _leaving = true;

        await _vm.LeaveAsync();
        Close();
    }
}
