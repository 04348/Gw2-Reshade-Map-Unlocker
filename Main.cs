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

namespace Gw2GraphicalOverall {
    class Program {
        public const short PollIntervalMs = 100; // Prevent ReShade from disregarding changes by sleeping this long after each change
        public const short WriteDelayMs = 5000; // Proclaim inactivity after this period without Mumble Link updates
        const int ActivityTimeoutMs = 5 * 60 * 1000;
        static string fileName = "gw2map.h";
        static string launch = ".\\Gw2-64.exe";
        static string logFile = ".\\Gw2GO.log";
        static string launchArgs = null;
        static Process game = null;

        static string oldContents = "";

        static void Main(string[] args) {
            ShowWindow(GetConsoleWindow(), ShowWindowCommands.Hide);
            System.IO.File.WriteAllText(logFile, "GuildWars 2 : Graphical Overhaul\n");

            if (!ParseArgs(args)) {
                PrintUsage();
                return;
            }
            if (launch != null) {
                try {
                    Program.log("Launching Gw2");
                    game = Process.Start(launch, launchArgs);
                } catch (Exception ex) {
                    Program.log(ex.Message);
                    Program.log("Launch failed");
                }
            }

            Unlocker.Execute();

            using (var ml = MumbleLink.Open()) {
                try {
                    MainLoop(fileName, ml);
                } catch (UnauthorizedAccessException ex) {
                    Program.log(string.Format((ex.Message)));
                    if (!isAdministrator()) {
                        try {
                            elevate(args);
                            return;
                        } catch (Exception) { }
                    }
                    Program.log("Permission was denied");
                    return;
                }
            }
        }

        public static void log(String str) {
            File.AppendAllText(logFile, DateTime.Now.ToString() + " | " + str + Environment.NewLine);
        }

        private static void PrintUsage() {
            Program.log(string.Format("Command-line usage:"));
            Program.log(string.Format("  {0} [/hide] [/launch [program]] [/launchargs {args}]"
                + " [file]", System.AppDomain.CurrentDomain.FriendlyName));
            Program.log(string.Format("    Maintain {file} with map data from Guild Wars 2."
                + "{file} defaults to \"gw2map.h\"."));
            Program.log(string.Format(""));
            Program.log(string.Format("  /hide:"));
            Program.log(string.Format("    Hide console window"));
            Program.log(string.Format("  /launch [program]:"));
            Program.log(string.Format("    Start {program}, and run until it quits."
                + " {program} defaults to \"..\\Gw2.exe\"."));
            Program.log(string.Format("  /launchargs {args}:"));
            Program.log(string.Format("    When launching a program with /launch,"
                + " pass it {args} as arguments."));
        }

        private static bool ParseArgs(string[] args) {
            for (int i = 0; i < args.Length; i++) {
                if (args[i].StartsWith("/") || args[i].StartsWith("-")) {
                    string option = args[i].TrimStart('/', '-');
                    switch (option) {
                        case "launch":
                            if (i != args.Length - 1) launch = args[++i];
                            break;
                        case "launchargs":
                            if (i == args.Length - 1) {
                                Program.log("Option launchargs needs an argument");
                                return false;
                            } else {
                                launchArgs = args[++i];
                            }
                            break;
                        default:
                            Program.log(string.Format("Unknown option {0}", args[i]));
                            return false;
                    }
                } else {
                    if (i != args.Length - 1) {
                        Program.log(string.Format("Too many files given: {0}",
                            String.Join(" ", args, i, args.Length - i)));
                        return false;
                    }
                    fileName = args[i];
                }
            }
            return true;
        }

        enum ShowWindowCommands : int {
            Hide = 0,
            Normal = 1,
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        private static void MainLoop(string fileName, MumbleLink ml) {
            while (!ShouldExit()) {
                MumbleLink.LinkedMem state;
                MumbleLink.GW2Context context;
                ml.Read(out state, out context);

                string newContents = genContents(state, context);
                if (newContents != oldContents) {
                    File.WriteAllText(fileName, newContents);

                    DayNightCycle.TimeOfDay tod = DayNightCycle.Classify();

                    oldContents = newContents;
                    Thread.Sleep(WriteDelayMs);
                }
                Thread.Sleep(PollIntervalMs);
            }
        }

        private static bool ShouldExit() {
            if (game != null) {
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
            MumbleLink.LinkedMem state, MumbleLink.GW2Context context) {
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

        static void elevate(string[] args) {
            // Restart program and run as admin
            string exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName, String.Join(" ", args));
            startInfo.Verb = "runas";
            Process.Start(startInfo);
        }

        static bool isAdministrator() {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
