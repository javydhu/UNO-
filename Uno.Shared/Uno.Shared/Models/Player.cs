using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Uno;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Uno.Shared.Models;
    
public class Player
{
    public string Name { get; set; }
    public int Id { get; set; }

    public ObservableCollection<Card> Hand { get; set; } = new();
    
    public Player(int id, string name)
    {
        Name = name;
        Id = id;
    } 
}
