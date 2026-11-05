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
    public int PartidaId { get; }

    public bool IsStarted { get; private set; } = false;
    public bool IsFinished { get; private set; } = false;

    public Player? Winner { get; private set; }

    public bool HasDrawnThisTurn { get; private set; } = false;
    private Guid? DrawnCardThisTurnId { get; set; }
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

        if (players.Select(p => p.Id).Distinct().Count() != players.Count)
            throw new ArgumentException(
                "No puede haber jugadores con identificadores repetidos.");

        Players = players.ToList();
        PartidaId = id;

        InitializeDeck();
    }

    // INICIALIZACIÓN DEL MAZO

    private void InitializeDeck()
    {
        DrawPile.Clear();
        DiscardPile.Clear();

        foreach (CardColor color in Enum.GetValues(typeof(CardColor)))
        {
            if (color == CardColor.Wild)
                continue;

            // Un cero por color.
            DrawPile.Add(new Card(color, CardValue.Zero));

            // Dos cartas de cada valor del 1 al 9
            // y dos de cada carta especial.
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

            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }
    }

    // INICIAR PARTIDA

    public void StartGame()
    {
        if (IsStarted)
            throw new InvalidOperationException(
                "La partida ya ha sido iniciada.");

        // Reiniciar el estado de los jugadores.
        foreach (Player player in Players)
        {
            player.Hand.Clear();
        }

        DiscardPile.Clear();

        CurrentPlayerIndex = 0;
        Direction = 1;
        PendingDrawCards = 0;

        Winner = null;
        IsFinished = false;

        HasDrawnThisTurn = false;
        DrawnCardThisTurnId = null;

        // Repartir siete cartas a cada jugador.
        foreach (Player player in Players)
        {
            for (int i = 0; i < 7; i++)
            {
                Card? drawn = DrawCardFromPile();

                if (drawn != null)
                    player.Hand.Add(drawn);
            }
        }

        // La carta inicial será numérica.
        Card firstCard;

        do
        {
            firstCard = DrawCardFromPile()
                ?? throw new InvalidOperationException(
                    "No hay cartas suficientes para iniciar la partida.");

            if (firstCard.Color == CardColor.Wild ||
                !IsNumberCard(firstCard.Value))
            {
                DrawPile.Add(firstCard);
                Shuffle();
            }

        } while (firstCard.Color == CardColor.Wild ||
                 !IsNumberCard(firstCard.Value));

        DiscardPile.Add(firstCard);

        ActiveColor = firstCard.Color;

        IsStarted = true;
    }

    private bool IsNumberCard(CardValue value)
    {
        return value is CardValue.Zero
            or CardValue.One
            or CardValue.Two
            or CardValue.Three
            or CardValue.Four
            or CardValue.Five
            or CardValue.Six
            or CardValue.Seven
            or CardValue.Eight
            or CardValue.Nine;
    }

    // ROBAR CARTAS

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

        if (!IsStarted || IsFinished || player == null)
            return false;

        if (PendingDrawCards > 0 || HasDrawnThisTurn)
            return false;

        Player? actualPlayer = Players.FirstOrDefault(
            p => p.Id == player.Id);

        if (actualPlayer == null ||
            actualPlayer.Id != CurrentPlayer.Id)
        {
            return false;
        }

        drawnCard = DrawCardFromPile();

        if (drawnCard == null)
            return false;

        actualPlayer.Hand.Add(drawnCard);

        HasDrawnThisTurn = true;
        DrawnCardThisTurnId = drawnCard.Id;

        return true;
    }
    public bool ResolvePendingDraws(
        Player player,
        out List<Card> drawnCards)
    {
        drawnCards = new List<Card>();

        if (!IsStarted || IsFinished ||
            player == null || PendingDrawCards <= 0)
        {
            return false;
        }

        Player? actualPlayer = Players.FirstOrDefault(
            p => p.Id == player.Id);

        if (actualPlayer == null ||
            actualPlayer.Id != CurrentPlayer.Id)
        {
            return false;
        }
        
        for (int i = 0; i < PendingDrawCards; i++)
        {
            Card? drawn = DrawCardFromPile();

            if (drawn == null)
                break;

            actualPlayer.Hand.Add(drawn);
            drawnCards.Add(drawn);
        }

        PendingDrawCards = 0;

        HasDrawnThisTurn = false;
        DrawnCardThisTurnId = null;
        
        AdvanceTurn();

        return true;
    }
    
    public bool ResolvePendingDraws(Player player)
    {
        return ResolvePendingDraws(player, out _);
    }

    // JUGAR CARTAS

    public bool PlayCard(
        Player player,
        Card card,
        CardColor? chosenColor = null)
    {
        if (!IsStarted || IsFinished ||
            player == null || card == null)
        {
            return false;
        }

        // Buscar al jugador real dentro de la partida.
        Player? actualPlayer = Players.FirstOrDefault(
            p => p.Id == player.Id);

        if (actualPlayer == null ||
            actualPlayer.Id != CurrentPlayer.Id)
        {
            return false;
        }

        // Buscar la carta real dentro de su mano.
        Card? ownedCard = actualPlayer.Hand.FirstOrDefault(
            c => c.Id == card.Id);

        if (ownedCard == null)
            return false;

        // Si ya robó una carta durante este turno,
        if (HasDrawnThisTurn &&
            ownedCard.Id != DrawnCardThisTurnId)
        {
            return false;
        }

        if (ownedCard.Color == CardColor.Wild)
        {
            if (!chosenColor.HasValue ||
                chosenColor.Value == CardColor.Wild ||
                !Enum.IsDefined(
                    typeof(CardColor),
                    chosenColor.Value))
            {
                return false;
            }
        }
        else if (chosenColor.HasValue)
        {
            return false;
        }

        // Validación de penalizaciones acumuladas.
        if (PendingDrawCards > 0)
        {
            if (ownedCard.Value != CardValue.DrawTwo &&
                ownedCard.Value != CardValue.WildDrawFour)
            {
                return false;
            }
        }
        else if (!ownedCard.CanPlayOn(TopCard, ActiveColor))
        {
            return false;
        }

        // Jugada valida
        
        actualPlayer.Hand.Remove(ownedCard);

        DiscardPile.Add(ownedCard);

        HasDrawnThisTurn = false;
        DrawnCardThisTurnId = null;

        // Determinar el color activo.
        ActiveColor = ownedCard.Color == CardColor.Wild
            ? chosenColor!.Value
            : ownedCard.Color;

        // Aplicar el efecto de la carta.
        bool skipNextPlayer = false;

        switch (ownedCard.Value)
        {
            case CardValue.DrawTwo:

                PendingDrawCards += 2;
                break;

            case CardValue.WildDrawFour:

                PendingDrawCards += 4;
                break;

            case CardValue.Skip:

                skipNextPlayer = true;
                break;

            case CardValue.Reverse:

                Direction *= -1;
                
                if (Players.Count == 2)
                    skipNextPlayer = true;

                break;
        }

        // Comprobar si el jugador ganó.
        if (actualPlayer.Hand.Count == 0)
        {
            Winner = actualPlayer;
            IsFinished = true;

            return true;
        }

        // Aplicar los cambios de turno.
        if (skipNextPlayer)
            AdvanceTurn();

        AdvanceTurn();

        return true;
    }

    // PASAR TURNO

    public bool PassTurn(Player player)
    {
        if (!IsStarted || IsFinished ||
            player == null || !HasDrawnThisTurn)
        {
            return false;
        }

        Player? actualPlayer = Players.FirstOrDefault(
            p => p.Id == player.Id);

        if (actualPlayer == null ||
            actualPlayer.Id != CurrentPlayer.Id)
        {
            return false;
        }

        Card? drawnCard = actualPlayer.Hand.FirstOrDefault(
            c => c.Id == DrawnCardThisTurnId);

        if (drawnCard == null)
            return false;

        // Si la carta robada se puede jugar,
        if (drawnCard.CanPlayOn(TopCard, ActiveColor))
            return false;

        HasDrawnThisTurn = false;
        DrawnCardThisTurnId = null;

        AdvanceTurn();

        return true;
    }

    
    // CAMBIO DE TURNO

    private void AdvanceTurn()
    {
        CurrentPlayerIndex =
            (CurrentPlayerIndex + Direction + Players.Count)
            % Players.Count;
    }

    // SISTEMA DE PUNTUACIÓN

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

                CardValue.Skip => 20,
                CardValue.Reverse => 20,
                CardValue.DrawTwo => 20,

                CardValue.Wild => 50,
                CardValue.WildDrawFour => 50,

                _ => 0
            };
        }

        return score;
    }
}