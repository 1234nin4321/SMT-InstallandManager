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
        public const string Version = "0.2.0";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> FraudChance;
        internal static ConfigEntry<float> EmployeeCatchChance;
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

        void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("Payment fraud", "Enabled", true,
                "Customers at the registers sometimes pay with fake cash or a stolen credit card (host only)");
            FraudChance = Config.Bind("Payment fraud", "Fraud chance", 0.1f,
                new ConfigDescription("Chance that a customer at a register pays with fake cash or a stolen card (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            EmployeeCatchChance = Config.Bind("Payment fraud", "Employee catch chance", 0.5f,
                new ConfigDescription("Chance that an employee at a register spots the fake cash or stolen card (host only)",
                    new AcceptableValueRange<float>(0f, 1f)));
            ShowHint = Config.Bind("Payment fraud", "Show hint", true,
                "Fake bills and stolen cards look slightly off-colour");
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

            new Harmony(Guid).PatchAll(typeof(CustomerImprovementsPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void Update()
        {
            Net.Update();
            Families.Update();
        }

        void OnGUI() => CustomerCounter.Draw();
    }
}
