using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using TravelAgencyAPI.Models;
using TravelAgencyAPI.Services;

namespace TravelAgencyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientsController : ControllerBase
    {
        private readonly DbService _dbService;

        public ClientsController(DbService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// GET /api/clients/{id}/trips - Pobiera wszystkie wycieczki powiązane z konkretnym klientem.
        /// </summary>
        [HttpGet("{id}/trips")]
        public IActionResult GetClientTrips(int id)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_dbService.GetConnectionString()))
                {
                    connection.Open();

                    // Sprawdzamy, czy klient istnieje
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);
                        int clientExists = (int)command.ExecuteScalar();

                        if (clientExists == 0)
                        {
                            return NotFound($"Klient o ID {id} nie istnieje");
                        }
                    }

                    var clientTrips = new List<object>();

                    // Pobieramy wszystkie wycieczki klienta wraz z informacjami o rejestracji/płatności
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                                ct.RegisteredAt, ct.PaymentDate
                          FROM Trip t
                          JOIN Client_Trip ct ON t.IdTrip = ct.IdTrip
                          WHERE ct.IdClient = @IdClient
                          ORDER BY t.DateFrom", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var trip = new
                                {
                                    IdTrip = (int)reader["IdTrip"],
                                    Name = reader["Name"].ToString(),
                                    Description = reader["Description"].ToString(),
                                    DateFrom = ((DateTime)reader["DateFrom"]).ToString("yyyy-MM-dd"),
                                    DateTo = ((DateTime)reader["DateTo"]).ToString("yyyy-MM-dd"),
                                    MaxPeople = (int)reader["MaxPeople"],
                                    RegisteredAt = (int)reader["RegisteredAt"],
                                    PaymentDate = reader["PaymentDate"] != DBNull.Value ? (int?)reader["PaymentDate"] : null,
                                    Countries = new List<string>()
                                };

                                clientTrips.Add(trip);
                            }
                        }
                    }

                    if (clientTrips.Count == 0)
                    {
                        return Ok(new { Message = $"Klient o ID {id} nie jest zarejestrowany na żadną wycieczkę" });
                    }

                    return Ok(clientTrips);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd serwera: {ex.Message}");
            }
        }

        /// <summary>
        /// POST /api/clients - Tworzy nowy rekord klienta.
        /// </summary>
        [HttpPost]
        public IActionResult CreateClient([FromBody] Client client)
        {
            try
            {
                // Walidacja danych wejściowych
                if (client == null)
                {
                    return BadRequest("Brak danych klienta");
                }

                if (string.IsNullOrEmpty(client.FirstName))
                {
                    return BadRequest("Imię jest wymagane");
                }

                if (string.IsNullOrEmpty(client.LastName))
                {
                    return BadRequest("Nazwisko jest wymagane");
                }

                if (string.IsNullOrEmpty(client.Email))
                {
                    return BadRequest("Email jest wymagany");
                }

                int newClientId;

                using (SqlConnection connection = new SqlConnection(_dbService.GetConnectionString()))
                {
                    connection.Open();

                    // Wstawiamy nowego klienta
                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                          VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
                          SELECT SCOPE_IDENTITY();", connection))
                    {
                        command.Parameters.AddWithValue("@FirstName", client.FirstName);
                        command.Parameters.AddWithValue("@LastName", client.LastName);
                        command.Parameters.AddWithValue("@Email", client.Email);
                        command.Parameters.AddWithValue("@Telephone", client.Telephone ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Pesel", client.Pesel ?? (object)DBNull.Value);

                        newClientId = Convert.ToInt32(command.ExecuteScalar());
                    }
                }

                return CreatedAtAction(nameof(GetClientTrips), new { id = newClientId }, 
                    new { 
                        IdClient = newClientId, 
                        Message = "Klient został pomyślnie utworzony" 
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd serwera: {ex.Message}");
            }
        }

        /// <summary>
        /// PUT /api/clients/{id}/trips/{tripId} - Rejestruje klienta na konkretną wycieczkę.
        /// </summary>
        [HttpPut("{id}/trips/{tripId}")]
        public IActionResult RegisterClientForTrip(int id, int tripId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_dbService.GetConnectionString()))
                {
                    connection.Open();

                    // Sprawdzamy, czy klient istnieje
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);
                        int clientExists = (int)command.ExecuteScalar();

                        if (clientExists == 0)
                        {
                            return NotFound($"Klient o ID {id} nie istnieje");
                        }
                    }

                    // Sprawdzamy, czy wycieczka istnieje
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(1) FROM Trip WHERE IdTrip = @IdTrip", connection))
                    {
                        command.Parameters.AddWithValue("@IdTrip", tripId);
                        int tripExists = (int)command.ExecuteScalar();

                        if (tripExists == 0)
                        {
                            return NotFound($"Wycieczka o ID {tripId} nie istnieje");
                        }
                    }

                    // Sprawdzamy, czy klient jest już zarejestrowany na tę wycieczkę
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);
                        command.Parameters.AddWithValue("@IdTrip", tripId);
                        int registrationExists = (int)command.ExecuteScalar();

                        if (registrationExists > 0)
                        {
                            return Conflict($"Klient o ID {id} jest już zarejestrowany na wycieczkę o ID {tripId}");
                        }
                    }

                    // Sprawdzamy, czy nie przekroczono maksymalnej liczby uczestników
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.MaxPeople, (SELECT COUNT(1) FROM Client_Trip WHERE IdTrip = @IdTrip) AS CurrentParticipants
                          FROM Trip t WHERE t.IdTrip = @IdTrip", connection))
                    {
                        command.Parameters.AddWithValue("@IdTrip", tripId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int maxPeople = (int)reader["MaxPeople"];
                                int currentParticipants = (int)reader["CurrentParticipants"];

                                if (currentParticipants >= maxPeople)
                                {
                                    return BadRequest("Osiągnięto maksymalną liczbę uczestników dla tej wycieczki");
                                }
                            }
                        }
                    }

                    // Pobierz dzisiejszą datę w formacie yyyyMMdd
                    int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));

                    // Rejestrujemy klienta na wycieczkę
                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt, PaymentDate)
                          VALUES (@IdClient, @IdTrip, @RegisteredAt, NULL)", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);
                        command.Parameters.AddWithValue("@IdTrip", tripId);
                        command.Parameters.AddWithValue("@RegisteredAt", today);

                        command.ExecuteNonQuery();
                    }

                    return Ok(new { Message = $"Klient o ID {id} został pomyślnie zarejestrowany na wycieczkę o ID {tripId}" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd serwera: {ex.Message}");
            }
        }

        /// <summary>
        /// DELETE /api/clients/{id}/trips/{tripId} - Usuwa rejestrację klienta z wycieczki.
        /// </summary>
        [HttpDelete("{id}/trips/{tripId}")]
        public IActionResult UnregisterClientFromTrip(int id, int tripId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_dbService.GetConnectionString()))
                {
                    connection.Open();

                    // Sprawdzamy, czy rejestracja istnieje
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);
                        command.Parameters.AddWithValue("@IdTrip", tripId);
                        int registrationExists = (int)command.ExecuteScalar();

                        if (registrationExists == 0)
                        {
                            return NotFound($"Klient o ID {id} nie jest zarejestrowany na wycieczkę o ID {tripId}");
                        }
                    }

                    // Usuwamy rejestrację
                    using (SqlCommand command = new SqlCommand(
                        "DELETE FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip", connection))
                    {
                        command.Parameters.AddWithValue("@IdClient", id);
                        command.Parameters.AddWithValue("@IdTrip", tripId);

                        command.ExecuteNonQuery();
                    }

                    return Ok(new { Message = $"Rejestracja klienta o ID {id} na wycieczkę o ID {tripId} została pomyślnie usunięta" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd serwera: {ex.Message}");
            }
        }
    }
}