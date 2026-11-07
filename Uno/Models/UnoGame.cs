using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Uno.Services;
using Uno.Models;
using System.Threading.Tasks;

namespace Uno.Models;

public class UnoGame
{
    public List<Player> Jugadores { get; private set; } = new();
    public List<Card> Pila { get; private set; } = new();
    public List<Card> PilaJugada { get; private set; } = new();

    public int actualJugadorIndex { get; private set; } = 0;
    public int Direccion { get; private set; } = 1;
    public CardColor ColorActual { get; set; }

    public int PilaPendiente { get; set; } = 0; 
    public int PasesContinuos { get; set; } = 0;
    public int partidaID = 1;

    public Card TopCard => PilaJugada.LastOrDefault()!;
    public Player actualJugador => Jugadores[actualJugadorIndex];

    public UnoGame()
    {
        InitializeDeck();
        StartGame();
    }
    public UnoGame(List<Player> players, int id)
    {
        partidaID = id;
        Jugadores = players;
        InitializeDeck();
        StartGame();
    }

    private void InitializeDeck()
    {
        Pila.Clear();
        PilaJugada.Clear();
        
        // 108 Cartas Exactas de UNO
        foreach (CardColor color in Enum.GetValues(typeof(CardColor)))
        {
            if (color == CardColor.Wild) continue;

            Pila.Add(new Card(color, CardValue.Zero)); // 1 cero por color (4)

            for (int i = 0; i < 2; i++) // 2 de cada una (1-9, Skip, Reverse, DrawTwo) (18 * 4 = 72)
            {
                foreach (CardValue val in Enum.GetValues(typeof(CardValue)))
                {
                    if (val == CardValue.Zero || val == CardValue.Wild || val == CardValue.WildDrawFour) continue;
                    Pila.Add(new Card(color, val));
                }
            }
        }

        for (int i = 0; i < 4; i++) // 4 Comodines normales y 4 Comodines +4 (8)
        {
            Pila.Add(new Card(CardColor.Wild, CardValue.Wild));
            Pila.Add(new Card(CardColor.Wild, CardValue.WildDrawFour));
        }

        Shuffle();
    }

    private void Shuffle()
    {
        Random rnd = new();
        Pila = Pila.OrderBy(_ => rnd.Next()).ToList();
    }

    public void StartGame()
    {
        foreach (var player in Jugadores)
        {
            for (int i = 0; i < 7; i++)
            {
                if (Pila.Count > 0)
                {
                    
                    entregar_carta(player);
                }
            }
        }

        Card primeraCarta;
        do
        {
            primeraCarta = TomarCartaDePila()!;
            PilaJugada.Add(primeraCarta);
        } while (primeraCarta.Color == CardColor.Wild);

        ColorActual = primeraCarta.Color;
    }
    
    public async void entregar_carta(Player player)
    {
        
        Card? card = TomarCartaDePila();
        if (card == null)  return;
        
        player.Mano.Add(TomarCartaDePila()!);
        await RegistrarRoboEnServidor(player, card);
    }

    public Card? TomarCartaDePila()
    {
        if (Pila.Count == 0)
        {
            return null; // Mazo agotado (sin rebarajear)
        }

        Card actualCarta = Pila[0];
        Pila.RemoveAt(0);
        if (Pila.Count == 0){
            PilaTermino();
        }

        return actualCarta;
    }

    public bool JugarCarta(Player jugador, Card carta, CardColor? actualColor = null)
    {
        if (jugador != actualJugador) return false;
        if (!jugador.Mano.Contains(carta)) return false;

        // Regla de acumulación de +2 / +4
        if (PilaPendiente > 0)
        {
            if (carta.Value != CardValue.DrawTwo && carta.Value != CardValue.WildDrawFour)
                return false; // Si hay castigo acumulado, solo puede responder con +2 o +4
        }
        else if (!carta.Jugable(TopCard, ColorActual))
        {
            return false;
        }

        jugador.Mano.Remove(carta);
        PilaJugada.Add(carta);
        PasesContinuos = 0; // Se resetea el contador de pases

        ColorActual = carta.Color == CardColor.Wild ? (actualColor ?? CardColor.Red) : carta.Color;

        switch (carta.Value)
        {
            case CardValue.DrawTwo:
                PilaPendiente += 2;
                break;
            case CardValue.WildDrawFour:
                PilaPendiente += 4;
                break;
            case CardValue.Skip:
                SiguienteTurno();
                break;
            case CardValue.Reverse:
                Direccion *= -1;
                break;
        }

        SiguienteTurno();
        return true;
    }

    public void ResuelveTomarPendientes(Player player)
    {
        for (int i = 0; i < PilaPendiente; i++)
        {
            Card? actualCarta = TomarCartaDePila();
            if (actualCarta != null) player.Mano.Add(actualCarta);
        }
        PilaPendiente = 0;
        SiguienteTurno();
    }

    public int CalcularPuntos(Player player)
    {
        int puntos = 0;
        foreach (var carta in player.Mano)
        {
            puntos += carta.Value switch
            {
                CardValue.Zero => 0,
                CardValue.One => 1,
                CardValue.Two => 2,
                CardValue.Three => 3,
                CardValue.Four => 4,
                CardValue.Five => 5,
                CardValue.Six => 6,
                CardValue.Seven => 7,
                CardValue.Eight => 8,
                CardValue.Nine => 9,
                _ => 0 // Comodines y especiales valen 0
            };
        }
        return puntos;
    }

    public void SiguienteTurno()
    {
        actualJugadorIndex = (actualJugadorIndex + Direccion + Jugadores.Count) % Jugadores.Count;
    }
    
    public async Task RegistrarRoboEnServidor(Player jugador, Card card)
    {
        ApiService api = new ApiService();
        var datosRobo = new { jugador_id = jugador.ID, partida_id =  partidaID, color_carta= card.Color.ToString(), valor_carta =card.Value.ToString() };
        string respuesta = await api.EnviarJugadaLog("/partida/robar-carta", datosRobo);
    }

    public async void PilaTermino()
    {
        if (Jugadores.Count == 0) return;
        ApiService api = new ApiService();
        var datosJugada = new { jugador_id = actualJugador.ID, partida_id = partidaID };
        string ans = await api.EnviarJugadaLog("/partida/sin-cartas", datosJugada);
        if (ans != null) Console.WriteLine("Acción registrada en el servidor correctamente.");
    }
}