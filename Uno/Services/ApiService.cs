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
            //hace el objeto que le mandamos en formato JSON
            string jsonMessage = JsonSerializer.Serialize(data);

            //ahora que ya esta en formato JSON se hace conte
            HttpContent content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");

            string url = BaseURL + endpoint;
            HttpResponseMessage response = await _client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            string respuestaServidor = await response.Content.ReadAsStringAsync();
            return respuestaServidor;
        }
        catch (Exception e)
        {
            //Se atrapa cualquier excepcion (servidor apagado, tiempo agotado, etc.) porque estas funciones se llaman
            //desde metodos async void y una excepcion sin atrapar cerraria toda la aplicacion
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
}
