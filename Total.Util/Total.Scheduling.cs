using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Total.Util
{
    public class EventSchedule
    {
        private SortedList runList;
        public SortedList Schedule;
        public struct nextRun
        {
            public string   Schedule { get; set; }
            public DateTime AbsTime  { get; set; }
            public TimeSpan RelTime  { get; set; }
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Overloads for the public methods 
        // ------------------------------------------------------------------------------------------------------------------------
        public Boolean AddSchedule(string Name, string Days, string Start) { return this.add_Schedule(Name, Days, Start, "", "", ".*", true); }
        public Boolean AddSchedule(string Name, string Days, string Start, string End = "") { return this.add_Schedule(Name, Days, Start, End, "", ".*", true); }
        public Boolean AddSchedule(string Name, string Days, string Start, string End = "", string Interval = "") { return this.add_Schedule(Name, Days, Start, End, Interval, ".*", true); }
        public Boolean AddSchedule(string Name, string Days, string Start, string End = "", string Interval = "", string Server = ".*", bool Strict = true) { return this.add_Schedule(Name, Days, Start, End, Interval, Server, Strict); }

        public Boolean ClearSchedules() { return this.clear_Schedules(); }

        public Boolean DeleteSchedule(string Name) { return this.delete_Schedule(Name); }

        public void ShowSchedule()             { this.show_Schedule(""); }
        public void ShowSchedule(string Names) { this.show_Schedule(Names); }

        public void CreateRunList()    { this.new_RunList();  }
        public SortedList GetRunList() { return this.get_RunList(); }
        public nextRun GetNextRun()    { return this.get_NextRun(); }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Private methods used by the overloads
        // ------------------------------------------------------------------------------------------------------------------------
        private Boolean add_Schedule(string Name, string Days, string Start, string End, string Interval, string Server, bool Strict)
        {
            if (this.Schedule == null) { this.Schedule = new SortedList(); }
            string regexTime = "^(?:0?[0-9]|1[0-9]|2[0-3]):[0-5][0-9]$";
            BitArray dayMask = new BitArray(7, false);
            // Matching order is important here! This way we wull be resolved as wednesday and not as weekend or weekdays 
            foreach (string Day in Days.Split(','))
            {
                if (Regex.Match(Day, "^e.*$",      RegexOptions.IgnoreCase).Success) { dayMask.SetAll(true); continue; }
                if ((Regex.Match(Day, "^weekd.*$", RegexOptions.IgnoreCase).Success) ||
                    (Regex.Match(Day, "^wd$",      RegexOptions.IgnoreCase).Success)) { for (int i = 1; i < 6; i++) { dayMask.Set(i, true); } continue; }
                if ((Regex.Match(Day, "^weeke.*$", RegexOptions.IgnoreCase).Success) ||
                    (Regex.Match(Day, "^wn$",      RegexOptions.IgnoreCase).Success)) { dayMask.Set(0, true); ; dayMask.Set(6, true); continue; }
                if (Regex.Match(Day, "^su.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(0, true);  continue; }
                if (Regex.Match(Day, "^mo.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(1, true);  continue; }
                if (Regex.Match(Day, "^tu.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(2, true);  continue; }
                if (Regex.Match(Day, "^we.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(3, true);  continue; }
                if (Regex.Match(Day, "^th.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(4, true);  continue; }
                if (Regex.Match(Day, "^fr.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(5, true);  continue; }
                if (Regex.Match(Day, "^sa.*$",     RegexOptions.IgnoreCase).Success) { dayMask.Set(6, true);  continue; }
            }
            // --------------------------------------------------------------------------------------------------------------------
            // Verify wheter Start is a valid HH:MM time mask
            // --------------------------------------------------------------------------------------------------------------------
            if (!Regex.Match(Start, regexTime, RegexOptions.IgnoreCase).Success) { total.Logger.Warn(Start + " is not a valid hh:mm start time"); return false; }
            string startTime = (DateTime.Parse(Start).ToString("HH:mm"));
            // --------------------------------------------------------------------------------------------------------------------
            //  Check whether we have a strict schedule ar that some delta has to be applied The delta is constructed from the
            //  1st valid macaddress of the system. The delta will vary between 0 and 26 (minutes)
            // --------------------------------------------------------------------------------------------------------------------
            int _delta = 0;
            if (!Strict)
            {
                string _macAddress = total.getMacAddresses()[0];
                total.Logger.Debug("Using mac address " + _macAddress + " as delta seed");
                foreach (string _maValue in _macAddress.Split('-')) { _delta += Int32.Parse(_maValue); }
                _delta /= 60;
                total.Logger.Debug("Schedule " + Name + " will use a delta of " + _delta + " minutes");
            }
            startTime = (DateTime.Parse(Start).AddMinutes(_delta).ToString("HH:mm"));
            // --------------------------------------------------------------------------------------------------------------------
            // If End is specified, an interval is manadtory - and v.v.
            // --------------------------------------------------------------------------------------------------------------------
            string endTime = "--:--";
            if (!string.IsNullOrEmpty(End))
            {
                if (!Regex.Match(End, regexTime, RegexOptions.IgnoreCase).Success) { total.Logger.Warn(End + " is not a valid HH: MM end time"); return false; }
                if (string.IsNullOrEmpty(Interval)) { Interval = "01:00"; total.Logger.Debug("Using default interval (" + Interval + ")"); }
                endTime = (DateTime.Parse(End).ToString("HH:mm"));
            }
            // --------------------------------------------------------------------------------------------------------------------
            // If Interval is specified, End is manadtory - and v.v.
            // --------------------------------------------------------------------------------------------------------------------
            string intTime = "--:--";
            if (!string.IsNullOrEmpty(Interval))
            {
                if (!Regex.Match(Interval, regexTime, RegexOptions.IgnoreCase).Success) { total.Logger.Warn(Interval + " is not a valid HH: MM Interval time"); return false; }
                if (string.IsNullOrEmpty(End)) { endTime = "23:59"; total.Logger.Debug("Using default end time (" + endTime + ")"); }
                intTime = (DateTime.Parse(Interval).ToString("HH:mm"));
            }
            // --------------------------------------------------------------------------------------------------------------------
            //  Lets save what we have and calculate the ruinlist and return
            // --------------------------------------------------------------------------------------------------------------------
            this.Schedule[Name] = new Hashtable() { {"Mask", dayMask}, {"Start", startTime}, {"End", endTime}, {"Interval", intTime}, {"Server", Server}, {"Strict", Strict}, {"Delta", _delta} };
            //this.new_ScheduleSchema();
            return (this.Schedule[Name] != null);
        }

        private Boolean clear_Schedules()
        {
            if (this.Schedule == null) { total.Logger.Debug("Nothing to clear. No event schedules defined"); return true; }
            this.Schedule.Clear();
            this.runList.Clear();
            return ((this.Schedule == null) & (this.runList == null));
        }

        private Boolean delete_Schedule(string Name)
        {
            if (this.Schedule == null) { total.Logger.Debug("Nothing to delete. No event schedules defined"); return true; }
            if (this.Schedule[Name] == null) { total.Logger.Debug("Nothing to delete. No such event schedule " + Name); return true; }
            this.Schedule.Remove(Name);
            return (this.Schedule[Name] == null);
        }

        private void show_Schedule(string Names)
        {
            List<string> nameList = new List<string>();
            if (this.Schedule == null) { total.Logger.Debug("Nothing to show. No event schedules defined"); return; }
            if (string.IsNullOrEmpty(Names)) { nameList = this.Schedule.Keys.Cast<string>().ToList(); }
            else { nameList = Names.Split(',').Cast<string>().ToList(); }
            foreach (string schKey in nameList)
            {
                Hashtable thisSchedule = (Hashtable)this.Schedule[schKey];
                string _startTime = (string)thisSchedule["Start"];
                string _endTime   = (string)thisSchedule["End"];
                string _intTime   = (string)thisSchedule["Interval"];
                string _schServer = (string)thisSchedule["Server"];
                bool   _schStrict = (bool)thisSchedule["Strict"];
                int    _schDelta  = (int)thisSchedule["Delta"];
                BitArray dayMask  = (BitArray)thisSchedule["Mask"];

                if (_schStrict) { Console.WriteLine("\r\nSchedule: " + schKey + "  (Strict)"); }
                else { Console.WriteLine("\r\nSchedule: " + schKey + "  (Delta: " + _schDelta + " min.)"); }
                Console.WriteLine(new String('=', 32));
                Console.WriteLine("  Day    Start     Int.    End");
                Console.WriteLine(String.Format(" {0}", new String('-', 31)));
                for (int i = 0; i < 7; i++)
                {
                    if (!dayMask.Get(i)) { continue; }
                    string schDoW = DateTimeFormatInfo.CurrentInfo.AbbreviatedDayNames[i];
                    Console.WriteLine("  " + schDoW + "    " + _startTime + "    " + _intTime + "   " + _endTime);
                }
                Console.WriteLine();
            }
            return;
        }

        private void new_RunList()
        {
            this.runList = new SortedList();
            foreach (string schKey in this.Schedule.Keys)
            {
                Hashtable thisSchedule = (Hashtable)this.Schedule[schKey];
                string   startTime = (string)thisSchedule["Start"];
                string   endTime   = (string)thisSchedule["End"];
                string   intTime   = (string)thisSchedule["Interval"];
                BitArray dayMask   = (BitArray)thisSchedule["Mask"];
                // Create the new schedule
                for (int i = 0; i < 7; i++)
                {
                    if (!dayMask.Get(i)) { continue; }
                    int dayValue = 1440 * i;
                    int firstRun = (int)TimeSpan.Parse(startTime).TotalMinutes;
                    int offSet = dayValue + firstRun;
                    if (this.runList[offSet] == null) { this.runList[offSet] = schKey; }
                    else { this.runList[offSet] += (", " + schKey); }
                    if (intTime != "--:--")
                    {
                        int intEnd = (int)TimeSpan.Parse(endTime).TotalMinutes;
                        int intInt = (int)TimeSpan.Parse(intTime).TotalMinutes;
                        for (int j = firstRun + intInt; j <= intEnd; j += intInt)
                        {
                            offSet = dayValue + j;
                            if (this.runList[offSet] == null) { this.runList[offSet] = schKey; }
                            else { this.runList[offSet] += (", " + schKey); }
                        }
                    }
                }
            }
        }

        private SortedList get_RunList() { return this.runList;  }

        private nextRun get_NextRun()
        {
            nextRun nxt = new nextRun();
            if (this.runList == null) { new_RunList(); }
            int idxNow = (1440 * (int)DateTime.Now.DayOfWeek) + ((int)DateTime.Now.TimeOfDay.TotalMinutes);
            int idxNxt = (int)this.runList.GetKey(0);
            foreach (int idx in this.runList.Keys) { if (idx > idxNow) { idxNxt = idx; break; } }
            nxt.Schedule = this.runList[idxNxt].ToString();
            int idxRun = idxNxt > idxNow ? idxNxt - idxNow : idxNxt - idxNow + (7 * 1440);
            nxt.AbsTime = new DateTime((DateTime.Now.AddMinutes(idxRun).Ticks / TimeSpan.TicksPerMinute) * TimeSpan.TicksPerMinute);
            nxt.RelTime = new TimeSpan(nxt.AbsTime.Ticks - DateTime.Now.Ticks + TimeSpan.TicksPerSecond);
            return nxt;
        }
    }

}
// ================================================================================================================================================================================
//    End of classes, Sayonara!
// ================================================================================================================================================================================