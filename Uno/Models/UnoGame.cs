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

            DrawPile.Add(new Card(color, CardValue.Zero));

            for (int i = 0; i < 2; i++)
            {
                foreach (CardValue val in Enum.GetValues(typeof(CardValue)))
                {
                    if (val == CardValue.Zero || val == CardValue.Wild || val == CardValue.WildDrawFour) continue;
                    DrawPile.Add(new Card(color, val));
                }
            }
        }

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
        if (player != CurrentPlayer || !card.CanPlayOn(TopCard, ActiveColor))
            return false;

        player.Hand.Remove(card);
        DiscardPile.Add(card);

        ActiveColor = card.Color == CardColor.Wild ? (chosenColor ?? CardColor.Red) : card.Color;

        switch (card.Value)
        {
            case CardValue.Skip:
                NextTurn();
                break;
            case CardValue.Reverse:
                Direction *= -1;
                break;
            case CardValue.DrawTwo:
                NextTurn();
                DrawCards(CurrentPlayer, 2);
                break;
            case CardValue.WildDrawFour:
                NextTurn();
                DrawCards(CurrentPlayer, 4);
                break;
        }

        NextTurn();
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
