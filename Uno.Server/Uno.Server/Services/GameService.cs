using System;
using System.Linq;
using System.Threading.Tasks;
using Uno.Shared.Models;

namespace Uno.Server.Services;

public class GameService
{
    private readonly PythonApiService _pythonApi;

    public GameService(PythonApiService pythonApi)
    {
        _pythonApi = pythonApi;
    }

    public async Task<ActionResultMessage> ProcessActionAsync(
        UnoGame game,
        Player player,
        NetworkMessage request)
    {
        switch (request.Type)
        {
         //Jugar carta

            case "play_card":
            {
                PlayCardRequest? data =
                    request.GetData<PlayCardRequest>();

                if (data == null)
                    return Reject("Solicitud inválida.");

                Card? card = player.Hand.FirstOrDefault(
                    c => c.Id == data.CardId);

                if (card == null)
                    return Reject("La carta no pertenece a tu mano.");

                bool accepted = game.PlayCard(
                    player,
                    card,
                    data.ChosenColor);

                if (!accepted)
                    return Reject("La jugada no es válida.");

                await TryRegisterAsync(() =>
                    _pythonApi.RegistrarJugadaEnServidorAsync(
                        player,
                        game.PartidaId,
                        card,
                        data.ChosenColor));

                if (game.LastUnoDecision == false)
                {
                    await TryRegisterAsync(() =>
                        _pythonApi.RegistrarUnoAsync(
                            player,
                            game.PartidaId,
                            false));

                    foreach (Card penaltyCard in game.LastUnoPenaltyCards)
                    {
                        await TryRegisterAsync(() =>
                            _pythonApi.RegistrarRoboEnServidorAsync(
                                player,
                                game.PartidaId,
                                penaltyCard));
                    }
                }

                if (game.IsFinished && game.Winner != null)
                {
                    await TryRegisterAsync(() =>
                        _pythonApi.RegistrarVictoriaEnServidorAsync(
                            game.Winner,
                            game.PartidaId));
                }

                string message = game.IsFinished
                    ? $"¡{game.Winner?.Name} ganó la partida!"
                    : "Carta jugada correctamente.";

                if (game.LastUnoPenaltyApplied)
                {
                    message +=
                        $" Olvidaste decir UNO y recibiste " +
                        $"{game.LastUnoPenaltyCards.Count} cartas de castigo.";
                }

                return Accept(message);
            }

            // Robar carta

            case "draw_card":
            {
                bool accepted = game.DrawCard(
                    player,
                    out Card? drawnCard);

                if (!accepted || drawnCard == null)
                {
                    return Reject(
                        "No puedes robar una carta en este momento.");
                }

                await TryRegisterAsync(() =>
                    _pythonApi.RegistrarRoboEnServidorAsync(
                        player,
                        game.PartidaId,
                        drawnCard));

                return Accept("Carta robada correctamente.");
            }

            // Resolver penalizacion

            case "resolve_penalty":
            {
                bool accepted = game.ResolvePendingDraws(
                    player,
                    out var drawnCards);

                if (!accepted)
                {
                    return Reject(
                        "No puedes resolver una penalización.");
                }

                await TryRegisterAsync(() =>
                    _pythonApi.RegistrarPenalizacionEnServidorAsync(
                        player,
                        game.PartidaId));

                return Accept(
                    $"Has robado {drawnCards.Count} cartas.");
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

                await TryRegisterAsync(() =>
                    _pythonApi.RegistrarPaseDeTurnoEnServidorAsync(
                        player,
                        game.PartidaId));

                if (game.IsFinished && game.Winner != null)
                {
                    await TryRegisterAsync(() =>
                        _pythonApi.RegistrarMazoAgotadoAsync(
                            game.Winner,
                            game.PartidaId));

                    await TryRegisterAsync(() =>
                        _pythonApi.RegistrarVictoriaEnServidorAsync(
                            game.Winner,
                            game.PartidaId));
                }

                return Accept(
                    game.IsFinished
                        ? $"Partida terminada. Ganador: {game.Winner?.Name}"
                        : "Turno pasado correctamente.");
            }

            // Decir UNO

            case "shout_uno":
            {
                bool accepted = game.ShoutUno(player);

                if (!accepted)
                {
                    return Reject(
                        "No puedes declarar UNO en este momento.");
                }

                await TryRegisterAsync(() =>
                    _pythonApi.RegistrarUnoAsync(
                        player,
                        game.PartidaId,
                        true));

                return Accept($"{player.Name} ha dicho UNO.");
            }

            default:

                return Reject("Acción desconocida.");
        }
    }

    // Registro sin afectar la partida

    private static async Task TryRegisterAsync(
        Func<Task> registerAction)
    {
        try
        {
            await registerAction();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"No se pudo registrar en Python: {ex.Message}");
        }
    }

    private static ActionResultMessage Accept(string message)
    {
        return new ActionResultMessage
        {
            Accepted = true,
            Message = message
        };
    }

    private static ActionResultMessage Reject(string message)
    {
        return new ActionResultMessage
        {
            Accepted = false,
            Message = message
        };
    }
}