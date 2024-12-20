- **1.3.2**

    - Fixed an issue where if players had Artifice Factory disabled and mineshaft factory disabled, mineshaft would spawn on everything but Artifice
    - There is now a config option to attempt to force manor on every moon. This is only possible on moons that allow Manors to spawn, and the seed will reroll up to a maximum of 1000 times before resorting to your other interior config settings

- **1.3.1**

    - Fixed the cruiser teleport fix
        - Upon some further personal testing, this was not originally working
        - Uses an async task to teleport the player
        - Please let me know if there are any issues with this, either performance or anything else!
    - Happy holidays! 

- **1.3.0**

    - Added a configuration option that sets Artifice's loot spawn rates to a minimum of 31 to a maximum of 37. This is the range set in v56, and is much better than the current spawns of (26, 30)
        - These values do not account for beehives, or the boosted spawn rates on mineshafts
        - This option is disabled by default, as some players may find it too game breaking
        - Statistics: I ran the terminal 'scan' command 5 times for both cases (config on vs. off)
            - **Config On**: scans = [4763, 3756, 4769, 4445, 5111], mean = 4568.8, median = 4763.0
            - **Config Off**: scans = [3922, 4003, 3514, 3526, 3580], mean = 3709.0, median = 3580.0
            - To get a true comparison, many tests would have to be run, so use this information as you will :)
            - These scrap values were calculated by me (host) and interpreting them correctly requires an understanding of the scan command which is explained well in this [Bread video](https://youtu.be/uMRHXBb4K1Q?si=y3q6TRoadqn6Kvkl)
    - Massive refactoring changes, code is much better suited for scalability for future game mechanic choices that I don't like
        - Removed a lot of unneeded imports generated by using Tab to autocomplete
    - Seed Rerolling is now much more intuitive and contains better logging
        - Should be easier to adopt into other projects if desired
    - This will most likely be the last update for a bit, as I'll be playing the game more and coding less
        - Happy Holidays! If any issues arise in your gameplay with this mod, please open an [issue](https://github.com/trevorswan11/RemoveTheAnnoying/issues) on the repository's GitHub! 

- **1.2.0**

    - In vanilla, players in the front seats of the cruiser are counted as dead after performing a cruiser jump onto the ships magnet
        - Implemented a fix to allow players in this position to be counted as on the ship. This will prevent wipes caused by clutch cruiser jumps last minute. 
        - This is a config option that is enabled by default.
        - I have not been able to test this in game yet, but from debugging it look like it works, if there are any bugs you encounter with this, please open a new issue on the mod's github repo
    - Minor refactoring changes in source code

- **1.1.1**

    - Fixed spelling issue in config
    - Updated source code comments

- **1.1.0**

    - Added config options for disabling mineshaft and barber/maneater spawns
        - You can now toggle the spawning of Barbers and Maneaters
        - The mineshaft can be allowed if desired, but like why lol
        - All Config options are independent of each other, they will not do anything silly if different combinations are used
    - Added a new feature that prevents Facility generation on Artifice. This is a feature OFF BY DEFAULT, and can be toggled in the config
    - Other minor refactoring
    
    *Note: Config options DO NOT update midgame and DO require a restart upon changing*

    Thanks to [Jaexyr](https://github.com/Jaexyr) for the config suggestion!

- **1.0.3**

    - Debugging Fixes
    - Code is marginally more readable
    - This release is not necessary for anything plugin related, but is more debugging/efficiency focused

- **1.0.2**

    - Small fixes, nothing changed plugin wise

- **1.0.1**

    - Fixed an issue that prevented you from landing on The Company Building

- **1.0.0**

    - Mod Release