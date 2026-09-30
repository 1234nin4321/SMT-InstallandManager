using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Random = UnityEngine.Random;

namespace SMTRandomAnnouncements
{
    // The lines to pick from, kept in BepInEx/config/SMTRandomAnnouncements.txt so players can edit them
    static class Announcements
    {
        const string FileName = "SMTRandomAnnouncements.txt";
        const int NoRepeatWithin = 5;

        static readonly string[] Defaults =
        {
            "Attention shoppers. Our fresh produce has just been restocked. Enjoy!",
            "Cleanup on aisle {aisle}. Cleanup on aisle {aisle}.",
            "Would the owner of a blue shopping cart please return to aisle {aisle}. It misses you.",
            "Register {register} is now open. No waiting at register {register}.",
            "Attention staff. Price check on aisle {aisle}. Price check on aisle {aisle}.",
            "Today only: buy one, get one... eventually. Ask a member of staff for details.",
            "Parents, please keep an eye on your children. The shopping carts are not race cars.",
            "Thank you for shopping with us. Your satisfaction is our top priority.",
            "A friendly reminder: please do not open products before paying for them.",
            "Attention shoppers. The frozen aisle is cold. This has been a public service announcement.",
            "Would the employee who parked a pallet in aisle {aisle} please move it. Thank you.",
            "Did you remember the milk? Now is a good time to check.",
            "We are hiring! Ask at the front desk about joining our friendly team.",
            "A lost wallet has been found. The owner can collect it at register {register}.",
            "Please keep the aisles clear of boxes. Safety first.",
            "Shoppers, our self checkout is working today. Probably.",
            "Staff meeting in {minutes} minutes in the back room. Snacks not provided.",
            "Whoever keeps putting pineapples in the bread section, we see you.",
            "Reminder: shoplifting is not a sport. Please pay for your items.",
            "Attention shoppers. A spill has been reported in aisle {aisle}. Please walk carefully.",
            "Remember to bring your own bags. The planet thanks you.",
            "Customers are reminded that the staff only door is for staff only.",
            "Our cashiers are doing their best. Please be kind to them.",
            "Would the driver of the delivery truck please report to the loading bay.",
            "Fun fact: bananas are berries, but strawberries are not. Happy shopping!",
            "Attention staff: the break room coffee machine is broken again. Stay strong.",
            "This is a test of the store announcement system. This is only a test.",
            "Shoppers are reminded to return their carts after use. Thank you.",
            "Congratulations! You are our one millionth customer. Just kidding. Happy shopping.",
            "If you find a product without a price, please let a member of staff know.",
            "Attention shoppers. Our bakery has fresh bread straight out of the oven. Follow your nose.",
            "Would a member of staff please bring a mop to aisle {aisle}. Thank you.",
            "Friendly reminder: the samples are for tasting, not for lunch.",
            "Attention shoppers. A shopping cart with a wobbly wheel has been spotted. Approach with caution.",
            "Lost child at register {register}. He says his name is Timmy and he would like a cookie.",
            "Please do not climb the shelves. Our shelves are for products, not for people.",
            "Attention staff. Aisle {aisle} is looking a little empty. Time to restock.",
            "Hot tip: the best deals are always on the bottom shelf.",
            "Attention shoppers. Our checkout lines are short right now. This is your moment.",
            "Please remember to check the dates on your dairy products. We do. Mostly.",
            "The music you are hearing has been specially chosen to make you buy more. Is it working?",
            "Would the person humming in aisle {aisle} please continue. It is lovely.",
            "Attention shoppers. We have more cereal than anyone could ever need. Stock up anyway.",
            "Staff are reminded that the forklift is not a ride. Thank you.",
            "Someone has lost a single sock near register {register}. Just one sock. We have questions.",
            "Please do not squeeze the tomatoes. They have feelings too.",
            "Attention shoppers. Our snacks are on special. Your diet can start tomorrow.",
            "Would the customer who left their ice cream in aisle {aisle} please come back for it. It is melting fast.",
            "Remember: an apple a day keeps the doctor away. We sell apples.",
            "Staff, please remember to smile. Customers can hear it in your voice.",
            "Attention shoppers. For the next {minutes} minutes, every smile at the checkout is free.",
            "Please put heavy items at the bottom of your bag. Your eggs will thank you.",
            "Attention staff. There is a very confused pigeon near the entrance. Please show it the way out.",
            "Did you know? We were voted the friendliest store on this street. We are the only store on this street.",
            "Please do not ride the shopping carts. It looks fun. It is fun. But please do not.",
            "Attention shoppers. The toilet paper is fully stocked. There is no need to panic.",
            "We accept cash, card, or a very good smile. Only two of those work.",
            "Would the owner of a shopping list that says eggs, milk, and a surprise please collect it at register {register}.",
            "Staff are reminded that tasting the products is not quality control.",
            "Attention shoppers. We know the lights are bright. It is so you can see our amazing prices.",
            "Thank you for choosing us today. We know you had other options. Well, one other option.",
            "Attention staff. Please stop giving the shopping carts names.",
            "If you are reading the label on every product, you are doing great. Take your time.",
            "Attention shoppers. Aisle {aisle} now has extra room to dance. Please dance responsibly.",
            "Security reminder: the cameras are always watching. They also think you look nice today.",
            "Attention shoppers. The floor is not lava. Please walk normally.",
            "Would whoever built a pyramid of cans in aisle {aisle} please come forward. We are impressed, and also concerned.",
            "Time flies when you are shopping. Please do not forget why you came in.",
            "Attention staff. Break time is in {minutes} minutes. Hang in there.",
            "Keep calm and keep shopping.",

            "[Big Boss] This is the greatest cleanup on aisle {aisle}. Maybe the greatest cleanup in history. Everybody says so.",
            "[Big Boss] Register {register} is open. Tremendous register. The best register. Other stores wish they had it.",
            "[Big Boss] We have the best shelves. Beautiful shelves. People come up to me and say, sir, those shelves.",
            "[Big Boss] Our prices are low. Very low. So low, the other stores are calling me. They are not happy.",
            "[Big Boss] Aisle {aisle} is a disaster. A total disaster. We are going to fix it, and we are going to fix it fast.",
            "[Big Boss] Nobody stocks shelves like we stock shelves. Nobody. Believe me.",
            "[Big Boss] The frozen aisle is very cold. The coldest. Some people say too cold. I say, that is how we win.",
            "[Big Boss] Staff meeting in {minutes} minutes. It is going to be a fantastic meeting. The best meeting.",
            "[Big Boss] We are going to build a checkout line. A beautiful checkout line. And it is going to move fast.",
            "[Big Boss] Many people are saying this is the best supermarket in the world. I did not say it. They said it.",
            "[Big Boss] Shoplifters, you are fired. And you do not even work here.",
            "[Big Boss] Bread in aisle {aisle}. Incredible bread. Fantastic bread. Frankly, bread like you have never seen.",
            "[Big Boss] Our carts are the best carts. Four wheels. Some stores have three. Sad.",
            "[Big Boss] I know shopping. Nobody knows more about shopping than me. Nobody.",
            "[Big Boss] Look at this crowd. Look at it. The biggest crowd this store has ever seen. Fantastic.",
            "[Big Boss] Cheese in aisle {aisle}. Very strong cheese. The strongest cheese. People cannot believe it.",
            "[Big Boss] Register {register} is moving very fast. Some say too fast. I say, we are winning.",
            "[Big Boss] We will have so many sales, you will get tired of sales. You will say, please, no more sales.",
            "[Big Boss] The milk is cold, the prices are hot, and this store is doing incredibly well. Everybody knows it.",
            "[Big Boss] I have been told this is a very good announcement. Maybe the best announcement."
        };

        // "[Big Boss] Some line" is only read by that voice
        static readonly Regex VoiceTag = new Regex(@"^\[([^\]]+)\]\s*(.+)$");
        static readonly Regex Placeholder = new Regex(@"\{(aisle|register|minutes)\}");
        static readonly Queue<string> recent = new Queue<string>();
        static string path;
        static DateTime loadedAt;
        static List<Line> lines = Parse(Defaults);

        struct Line
        {
            public readonly string Voice;  // null: any voice without lines of its own
            public readonly string Text;
            public Line(string voice, string text) { Voice = voice; Text = text; }
        }

        public static void Load(string configDir)
        {
            path = Path.Combine(configDir, FileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllLines(path, new[]
                    {
                        "# Random store announcements, one per line. Lines starting with # are ignored.",
                        "# {aisle}, {register} and {minutes} are replaced with a random number each time.",
                        "# Start a line with a voice name in brackets, like [Big Boss], to have only that voice read it.",
                        "# A voice with lines of its own reads only those; the others read the lines without a voice name.",
                        ""
                    }.Concat(Defaults));
                }
                ReloadIfChanged();
            }
            catch (Exception e)
            {
                RandomAnnouncementsPlugin.Log.LogWarning($"Could not read {FileName}, using the built-in lines: {e.Message}");
            }
        }

        // Picks a line for the voice not heard in the last few announcements and fills in its numbers
        public static string Pick(Voice voice)
        {
            try { ReloadIfChanged(); }
            catch (Exception e) { RandomAnnouncementsPlugin.Log.LogWarning($"Could not reload {FileName}: {e.Message}"); }

            var own = LinesFor(voice.Name);
            var pool = own.Count > 0 ? own : LinesFor(null);
            if (pool.Count == 0) pool = lines.Select(l => l.Text).ToList();

            var choices = pool.Where(l => !recent.Contains(l)).ToList();
            if (choices.Count == 0) choices = pool;
            string line = choices[Random.Range(0, choices.Count)];

            recent.Enqueue(line);
            while (recent.Count > NoRepeatWithin) recent.Dequeue();

            // The same placeholder gets the same number throughout a line ("aisle 4. Cleanup on aisle 4")
            var numbers = new Dictionary<string, int>
            {
                ["aisle"] = Random.Range(1, 13),
                ["register"] = Random.Range(1, 7),
                ["minutes"] = Random.Range(2, 7) * 5
            };
            return Placeholder.Replace(line, m => numbers[m.Groups[1].Value].ToString());
        }

        static List<string> LinesFor(string voiceName) =>
            lines.Where(l => string.Equals(l.Voice, voiceName, StringComparison.OrdinalIgnoreCase)).Select(l => l.Text).ToList();

        static List<Line> Parse(IEnumerable<string> raw) => raw.Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#"))
            .Select(l =>
            {
                var tagged = VoiceTag.Match(l);
                if (!tagged.Success) return new Line(null, l);
                var voice = Voice.Find(tagged.Groups[1].Value);
                if (voice == null)
                    RandomAnnouncementsPlugin.Log.LogWarning($"Unknown voice [{tagged.Groups[1].Value}] in {FileName}; any voice will read that line.");
                return new Line(voice?.Name, voice != null ? tagged.Groups[2].Value : l);
            }).ToList();

        static void ReloadIfChanged()
        {
            if (path == null || !File.Exists(path)) return;
            var modified = File.GetLastWriteTimeUtc(path);
            if (modified == loadedAt) return;
            loadedAt = modified;

            var read = Parse(File.ReadAllLines(path));
            if (read.Count == 0)
            {
                RandomAnnouncementsPlugin.Log.LogWarning($"{FileName} has no announcements, using the built-in lines.");
                read = Parse(Defaults);
            }
            lines = read;
            recent.Clear();
        }
    }
}
