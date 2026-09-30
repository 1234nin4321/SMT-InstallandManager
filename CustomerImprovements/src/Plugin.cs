using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SMTCustomerImprovements
{
    [BepInPlugin(Guid, Name, Version)]
    public class CustomerImprovementsPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.customerimprovements";
        public const string Name = "SMT Customer Improvements";
        public const string Version = "0.6.0";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> FraudChance;
        internal static ConfigEntry<float> EmployeeBaseCatchChance;
        internal static ConfigEntry<float> CatchChancePerSecurityValue;
        internal static ConfigEntry<float> CatchChancePerSecurityLevel;
        internal static ConfigEntry<bool> ShowHint;
        internal static ConfigEntry<KeyboardShortcut> CheckPayment;
        internal static ConfigEntry<bool> AnnounceInChat;
        internal static ConfigEntry<bool> FamiliesEnabled;
        internal static ConfigEntry<float> FamilyChance;
        internal static ConfigEntry<int> MaxChildren;
        internal static ConfigEntry<int> ExtraItemsPerChild;
        internal static ConfigEntry<float> ChildSize;
        internal static ConfigEntry<bool> ChildrenTalk;
        internal static ConfigEntry<bool> ShowCustomerCount;
        internal static ConfigEntry<bool> PartiesEnabled;
        internal static ConfigEntry<float> PartyChance;
        internal static ConfigEntry<int> PartyMinSize;
        internal static ConfigEntry<int> PartyMaxSize;
        internal static ConfigEntry<int> PartyItemsEach;
        internal static ConfigEntry<string> AlcoholKeywords;
        internal static ConfigEntry<string> SnackKeywords;
        internal static ConfigEntry<bool> PartyAura;
        internal static ConfigEntry<bool> PartyShouts;
        internal static ConfigEntry<KeyboardShortcut> PartyNow;
        internal static ConfigEntry<bool> HobosEnabled;
        internal static ConfigEntry<float> HoboChance;
        internal static ConfigEntry<float> HoboStealChance;
        internal static ConfigEntry<float> HoboFraudChance;
        internal static ConfigEntry<float> HoboTrashEvery;
        internal static ConfigEntry<int> HoboMostTrash;
        internal static ConfigEntry<bool> HoboSmellComplaints;
        internal static ConfigEntry<bool> HoboAura;
        internal static ConfigEntry<bool> HoboMumbles;
        internal static ConfigEntry<KeyboardShortcut> HoboNow;
        internal static ConfigEntry<bool> StonersEnabled;
        internal static ConfigEntry<float> StonerChance;
        internal static ConfigEntry<int> StonerSnacks;
        internal static ConfigEntry<bool> StonerAura;
        internal static ConfigEntry<bool> StonerMumbles;
        internal static ConfigEntry<KeyboardShortcut> StonerNow;
        internal static ConfigEntry<bool> CartsEnabled;
        internal static ConfigEntry<float> CartSize;
        internal static ConfigEntry<bool> CartProducts;

        void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("Payment fraud", "Enabled", true,
                "Customers at the registers sometimes pay with fake cash or a stolen credit card (host only)");
            FraudChance = Config.Bind("Payment fraud", "Fraud chance", 0.1f,
                new ConfigDescription("Chance that a customer at a register pays with fake cash or a stolen card (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            EmployeeBaseCatchChance = Config.Bind("Payment fraud", "Employee base catch chance", 0.2f,
                new ConfigDescription("Chance that an employee at a register spots fake cash or a stolen card, before their security skills (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            CatchChancePerSecurityValue = Config.Bind("Payment fraud", "Catch chance per security point", 0.04f,
                new ConfigDescription("Added for each point of the employee's security rating, 1 to 10 (host only)",
                    new AcceptableValueRange<float>(0f, 0.1f)));
            CatchChancePerSecurityLevel = Config.Bind("Payment fraud", "Catch chance per security level", 0.005f,
                new ConfigDescription("Added for each security level the employee has earned, 1 to 100 (host only)",
                    new AcceptableValueRange<float>(0f, 0.01f)));
            ShowHint = Config.Bind("Payment fraud", "Show hint", true,
                "Fake bills and stolen cards flash in alternating colours");
            CheckPayment = Config.Bind("Payment fraud", "Check payment", new KeyboardShortcut(KeyCode.G),
                "Checks the payment of the customer at the nearest register");
            AnnounceInChat = Config.Bind("Payment fraud", "Announce in chat", true,
                "Tell everyone in the chat when a fraudster is caught or gets away (host only)");

            FamiliesEnabled = Config.Bind("Families", "Enabled", true,
                "Some customers come in with their children (host only)");
            FamilyChance = Config.Bind("Families", "Family chance", 0.2f,
                new ConfigDescription("Chance that a customer brings their children (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            MaxChildren = Config.Bind("Families", "Most children", 2,
                new ConfigDescription("Most children one customer brings (host only)", new AcceptableValueRange<int>(1, 3)));
            ExtraItemsPerChild = Config.Bind("Families", "Extra items per child", 2,
                new ConfigDescription("Things each child adds to their parent's shopping list (host only)",
                    new AcceptableValueRange<int>(0, 5)));
            ChildSize = Config.Bind("Families", "Child size", 0.6f,
                new ConfigDescription("How big children are next to adults (host only)", new AcceptableValueRange<float>(0.4f, 0.9f)));
            ChildrenTalk = Config.Bind("Families", "Children talk", true,
                "Children now and then say something above their heads (host only)");

            ShowCustomerCount = Config.Bind("Customer count", "Show", true,
                "Shows how many customers are in the store at the right side of the screen");

            PartiesEnabled = Config.Bind("Party animals", "Enabled", true,
                "Now and then a group of men comes in to buy loads of alcohol and snacks (host only)");
            PartyChance = Config.Bind("Party animals", "Party chance per minute", 0.1f,
                new ConfigDescription("Chance each minute the store is open that a party walks in (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            PartyMinSize = Config.Bind("Party animals", "Smallest party", 5,
                new ConfigDescription("Fewest people in a party (host only)", new AcceptableValueRange<int>(5, 12)));
            PartyMaxSize = Config.Bind("Party animals", "Biggest party", 8,
                new ConfigDescription("Most people in a party (host only)", new AcceptableValueRange<int>(5, 12)));
            PartyItemsEach = Config.Bind("Party animals", "Items each", 12,
                new ConfigDescription("About how many things each party-goer buys (host only)", new AcceptableValueRange<int>(2, 30)));
            AlcoholKeywords = Config.Bind("Party animals", "Alcohol keywords",
                "beer, wine, vodka, whisky, whiskey, rum, gin, tequila, champagne, cider, liquor, liqueur, brandy, cognac, lager, ale, sake, prosecco, vermouth, bourbon, alcohol",
                "Products whose name contains one of these count as alcohol (host only)");
            SnackKeywords = Config.Bind("Party animals", "Snack keywords",
                "chips, crisps, snack, nachos, popcorn, pretzel, peanut, nut, candy, chocolate, cookie, biscuit, gummy, sweets, cracker, jerky, dip, salsa",
                "Products whose name contains one of these count as snacks (host only)");
            PartyAura = Config.Bind("Party animals", "Glowing aura", true,
                "Party-goers glow with a colour-cycling aura");
            PartyShouts = Config.Bind("Party animals", "Shouts", true,
                "Party-goers now and then shout something above their heads (host only)");
            PartyNow = Config.Bind("Party animals", "Party now", new KeyboardShortcut(KeyCode.F10),
                "Sends in a party straight away (host only)");

            HobosEnabled = Config.Bind("Hobos", "Enabled", true,
                "Now and then a smelly hobo wanders in and leaves a trail of garbage (host only)");
            HoboChance = Config.Bind("Hobos", "Hobo chance per minute", 0.08f,
                new ConfigDescription("Chance each minute the store is open that a hobo wanders in (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            HoboStealChance = Config.Bind("Hobos", "Steal chance", 0.35f,
                new ConfigDescription("Chance that a hobo runs off with their shopping instead of paying (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            HoboFraudChance = Config.Bind("Hobos", "Fraud chance", 0.4f,
                new ConfigDescription("Chance that a hobo who pays uses fake cash or a stolen card (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            HoboTrashEvery = Config.Bind("Hobos", "Trash every", 8f,
                new ConfigDescription("About how many seconds between bits of garbage a hobo drops (host only)",
                    new AcceptableValueRange<float>(2f, 60f)));
            HoboMostTrash = Config.Bind("Hobos", "Most trash", 10,
                new ConfigDescription("Most garbage one hobo drops (host only)", new AcceptableValueRange<int>(0, 50)));
            HoboSmellComplaints = Config.Bind("Hobos", "Smell complaints", true,
                "Customers near a hobo complain about the smell; it counts as a complaint about filth (host only)");
            HoboAura = Config.Bind("Hobos", "Stink cloud", true,
                "A murky green and brown glow hangs around hobos");
            HoboMumbles = Config.Bind("Hobos", "Mumbles", true,
                "Hobos now and then mumble something above their heads (host only)");
            HoboNow = Config.Bind("Hobos", "Hobo now", new KeyboardShortcut(KeyCode.F11),
                "Sends in a hobo straight away (host only)");

            StonersEnabled = Config.Bind("Stoners", "Enabled", true,
                "Now and then a customer high on weed comes in with the munchies, buys only snacks and asks the cashier for weed (host only)");
            StonerChance = Config.Bind("Stoners", "Stoner chance per minute", 0.08f,
                new ConfigDescription("Chance each minute the store is open that a stoner shuffles in (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            StonerSnacks = Config.Bind("Stoners", "Snacks", 6,
                new ConfigDescription("About how many snacks a stoner buys; they use the party animals' snack keywords (host only)",
                    new AcceptableValueRange<int>(1, 20)));
            StonerAura = Config.Bind("Stoners", "Hazy glow", true,
                "A hazy green glow hangs around stoners");
            StonerMumbles = Config.Bind("Stoners", "Mumbles", true,
                "Stoners now and then mumble about food above their heads (host only)");
            StonerNow = Config.Bind("Stoners", "Stoner now", new KeyboardShortcut(KeyCode.F8),
                "Sends in a stoner straight away (host only)");

            CartsEnabled = Config.Bind("Shopping carts", "Enabled", true,
                "Every customer pushes a shopping cart. Only for show, and only players with the mod see them");
            CartSize = Config.Bind("Shopping carts", "Cart size", 1f,
                new ConfigDescription("How big the carts are", new AcceptableValueRange<float>(0.6f, 1.5f)));
            CartProducts = Config.Bind("Shopping carts", "Show products", true,
                "What customers have picked up lies in their cart (the host needs the mod for this)");
            Carts.Folder = System.IO.Path.GetDirectoryName(Info.Location);

            new Harmony(Guid).PatchAll(typeof(CustomerImprovementsPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void Update()
        {
            Run("Net", Net.Update);
            Run("Families", Families.Update);
            Run("PaymentHint", PaymentHint.Update);
            Run("Party", Party.Update);
            Run("Hobos", Hobos.Update);
            Run("Stoners", Stoners.Update);
            Run("Carts", Carts.Update);
            Run("Aura", Aura.Update);
        }

        // One feature failing mustn't stop the others. Each error is logged once, so the log doesn't fill up every frame.
        static readonly System.Collections.Generic.HashSet<string> failed = new System.Collections.Generic.HashSet<string>();

        static void Run(string feature, System.Action update)
        {
            try
            {
                update();
            }
            catch (System.Exception e)
            {
                if (failed.Add(feature + e.Message)) Log.LogError($"{feature} failed: {e}");
            }
        }

        void LateUpdate() => Run("Cart hands", Carts.LateUpdate);

        void OnGUI() => CustomerCounter.Draw();
    }
}
