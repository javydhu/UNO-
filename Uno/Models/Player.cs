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
    public ObservableCollection<Card> Hand { get; set; } = new();

    public Player(string name)
    {
        Name = name;
    } 
}
