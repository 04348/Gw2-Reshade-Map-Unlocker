using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2GraphicalOverall
{
    class Program
    {
        public const short      PollIntervalMs = 100; // Prevent ReShade from disregarding changes by sleeping this long after each change
        public const short      WriteDelayMs = 5000; // Proclaim inactivity after this period without Mumble Link updates
        const int               ActivityTimeoutMs = 5 * 60 * 1000;
        static string           fileName = "gw2map.h";
        static string           launch = ".\\Gw2-64.exe";
        static string           launchArgs = null;
        static bool             hide = true;
        static Process          game = null;

        static string oldContents = "";


        static void Main(string[] args)
        {
            if (!ParseArgs(args))
            {
                PrintUsage();
                return;
            }
            if (launch != null)
            {
                try
                {
                    Console.WriteLine("Launching \"{0}\" {1}", launch, launchArgs);
                    game = Process.Start(launch, launchArgs);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine("\nLaunch failed. Press enter to exit.");
                    Console.ReadLine();
                    return;
                }
            }

            Console.WriteLine("Maintaining {0} with map data from Guild Wars 2 "
                              + "using the Mumble Link API", fileName);
            if (hide) ShowWindow(GetConsoleWindow(), ShowWindowCommands.Hide);

            Unlocker.Execute();

            using (var ml = MumbleLink.Open())
            {
                try
                {
                    MainLoop(fileName, ml);
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (hide) ShowWindow(GetConsoleWindow(), ShowWindowCommands.Normal);
                    Console.WriteLine(ex.Message);
                    if (!isAdministrator())
                    {
                        try
                        {
                            elevate(args);
                            return;
                        }
                        catch (Exception) { }
                    }
                    Console.WriteLine("\nPermission was denied. Press enter to exit.");
                    Console.ReadLine();
                    return;
                }
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Command-line usage:");
            Console.Error.WriteLine("  {0} [/hide] [/launch [program]] [/launchargs {args}]"
                + " [file]", System.AppDomain.CurrentDomain.FriendlyName);
            Console.Error.WriteLine("    Maintain {file} with map data from Guild Wars 2."
                + "{file} defaults to \"gw2map.h\".");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("  /hide:");
            Console.Error.WriteLine("    Hide console window");
            Console.Error.WriteLine("  /launch [program]:");
            Console.Error.WriteLine("    Start {program}, and run until it quits."
                + " {program} defaults to \"..\\Gw2.exe\".");
            Console.Error.WriteLine("  /launchargs {args}:");
            Console.Error.WriteLine("    When launching a program with /launch,"
                + " pass it {args} as arguments.");
        }

        private static bool ParseArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("/") || args[i].StartsWith("-"))
                {
                    string option = args[i].TrimStart('/', '-');
                    switch (option)
                    {
                        case "launch":
                            if (i != args.Length - 1) launch = args[++i];
                            break;
                        case "launchargs":
                            if (i == args.Length - 1)
                            {
                                Console.Error.WriteLine("Option launchargs needs an argument");
                                return false;
                            }
                            else {
                                launchArgs = args[++i];
                            }
                            break;
                        case "show":
                            hide = false;
                            break;
                        default:
                            Console.Error.WriteLine("Unknown option {0}", args[i]);
                            return false;
                    }
                }
                else {
                    if (i != args.Length - 1)
                    {
                        Console.Error.WriteLine("Too many files given: {0}",
                            String.Join(" ", args, i, args.Length - i));
                        return false;
                    }
                    fileName = args[i];
                }
            }
            return true;
        }

        enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        private static void MainLoop(string fileName, MumbleLink ml)
        {
            while (!ShouldExit())
            {
                MumbleLink.LinkedMem state;
                MumbleLink.GW2Context context;
                ml.Read(out state, out context);

                string newContents = genContents(state, context);
                if (newContents != oldContents)
                {
                    File.WriteAllText(fileName, newContents);

                    DayNightCycle.TimeOfDay tod = DayNightCycle.Classify();
                    Console.WriteLine("{0}: Updated file: GW2MapId = {1}, "
                                + "GW2TOD = {2}, GW2Active = {3}.",
                        DateTime.Now,
                        context.mapId, (int)tod, active ? 1 : 0);

                    oldContents = newContents;
                    Thread.Sleep(WriteDelayMs);
                }
                Thread.Sleep(PollIntervalMs);
            }
        }

        private static bool ShouldExit()
        {
            if (game != null)
            {
                if (game.HasExited) return true;
            }
            return false;
        }

        const int ActivityTimeoutTicks =
            (ActivityTimeoutMs + PollIntervalMs - 1) / PollIntervalMs;
        static UInt32 lastUiTickValue = 0;
        static int lastChangedTick = -ActivityTimeoutTicks; // Have it inactive from startup
        static int currentTick = 0;

        static bool active;

        private static string genContents(
            MumbleLink.LinkedMem state, MumbleLink.GW2Context context)
        {
            currentTick++;
            if (lastUiTickValue != state.uiTick) lastChangedTick = currentTick;
            lastUiTickValue = state.uiTick;
            active = currentTick - lastChangedTick < ActivityTimeoutTicks;

            return String.Format("#define GW2MapId {0}\n"
                                + "#define GW2TOD {1}\n"
                                + "#define GW2Active {2}\n"
                                + "#define TimeZone {3}\n",
                context.mapId,
                (int)DayNightCycle.Classify(),
                active ? 1 : 0,
                (int)TimeZone.CurrentTimeZone.GetUtcOffset(
                    DateTime.Now).TotalSeconds);
        }

        static void elevate(string[] args)
        {
            // Restart program and run as admin
            string exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName, String.Join(" ", args));
            startInfo.Verb = "runas";
            Process.Start(startInfo);
        }

        static bool isAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
