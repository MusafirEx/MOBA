Monster Arena Prototype - Minimum Scripts

Install/setup required:
1. Unity project with Photon Fusion 2 installed.
2. TextMeshPro imported.
3. Arena scene with:
   - FusionLauncher GameObject
   - FusionConnectionUI button
   - ArenaMatchManagerNetwork as NetworkObject scene object
   - RespawnManagerNetwork as NetworkObject scene object
   - CapturePointNetwork GameObject with trigger collider + NetworkObject
   - Camera with MonsterCameraController
   - GameJoltManager GameObject + GameJoltConfig asset
4. Player monster prefab with:
   - NetworkObject
   - CharacterController
   - MonsterControllerFusion
   - MonsterStatsNetwork
   - MonsterWeaponManager
   - ProjectileWeapon child weapon(s)
5. Projectile prefab with:
   - NetworkObject
   - NetworkProjectile
   - Collider optional visual mesh

Important:
- Add all NetworkObject prefabs to Fusion NetworkProjectConfig prefab table if required by your Fusion setup.
- Game Jolt save is minimal prototype. Login/auth and set data are included; robust data loading parser can be improved next.
