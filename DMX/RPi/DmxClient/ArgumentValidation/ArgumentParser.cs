using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace DmxClient.ArgumentValidation;

internal static class ArgumentParser
{
    internal static bool TryValidateArguments(
        string[] args, ILogger logger, out bool setBrightness, [NotNullWhen(true)] out string? ttyPort, out string? darkPath, out List<string> customArguments,
        [NotNullWhen(true)] out Dictionary<int, string>? playPaths,
        [NotNullWhen(true)] out Dictionary<int, int>? channels)
    {
        ttyPort = null;
        setBrightness = false;
        playPaths = null;
        channels = null;
        darkPath = null;
        
        customArguments = new();
        List<string> paths = new();

        foreach (var arg in args)
        {
            if (string.Equals(arg, "b", StringComparison.CurrentCultureIgnoreCase))
            {
                // Brightness instead of contrast - only one letter, let's treat it specially
                setBrightness = true;
                continue;
            }

            if (arg.Length <= 2)
            {
                logger.LogCritical("An argument is not well defined! Argument: {arg}", arg);
                return false;
            }

            switch (arg[..2])
            {
                case "s:":
                    {
                        ttyPort = arg[2..];
                        if (string.IsNullOrWhiteSpace(ttyPort))
                        {
                            logger.LogCritical("The serial port is not correct!");
                            return false;
                        }

                        break;
                    }

                case "c:":
                    {
                        var channelText = arg[2..];
                        if (string.IsNullOrWhiteSpace(channelText) || !int.TryParse(channelText, out int channel) || channel <= 0)
                        {
                            logger.LogCritical("A channel definition is not correct! Channel definition: {channel}", channelText);
                            return false;
                        }

                        (channels ??= new()).Add(channel, 0);
                        break;
                    }

                case "p:":
                case "f:":
                    {
                        var pathText = arg[2..];
                        if (string.IsNullOrWhiteSpace(pathText) || pathText.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                        {
                            logger.LogCritical("A path definition is not correct! Path definition: {path}", pathText);
                            return false;
                        }
                        if (!File.Exists(pathText))
                        {
                            logger.LogCritical("File '{file}' does not exist!", pathText);
                            return false;
                        }

                        paths.Add(pathText);
                        break;
                    }

                case "h:":
                    {
                        var pathText = arg[2..];
                        if (string.IsNullOrWhiteSpace(pathText) || pathText.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                        {
                            logger.LogCritical("A path definition is not correct! Path definition: {path}", pathText);
                            return false;
                        }
                        if (!File.Exists(pathText))
                        {
                            logger.LogCritical("File '{file}' does not exist!", pathText);
                            return false;
                        }

                        darkPath = pathText;
                        break;
                    }

                default:
                    {
                        customArguments.Add(arg);
                        break;
                    }
            }
        }

        if (string.IsNullOrWhiteSpace(ttyPort))
        {
            logger.LogCritical("The serial port is not defined!");

            logger.LogInformation("The following ports are open:");
            foreach (var portName in System.IO.Ports.SerialPort.GetPortNames())
            {
                logger.LogInformation(" - {portName}", portName);
            }
            return false;
        }

        if (channels is null || channels.Count == 0)
        {
            logger.LogCritical("No channels defined!");
            return false;
        }

        if (paths.Count is < 1 or > 10)
        {
            logger.LogCritical("At least one and at most 10 files are supported to play at a time!");
            return false;
        }

        playPaths = new(paths.Count);
        for (int i = 0; i < paths.Count; i++)
        {
            playPaths.Add(channels.ElementAt(i).Key, paths[i]);
        }

        logger.LogInformation("Parsed arguments correctly.");
        return true;
    }
}