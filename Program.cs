using System;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Net;

namespace osl_bridge;

public class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    private static readonly string[] ValidLaunchOptions = { "launch-credentials", "launch" };

    private static bool PreSetup()
    {
        try
        {
            var key = Registry.ClassesRoot.CreateSubKey("osl");

            key.SetValue("", "OSL Protocol");
            key.SetValue("URL Protocol", "");

            var commandKey = key.CreateSubKey("shell\\open\\command");
            commandKey.SetValue("", $"\"{Process.GetCurrentProcess().MainModule.FileName}\" \"%1\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occured while setup: " + ex.Message);
            return false;
        }

        return true;
    }

    private static string GetOsuPath()
    {
        var key = Registry.ClassesRoot.OpenSubKey("osustable.Uri.osu\\shell\\open\\command");
        key ??= Registry.ClassesRoot.OpenSubKey("osu\\shell\\open\\command");

        if (key == null)
        {
            Console.WriteLine("osu! was not found.");
            return null;
        }

        return key.GetValue("").ToString().Replace("\"%1\"", "").Replace("\"", "").Trim();
    }

    private static bool IsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            AllocConsole();

            if (!IsAdmin())
                Console.WriteLine("OSL needs to be run as administrator to run setup.");
            else
            {
                Console.WriteLine("Welcome to the OSL setup wizard.");
                Console.WriteLine("This will setup OSL to be used in your browser.");
                Console.WriteLine("Please make sure you have OSL in a place you won't move it from.");
                Console.WriteLine("If you do move it, you will need to run this setup again.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();

                if (PreSetup())
                {
                    Console.WriteLine("OSL has been setup successfully!");
                    Console.WriteLine("You can now use OSL in your browser.");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        var url = args[0];
        var split = url.Split('/');
        var action = split[2];

        if (!ValidLaunchOptions.Contains(action))
            return;

        var osuPath = GetOsuPath();

        if (osuPath == null)
        {
            AllocConsole();
            Console.WriteLine("osu! was not found.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        var serverUrl = string.Empty;
        if (action == "launch-credentials")
        {
            serverUrl = split[4];
            
            var credentials = split[3].Split(':');
            var osuConfigPath = osuPath.Replace("osu!.exe", $"osu!.{Environment.UserName}.cfg");

            if (!File.Exists(osuConfigPath))
            {
                AllocConsole();
                Console.WriteLine("Your osu! config file was not found.");
                Console.WriteLine("Please open your game at least once before using the credentials feature.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var lines = File.ReadAllLines(osuConfigPath);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Username ="))
                    lines[i] = $"Username = {WebUtility.UrlDecode(credentials[0])}";
                else if (lines[i].StartsWith("Password ="))
                    lines[i] = $"Password = {WebUtility.UrlDecode(credentials[1])}";
                else if (lines[i].StartsWith("CredentialEndpoint ="))
                    lines[i] = $"CredentialEndpoint = {(serverUrl != "ppy.sh" ? serverUrl : "")}";
                else if (lines[i].StartsWith("SaveUsername ="))
                    lines[i] = "SaveUsername = 1";
                else if (lines[i].StartsWith("SavePassword ="))
                    lines[i] = "SavePassword = 1";
            }

            File.WriteAllLines(osuConfigPath, lines);
        }

        if (string.IsNullOrEmpty(serverUrl)) serverUrl = split[3];
        var psi = new ProcessStartInfo
        {
            FileName = osuPath,
            Arguments = serverUrl != "ppy.sh" ? $"-devserver {serverUrl}" : "",
            UseShellExecute = false
        };
            
        Process.Start(psi);
    }
}
