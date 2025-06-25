using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminBookingController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public AdminBookingController(DatabaseConnection db)
        {
            _db = db;
        }

        // GET: /api/adminbooking?date=YYYY-MM-DD
        [HttpGet]
        public IActionResult GetBookingsByStatus([FromQuery] string status)
        {
            var bookings = new List<BookingDisplayDto>();
            string query = @"
        SELECT b.BookingDate, u.Name, u.PhoneNumber, b.SlotTimeFrom, b.SlotTimeTo, b.Amount
        FROM Bookings b
        JOIN Users u ON b.UserId = u.UserId
        WHERE {0}
        ORDER BY b.BookingDate, b.SlotTimeFrom;
    ";

            string condition;
            DateTime today = DateTime.Today;

            switch (status?.ToLower())
            {
                case "today":
                    condition = "b.BookingDate = @targetDate";
                    break;
                case "past":
                    condition = "b.BookingDate < @targetDate";
                    break;
                case "upcoming":
                    condition = "b.BookingDate > @targetDate";
                    break;
                default:
                    return BadRequest("Invalid status. Use 'past', 'today', or 'upcoming'.");
            }

            string finalQuery = string.Format(query, condition);

            using var conn = _db.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(finalQuery, conn);
            cmd.Parameters.AddWithValue("@targetDate", today);

            using var reader = cmd.ExecuteReader();
            int count = 1;

            while (reader.Read())
            {
                var dto = new BookingDisplayDto
                {
                    No = count++,
                    Date = ((DateTime)reader["BookingDate"]).ToString("dd/MM/yyyy"),
                    Name = reader["Name"].ToString(),
                    Phone = reader["PhoneNumber"].ToString(),
                    Time = $"{reader["SlotTimeFrom"]} - {reader["SlotTimeTo"]}",
                    Price = Convert.ToDecimal(reader["Amount"]),
                    Status = "Booked"
                };

                bookings.Add(dto);
            }

            return Ok(bookings);
        }

    }

    public class BookingDisplayDto
    {
        public int No { get; set; }
        public string Date { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Time { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
    }
}
