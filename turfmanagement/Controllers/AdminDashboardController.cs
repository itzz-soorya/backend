using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly DatabaseConnection _db;
        private static readonly string[] MonthNames = {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun",
            "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        };
        private static readonly string[] Colors = {
            "#FF5733", "#33C1FF", "#FFC300", "#DAF7A6", "#C70039", "#900C3F",
            "#581845", "#4CAF50", "#FF9800", "#3F51B5", "#E91E63", "#795548"
        };

        public AdminDashboardController(DatabaseConnection db)
        {
            _db = db;
        }

        [HttpGet("year")]
        public IActionResult GetYearlyStats([FromQuery] int year)
        {
            var result = new List<MonthBookingData>();
            int totalBookings = 0;
            int totalHours = 0;

            try
            {
                using var conn = _db.GetConnection();
                conn.Open();

                string query = @"
                    SELECT 
                        EXTRACT(MONTH FROM BookingDate) AS month,
                        COUNT(*) AS count,
                        SUM(EXTRACT(HOUR FROM AGE(to_timestamp(SlotTimeTo, 'HH12:MI AM'), to_timestamp(SlotTimeFrom, 'HH12:MI AM')))) AS hours
                    FROM Bookings
                    WHERE EXTRACT(YEAR FROM BookingDate) = @year
                    GROUP BY month
                    ORDER BY month;
                ";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@year", year);

                using var reader = cmd.ExecuteReader();
                var counts = new Dictionary<int, (int count, int hours)>();

                while (reader.Read())
                {
                    int month = Convert.ToInt32(reader["month"]);
                    int count = Convert.ToInt32(reader["count"]);
                    int hours = Convert.ToInt32(reader["hours"]);

                    totalBookings += count;
                    totalHours += hours;
                    counts[month] = (count, hours);
                }

                for (int i = 1; i <= 12; i++)
                {
                    result.Add(new MonthBookingData
                    {
                        Label = MonthNames[i - 1],
                        Bookings = counts.ContainsKey(i) ? counts[i].count : 0,
                        Hours = counts.ContainsKey(i) ? counts[i].hours : 0,
                        Color = Colors[i - 1]
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error in GetYearlyStats: " + ex.Message);
                return StatusCode(500, new { message = "Failed to fetch yearly stats." });
            }

            return Ok(new { totalBookings, totalHours, data = result });
        }

        [HttpGet("month")]
        public IActionResult GetMonthlyStats([FromQuery] int month, [FromQuery] int year)
        {
            var result = new List<DayBookingData>();
            int totalBookings = 0;
            int totalHours = 0;

            try
            {
                using var conn = _db.GetConnection();
                conn.Open();

                string query = @"
                    SELECT 
                        EXTRACT(DAY FROM BookingDate) AS day,
                        COUNT(*) AS count,
                        SUM(EXTRACT(HOUR FROM AGE(to_timestamp(SlotTimeTo, 'HH12:MI AM'), to_timestamp(SlotTimeFrom, 'HH12:MI AM')))) AS hours
                    FROM Bookings
                    WHERE EXTRACT(MONTH FROM BookingDate) = @month AND EXTRACT(YEAR FROM BookingDate) = @year
                    GROUP BY day
                    ORDER BY day;
                ";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@month", month);
                cmd.Parameters.AddWithValue("@year", year);

                using var reader = cmd.ExecuteReader();
                var counts = new Dictionary<int, (int count, int hours)>();

                while (reader.Read())
                {
                    int day = Convert.ToInt32(reader["day"]);
                    int count = Convert.ToInt32(reader["count"]);
                    int hours = Convert.ToInt32(reader["hours"]);

                    totalBookings += count;
                    totalHours += hours;
                    counts[day] = (count, hours);
                }

                // ✅ Dynamic day count for the month
                int daysInMonth = DateTime.DaysInMonth(year, month);
                for (int i = 1; i <= daysInMonth; i++)
                {
                    result.Add(new DayBookingData
                    {
                        Day = i,
                        Bookings = counts.ContainsKey(i) ? counts[i].count : 0,
                        Hours = counts.ContainsKey(i) ? counts[i].hours : 0
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error in GetMonthlyStats: " + ex.Message);
                return StatusCode(500, new { message = "Failed to fetch monthly stats." });
            }

            return Ok(new { totalBookings, totalHours, data = result });
        }

        [HttpGet("summary")]
        public IActionResult GetSummary()
        {
            var summary = new CountSummary();

            try
            {
                using var conn = _db.GetConnection();
                conn.Open();

                // Count Bookings
                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Bookings WHERE BookingDate = CURRENT_DATE", conn))
                    summary.Today = Convert.ToInt32(cmd.ExecuteScalar());

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Bookings WHERE BookingDate > CURRENT_DATE", conn))
                    summary.Upcoming = Convert.ToInt32(cmd.ExecuteScalar());

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Bookings WHERE BookingDate < CURRENT_DATE", conn))
                    summary.Past = Convert.ToInt32(cmd.ExecuteScalar());

                // Total Hours (assuming 1 hour per time slot and time_slots is a JSON array)
                using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(jsonb_array_length(time_slots::jsonb)), 0) FROM Bookings WHERE BookingDate = CURRENT_DATE", conn))
                    summary.TodayHours = Convert.ToInt32(cmd.ExecuteScalar());

                using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(jsonb_array_length(time_slots::jsonb)), 0) FROM Bookings WHERE BookingDate > CURRENT_DATE", conn))
                    summary.UpcomingHours = Convert.ToInt32(cmd.ExecuteScalar());

                using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(jsonb_array_length(time_slots::jsonb)), 0) FROM Bookings WHERE BookingDate < CURRENT_DATE", conn))
                    summary.PastHours = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error in GetSummary: " + ex.Message);
                return StatusCode(500, new { message = "Failed to fetch summary data." });
            }

            return Ok(summary);
        }
 
        }

        // DTOs
        public class MonthBookingData
        {
            public string Label { get; set; }  // Jan, Feb, etc.
            public int Bookings { get; set; }
            public int Hours { get; set; }
            public string Color { get; set; }
        }

        public class DayBookingData
        {
            public int Day { get; set; }
            public int Bookings { get; set; }
            public int Hours { get; set; }
        }

        public class CountSummary
        {
            public int Today { get; set; }
            public int Upcoming { get; set; }
            public int Past { get; set; }


            public int TodayHours { get; set; }
            public int UpcomingHours { get; set; }
            public int PastHours { get; set; }
        }
    }

