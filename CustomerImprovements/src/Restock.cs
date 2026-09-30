using System.Collections.Generic;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Host: puts products back into stock. Each one goes on a shelf row that already holds that product and has
    // room, otherwise into a storage box of that product that isn't full, otherwise into a new box in an empty
    // storage slot, labelled for that product first. Uses the same calls employees use, so every player sees it.
    static class Restock
    {
        public struct Result
        {
            public int shelved, stored, lost;
        }

        public static Result Return(IEnumerable<int> products)
        {
            var result = new Result();
            var manager = NPC_Manager.Instance;
            foreach (int id in products)
            {
                if (ToShelf(manager, id)) result.shelved++;
                else if (ToStorage(manager, id)) result.stored++;
                else result.lost++;
            }
            return result;
        }

        static bool ToShelf(NPC_Manager manager, int id)
        {
            foreach (Transform child in manager.shelvesOBJ.transform)
            {
                var shelf = child.GetComponent<Data_Container>();
                if (shelf == null) continue;
                var rows = shelf.productInfoArray;
                for (int row = 0; row + 1 < rows.Length; row += 2)
                {
                    if (rows[row] != id || rows[row + 1] < 0 || rows[row + 1] >= RowCapacity(shelf, id)) continue;
                    shelf.EmployeeAddsItemToRow(row, 1);
                    return true;
                }
            }
            return false;
        }

        // How many of a product fit one shelf row, worked out the way the game does when a player fills it
        static int RowCapacity(Data_Container shelf, int id)
        {
            var data = ProductListing.Instance.productsData[id];
            var size = data.colliderSize;
            int across = Mathf.Clamp(Mathf.FloorToInt(shelf.shelfLength / (size.x * 1.1f)), 1, 100);
            int deep = Mathf.Clamp(Mathf.FloorToInt(shelf.shelfWidth / (size.z * 1.1f)), 1, 100);
            int high = data.isStackable ? Mathf.Clamp(Mathf.FloorToInt(shelf.shelfHeight / (size.y * 1.1f)), 1, 100) : 1;
            return across * deep * high;
        }

        // Storage slots hold a product ID and a box count; a count of -1 means no box, and a product ID on an
        // empty slot means it's labelled for that product
        static bool ToStorage(NPC_Manager manager, int id)
        {
            int perBox = ProductListing.Instance.productsData[id].maxItemsPerBox;
            return AddToStorage(manager, (pid, count) => pid == id && count >= 0 && count < perBox, id, count => count + 1)
                || AddToStorage(manager, (pid, count) => pid == id && count < 0, id, _ => 1)
                || AddToStorage(manager, (pid, count) => pid < 0 && count < 0, id, _ => 1);
        }

        static bool AddToStorage(NPC_Manager manager, System.Func<int, int, bool> fits, int id, System.Func<int, int> newCount)
        {
            foreach (Transform child in manager.storageOBJ.transform)
            {
                var storage = child.GetComponent<Data_Container>();
                if (storage == null) continue;
                var slots = storage.productInfoArray;
                for (int slot = 0; slot + 1 < slots.Length; slot += 2)
                {
                    if (!fits(slots[slot], slots[slot + 1])) continue;
                    storage.EmployeeUpdateArrayValuesStorage(slot, id, newCount(slots[slot + 1]));
                    return true;
                }
            }
            return false;
        }
    }
}
