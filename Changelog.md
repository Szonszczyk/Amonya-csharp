## Version 2.2.0 for SPT ~4.1.2

- Initial release for SPT 4.1.2

## Version 2.1.1 for SPT ~4.0.13

- Fixed rollbacked function of adding to special slots used by MoH items
- Added changing variant bullet name format to config\
    If you do not see this option, update your config. Instructions can be found in the mod description
- Added first lore to 5.45x39 quest tree\
    Made by Razh
- Old 5.45x39 pictures were moved to .44 Magnum modded caliber

## Version 2.1.0 for SPT 4.0.*

### New functionality - Automatic addition of modded bullets to quests

- All modded bullets can now be automatically added to known quests caliber trees
- Based on bullet rating (weights can be adjusted in config), bullets are added to the appropriate quest in tree based on several specified properties. Property weights can be configured under ``BulletRatingWeights`` in the config file
- Can be disabled in the config\
    If you do not see this option, update your config. Instructions can be found in the mod description

### Content Update
- Added questlines for calibers introduced by [WTT-Armory](https://sp-mod.com/mod/2246/wtt-armory)
    - 7.92x57mm - unlocked by completing 7.62x54R Beginner with 4 new quests and 2 bullet variants
    - .300 Winchester Magnum - unlocked by completing 7.62x51 Novice with 4 new quests and 3 bullet variants
    - .338 Norma - unlocked by completing .338 Beginner with 3 new quests and 2 bullet variants
    - .408 Cheyenne Tactical unlockable by completing 7.62x51 Expert with 4 new quests and 3 bullet variants
    - 25x59mm Grenade - unlocked by completing Introduction, but accessible in LL2 and above with 5 new quests and 2 bullet variants
    - .44 Remington Magnum - unlocked by completing .357 Beginner with 6 new quests and 2 bullet variants
- Added questlines for caliber introduced by [SaintDeerWeapons](https://sp-mod.com/mod/2590/saintdeerweapons)
    - 5.8x42mm - unlocked by completing 5.56x45 Novice with 4 new quests and 3 bullet variants
- If you are using [Danger Blicky](https://sp-mod.com/mod/2536/danger-blicky) mod, you can access new 20x1mm questline
    - 20x1mm - unlocked by completing Introduction, but accessible in LL2 and above with 3 new quests and 2 bullet variants
- Added 20x1mm Tazer bullet to Amonya LL2
- Added Korean translation by ssal_pt and Russian translation by kokosik\
    If you would like your translation to be included in Amonya, contact me on Discord

### Other stuff
- Changed the Lucky Shot bullet variant's Damage modifier to -45% (from -33%) and Projectile Count to 9 (from 12)
- Reduced the USD requirement for Expert difficulty to 7,000 (from 20,000)
- Added a config option to adjust load speed of Mag of Holding items
- Added a config option to adjust trader refresh timer
- Added default config option of changing 20x1mm caliber stack to 200
- Added toggleable extended debug capabilities (disabled by default)\
    You can save JSON files containing Items, Quests, and Locales added by this mod, as well as Bullets and Weapons loaded by the mod (used when adding variant bullets and weapons to quests)

### Fixed
- Fixed the Cluster Bomb bullet variant not functioning correctly
- Mini-nuke variant is now more powerful, cheaper and has 0.5s fuze time (down from 1s)
- Adjusted some weapon categories in quests to better match their in-game display information
- Disabling a bullet variant no longer prevents it from being generated; it now only removes it from quests and traders
- Enabled the Tazer round (it had been intended to be enabled for some time, but a config error prevented it)
- Bullet names in quest descriptions now use the correct language instead of always defaulting to English
- Added missing strings into locale files