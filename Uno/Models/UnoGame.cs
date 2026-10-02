using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Uno.Models;

public class UnoGame
{
    public List<Player> Players { get; private set; } = new();
    public List<Card> DrawPile { get; private set; } = new();
    public List<Card> DiscardPile { get; private set; } = new();
    public int CurrentPlayerIndex { get; private set; } = 0;
    public int Direction { get; private set; } = 1; // 1 = Sentido horario, -1 = Antihorario
    public CardColor ActiveColor { get; set; }

    public Card TopCard => DiscardPile.LastOrDefault()!;
    public Player CurrentPlayer => Players[CurrentPlayerIndex];

    public UnoGame()
    {
        Players.Add(new Player("Jugador 1"));
        Players.Add(new Player("Jugador 2"));
        Players.Add(new Player("Jugador 3"));

        InitializeDeck();
        StartGame();
    }

    private void InitializeDeck()
    {
        DrawPile.Clear();
        foreach (CardColor color in Enum.GetValues(typeof(CardColor)))
        {
            if (color == CardColor.Wild) continue;

            // Iteramos una sola vez por cada valor (0 al 9, Skip, Reverse, DrawTwo)
            foreach (CardValue val in Enum.GetValues(typeof(CardValue)))
            {
                if (val == CardValue.Wild || val == CardValue.WildDrawFour) continue;
            
                // Esto asegura que solo haya UN "1 azul", UN "Reversa rojo", etc.
                DrawPile.Add(new Card(color, val));
            }
        }

        // 4 de cada comodín
        for (int i = 0; i < 4; i++)
        {
            DrawPile.Add(new Card(CardColor.Wild, CardValue.Wild));
            DrawPile.Add(new Card(CardColor.Wild, CardValue.WildDrawFour));
        }

        Shuffle();
    }

    private void Shuffle()
    {
        Random rnd = new();
        DrawPile = DrawPile.OrderBy(_ => rnd.Next()).ToList();
    }

    public void StartGame()
    {
        foreach (var player in Players)
        {
            for (int i = 0; i < 7; i++)
            {
                player.Hand.Add(DrawCardFromPile());
            }
        }

        Card firstCard;
        do
        {
            firstCard = DrawCardFromPile();
            DiscardPile.Add(firstCard);
        } while (firstCard.Color == CardColor.Wild);

        ActiveColor = firstCard.Color;
    }

    public Card DrawCardFromPile()
    {
        if (DrawPile.Count == 0)
        {
            Card top = DiscardPile.Last();
            DiscardPile.Remove(top);
            DrawPile = DiscardPile.OrderBy(_ => Random.Shared.Next()).ToList();
            DiscardPile.Clear();
            DiscardPile.Add(top);
        }

        Card drawn = DrawPile[0];
        DrawPile.RemoveAt(0);
        return drawn;
    }

    public bool PlayCard(Player player, Card card, CardColor? chosenColor = null)
    {
        // Verificar si es el jugador actual y si la carta es válida
        if (player != CurrentPlayer || !card.CanPlayOn(TopCard, ActiveColor))
            return false;

        // Mover carta de la mano a la pila de descarte
        player.Hand.Remove(card);
        DiscardPile.Add(card);

        // Actualizar el color activo (por ahora los comodines serán Rojos por defecto)
        ActiveColor = card.Color == CardColor.Wild ? (chosenColor ?? CardColor.Red) : card.Color;

        // Determinar a quién le afectan las cartas de acción ANTES de cambiar el turno
        int nextPlayerIdx = (CurrentPlayerIndex + Direction + Players.Count) % Players.Count;
        Player targetPlayer = Players[nextPlayerIdx];

        switch (card.Value)
        {
            case CardValue.Skip:
                NextTurn(); // Avanza 1 vez (el ENTER avanzará la 2da vez, saltándolo)
                break;
            case CardValue.Reverse:
                Direction *= -1;
                break;
            case CardValue.DrawTwo:
                DrawCards(targetPlayer, 2);
                NextTurn(); // El jugador objetivo roba y pierde su turno
                break;
            case CardValue.WildDrawFour:
                DrawCards(targetPlayer, 4);
                NextTurn(); // El jugador objetivo roba y pierde su turno
                break;
        }

        // El turno se quedará pausado hasta que el usuario presione ENTER.
        return true;
    }

    public void DrawCards(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            player.Hand.Add(DrawCardFromPile());
        }
    }

    public void NextTurn()
    {
        CurrentPlayerIndex = (CurrentPlayerIndex + Direction + Players.Count) % Players.Count;
    }
}
