using System.IO;
using System.Text;

namespace Client.Services;

/// <summary>
/// TextWriter that mirrors console output into the UI log callback while keeping the original console output.
/// </summary>
public sealed class ConsoleRedirectWriter : TextWriter
{
    private readonly TextWriter _innerWriter;
    private Action<string> _forwardCallback;
    private readonly StringBuilder _buffer = new();

    public ConsoleRedirectWriter(TextWriter innerWriter, Action<string> forwardCallback)
    {
        _innerWriter = innerWriter;
        _forwardCallback = forwardCallback;
    }

    public override Encoding Encoding => _innerWriter.Encoding;

    public void UpdateCallback(Action<string> forwardCallback)
    {
        _forwardCallback = forwardCallback;
    }

    public override void Write(char value)
    {
        _innerWriter.Write(value);
        AppendChar(value);
    }

    public override void Write(string? value)
    {
        _innerWriter.Write(value);
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (var ch in value)
        {
            AppendChar(ch);
        }
    }

    public override void WriteLine(string? value)
    {
        _innerWriter.WriteLine(value);
        if (!string.IsNullOrEmpty(value))
        {
            _buffer.Append(value);
        }
        FlushBuffer();
    }

    public override void WriteLine()
    {
        _innerWriter.WriteLine();
        FlushBuffer();
    }

    private void AppendChar(char value)
    {
        if (value == '\r')
        {
            return;
        }

        if (value == '\n')
        {
            FlushBuffer();
            return;
        }

        _buffer.Append(value);
    }

    private void FlushBuffer()
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        var line = _buffer.ToString();
        _buffer.Clear();
        _forwardCallback(line);
    }
}
