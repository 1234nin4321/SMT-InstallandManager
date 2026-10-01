# SMT-InstallandManager

Mod tools for Supermarket Together.

## SMTInstaller.exe

Windows installer that sets up:

- [BepInEx 5](https://github.com/BepInEx/BepInEx), the mod loader
- [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), in-game mod settings (F1)
- SMT Mod Browser (below), from this repo's GitHub releases

These three are always installed. It finds the game in any Steam library and always downloads the
latest release of each. Optional mods listed in `optional-mods.json` (below) show up on the Optional Mods tab, each with a checkbox.
It checks the versions of the mod DLLs already in the game folder, however they got there, and only
installs what's missing or out of date. When everything is current, the button reinstalls it all.
On start it checks this repo's releases for a newer installer and offers to update itself.

## SMT Mod Browser (plugin)

A BepInEx plugin that lets you browse, install, update and remove
[Thunderstore](https://thunderstore.io/c/supermarket-together/) mods from inside the game.
Press **F6** to open it (configurable in F1). Dependencies are installed automatically;
new mods load after restarting the game.
Every installed mod has a **Remove** button (click it twice to confirm) that deletes its folders.
Mods that were deprecated or taken off Thunderstore still show up so they can be removed.

Mods are installed the same way r2modman does (`BepInEx/plugins/Owner-Name/`). Updates or
removals of mods that are currently loaded are applied on the next launch by a small preloader
patcher (`BepInEx/patchers/SMTModBrowser/`).

## SMT UberEats (optional plugin)

Replaces the delivery where ordered boxes drop from the sky one at a time (0.5–1 s each).
When the order arrives, each box goes straight into a free storage slot, picked the same way employees pick one
(labelled slots for that product first, then the nearest empty ones). Boxes that don't fit are stacked in a neat
grid next to the delivery point. Settings in F1: mode (`StorageThenFloor`, `FloorOnly`, `Vanilla`), stack height,
and the delivery time (10 s by default, counted down in a banner at the top of the screen; orders placed
during the countdown come with the same delivery). Only the host needs it; everything is synced through the game's own network calls.
The host also posts the countdown and arrival in the game chat (setting *Announce in chat*), so every player sees them;
players who have the mod get the banner instead of the chat lines.

## SMT Random Announcements (optional plugin)

Plays random store announcements over the loudspeakers at random times of the in-game day while the store is open
("Cleanup on aisle 4", "Register 2 is now open", ... 90 of them built in). It uses the game's own announcement system, so you need
an announcement desk and at least one speaker placed in the store, and like the desk it stays quiet in public games.
Only the host needs it; every player hears the announcements on the speakers.

Each announcement is read by one of several voices, all variations of the game's English voice: Announcer,
Store Manager (deeper), Summer Intern (higher), Security (megaphone), Back Office (muffled), Warehouse (echo),
Chipmunk, and Big Boss (booming, with bragging lines of his own). Players who have the mod hear the host's chosen voice;
the host tells them which voice through a chat line (setting *Announce in chat*), which every player sees in the chat.
Players without the mod hear the plain voice.
Settings in F1: announcements per day (8 by default, spread at random over the opening hours), which voices to use,
*Show in chat* (turn it off to keep the announcements out of your own chat), and
**F9** to play one straight away (host only). The lines are in `BepInEx/config/SMTRandomAnnouncements.txt`,
one per line; `{aisle}`, `{register}` and `{minutes}` are filled in with random numbers. A line starting with a
voice name in brackets (`[Big Boss] ...`) is only read by that voice, and a voice with lines of its own reads only those.

## SMT Auto Manufacturing (optional plugin)

Keeps the manufacturing shelves stocked without anyone pressing buttons on the manufacturing desk. Every 10 seconds
the host looks at every product labelled on the manufacturing shelves and counts what's on the shelves, in manufacturing
storage, in boxes lying around, in the machines' queues and in the machines right now. When that's less than what fits on
the shelves plus one spare box, it queues another one on the machine with the shortest queue, emptiest product first.
Before queuing it checks that the store has the ingredients (in storage or on the shelves, where employees fetch them), and
skips products it can't make yet. Extra ingredients only count when they're in storage: customers can buy them off the
shelves first, and an employee waits for a missing extra until it turns up. While a manufacturing desk has orders
waiting, machines are only filled up to 2 so the desk can still hand them over.

It only decides *what* to make: employees assigned to manufacturing still fetch the ingredients, start the machines and
restock the shelves, as in the unmodded game. Without them, the queue fills up and waits for a player to add the ingredients.
Settings in F1: on/off, boxes in reserve, queue per machine and the check interval. Only the host needs it.

## SMT Anyone Continues (optional plugin)

At the end of the day the unmodded game waits for the host to press a key before the next day starts, and only the host
sees the "press any key" prompt. With this mod every player sees the prompt, and a key press from any of them starts the
next day. The host and every player who should be able to continue need it; players without it just wait for someone
who has it. The host tells the others about the prompt, and they send their key press back, through the game's own chat
calls in lines the game's chat never shows.

## SMT Customer Improvements (optional plugin)

Changes to how customers behave: payment fraud, families, party animals, hobos, stoners, shopping carts and a customer counter.

**Payment fraud.** Now and then (10% of payments by default) a customer at a register pays with fake cash or a stolen credit card.
Stand near the register while they're paying and press **G** to check the payment:

- **Fraud caught:** the customer grabs everything that was scanned (the bags vanish from the counter) and runs off
  like any other thief. Hit them, or let security guards chase them, to get the products back.
- **Honest customer accused:** they storm out insulted without paying, and the sale is lost, but what they had scanned
  goes straight back into stock: onto a shelf row that already holds that product and has room, otherwise into a
  storage box of that product, otherwise as a new box in an empty storage slot. The chat says where it went. The customer tells you what they
  think of the accusation above their head ("I want to speak to your manager!"), with lines of their own for cash and for cards; players without the mod don't see these.
- **Nobody checks:** the payment goes through like any other and the customer leaves with their shopping. At the end
  of the day the fake bills turn up in the till and the card company takes back what stolen cards paid: the total comes
  off the store's funds and shows up as its own line in the end-of-day summary ("Fake cash & stolen cards"), included
  in the day's balance. Players need the mod to see that line; the host also posts the losses in the chat.

Fake bills flash, pulsing between purple and yellow, and stolen cards between red and blue, until the payment is
over (setting *Show hint*).
Employees working a register spot fraud depending on their security skills: 20%, plus 4% per point of their
security rating (1–10), plus 0.5% per security level they've earned (1–100), up to 95%. A rookie catches about a
quarter, a top-rated veteran most of them, and every catch earns them security experience like stopping a thief does. The host posts what happened
in the game chat (setting *Announce in chat*), which every player sees.
Settings in F1: on/off, fraud chance, the three employee catch chance numbers, the hint, the check key and the chat lines.
Players send their check to the host through the game's own chat calls, in lines the game's chat never shows.

**Families.** Some customers (20% by default) come in with one or two children. The game has no child models, so a
child is one of the customer models at 60% size. Children walk along behind their parent, wait with them in the
queue, and leave with them, running off too if the parent turns thief. Each child adds two things to the parent's
shopping list, and now and then says something ("Can we get candy?", "Are we done yet?").
Settings in F1: on/off, family chance, most children (up to 3), extra items per child, child size and whether children
talk. Players without the mod see children as full-size customers following their parent around and don't see what
they say; players who join after a family came in see those children at full size too.

**Party animals.** Now and then (a 10% chance each minute the store is open) a group of 5 to 8 men walks in
together to stock up for a party. They're ordinary customers, but they glow with an aura that cycles through the
colours, buy about 12 things each, all alcohol and snacks, and shout now and then ("Grab the beer, boys!").
The game has no product categories, so alcohol and snacks are found by keywords in the product names (beer, wine,
vodka, chips, candy, ...); the BepInEx log lists which products matched, and both keyword lists can be changed in F1.
If the store sells nothing that matches, no party comes. Party-goers use the male models (the first 53, as the game
itself assumes). Press **F10** to send one in straight away (host only).
Settings in F1: on/off, party chance, smallest and biggest party, items each, the keyword lists, the aura, the shouts
and the key.

**Hobos.** Now and then (an 8% chance each minute the store is open) a hobo wanders in, wrapped in a murky green
and brown glow. They buy a few things like any customer but drop a piece of garbage every 8 seconds or so, up to 10,
wherever they walk on the store floor. It's the game's own trash, so cleaners and cleaning robots pick it up.
Customers who get within a few metres complain about the smell ("Did something die in here?"), and each complaint
counts towards the day's complaints about filth. Hobos are trouble at the register too: 35% of them steal their
shopping like any other thief, and 40% of those who pay use fake cash or a stolen card (instead of 10%).
They mumble now and then ("Spare some change?"). Press **F11** to send one in straight away (host only).
Settings in F1: on/off, hobo chance, steal chance, fraud chance, how often and how much trash, the smell
complaints, the stink cloud, the mumbles and the key.

**Stoners.** Now and then (an 8% chance each minute the store is open) a customer who is high on weed shuffles in
with the munchies, wrapped in a hazy green glow. They walk a bit slower than other customers and buy about 6
things, all snacks (found with the party animals' snack keywords; if the store sells no snacks, no stoner comes).
They mumble about food now and then ("Do chips have feelings?"), and once they get to a register they ask the
cashier whether there's any weed behind the checkout ("Psst... you got any weed back there?"). An employee working
that register answers ("We have oregano in aisle 3."). Press **F8** to send one in straight away (host only).
Settings in F1: on/off, stoner chance, how many snacks, the hazy glow, the mumbles and the key.

**Shopping carts.** Every customer pushes a shopping cart once they're inside the shop (not out on the street), with
both hands on the handle, and what they've picked up so far lies in the basket: copies of the products' own models, shrunk to fit if they're big, up to 24 in two layers.
At the register the cart empties as the products go onto the counter. Carts are only for show: customers walk where
they always did, so a cart can pass through a shelf or another customer. Each player with the mod sees the carts;
what's in them comes from the host, so with a host without the mod the carts stay empty. Players without the mod
see customers as usual. Carts and the products in them don't cast shadows unless you turn that on, which keeps
busy stores smooth.
Settings in F1: on/off, cart size, whether products show in the cart, and shadows.
The shopping cart model is by mechano-file, used under the MIT License
([`CustomerImprovements/assets/LICENSE-ShoppingCart.txt`](CustomerImprovements/assets/LICENSE-ShoppingCart.txt)),
which ships in the mod's zip with the model.

**Customer count.** A box at the right side of the screen, a quarter of the way down, shows how many customers
are in the store right now ("Customers: 12"). It counts every customer the game is running, including those still
walking in from the street or on their way out; children aren't counted. Every player who has the mod sees it,
host or not. Turn it off in F1 (*Customer count* → *Show*).

Everything is decided by the host; other players need the mod to check payments and see the hints, the summary line,
the children's size, the party aura, the stink cloud and what customers say.

## SMT Decorator (optional plugin)

Adds the **Decorator**, a tablet for decorating the store. Press **F7** with empty hands to take it out, and again to put it away.
It's the ordering tablet's model, held the same way, and its screen shows the colour or picture you've picked. Press **Tab** to open
its menu, where you choose between painting walls and hanging pictures.

**Painting walls.** Pick any colour in the menu: red, green and blue sliders, a hex code, 20 ready-made colours and the
colours you used lately. Aim at a wall and click to paint that panel; Shift+click paints every panel of the wall.
Right-click a wall to pick up its colour. Walls you place yourself work too, each painted as one piece; one that's moved
loses its Decorator colour. It tints the material the panel already has (pick a material with the game's own paint tablet), and
painting the panel with the game's tablet again takes the Decorator's colour off. $2 per panel by default.

**Pictures.** In the menu, *Import from PC...* opens Windows' Open dialog for a PNG or JPEG. Pictures can also be dropped into
`BepInEx/config/SMTDecorator/Import/` and imported from the list in the menu. Pictures bigger than 1024 pixels across are scaled down
first. Pick one, aim at a wall and click to hang it, or at the floor to lay it flat there (its top points away from you); a preview shows where it will go, and scrolling (or the slider in the menu) sets its width.
Right-click a picture to take it down. $10 per picture by default.

Everything is done by the host: players send what they want to do to the host, which charges for it and passes it on to everyone,
so every player with the mod sees the same walls and pictures, players who join later included. A picture a player imports is sent to the host
in pieces through the game's chat calls (in lines the game's chat never shows), and the host keeps it in
`BepInEx/config/SMTDecorator/images/`. Other players get it from the host when they need it and keep a copy in the same folder.
The host saves the paint and pictures with the store whenever the game saves, in a file next to the save
(`<save>.decorator.txt` in the game's save folder, or `Autosaves/Autosave001.es3.decorator.txt` for autosaves and Save and quit).
Other players don't see you holding the tablet, since the game only shows its own items in players' hands. Players without the mod
see the walls as the game's own paint left them and no pictures.
Settings in F1: the two keys, the prices (host only), the largest picture size and how far you can reach.

## Layout

| Folder         | What                                                        |
| -------------- | ----------------------------------------------------------- |
| `Installer/`   | SMTInstaller.exe (WinForms, .NET Framework 4.8)             |
| `Plugin/`      | SMTModBrowser.dll (BepInEx 5 plugin, Unity 2022.3)          |
| `Preloader/`   | SMTModBrowser.Preloader.dll (applies deferred mod changes)  |
| `UberEats/`    | SMTUberEats.dll (optional BepInEx plugin, delivery)       |
| `RandomAnnouncements/` | SMTRandomAnnouncements.dll (optional BepInEx plugin, loudspeaker) |
| `AutoManufacturing/` | SMTAutoManufacturing.dll (optional BepInEx plugin, manufacturing) |
| `AnyoneContinues/` | SMTAnyoneContinues.dll (optional BepInEx plugin, end of day) |
| `CustomerImprovements/` | SMTCustomerImprovements.dll (optional BepInEx plugin, customers) |
| `Decorator/` | SMTDecorator.dll (optional BepInEx plugin, wall paint and pictures) |
| `sources/`     | The game's `Assembly-CSharp.dll` and `Mirror.dll` (not committed) |
| `Plugin.Tests/`| Tests for the plugin's install logic, run on plain .NET     |
| `dist/`        | Prebuilt installer exe and the plugin release zip           |

## Build

Builds on Windows or Linux with the .NET 8 SDK:

```
dotnet build -c Release
dotnet run -c Release --project Plugin.Tests
```

A Release build writes `dist/SMTInstaller_vX.Y.Z.exe` and `dist/SMTModBrowser_vX.Y.Z.zip`.
Attach them to a GitHub release so the installer can find them (the repo must be public for that).

UberEats, Random Announcements, Auto Manufacturing, Anyone Continues, Customer Improvements and Decorator are built on their own because they need the game's assemblies. Copy `Assembly-CSharp.dll` and
`Mirror.dll` from the game's `Supermarket Together_Data/Managed/` folder into `sources/` (git ignores them), then:

```
dotnet build -c Release UberEats/SMTUberEats.csproj
dotnet build -c Release RandomAnnouncements/SMTRandomAnnouncements.csproj
dotnet build -c Release AutoManufacturing/SMTAutoManufacturing.csproj
dotnet build -c Release AnyoneContinues/SMTAnyoneContinues.csproj
dotnet build -c Release CustomerImprovements/SMTCustomerImprovements.csproj
dotnet build -c Release Decorator/SMTDecorator.csproj
```

That writes `dist/SMTUberEats_vX.Y.Z.zip`, `dist/SMTRandomAnnouncements_vX.Y.Z.zip`, `dist/SMTAutoManufacturing_vX.Y.Z.zip`
`dist/SMTAnyoneContinues_vX.Y.Z.zip`, `dist/SMTCustomerImprovements_vX.Y.Z.zip` and `dist/SMTDecorator_vX.Y.Z.zip`.

- The installer finds updates of itself by the version in the exe's file name, so keep the
  `SMTInstaller_vX.Y.Z.exe` name and bump `<Version>` in `Installer/SMTInstaller.csproj` to release one.
  It looks through the last 30 non-prerelease releases, so a release doesn't have to include the exe.
- For every download the installer picks the highest version (read from the file name) among the repo's
  last 30 non-prerelease releases, so a release only needs to include the files that changed.

## Optional mods

The installer reads [`optional-mods.json`](optional-mods.json) from the `main` branch on start and shows each
entry as an optional mod the user can tick. To add one, attach its zip to a release, then add an entry:

```json
{
  "mods": [
    {
      "name": "Example Mod",
      "description": "One line shown under the name",
      "asset": "^SMTExampleMod_v.*\\.zip$",
      "file": "BepInEx/plugins/ExampleMod/ExampleMod.dll",
      "selected": false
    }
  ]
}
```

| Field         | Meaning                                                                          |
| ------------- | -------------------------------------------------------------------------------- |
| `name`        | Shown on the card                                                                |
| `description` | Shown under the name                                                             |
| `asset`       | Regex for the release zip's file name. Put the version in the name (`_v1.0.0`)    |
| `file`        | The mod's DLL, relative to the game folder. Its file version tells the installer which version is installed, so keep it in step with the zip name (the csproj `<Version>`). Without it, the mod is always offered for install |
| `repo`        | Optional, `owner/name` of another repo to download from. Defaults to this repo    |
| `selected`    | Optional, `true` to have it ticked by default                                    |

The zip is extracted into the game folder, like the others, so lay it out the same way as
`SMTModBrowser_vX.Y.Z.zip` (e.g. `BepInEx/plugins/ExampleMod/ExampleMod.dll`). No installer release is needed
to add or change entries; installers already out there pick the change up on their next start.
