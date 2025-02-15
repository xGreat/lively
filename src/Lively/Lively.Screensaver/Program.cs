﻿using Lively.Common.Com;
using Lively.Common.Helpers;
using Lively.Common.Helpers.Pinvoke;
using Lively.Grpc.Client;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using static Lively.Common.Constants;

namespace Lively.Screensaver
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Not possible to display screensaver in restricted lockscreen region since this utility and screensaver(s) are different applications.
            if (IsSystemLocked())
                return;

            var (option, hwnd) = ParseScreensaverArgs(args);
            if (SingleInstanceUtil.IsAppMutexRunning(SingleInstance.UniqueAppName))
            {
                // Application is running
                var commandsClient = new CommandsClient();
                switch (option)
                {
                    case ScreensaverOptions.show:
                        await commandsClient.ScreensaverShow(true);
                        break;
                    case ScreensaverOptions.preview:
                        await commandsClient.ScreensaverPreview(hwnd);
                        break;
                    case ScreensaverOptions.configure:
                        await commandsClient.ScreensaverConfigure();
                        break;
                    case ScreensaverOptions.undefined:
                        // Incorrect argument, ignore.
                        break;
                }
            }
            else if (option == ScreensaverOptions.show)
            {
                // Application is not running, launch in screensaver only mode if show requested.
                var startArgs = "screensaver --showExclusive true";
                var installerGuid = "{E3E43E1B-DEC8-44BF-84A6-243DBA3F2CB1}";
                var packageFamilyName = "12030rocksdanister.LivelyWallpaper_97hta09mmv6hy";
                var appUserModelId = $"{packageFamilyName}!App";

                if (TryGetInnoInstalledAppPath(installerGuid, out string installedPath))
                {
                    Process.Start(Path.Combine(installedPath, "Lively.exe"), startArgs);
                }
                else if (IsStoreAppInstalled(packageFamilyName))
                {
                    try
                    {
                        _ = new ApplicationActivationManager().ActivateApplication(appUserModelId, startArgs, ActivateOptions.None, out _);
                    }
                    catch { /* Ignore */ }
                }
                else
                {
                    // Application not installed, ignore.
                }
            }
        }

        // Ref: https://sites.harding.edu/fmccown/screensaver/screensaver.html
        // CC BY-SA 2.0
        private static (ScreensaverOptions, int) ParseScreensaverArgs(string[] args)
        {
            if (args.Length > 0)
            {
                string firstArgument = args[0].ToLower().Trim();
                string secondArgument = null;

                // Handle cases where arguments are separated by colon.
                // Examples: /c:1234567 or /P:1234567
                // ref: https://sites.harding.edu/fmccown/screensaver/screensaver.html
                if (firstArgument.Length > 2)
                {
                    secondArgument = firstArgument.Substring(3).Trim();
                    firstArgument = firstArgument.Substring(0, 2);
                }
                else if (args.Length > 1)
                    secondArgument = args[1];

                if (firstArgument == "/c")  // Configuration mode
                {
                    return (ScreensaverOptions.configure, 0);
                }
                else if (firstArgument == "/p") // Preview mode
                {
                    return (ScreensaverOptions.preview, int.Parse(secondArgument));
                }
                else if (firstArgument == "/s") // Full-screen mode
                {
                    return (ScreensaverOptions.show, 0);
                }
                else 
                {
                    // Undefined argument
                    return (ScreensaverOptions.undefined, 0);
                }
            }
            else  // No arguments - treat like /c
            {
                return (ScreensaverOptions.configure, 0);
            }
        }

        // The path is stored to registry to HKLM (administrative install mode) or HKCU (non administrative install mode) to a subkey named after the AppId with _is1 suffix,
        // stored under a key SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall (as you alraedy know). The value name is Inno Setup: App Path.
        // The path is also stored to InstallLocation with additional trailing slash, as that's where Windows reads it from. But Inno Setup reads the first value.
        // Ref: https://stackoverflow.com/questions/68990713/how-to-access-the-path-of-inno-setup-installed-program-from-outside-of-inno-setu
        private static bool TryGetInnoInstalledAppPath(string appId, out string installPath)
        {
            var appPathValueName = "Inno Setup: App Path";
            var registryPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{appId}}}_is1";

            installPath = GetRegistryValue(RegistryHive.CurrentUser, registryPath, appPathValueName) ??
                GetRegistryValue(RegistryHive.LocalMachine, registryPath, appPathValueName);

            return installPath != null;
        }

        private static bool IsStoreAppInstalled(string packageFamilyName)
        {
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser(string.Empty, packageFamilyName);
            return packages.Any();
        }

        private static bool IsSystemLocked()
        {
            bool result = false;
            var fHandle = NativeMethods.GetForegroundWindow();
            try
            {
                NativeMethods.GetWindowThreadProcessId(fHandle, out int processID);
                using Process fProcess = Process.GetProcessById(processID);
                result = fProcess.ProcessName.Equals("LockApp", StringComparison.OrdinalIgnoreCase);
            }
            catch { /* Ignore */ }
            return result;
        }

        private static string GetRegistryValue(RegistryHive hive, string registryPath, string valueName)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var subKey = baseKey.OpenSubKey(registryPath);

            return subKey?.GetValue(valueName) as string;
        }

        private enum ScreensaverOptions
        {
            show,
            preview,
            configure,
            undefined
        }
    }
}