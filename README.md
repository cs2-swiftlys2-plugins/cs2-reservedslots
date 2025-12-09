## Reserved Slots

If you want to donate or need a help about plugin, you can contact me in discord private/server

Discord nickname: schwarper

Discord link : [Discord server](https://discord.gg/4zQfUzjk36)

# Info
Remake of [reservedslots made by Alliedmodders for CSGO](https://github.com/alliedmodders/sourcemod/blob/master/plugins/reservedslots.sp)

# Reserved Slots
[Reserved Slots (SourceMod)](https://wiki.alliedmods.net/Reserved_Slots_(SourceMod))
```js
reserved_slots_flag <permission>
/*
   - This controles which permission flag a player needs to have to access a reserved slot (the default is reserved.slot).
*/
reserved_slots <#>
/*
   - This controls how many slots get reserved by the plugin (the default is 0).
   - Using css_reserve_type 0 this is how many admins can join the server after it appears full to the public. Using css_reserve_type 1 this is how many slots are saved for swapping admins in (you shouldn't need more than one)
*/
hide_slots <0|1>
/*
  - This controls the plugin hides the reserved slots (the default is 0).
  - If enabled (1) reserve slots are hidden in the server browser window when they are not in use. For example a 24 player server with 2 reserved slots will show as a 22 player server (until the reserved slots are occupied). If you experience that the slots are not hidden, despite setting css_hide_slots to 1, then adding host_info_show 2 to your server.cfg may solve this problem. To connect to the reserved slot of a server that shows as full you will need to use 'connect ip:port' in console. (e.g. 'connect 192.168.1.100:27015').
  - There is no possible way for the reserved slots to be visible to admins and hidden from normal users. Admin authentication can only happen after the user is fully connected to the server and their steam id is available to SourceMod. For this reason it is often better to hide the slots otherwise public users will attempt to join the server and will get kicked again (rendering the ‘autojoin’ feature useless)
*/
reserve_type <0|1|2>
/*
  - This controls how reserve slots work on the server (the default is 0).

  - reserve_type 0
  - Public slots are used in preference to reserved slots. Reserved slots are freed before public slots. No players are ever kicked and once reserved slots are filled by a reserve slot player (and the rest of the server is full) they will remain occupied until a player leaves. The use of this is that there can always be at least one admin (assuming you only give reserved slots to admins) on the server at any time. If players inform you that there is a cheater on the server, at least one admin should be able to get it and do something about it. If a player without reserve slot access joins when there are only reserved spaces remaining they will be kicked from the server.

  - reserve_type 1
  - If someone with reserve access joins into a reserved slot, the player with the highest latency and without reserve access (spectator players are selected first) is kicked to make room. Thus, the reserved slots always remain free. The only situation where the reserved slot(s) can become properly occupied is if the server is full with reserve slot access clients. This is for servers that want some people to have playing preference over other. With this method admins could one by one join a full server until they all get in.

  - reserve_type 2 - Only available in SourceMod 1.1 or higher.
  - The same as css_reserve_type 1 except once a certain number of admins have been reached the reserve slot stops kicking people and anyone can join to fill the server. You can use this to simulate having a large number of reserved slots with css_reserve_type 0 but with only need to have 1 slot unavailable when there are less admins connected.
*/
reserve_maxadmins <#>
/*
  - This controls how many admins can join the server before the reserved slots are made public (only relevant to css_reserve_type 2)
*/
reserve_kicktype <0|1|2>
/*
  - This controls how a client is selected to be kicked (only relevant to css_reserve_type 1/2)

  - 0 - Highest Ping
  - 1 - Highest Connection Time
  - 2 - Random Player
*/