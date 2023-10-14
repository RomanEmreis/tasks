public sealed class BusMessageWriter
{
    private readonly IBusConnection _connection;
    private readonly BusMessageWriterSettings _settings;
  
    private readonly MemoryStream _buffer = new();
    private readonly SemaphoreSlim _bufferLock = new(1);
    
    public BusMessageWriter(IBusConnection connection, IOptions<BusMessageWriterSettings> options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task SendMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        await _bufferLock.WaitAsync(cancellationToken); // Acquire the lock.
  
        try
        {
            await _buffer.WriteAsync(message, 0, message.Length, cancellationToken);
    
            if (IsThresholdReached())
            {
                await PublishBufferedMessagesAsync(cancellationToken);
            }
        }
        finally
        {
            _bufferLock.Release(); // Release the lock.
        }
    }

    private bool IsThresholdReached() => _buffer.Length > _settings.BufferThreshold;

    private async Task PublishBufferedMessagesAsync(CancellationToken cancellationToken = default)
    {
        var messages = _buffer.ToArray();
        await _connection.PublishAsync(messages, cancellationToken);

        ResetBuffer();
    }

    //NOTE: this could be an extension method for MemoryStream
    private void ResetBuffer()
    {
        var buffer = _buffer.GetBuffer();
        Array.Clear(buffer, 0, buffer.Length);
        _buffer.Position = 0;
        _buffer.SetLength(0); 
    }
}

public record BusMessageWriterSettings(int BufferThreshold = 1000);
