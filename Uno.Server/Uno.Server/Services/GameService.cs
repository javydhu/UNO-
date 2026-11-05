using System.Linq;
using Uno.Shared.Models;

namespace Uno.Server.Services;

public class GameService
{
    public ActionResult ProcessAction(
        UnoGame game,
        Player player,
        NetworkMessage request)
    {
        switch (request.Type)
        {
            // Jugar una carta
            case "play_card":
            {
                PlayCardRequest? data =
                    request.GetData<PlayCardRequest>();

                if (data == null)
                {
                    return Reject("Solicitud de jugada inválida.");
                }

                // Buscar la carta real dentro de la mano.
                Card? card = player.Hand.FirstOrDefault(
                    c => c.Id == data.CardId);

                if (card == null)
                {
                    return Reject(
                        "La carta no pertenece a tu mano.");
                }

                bool accepted = game.PlayCard(
                    player,
                    card,
                    data.ChosenColor);

                if (!accepted)
                {
                    return Reject(
                        "La jugada no es válida.");
                }

                if (game.IsFinished)
                {
                    return Accept(
                        "Jugada aceptada. ¡Tenemos un ganador!");
                }

                if (game.LastUnoPenaltyApplied)
                {
                    return Accept(
                        $"Olvidaste decir UNO. " +
                        $"Recibiste {game.LastUnoPenaltyCards.Count} cartas de castigo.");
                }

                return Accept("Carta jugada correctamente.");
            }

            // Robar una carta
            

            case "draw_card":
            {
                bool accepted = game.DrawCard(
                    player,
                    out Card? drawnCard);

                if (!accepted)
                {
                    return Reject(
                        "No puedes robar una carta en este momento.");
                }

                return Accept("Carta robada correctamente.");
            }

            // Tomar penalizacion

            case "resolve_penalty":
            {
                bool accepted = game.ResolvePendingDraws(
                    player,
                    out var drawnCards);

                if (!accepted)
                {
                    return Reject(
                        "No puedes tomar una penalización en este momento.");
                }

                return Accept(
                    $"Has robado {drawnCards.Count} cartas de penalización.");
            }

            
            // Pasar turno

            case "pass_turn":
            {
                bool accepted = game.PassTurn(player);

                if (!accepted)
                {
                    return Reject(
                        "No puedes pasar el turno en este momento.");
                }

                if (game.IsFinished)
                {
                    return Accept(
                        "Todos los jugadores pasaron. " +
                        "La partida terminó por puntuación.");
                }

                return Accept("Turno pasado correctamente.");
            }

            // Declarar uno

            case "shout_uno":
            {
                bool accepted = game.ShoutUno(player);

                if (!accepted)
                {
                    return Reject(
                        "No puedes declarar UNO en este momento.");
                }

                return Accept(
                    $"{player.Name} ha declarado UNO.");
            }

            
            //Por si no coincide ninguna solicitud

            default:

                return Reject(
                    "No se reconoce el tipo de solicitud.");
        }
    }

    private ActionResult Accept(string message)
    {
        return new ActionResult
        {
            Accepted = true,
            Message = message
        };
    }

    private ActionResult Reject(string message)
    {
        return new ActionResult
        {
            Accepted = false,
            Message = message
        };
    }
}