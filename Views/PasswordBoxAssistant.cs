using System.Windows;
using System.Windows.Controls;

namespace MultiSmbServer.Views;

public static class PasswordBoxAssistant
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxAssistant),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    private static bool _updating;

    public static string GetBoundPassword(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(BoundPasswordProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox box)
            return;

        box.PasswordChanged -= OnPasswordChanged;

        if (!_updating)
            box.Password = (string)e.NewValue;

        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box)
            return;

        _updating = true;
        SetBoundPassword(box, box.Password);
        _updating = false;
    }
}
