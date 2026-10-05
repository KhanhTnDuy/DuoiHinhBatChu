using System.ComponentModel;
using System.Runtime.CompilerServices;
using DuoiHinhBatChu.Services;

namespace DuoiHinhBatChu.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Màn chơi nhận chữ gõ từ bàn phím vật lý.
///
/// Màn Cổ điển và màn Đấu làm y như nhau nên cùng khai đúng ba việc này, để
/// <see cref="Services.LetterKeys"/> lo phần phím cho cả hai.
/// </summary>
public interface ILetterTyping
{
    /// <summary>Gõ một chữ cái vào ô trống kế tiếp.</summary>
    void TypeLetter(char c);

    /// <summary>Backspace: lấy chữ ở ô cuối cùng đã điền ra.</summary>
    void EraseLast();

    /// <summary>Enter: nộp đáp án nếu đã điền kín.</summary>
    void Submit();
}

/// <summary>
/// Màn hình nào cũng có nút đổi sáng/tối, và việc phải làm y như nhau: lật cờ
/// trong tùy chọn, lưu lại, rồi áp bộ màu mới. Để ở đây một lần cho cả năm màn
/// thay vì chép đi chép lại ở từng view model.
/// </summary>
public abstract class ThemedViewModel : ViewModelBase
{
    /// <summary>Tùy chọn chung của app; mọi màn đều cần tới nên để sẵn ở đây.</summary>
    protected readonly AppSettings Settings;

    protected ThemedViewModel(AppSettings settings)
    {
        Settings = settings;
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public RelayCommand ToggleThemeCommand { get; }

    /// <summary>true = đang ở chế độ tối.</summary>
    public bool IsDarkTheme => Settings.IsDarkTheme;

    private void ToggleTheme()
    {
        Settings.IsDarkTheme = !Settings.IsDarkTheme;
        Settings.Save();
        ThemeService.Apply(Settings.IsDarkTheme);

        OnPropertyChanged(nameof(IsDarkTheme));
    }
}
