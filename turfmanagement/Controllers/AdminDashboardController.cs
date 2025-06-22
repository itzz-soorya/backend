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

        public AdminDashboardController(DatabaseConnection db)
        {
            _db = db;
        }

        [HttpGet("dashboard")]
        public IActionResult GetDashboard()
        {
            using var conn = _db.GetConnection();
            conn.Open();

            var result = new DashboardResult();
            var monthData = new List<MonthBookingData>();

            string[] monthNames = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            string[] colors = {
                "#FF5733", "#33C1FF", "#FFC300", "#DAF7A6", "#C70039", "#900C3F",
                "#581845", "#4CAF50", "#FF9800", "#3F51B5", "#E91E63", "#795548"
            };

            // 1. Get booking count per month
            string monthQuery = @"
                SELECT EXTRACT(MONTH FROM BookingDate) AS month, COUNT(*) AS count
                FROM Bookings
                GROUP BY month;
            ";

            using (var cmd = new NpgsqlCommand(monthQuery, conn))
            using (var reader = cmd.ExecuteReader())
            {
                var monthCounts = new Dictionary<int, int>();
                while (reader.Read())
                {
                    int month = (int)(reader.GetDouble(0)); // PostgreSQL EXTRACT returns double
                    int count = reader.GetInt32(1);
                    monthCounts[month] = count;
                }

                for (int i = 1; i <= 12; i++)
                {
                    monthData.Add(new MonthBookingData
                    {
                        Month = monthNames[i - 1],
                        Bookings = monthCounts.ContainsKey(i) ? monthCounts[i] : 0,
                        Color = colors[i - 1]
                    });
                }
            }

            result.BookingData = monthData;

            // 2. Get today / past / upcoming counts
            string todayQuery = "SELECT COUNT(*) FROM Bookings WHERE BookingDate = CURRENT_DATE";
            string upcomingQuery = "SELECT COUNT(*) FROM Bookings WHERE BookingDate > CURRENT_DATE";
            string pastQuery = "SELECT COUNT(*) FROM Bookings WHERE BookingDate < CURRENT_DATE";

            using (var cmd = new NpgsqlCommand(todayQuery, conn))
                result.Today = Convert.ToInt32(cmd.ExecuteScalar());

            using (var cmd = new NpgsqlCommand(upcomingQuery, conn))
                result.Upcoming = Convert.ToInt32(cmd.ExecuteScalar());

            using (var cmd = new NpgsqlCommand(pastQuery, conn))
                result.Past = Convert.ToInt32(cmd.ExecuteScalar());

            return Ok(result);
        }
    }

    // DTOs

    public class DashboardResult
    {
        public List<MonthBookingData> BookingData { get; set; }
        public int Today { get; set; }
        public int Upcoming { get; set; }
        public int Past { get; set; }
    }

    public class MonthBookingData
    {
        public string Month { get; set; }
        public int Bookings { get; set; }
        public string Color { get; set; }
    }
}
