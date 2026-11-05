using System;
using System.Collections.Generic;
using System.Linq;

namespace Uno.Shared.Models;

public class UnoGame
{
    public List<Player> Players { get; private set; } = new();

    public List<Card> DrawPile { get; private set; } = new();

    public List<Card> DiscardPile { get; private set; } = new();

    public int CurrentPlayerIndex { get; private set; } = 0;

    public int Direction { get; private set; } = 1;

    public CardColor ActiveColor { get; private set; }

    public int PendingDrawCards { get; private set; } = 0;

    public int ConsecutivePasses { get; private set; } = 0;

    public int PartidaId { get; }

    public bool IsStarted { get; private set; } = false;

    public bool IsFinished { get; private set; } = false;

    public Player? Winner { get; private set; }
    public bool HasShoutedUno { get; private set; } = false;
    
    public bool? LastUnoDecision { get; private set; }
    public bool LastUnoPenaltyApplied { get; private set; } = false;

    public int? LastUnoPenaltyPlayerId { get; private set; }

    public List<Card> LastUnoPenaltyCards { get; private set; } = new();
    public Card TopCard
    {
        get
        {
            if (DiscardPile.Count == 0)
                throw new InvalidOperationException(
                    "La partida todavía no tiene una carta inicial.");

            return DiscardPile[^1];
        }
    }

    public Player CurrentPlayer
    {
        get
        {
            if (!IsStarted || Players.Count == 0)
                throw new InvalidOperationException(
                    "La partida todavía no ha comenzado.");

            return Players[CurrentPlayerIndex];
        }
    }

    public UnoGame(List<Player> players, int id)
    {
        ArgumentNullException.ThrowIfNull(players);

        if (players.Count < 2 || players.Count > 10)
            throw new ArgumentException(
                "Una partida debe tener entre 2 y 10 jugadores.");

        if (players.Any(p => p == null))
            throw new ArgumentException(
                "La lista contiene jugadores inválidos.");

        if (players.Select(p => p.Id).Distinct().Count() != players.Count)
            throw new ArgumentException(
                "No puede haber jugadores con identificadores repetidos.");

        Players = players.ToList();

        PartidaId = id;

        InitializeDeck();
    }
    
    private void InitializeDeck()
    {
        DrawPile.Clear();
        DiscardPile.Clear();

        foreach (CardColor color in Enum.GetValues(typeof(CardColor)))
        {
            if (color == CardColor.Wild)
                continue;
            DrawPile.Add(new Card(color, CardValue.Zero));

            
            for (int i = 0; i < 2; i++)
            {
                foreach (CardValue value in Enum.GetValues(typeof(CardValue)))
                {
                    if (value == CardValue.Zero ||
                        value == CardValue.Wild ||
                        value == CardValue.WildDrawFour)
                    {
                        continue;
                    }

                    DrawPile.Add(new Card(color, value));
                }
            }
        }
        
        for (int i = 0; i < 4; i++)
        {
            DrawPile.Add(new Card(CardColor.Wild, CardValue.Wild));

            DrawPile.Add(new Card(
                CardColor.Wild,
                CardValue.WildDrawFour));
        }

        Shuffle();

        if (DrawPile.Count != 108)
            throw new InvalidOperationException(
                "El mazo debe contener exactamente 108 cartas.");
    }

    private void Shuffle()
    {
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);

            (DrawPile[i], DrawPile[j]) =
                (DrawPile[j], DrawPile[i]);
        }
    }

    public void StartGame()
    {
        if (IsStarted)
            throw new InvalidOperationException(
                "La partida ya fue iniciada.");

        foreach (Player player in Players)
        {
            player.Hand.Clear();
        }

        CurrentPlayerIndex = 0;
        Direction = 1;

        PendingDrawCards = 0;
        ConsecutivePasses = 0;

        Winner = null;
        IsFinished = false;

        HasShoutedUno = false;

        LastUnoDecision = null;
        LastUnoPenaltyApplied = false;
        LastUnoPenaltyPlayerId = null;
        LastUnoPenaltyCards.Clear();

        foreach (Player player in Players)
        {
            for (int i = 0; i < 7; i++)
            {
                Card? drawn = DrawCardFromPile();

                if (drawn != null)
                    player.Hand.Add(drawn);
            }
        }
        
        Card firstCard;

        do
        {
            firstCard = DrawCardFromPile()
                ?? throw new InvalidOperationException(
                    "No hay cartas suficientes para iniciar la partida.");

            DiscardPile.Add(firstCard);

        } while (firstCard.Color == CardColor.Wild);

        ActiveColor = firstCard.Color;

        IsStarted = true;
    }
    

    private Player? FindCurrentPlayer(Player player)
    {
        if (!IsStarted || IsFinished || player == null)
            return null;

        Player? registeredPlayer = Players.FirstOrDefault(
            p => p.Id == player.Id);

        if (registeredPlayer == null)
            return null;

        if (registeredPlayer.Id != CurrentPlayer.Id)
            return null;

        return registeredPlayer;
    }
    

    public bool CanPlayCard(Player player, Card card)
    {
        if (player == null || card == null)
            return false;

        Player? actualPlayer = FindCurrentPlayer(player);

        if (actualPlayer == null)
            return false;

        Card? ownedCard = actualPlayer.Hand.FirstOrDefault(
            c => c.Id == card.Id);

        if (ownedCard == null)
            return false;

        if (PendingDrawCards > 0)
        {
            return ownedCard.Value == CardValue.DrawTwo ||
                   ownedCard.Value == CardValue.WildDrawFour;
        }

        return ownedCard.CanPlayOn(TopCard, ActiveColor);
    }
    

    public bool CanShoutUno(Player player)
    {
        if (HasShoutedUno)
            return false;

        Player? actualPlayer = FindCurrentPlayer(player);

        if (actualPlayer == null)
            return false;

        // Debe tener exactamente dos cartas.
        if (actualPlayer.Hand.Count != 2)
            return false;

        // Al menos una de sus cartas debe ser jugable.
        return actualPlayer.Hand.Any(card =>
            CanPlayCard(actualPlayer, card));
    }
    
    public bool ShoutUno(Player player)
    {
        if (!CanShoutUno(player))
            return false;

        HasShoutedUno = true;

        return true;
    }
    private Card? DrawCardFromPile()
    {
        if (DrawPile.Count == 0)
            return null;

        Card drawn = DrawPile[0];

        DrawPile.RemoveAt(0);

        return drawn;
    }
    public bool DrawCard(Player player, out Card? drawnCard)
    {
        drawnCard = null;

        Player? actualPlayer = FindCurrentPlayer(player);

        if (actualPlayer == null)
            return false;

        // No puede robar normalmente si tiene
        // una penalización pendiente.
        if (PendingDrawCards > 0)
            return false;

        drawnCard = DrawCardFromPile();

        if (drawnCard == null)
            return false;

        actualPlayer.Hand.Add(drawnCard);

        return true;
    }

    // Jugar cartas

    public bool PlayCard(
        Player player,
        Card card,
        CardColor? chosenColor = null)
    {
        LastUnoDecision = null;

        LastUnoPenaltyApplied = false;

        LastUnoPenaltyPlayerId = null;

        LastUnoPenaltyCards.Clear();

        Player? actualPlayer = FindCurrentPlayer(player);

        if (actualPlayer == null || card == null)
            return false;

        Card? ownedCard = actualPlayer.Hand.FirstOrDefault(
            c => c.Id == card.Id);

        if (ownedCard == null)
            return false;

        // Validar el color elegido.
        if (ownedCard.Color == CardColor.Wild)
        {
            if (chosenColor == CardColor.Wild)
                return false;

            if (chosenColor.HasValue &&
                !Enum.IsDefined(typeof(CardColor), chosenColor.Value))
            {
                return false;
            }
        }
        else if (chosenColor.HasValue)
        {
            return false;
        }

        // Validar si la carta se puede jugar.
        if (!CanPlayCard(actualPlayer, ownedCard))
            return false;

        int cardsBeforePlay = actualPlayer.Hand.Count;

        if (cardsBeforePlay == 2)
        {
            LastUnoDecision = HasShoutedUno;

            if (!HasShoutedUno)
            {
                LastUnoPenaltyApplied = true;

                LastUnoPenaltyPlayerId = actualPlayer.Id;
            }
        }

        // Eliminar la carta de la mano.
        actualPlayer.Hand.Remove(ownedCard);

        DiscardPile.Add(ownedCard);
        ConsecutivePasses = 0;
        
        HasShoutedUno = false;

        // Cambiar el color activo.
        ActiveColor = ownedCard.Color == CardColor.Wild
            ? (chosenColor ?? CardColor.Red)
            : ownedCard.Color;
        
        switch (ownedCard.Value)
        {
            case CardValue.DrawTwo:

                PendingDrawCards += 2;

                break;

            case CardValue.WildDrawFour:

                PendingDrawCards += 4;

                break;

            case CardValue.Skip:

                // Saltar al siguiente jugador.
                AdvanceTurn();

                break;

            case CardValue.Reverse:

                // Cambiar dirección.
                Direction *= -1;

                break;
        }

        // Pasar al siguiente turno.
        AdvanceTurn();
        

        if (cardsBeforePlay == 2 && !LastUnoDecision.Value)
        {
            for (int i = 0; i < 2; i++)
            {
                Card? penaltyCard = DrawCardFromPile();

                if (penaltyCard == null)
                    break;

                actualPlayer.Hand.Add(penaltyCard);

                LastUnoPenaltyCards.Add(penaltyCard);
            }
        }
        

        if (actualPlayer.Hand.Count == 0)
        {
            Winner = actualPlayer;

            IsFinished = true;
        }

        return true;
    }
    

    public bool ResolvePendingDraws(
        Player player,
        out List<Card> drawnCards)
    {
        drawnCards = new List<Card>();

        Player? actualPlayer = FindCurrentPlayer(player);

        if (actualPlayer == null || PendingDrawCards <= 0)
            return false;

        // Robar las cartas acumuladas.
        for (int i = 0; i < PendingDrawCards; i++)
        {
            Card? drawn = DrawCardFromPile();

            if (drawn == null)
                break;

            actualPlayer.Hand.Add(drawn);

            drawnCards.Add(drawn);
        }

        // Reiniciar penalización.
        PendingDrawCards = 0;

        // Reiniciar estado de UNO.
        HasShoutedUno = false;

        // El jugador pierde su turno.
        AdvanceTurn();

        return true;
    }
    public bool ResolvePendingDraws(Player player)
    {
        return ResolvePendingDraws(player, out _);
    }

    public bool CanPassTurn()
    {
        return IsStarted &&
               !IsFinished &&
               DrawPile.Count == 0 &&
               PendingDrawCards == 0;
    }

    public bool PassTurn(Player player)
    {
        Player? actualPlayer = FindCurrentPlayer(player);

        if (actualPlayer == null || !CanPassTurn())
            return false;

        // Aumentar pases consecutivos.
        ConsecutivePasses++;

        // Reiniciar estado de UNO.
        HasShoutedUno = false;

        // Si todos pasaron, terminar por puntuación.
        if (ConsecutivePasses >= Players.Count)
        {
            FinishGameByPoints();

            return true;
        }

        // Pasar al siguiente jugador.
        AdvanceTurn();

        return true;
    }
    

    private void FinishGameByPoints()
    {
        var scores = Players
            .Select(player => new
            {
                Player = player,
                Score = CalculatePlayerPoints(player)
            })
            .OrderBy(x => x.Score)
            .ToList();

        Winner = scores.First().Player;

        IsFinished = true;
    }
    

    private void AdvanceTurn()
    {
        CurrentPlayerIndex =
            (CurrentPlayerIndex + Direction + Players.Count)
            % Players.Count;
    }

    public int CalculatePlayerPoints(Player player)
    {
        int score = 0;

        foreach (Card card in player.Hand)
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
                
                _ => 0
            };
        }

        return score;
    }
}