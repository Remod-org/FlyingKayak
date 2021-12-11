# FlyingKayak for Rust (Remod original)

## Overview
**Flying Kayak for Rust** is an Oxide plugin which allows an enabled user to spawn and ride their own flying kayak.

## Permissions

* flyingkayak.use -- Allows player to spawn and fly a kayak

## Chat Commands

* /fk  -- Spawn a flying kayak
* /fkd -- Despawn a flying kayak (must be within 10 meters of the kayak)
* /fkc -- List the current number of kayaks (Only useful if limit set higher than 1 per user)
* /fkhelp -- List the available commands (above)

## Configuration
Configuration is done via the FlyingKayak.json file under the oxide/config directory.  Following is the default:
```json
{
  "BlockInTunnel": true,
  "UseMaxKayakChecks": true,
  "debug": false,
  "MaxKayaks": 1,
  "VIPMaxKayaks": 2,
  "MinDistance": 10.0,
  "MinAltitude": 5.0,
  "CruiseAltitude": 35.0,
  "NormalSpeed": 12.0,
  "SprintSpeed": 25.0,
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 1
  }
}
```

## Flight School

 1. Type /fk to spawn a kayak.

 2. Sit in the kayak.  You will rise above the minimum height, enough to where spacebar will not allow dismount.

 3. From here on use, WASD, Shift (sprint), spacebar (up), and Ctrl (down) to fly.

 4. Once near the ground, use the spacebar to dismount.

 5. Use /fkd while standing next to the kayak to destroy it.


