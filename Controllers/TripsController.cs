using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using TravelAgencyAPI.Models;
using TravelAgencyAPI.Services;

namespace TravelAgencyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripsController : ControllerBase
    {
        private readonly DbService _dbService;

        public TripsController(DbService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// GET /api/trips - Pobiera wszystkie dostępne wycieczki wraz z ich podstawowymi informacjami.
        /// </summary>
        [HttpGet]
        public IActionResult GetTrips()
        {
            var trips = new List<Trip>();

            try
            {
                using (SqlConnection connection = new SqlConnection(_dbService.GetConnectionString()))
                {
                    connection.Open();

                    // Pobieramy wszystkie wycieczki
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople 
                          FROM Trip t
                          ORDER BY t.DateFrom", connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                trips.Add(new Trip
                                {
                                    IdTrip = (int)reader["IdTrip"],
                                    Name = reader["Name"].ToString(),
                                    Description = reader["Description"].ToString(),
                                    DateFrom = (DateTime)reader["DateFrom"],
                                    DateTo = (DateTime)reader["DateTo"],
                                    MaxPeople = (int)reader["MaxPeople"],
                                    Countries = new List<string>()
                                });
                            }
                        }
                    }

                    // Dla każdej wycieczki pobieramy powiązane kraje
                    foreach (var trip in trips)
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"SELECT c.Name
                              FROM Country c
                              JOIN Country_Trip ct ON c.IdCountry = ct.IdCountry
                              WHERE ct.IdTrip = @IdTrip", connection))
                        {
                            command.Parameters.AddWithValue("@IdTrip", trip.IdTrip);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    trip.Countries.Add(reader["Name"].ToString());
                                }
                            }
                        }
                    }
                }

                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd serwera: {ex.Message}");
            }
        }
    }
}