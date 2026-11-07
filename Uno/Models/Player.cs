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

  
    public Player(int id, string name)
    {
        Name = name;
        ID = id;
    } 
}
