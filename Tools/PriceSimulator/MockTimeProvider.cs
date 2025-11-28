using System;
using StardewCapital.Domain.Market;
using StardewCapital.Core.Time;

namespace StardewCapital.Simulator
{
    /// <summary>
    /// 模拟的时间提供器
    /// 无需游戏环境，支持设置任意季节、年份、日期
    /// </summary>
    public class MockTimeProvider : IGameTimeProvider
    {
        private Season _currentSeason;
        private int _currentYear;
        private int _currentDay;
        private int _openingTime;
        private int _closingTime;
        private int _currentTimeOfDay;

        public MockTimeProvider(Season season, int year, int openingTime = 600, int closingTime = 2600)
        {
            _currentSeason = season;
            _currentYear = year;
            _currentDay = 1;
            _openingTime = openingTime;
            _closingTime = closingTime;
            _currentTimeOfDay = openingTime;
        }

        // IGameTimeProvider 实现
        public int CurrentTimeOfDay => _currentTimeOfDay;
        
        public double TimeRatio => CalculateTimeRatio(_currentTimeOfDay);
        
        public bool IsPaused => false; // 模拟器中始终不暂停
        
        public int TotalMinutesToday => ToMinutes(_currentTimeOfDay);

        // 额外方法（用于模拟器）
        public Season GetCurrentSeason() => _currentSeason;
        public int GetCurrentYear() => _currentYear;
        public int GetCurrentDay() => _currentDay;
        public int GetOpeningTime() => _openingTime;
        public int GetClosingTime() => _closingTime;

        // 设置方法（用于模拟）
        public void SetSeason(Season season) => _currentSeason = season;
        public void SetYear(int year) => _currentYear = year;
        public void SetDay(int day) => _currentDay = day;
        public void SetOpeningTime(int time) => _openingTime = time;
        public void SetClosingTime(int time) => _closingTime = time;
        public void SetCurrentTime(int time) => _currentTimeOfDay = time;

        // 时间转换工具方法
        public int ToMinutes(int time)
        {
            int hours = time / 100;
            int minutes = time % 100;
            return hours * 60 + minutes;
        }

        public int ToGameTime(int minutes)
        {
            int hours = minutes / 60;
            int mins = minutes % 60;
            return hours * 100 + mins;
        }

        public double CalculateTimeRatio(int currentTime)
        {
            int startMinutes = ToMinutes(_openingTime);
            int endMinutes = ToMinutes(_closingTime);
            int currentMinutes = ToMinutes(currentTime);

            if (currentMinutes <= startMinutes) return 0.0;
            if (currentMinutes >= endMinutes) return 1.0;

            return (double)(currentMinutes - startMinutes) / (endMinutes - startMinutes);
        }
    }
}
