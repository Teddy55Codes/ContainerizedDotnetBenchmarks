using DesktopNotifications;

namespace ContainerizedDotnetBenchmarks.Server;

public interface INotificationService
{
    /// <summary>
    /// The underlying <see cref="INotificationManager"/>.
    /// </summary>
    public INotificationManager? NotificationManager { get; }
    
    /// <summary>
    /// Check if the current platform is supported.
    /// If you call <see cref="INotificationService.ShowNotification(string, string)"/> or <see cref="INotificationService.ShowNotification(Notification)"/> this is check automatically.
    /// </summary>
    /// <returns>Whether or not the current platform is supported.</returns>
    public bool IsPlatformSupported();
    
    /// <summary>
    /// Show a simple notification. Calls the <see cref="INotificationManager.ShowNotification"/> method and catches any exception.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message that is displayed in the notification.</param>
    /// <returns>True when no exception was thrown. False if an exception was thrown</returns>
    public Task<bool> ShowNotification(string title, string message);
    
    /// <summary>
    /// Show a notification for a given <see cref="Notification"/>. Calls the <see cref="INotificationManager.ShowNotification"/> method and catches any exception.
    /// </summary>
    /// <param name="notification">The <see cref="Notification"/> used in the <see cref="INotificationManager.ShowNotification"/> call.</param>
    /// <returns>True when no exception was thrown. False if an exception was thrown</returns>
    public Task<bool> ShowNotification(Notification notification);
}