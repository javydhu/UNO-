namespace Uno.Services;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Uno.Models;

//El servicio de la API es basicamente para poder centralizar las llamadas que se le hacen y para no tener que hacer
//todo el protocolo de llamadas cada que se quiere hacer una peticion por ejemplo de post, solo se le llama a esta
//funcion, se manda en que parte de la url se va a ocupar o que terminal va a tener y los datos que se le pasan o
//los datos que va a recibir

//resultado de una peticion. Ok = el servidor contesto 2xx, Status = codigo HTTP (0 si no hubo conexion)
public record ApiResult(bool Ok, int Status, string Body);

public class ApiService
{
    //Se declara la base de la url con la que se va a conectar con la base de datos
    public static string ServerIP { get; set; } = "127.0.0.1";
    public static string BaseURL => $"http://{ServerIP}:8000";

    //Un solo HttpClient compartido por toda la aplicacion (crear uno nuevo en cada peticion desperdicia sockets).
    //Si el servidor no contesta en 5 segundos la peticion se cancela y el juego sigue sin registrar ese movimiento
    private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    //Funcion que envia las jugadas o movimientos, tambien podria enviar en un futuro a los jugadores
    public async Task<string?> EnviarJugadaLog(string endpoint, object data)
    {
        try
        {
            string jsonMessage = JsonSerializer.Serialize(data);
            HttpContent content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");

            string url = BaseURL + endpoint;
            HttpResponseMessage response = await _client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            string respuestaServidor = await response.Content.ReadAsStringAsync();
            return respuestaServidor;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    public async Task<string?> Obtenerdatos(string endpoint)
    {
        try
        {
            string url = BaseURL + endpoint;

            HttpResponseMessage response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    // ---------------------------------------------------------------------
    // igual que las de arriba, pero conservan el codigo HTTP y el cuerpo
    // aunque el servidor responda error (404, 409...). Asi el cliente puede mostrar
    // el mensaje real, por ejemplo "¡No es tu turno!" o "La partida esta llena".
    // ---------------------------------------------------------------------
    public async Task<ApiResult> PostAsync(string endpoint, object? data)
    {
        try
        {
            string json = JsonSerializer.Serialize(data ?? new { });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync(BaseURL + endpoint, content);
            string body = await response.Content.ReadAsStringAsync();
            return new ApiResult(response.IsSuccessStatusCode, (int)response.StatusCode, body);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return new ApiResult(false, 0, string.Empty);
        }
    }

    public async Task<ApiResult> GetAsync(string endpoint)
    {
        try
        {
            HttpResponseMessage response = await _client.GetAsync(BaseURL + endpoint);
            string body = await response.Content.ReadAsStringAsync();
            return new ApiResult(response.IsSuccessStatusCode, (int)response.StatusCode, body);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return new ApiResult(false, 0, string.Empty);
        }
    }

    //FastAPI manda los errores como {"detail": "texto"}; esta funcion saca ese texto
    public static string ExtraerDetalle(ApiResult r)
    {
        if (r.Status == 0) return "No se pudo conectar con el servidor. Revisa la IP y que la API este encendida.";
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString() ?? "Error desconocido.";
        }
        catch (JsonException) { }
        return $"Error del servidor ({r.Status}).";
    }
}