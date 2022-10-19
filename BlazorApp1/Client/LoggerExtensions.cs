using Microsoft.Extensions.Logging;

namespace BlazorApp1.Client;

public static class LoggerExtensions
{
    // Extension method to create a LoggerProvider from
    // an existing ILogger
    public static ILoggerProvider AsLoggerProvider(this ILogger logger)
    {
        return new ExistingLoggerProvider(logger);
    }

    private class ExistingLoggerProvider : ILoggerProvider
    {
        public ExistingLoggerProvider(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        public void Dispose()
        {
            return;
        }

        private readonly ILogger _logger;
    }
}
