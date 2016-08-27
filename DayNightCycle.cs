using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gw2GraphicalOverall
{
    class DayNightCycle
    {
        /* From https://wiki.guildwars2.com/wiki/Day_and_night */
        const double origin = 25 * 60;  /* Cycle starts at 00:25 UTC */
        const double duration = 120 * 60; /* and lasts for two hours */

        public static double Time()
        {
            return ((DateTime.UtcNow.TimeOfDay.TotalSeconds - origin + duration)
                    / duration) % 1;
        }

        public static TimeOfDay Classify()
        {
            double time = DayNightCycle.Time();
            if (time < 5 / 120.0) return TimeOfDay.Dawn;
            if (time < 75 / 120.0) return TimeOfDay.Day;
            if (time < 80 / 120.0) return TimeOfDay.Dusk;
            return TimeOfDay.Night;
        }

        public enum TimeOfDay
        {
            Dawn,
            Day,
            Dusk,
            Night,
        }
    }
}
