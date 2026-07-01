## Version 2.1.0 for SPT 4.0.*

### New functionality - Automatic addition of modded bullets to quests

- All modded bullets can now be added automatically to known quests caliber trees
- Based on bullet rating (weights can be adjusted in config), bullets are being added to proper quest in tree based on few specified properties. Config of property weights can be found by searching "BulletRatingWeights" in config file
- Can be turned off in the config
    If you are not seeing this option, update config - instruction can be found in mod description

### Content Update
- Added quests for calibers introduced by [WTT-Armory](https://forge.sp-tarkov.com/mod/2246/wtt-armory)
    - 7.92x57mm unlockable by completing 7.62x54R Beginner with 4 new quests and 2 bullet variants
    - .300 Winchester Magnum unlockable by completing 7.62x51 Novice with 4 new quests and 3 bullet variants
    - .338 Norma unlockable by completing .338 Beginner with 3 new quests and 2 bullet variants
    - .408 Cheyenne Tactical unlockable by completing 7.62x51 Expert with 4 new quests and 3 bullet variants
    - 25x59mm Grenade unlockable by completing Introduction, but accessible in LL2 and above with 5 new quests and 2 bullet variants
    - .44 Remington Magnum unlockable by completing .357 Beginner with 6 new quests and 2 bullet variants
- Added quests for caliber introduced by [SaintDeerWeapons](https://forge.sp-tarkov.com/mod/2590/saintdeerweapons)
    - 5.8x42mm unlockable by completing 5.56x45 Novice with 4 new quests and 3 bullet variants
- If you are using [Danger Blicky](https://forge.sp-tarkov.com/mod/2536/danger-blicky) mod, you can access new 20x1mm questline
    - 20x1mm unlockable by completing Introduction, but accessible in LL2 and above with 3 new quests and 2 bullet variants
- Added 20x1mm Tazer bullet to Amonya LL2
- Added Korean translation by ssal_pt and Russian translation by kokosik\
    If you want to have your translation added to Amonya mod, reach out to me on Discord!

### Other stuff
- Changed Lucky Shot bullet variant Damage to -45% (from -33%) and Projectile Count to 9 (from 12)
- Changed Dollar requirement for Expert difficulty to 7000 (from 20000)
- Added config option to adjust load speed of Mag of Holding items
- Added config option to adjust trader refresh timer
- Added default config option of changing 20x1mm caliber stack to 200
- Added toggleable extended debug capabilities, default: turned off\
    You can save json file of Items, Quests, Locales added by this mod or Bullets and Weapons that are loaded by this mod (that are later used to add variant bullets to or weapons to quests)

### Fixed
- Cluster bomb variant bullet now works correctly
- Mini-nuke variant is now more powerful, cheaper and has 0.5s fuze time (down from 1s)
- Adjusted some weapon categories in quests to be more inline with their display information in game
- Disabling bullet variant was stopping generation of bullet variant instead of just removing them from quests/traders
- Enabled Tazer round (it was done a long time ago, but due to error in config, it was not enabled)
- Bullet names in quest description now use correct language instead of default (EN)
- Added missing strings into locales file