using System;

namespace ACS_4Series_Template_V3.QuickActions
{
    /// <summary>
    /// Sunrise/sunset calculator (NOAA solar-position approximation, ±1-2 min).
    /// Pure math — no Crestron Scheduler registry side effects, so quick-action
    /// schedules stay entirely inside quickActions.json.
    /// </summary>
    public static class SunCalc
    {
        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;
        private const double ZenithOfficial = 90.833; // includes refraction + solar disc radius

        /// <summary>
        /// Compute local sunrise and sunset for the given date and location.
        /// Returns false in polar edge cases (sun never rises/sets that day).
        /// Times are returned in the processor's local time zone.
        /// </summary>
        public static bool TryGetSunTimes(DateTime localDate, double latitude, double longitude,
            out DateTime sunrise, out DateTime sunset)
        {
            sunrise = DateTime.MinValue;
            sunset = DateTime.MinValue;

            double utcOffsetHours = TimeZoneInfo.Local.GetUtcOffset(localDate.Date).TotalHours;

            double riseUt, setUt;
            bool riseOk = TryCalcEventUt(localDate, latitude, longitude, true, out riseUt);
            bool setOk = TryCalcEventUt(localDate, latitude, longitude, false, out setUt);
            if (!riseOk || !setOk) return false;

            sunrise = ToLocalDateTime(localDate, riseUt + utcOffsetHours);
            sunset = ToLocalDateTime(localDate, setUt + utcOffsetHours);
            return true;
        }

        private static DateTime ToLocalDateTime(DateTime date, double localHours)
        {
            localHours = ((localHours % 24.0) + 24.0) % 24.0;
            int h = (int)localHours;
            int m = (int)Math.Round((localHours - h) * 60.0);
            if (m == 60) { m = 0; h = (h + 1) % 24; }
            return new DateTime(date.Year, date.Month, date.Day, h, m, 0);
        }

        /// <summary>Classic NOAA/almanac sunrise-sunset algorithm; result in UT hours.</summary>
        private static bool TryCalcEventUt(DateTime date, double latitude, double longitude,
            bool isRise, out double utHours)
        {
            utHours = 0;
            int dayOfYear = date.DayOfYear;
            double lngHour = longitude / 15.0;
            double t = isRise
                ? dayOfYear + ((6.0 - lngHour) / 24.0)
                : dayOfYear + ((18.0 - lngHour) / 24.0);

            // Sun's mean anomaly / true longitude
            double meanAnomaly = (0.9856 * t) - 3.289;
            double trueLong = meanAnomaly
                + (1.916 * Math.Sin(meanAnomaly * Deg2Rad))
                + (0.020 * Math.Sin(2 * meanAnomaly * Deg2Rad))
                + 282.634;
            trueLong = Normalize(trueLong, 360.0);

            // Right ascension, adjusted into the same quadrant as trueLong
            double rightAsc = Rad2Deg * Math.Atan(0.91764 * Math.Tan(trueLong * Deg2Rad));
            rightAsc = Normalize(rightAsc, 360.0);
            double lQuadrant = Math.Floor(trueLong / 90.0) * 90.0;
            double raQuadrant = Math.Floor(rightAsc / 90.0) * 90.0;
            rightAsc = (rightAsc + (lQuadrant - raQuadrant)) / 15.0; // → hours

            // Sun declination
            double sinDec = 0.39782 * Math.Sin(trueLong * Deg2Rad);
            double cosDec = Math.Cos(Math.Asin(sinDec));

            // Local hour angle
            double cosH = (Math.Cos(ZenithOfficial * Deg2Rad) - (sinDec * Math.Sin(latitude * Deg2Rad)))
                        / (cosDec * Math.Cos(latitude * Deg2Rad));
            if (cosH > 1.0 || cosH < -1.0) return false; // no rise/set at this latitude today

            double hourAngle = isRise
                ? 360.0 - Rad2Deg * Math.Acos(cosH)
                : Rad2Deg * Math.Acos(cosH);
            hourAngle /= 15.0;

            double localMeanTime = hourAngle + rightAsc - (0.06571 * t) - 6.622;
            utHours = Normalize(localMeanTime - lngHour, 24.0);
            return true;
        }

        private static double Normalize(double value, double range)
        {
            value = value % range;
            if (value < 0) value += range;
            return value;
        }
    }
}
