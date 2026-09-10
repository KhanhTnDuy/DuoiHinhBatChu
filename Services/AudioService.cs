using System;
using System.Media;
using System.Threading.Tasks;

namespace DuoiHinhBatChu.Services;

/// <summary>
/// Quản lý âm thanh hiệu ứng (SFX) trong game.
/// </summary>
public class AudioService
{
    public static AudioService Instance { get; } = new();

    public bool IsEnabled { get; set; } = true;

    private AudioService() { }

    public void PlayClick()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try { SystemSounds.Beep.Play(); } catch { }
        });
    }

    public void PlayWrong()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Âm thanh báo sai trầm giảm dần
                Console.Beep(330, 150); // E4
                Console.Beep(260, 250); // C4
            }
            catch
            {
                SystemSounds.Hand.Play();
            }
        });
    }

    public void PlayVictory()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                Console.Beep(523, 100); // C5
                Console.Beep(659, 100); // E5
                Console.Beep(784, 120); // G5
                Console.Beep(1046, 350); // C6
            }
            catch
            {
                SystemSounds.Exclamation.Play();
            }
        });
    }

    public void PlayHint()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                Console.Beep(880, 100);
                Console.Beep(1175, 180);
            }
            catch
            {
                SystemSounds.Question.Play();
            }
        });
    }
}
