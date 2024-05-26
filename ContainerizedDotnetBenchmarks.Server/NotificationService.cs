using System.Runtime.InteropServices;
using DesktopNotifications;
using DesktopNotifications.Apple;
using DesktopNotifications.FreeDesktop;
using DesktopNotifications.Windows;

namespace ContainerizedDotnetBenchmarks.Server;

public class NotificationService : INotificationService
{
    /// <inheritdoc />
    public INotificationManager? NotificationManager { get; }

    public NotificationService()
    {
        NotificationManager = CreateNotificationManager();
        NotificationManager?.Initialize();
    }
    
    /// <inheritdoc />
    public bool IsPlatformSupported() => NotificationManager is not null;

    /// <inheritdoc />
    public async Task<bool> ShowNotification(string title, string message)
    {
        if (!IsPlatformSupported()) return false;
        try
        {
            await NotificationManager.ShowNotification(new Notification
            {
                Title = title,
                Body = message
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ShowNotification(Notification notification)
    {
        if (!IsPlatformSupported()) return false;
        try
        {
            await NotificationManager.ShowNotification(notification);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    private static INotificationManager? CreateNotificationManager()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new FreeDesktopNotificationManager();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsNotificationManager();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new AppleNotificationManager();
        }

        return null;
    }
}