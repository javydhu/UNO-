using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Uno.Services;
using System.Collections.Generic;

namespace Uno.Models;

public class UnoGame
{
    public List<Player> Players { get; private set; } = new();
    public List<Card> DrawPile { get; private set; } = new();
    public List<Card> DiscardPile { get; private set; } = new();

    public int CurrentPlayerIndex { get; private set; } = 0;
    public int Direction { get; private set; } = 1;
    public CardColor ActiveColor { get; set; }

    public int PendingDrawCards { get; set; } = 0; 
    public int ConsecutivePasses { get; set; } = 0; 

    public Card TopCard => DiscardPile.LastOrDefault()!;
    public Player CurrentPlayer => Players[CurrentPlayerIndex];

    public UnoGame()
    {
        InitializeDeck();
        StartGame();
    }
    public UnoGame(List<Player> players)
    {
        Players = players;
        InitializeDeck();
        StartGame();
    }

    private void InitializeDeck()
    {
        DrawPile.Clear();
        DiscardPile.Clear();

        // 108 Cartas Exactas de UNO
        foreach (CardColor color in Enum.GetValues(typeof(CardColor)))
        {
            if (color == CardColor.Wild) continue;

            DrawPile.Add(new Card(color, CardValue.Zero)); // 1 cero por color (4)

            for (int i = 0; i < 2; i++) // 2 de cada una (1-9, Skip, Reverse, DrawTwo) (18 * 4 = 72)
            {
                foreach (CardValue val in Enum.GetValues(typeof(CardValue)))
                {
                    if (val == CardValue.Zero || val == CardValue.Wild || val == CardValue.WildDrawFour) continue;
                    DrawPile.Add(new Card(color, val));
                }
            }
        }

        for (int i = 0; i < 4; i++) // 4 Comodines normales y 4 Comodines +4 (8)
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
                if (DrawPile.Count > 0) player.Hand.Add(DrawCardFromPile()!);
            }
        }

        Card firstCard;
        do
        {
            firstCard = DrawCardFromPile()!;
            DiscardPile.Add(firstCard);
        } while (firstCard.Color == CardColor.Wild);

        ActiveColor = firstCard.Color;
    }

    public Card? DrawCardFromPile()
    {
        if (DrawPile.Count == 0) return null; // Mazo agotado (sin rebarajear)

        Card drawn = DrawPile[0];
        DrawPile.RemoveAt(0);
        return drawn;
    }

    public bool PlayCard(Player player, Card card, CardColor? chosenColor = null)
    {
        if (player != CurrentPlayer) return false;

        // Regla de acumulación de +2 / +4
        if (PendingDrawCards > 0)
        {
            if (card.Value != CardValue.DrawTwo && card.Value != CardValue.WildDrawFour)
                return false; // Si hay castigo acumulado, solo puede responder con +2 o +4
        }
        else if (!card.CanPlayOn(TopCard, ActiveColor))
        {
            return false;
        }

        player.Hand.Remove(card);
        DiscardPile.Add(card);
        ConsecutivePasses = 0; // Se resetea el contador de pases

        ActiveColor = card.Color == CardColor.Wild ? (chosenColor ?? CardColor.Red) : card.Color;

        switch (card.Value)
        {
            case CardValue.DrawTwo:
                PendingDrawCards += 2;
                break;
            case CardValue.WildDrawFour:
                PendingDrawCards += 4;
                break;
            case CardValue.Skip:
                NextTurn();
                break;
            case CardValue.Reverse:
                Direction *= -1;
                break;
        }

        NextTurn();
        return true;
    }

    public void ResolvePendingDraws(Player player)
    {
        for (int i = 0; i < PendingDrawCards; i++)
        {
            Card? drawn = DrawCardFromPile();
            if (drawn != null) player.Hand.Add(drawn);
        }
        PendingDrawCards = 0;
        NextTurn();
    }

    public int CalculatePlayerPoints(Player player)
    {
        int score = 0;
        foreach (var card in player.Hand)
        {
            score += card.Value switch
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
        return score;
    }

    public void NextTurn()
    {
        CurrentPlayerIndex = (CurrentPlayerIndex + Direction + Players.Count) % Players.Count;
    }
    
}