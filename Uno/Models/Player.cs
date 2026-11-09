using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Uno.Models;
    
public class Player
{
    public string Name { get; set; }
    public int ID { get; set; }

    public ObservableCollection<Card> Mano { get; set; } = new();
    
    public string ColorHex { get; set; } = "#FFD60A";
    public bool EsTurno { get; set; }
    public bool TieneUnaCarta { get; set; }
    public string Inicial => InicialDe(Name);

    public static string InicialDe(string? nombre)
    {
        string n = (nombre ?? string.Empty).Trim();
        return n.Length == 0 ? "?" : n.Substring(0, 1).ToUpperInvariant();
    }
  
    public Player(int id, string name)
    {
        Name = name;
        ID = id;
    } 
}