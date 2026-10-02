using System;

namespace Uno.Models;

public enum CardColor { Red, Yellow, Green, Blue, Wild }
public enum CardValue { Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine, Skip, Reverse, DrawTwo, Wild, WildDrawFour }

public class Card
{
    public CardColor Color { get; set; }
    public CardValue Value { get; set; }

    public string ImagePath => GetImagePath();
    public Action<Card>? PlayAction { get; set; }

    public Card(CardColor color, CardValue value)
    {
        Color = color;
        Value = value;
    }

    public bool CanPlayOn(Card topCard, CardColor currentActiveColor)
    {
        if (Color == CardColor.Wild) return true;
        if (Color == currentActiveColor) return true;
        if (Value == topCard.Value) return true;
        return false;
    }
    public void OnCardClicked()
    {
        PlayAction?.Invoke(this);
    }

    private string GetImagePath()
    {
        if (Color == CardColor.Wild)
        {
            return Value == CardValue.Wild
                ? "avares://Uno/Assets/Cards/wild.png"
                : "avares://Uno/Assets/Cards/wild_drawfour.png";
        }

        string colorStr = Color.ToString().ToLower();
        string valStr = Value switch
        {
            CardValue.Zero => "0",
            CardValue.One => "1",
            CardValue.Two => "2",
            CardValue.Three => "3",
            CardValue.Four => "4",
            CardValue.Five => "5",
            CardValue.Six => "6",
            CardValue.Seven => "7",
            CardValue.Eight => "8",
            CardValue.Nine => "9",
            CardValue.Skip => "_skip",
            CardValue.Reverse => "_reverse",
            CardValue.DrawTwo => "_drawtwo",
            _ => ""
        };

        return $"avares://Uno/Assets/Cards/{colorStr}{valStr}.png";
    }

    public override string ToString() => $"{Color} {Value}";
}