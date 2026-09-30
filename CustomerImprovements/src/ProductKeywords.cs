using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SMTCustomerImprovements
{
    // Products have no categories, so kinds of products (alcohol, snacks) are found by keywords in the product's name
    // (in the player's language), the name of its model and its brand.
    static class ProductKeywords
    {
        static readonly Dictionary<string, string> lastLogs = new Dictionary<string, string>();

        // Products the store sells that match any keyword in these comma-separated lists
        public static List<int> Find(string what, params string[] lists)
        {
            var keywords = lists.SelectMany(Keywords).ToList();
            var listing = ProductListing.Instance;
            var found = new List<int>();
            var names = new List<string>();
            if (listing == null || keywords.Count == 0) return found;

            foreach (int id in listing.availableProducts)
            {
                if (id < 0 || id >= listing.productsData.Length) continue;
                var data = listing.productsData[id];
                string name = LocalizationManager.instance != null ? LocalizationManager.instance.GetLocalizationString("product" + id) : "";
                string text = Words(name) + " " + Words(data.productPrefab != null ? data.productPrefab.name : "") + " " + Words(data.productBrand);
                if (!keywords.Any(k => k.IsMatch(text))) continue;
                found.Add(id);
                names.Add(name);
            }

            // Listed once in the BepInEx log whenever it changes, to help tune the keywords
            string log = string.Join(", ", names);
            if (!lastLogs.TryGetValue(what, out var last) || log != last)
            {
                lastLogs[what] = log;
                CustomerImprovementsPlugin.Log.LogInfo($"{what} ({found.Count}): {log}");
            }
            return found;
        }

        // "beer" also matches "beers" and "Beer_Can01", but "gin" doesn't match "original"
        static IEnumerable<Regex> Keywords(string list) =>
            list.Split(',').Select(k => k.Trim().ToLowerInvariant()).Where(k => k.Length > 0)
                .Select(k => new Regex(@"\b" + Regex.Escape(k) + @"(s|es)?\b"));

        static string Words(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return Regex.Replace(name, @"[_\-\.\d]+", " ").ToLowerInvariant();
        }
    }
}
