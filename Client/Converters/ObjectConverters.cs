using Avalonia.Data.Converters;

namespace Client.Converters;

/// <summary>
/// Static converters for common object operations in Avalonia XAML bindings
/// </summary>
public static class ObjectConverters
{
    /// <summary>
    /// Converter that checks if a boolean value is true
    /// ConverterParameter format: "TrueValue|FalseValue"
    /// </summary>
    public static readonly IValueConverter IsTrue = new FuncValueConverter<bool, string>((value) =>
    {
        return value ? "✅" : "❌";
    });

    /// <summary>
    /// Converter that checks if an object is null
    /// ConverterParameter format: "NullValue|NotNullValue"
    /// </summary>
    public static readonly IValueConverter IsNull = new FuncValueConverter<object?, string>((value) =>
    {
        return value == null ? "Add User" : "Edit User";
    });

    /// <summary>
    /// Converter that checks if an object is not null (returns boolean for IsVisible binding)
    /// </summary>
    public static readonly IValueConverter IsNotNull = new FuncValueConverter<object?, bool>((value) =>
    {
        return value != null;
    });

    /// <summary>
    /// Converter that converts IsEnabled boolean to toggle icon
    /// </summary>
    public static readonly IValueConverter EnabledToIcon = new FuncValueConverter<bool, string>((value) =>
    {
        return value ? "✓" : "✗";
    });
}
