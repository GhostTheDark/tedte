using UnityEngine;

namespace RustlikeClient.Items
{
    [System.Serializable]
    public class ItemData
    {
        public int id;
        public string itemName;
        public string description;
        public Sprite icon;
        public int maxStack;
        public bool isConsumable;
    }
}