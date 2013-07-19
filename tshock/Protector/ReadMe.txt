=================================================================================
 Protector Plugin for Terraria API
   (c) CoderCow 2013
=================================================================================
 
Protects Objects and Blocks
---------------------------------------------------------------------------------

This plugin provides players on TShock driven Terraria servers the possibility 
of taking ownership of certain objects or blocks, so that other players can not 
change or use them.
The content of a protected chest can not be viewed or altered by other players, 
protected switches can not be hit by other players, signs can not be edited, 
beds can not be used, doors not opened and even plants in protected clay pots 
can not be harvested without owning the clay pot.

And if a player wants to access items of a friend's chest, then the owner of it 
shall share it with them. Protector offers sharing of protections directly to 
specific users, to TShock user groups or just to everyone - and it's the owner of 
the protection doing that, no administrative actions required.

Stay in control, you define which blocks or objects can be protected, are 
automatically protected on placement, can be deprotected, how many protections 
a player can create in general and what can be shared or not.

Furthermore, one might make use of Protector's special chest related features, 
like powerful Refill Chests allowing a timed refill of their content, restrict 
players from looting them more than one time or allowing only X times of lootings 
in total. 
Also, if your server happens to change worlds frequently or if you just want 
to offer your players to use chests which can be easily transported including 
their content, then you can allow the usage of so called Bank Chests storing
their content world independently - imagine them as server sided piggy banks.

However when using Protector, worlds will be limited by Terraria's maximum of 
1000 chests and 1000 signs, so this is no replacement for Infinite Signs or
Infinite Chests. You can try to operate this plugin with other protection 
plugins, read the "About Data Import and Compatibility" for more information.

Note: This plugin requires Terraria Server API 1.12 and TShock 4 in order to work.

Suggestions? Bugs? File issues here:
https://github.com/CoderCow/Protector-Plugin/issues


Commands
---------------------------------------------------------------------------------
/Protect
/Deprotect
/ProtectionInfo
/Share <player name>
/Unshare <player name>
/ShareGroup <group name>
/UnshareGroup <group name>
/SharePublic
/UnsharePublic
/BankChest <number>
/RefillChest [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+p]
/RefillChestMany <selector> [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+p]
/LockChest
/Protector
/Protector Commands
/Protector RemoveEmptyChests
/Protector Summary
/Protector ImportInfiniteChests
/Protector ImportInfiniteSigns
/Protector ReloadConfig

To get more information about a command type 
/<command> help
ingame.


Permissions
---------------------------------------------------------------------------------
prot_manualprotect
  Can manually create protections (not required for auto protection).
prot_manualdeprotect
  Can manually remove owned protections (not required for auto deprotection).
prot_chestshare
  Can share Chests.
prot_switchshare
  Can share Switches / Levers / Pressure Plates / Timers / Music Boxes.
prot_othershare
  Can share Signs, Tombstones and Beds.
prot_sharewithgroups
  Can share protections with TShock Groups.
prot_setbankchest
  Can set up bank chests.
prot_bankchestshare
  Can share bank chests.
prot_nobankchestlimits
  Can set up unlimited bank chests.

prot_nolimits
  Can create an unlimited amount of protections.
prot_viewall
  Can view all information of all protections. Usually only owners or shared
  players can view extended information of a protection.
prot_useeverything
  Can use and alter any Chest, Sign, Switch, Lever, Pressure Plate etc. (does 
  not include removing them though).
prot_protectionmaster
  Can modify or remove any protection, can also alter any refill chest if 
  "prot_setrefillchest" is also given.
prot_setrefillchest
  Can set up a chest to automatically refill its content.
prot_utility
  Can display a summary about all chests, signs and protections of a world, can 
  lock chests, can convert all dungeon chests, sky island chests, ocean chests, 
  hell shadow chests to refill chests (also requires "prot_setrefillchest"), can 
  remove all empty non protected chests of the world.
prot_cfg
  Can import Infinite Chests' data or Infinite Signs' database files, can 
  reload Protector's configuration file.


About Data Import and Compatibility
---------------------------------------------------------------------------------
This plugin can import chest and sign data from the Infinite Chests and Infinite
Signs plugins. Make SURE you create world backups before using this functionality
as those changes can otherwise not be revoked.

This plugin might be operated together with Infinite Chests / Infinite Signs if 
protection of chests, signs and tombstones are not handled by Protector at all. 
Do NOT try to use any chest features of Protector together with Infinite Chests 
as this will cause mixed item data in the world file and the chest database.


Changelog
---------------------------------------------------------------------------------
Version 1.2 [19.07.2013]
  -Added /protector invalidate|ensure command to remove invalid protections and  
   bank chests.
  -Refill chest with auto locking will now only lock when they refill their 
   content, not each time they're closed.
  -Fixed /refillchest not working without parameters.
  -Fixed a bug causing exceptions thrown when Piggy Banks or Safes were closed.
  -/refillchestmany dungeon will now also consider wooden chests.
Version 1.1 [29.05.2013]
  -Please consider donating to support the developer.
  -Added +al|-al (auto locking) functionality to refill chests.
  -Changed the command alias /pinfo to /ptinfo.
  -Fixed a bug causing the creation time of protections not to be deserialized.
  -Fixed a bug causing chair and music box objects to be measured wrong and 
   thus having a wrong protection offset.
  -Fixed a rarely occuring bug causing exceptions on server shutdown.
Version 1.0.8 [24.04.2013]
  -Fixed a bug causing an invalid table for bank chest data being created for
   MySql databases.
  -Fixed a bug causing no message to be displayed for protected chests.

Version 1.0.7 [08.04.2013]
  -First public release by CoderCow.