namespace Uno.Services;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ApiService
{
    private readonly string _baseURL = "https://127.0.0.1:8000";

    public async Task<string> EnviarJugadaLog(string endpoint, object data)
    {
        using HttpClient client = new HttpClient();
        try
        {
            //hace el objeto que le mandamos en formato JSON
            string jsonMessage = JsonSerializer.Serialize(data);

            HttpContent content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(_baseURL, content);
            response.EnsureSuccessStatusCode();

            string respuestaServidor = await response.Content.ReadAsStringAsync();
            return respuestaServidor;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }
}