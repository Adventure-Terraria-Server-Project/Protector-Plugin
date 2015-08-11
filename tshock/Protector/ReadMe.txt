=================================================================================
 Protector Plugin for TerrariaServer-API
   (c) CoderCow 2013-2015
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

Note: This plugin requires Terraria Server API and TShock in order to work.

Suggestions? Bugs? File issues here:
https://github.com/CoderCow/Protector-Plugin/issues


Commands
---------------------------------------------------------------------------------
/Protect [+p]
/Deprotect [+p]
/ProtectionInfo [+p]
/Share <player name> [+p]
/Unshare <player name> [+p]
/ShareGroup <group name> [+p]
/UnshareGroup <group name> [+p]
/SharePublic [+p]
/UnsharePublic [+p]
/BankChest <number>
/DumpBankChest
/RefillChest [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+p]
/RefillChestMany <selector> [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+p]
/LockChest [+p]
/Protector
/Protector Commands
/Protector RemoveEmptyChests
/Protector Summary
/Protector Cleanup [-d]
/Protector RemoveAll <region <region>|user <user>> [-d]
/Protector ImportInfiniteChests
/Protector ImportInfiniteSigns
/Protector ReloadConfig

To get more information about a command type 
/<command> help
ingame.


Permissions
---------------------------------------------------------------------------------
prot.manualprotect
  Can manually create protections (not required for auto protection).
prot.manualdeprotect
  Can manually remove owned protections (not required for auto deprotection).
prot.chestshare
  Can share Chests.
prot.switchshare
  Can share Switches / Levers / Pressure Plates / Timers / Music Boxes.
prot.othershare
  Can share Signs, Tombstones and Beds.
prot.sharewithgroups
  Can share protections with TShock Groups.
prot.setbankchest
  Can set up bank chests.
prot.bankchestshare
  Can share bank chests.
prot.nobankchestlimits
  Can set up unlimited bank chests.
prot.dumpbankchest
  Can dump bank chest content (Warning: duplicates the bank chest's items).

prot.nolimits
  Can create an unlimited amount of protections.
prot.viewall
  Can view all information of all protections. Usually only owners or shared
  players can view extended information of a protection.
prot.useeverything
  Can use and alter any Chest, Sign, Switch, Lever, Pressure Plate etc. (does 
  not include removing them though).
prot.protectionmaster
  Can modify or remove any protection, can also alter any refill chest if 
  "prot_setrefillchest" is also given.
prot.setrefillchest
  Can set up a chest to automatically refill its content.
prot.utility
  Can display a summary about all chests, signs and protections of a world, can 
  lock chests, can convert all dungeon chests, sky island chests, ocean chests, 
  hell shadow chests to refill chests (also requires "prot_setrefillchest"), can 
  remove all empty non protected chests of the world.
prot.cfg
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
See commit log on the GitHub repository for future changes:
https://github.com/CoderCow/Protector-Plugin/commits/master

Version 1.3.9 [05/18/2014]
  -Updated to Terraria 1.2.4.1

Version 1.3.8 [03/17/2014]
  -Bank chests amount might now be limited per players of certain player groups.
  -Removed the outdated world metadata detection feature as it tends to fail a 
   lot in detecting this.
  -Configuration format has changed, you'll have to update your old config.
  -Updated to Plugin Common Lib 2.9.

Version 1.3.7 [02/19/2014]
  -Fixed some bugs regarding the three new chest styles.
  -Updated to Plugin Common Lib 2.8.

Version 1.3.6 [02/14/2014]
  -Updated to Terraria 1.2.3.
  -Updated to Plugin Common Lib 2.7.

Version 1.3.5 [02/13/2014]
  -Fixed a rare bug with one time lootable refill chests making them completely
   unlootable.
  -Added some previously unknown tiles causing many exceptions.
  -Fixed more exception spam caused by incompatibilites.
  -Updated to Plugin Common Lib 2.6.

Version 1.3.4 Beta [12/28/2013]
  -Fixed bed spawn protection moving players to the upper left corner of the 
   world.
  -Fixed a bug causing chests to be unplaceable on ice blocks but on ice rod
   blocks instead.

Version 1.3.3 Beta [12/27/2013]
  -Updated to Terraria 1.2.2.
  -NOTE: Truncating / deleting the "Protector_BankChests" table in your database 
   or removing Protector's entire database is REQUIRED after applying this update. 
   This will remove all player bank chests.
  -Fixed a critical bug causing deadlocks when database operations failed.
  -Fixed a deadlock occuring on setting up bank chests.
  -Fixed some textual errors.

Version 1.3.2 Beta [11/17/2013]
  -NOTE: Truncating the "Protector_BankChests" table in your database is recommended
   before or right after applying this update.
  -Updated for Terraria 1.2.1.2.
  -Fixed a critical packet handler bug causing corrupted item data in refill- and
   bank chests and eventually in player inventories.
  -Fixed a bug causing bank chests to dupe items when they were removed.
  -Updated to Common Lib 2.4.

Version 1.3.1 Beta [10/14/2013]
  -Updated for TShock 4.2 Pre 15.
  -Fixed a bug where refill- and bank chests were working incorrectly due to the
   1.2 double sized chests.

Version 1.3 Beta [10/06/2013]
  -Updated for Terraria 1.2 and TShock 4.2 pre.
  -Changed the permission model to "prot.<perm>" ("_" to ".").

Version 1.2.1 [08/01/2013]
  -Improved protection management.
  -Protector will no longer throw an exception when a user tried to create a bank
   chest instance on a tile which is no chest.
  -Protector will no longer throw an exception when protections with invalid
   tile locations get removed.
  -Fixed a bug causing /protector removeall help to not state the help test.

Version 1.2 [07/20/2013]
  -Added /dumpbankchest command allowing admins to use bank chests like chest 
   templates.
  -Added /protector invalidate command to remove invalid protections and  
   lost bank chests instances.
  -Added /protector removeall command to either remove all protections inside a
   specific region or owned by a specific user.
  -Added /protector cleanup command to remove all protections of users which where
   already removed from the TShock database.
  -Added "AllowRefillChestContentChanges" setting.
  -Improved protection management: Protections which became invalid due to 
   unknown tile changes (done by other plugins for example, or by falling blocks) 
   will now be automatically removed if Protector notices that they're invalid.
  -/refillchestmany dungeon will now also consider wooden dungeon chests.
  -Refill chest with auto locking will now lock when they actually refill their 
   content, instead of locking each time they're closed.
  -Fixed a bug causing chest protections to be removed when a chest was hit, but 
   not destroyed.
  -Fixed /refillchest and /refillchestmany not working without time parameters.
  -Fixed a bug causing exceptions thrown when Piggy Banks or Safes were closed.
  -Fixed a bug causing the -ll on refill chest commands to not work properly.
  -Fixed server being able to execute most Protector commands it shouldn't be
   able to.
  -Updated to Common Lib 2.0.

Version 1.1 [05/29/2013]
  -Please consider donating to support the developer.
  -Added +al|-al (auto locking) functionality to refill chests.
  -Changed the command alias /pinfo to /ptinfo.
  -Fixed a bug causing the creation time of protections not to be deserialized.
  -Fixed a bug causing chair and music box objects to be measured wrong and 
   thus having a wrong protection offset.
  -Fixed a rarely occuring bug causing exceptions on server shutdown.

Version 1.0.8 [04/24/2013]
  -Fixed a bug causing an invalid table for bank chest data being created for
   MySql databases.
  -Fixed a bug causing no message to be displayed for protected chests.

Version 1.0.7 [04/08/2013]
  -First public release by CoderCow.