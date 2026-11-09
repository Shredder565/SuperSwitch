namespace SuperSwitch.Common.Logging.Formatters
{
    interface ILogFormatter
    {
        string Format(LogEventArgs args);
    }
}
