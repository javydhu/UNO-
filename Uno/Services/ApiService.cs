using Avalonia.Platform;

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
    private readonly string _baseURL = "https://127.0.0.1:8000";

    //Funcion que envia las jugadas o movimientos, tambien podria enviar en un futuro a los jugadores
    public async Task<string> EnviarJugadaLog(string endpoint, object data)
    {
        using HttpClient client = new HttpClient();
        try
        {
            //hace el objeto que le mandamos en formato JSON
            string jsonMessage = JsonSerializer.Serialize(data);

            //ahora que ya esta en formato JSON se hace conte
            HttpContent content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");

            string url = _baseURL + endpoint;
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

    public async Task<string> Obtenerdatos(string endpoint)
    {
        using HttpClient client = new HttpClient();
        try
        {
            string URL = _baseURL + endpoint;

            HttpResponseMessage response = await client.GetAsync(URL);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }
}