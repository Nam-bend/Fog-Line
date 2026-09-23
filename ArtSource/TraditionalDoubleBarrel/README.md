# Traditional Double Barrel

An original low-poly double-barrel shotgun inspired by the grimy PSX horror mood of
*Blood Mall*. It is not a replica of any specific weapon asset.

- `TraditionalDoubleBarrel.blend` is the editable Blender source.
- `TraditionalDoubleBarrel.glb` is a portable preview model.
- `Preview.png` is the presentation render.
- Unity-ready FBX and 64 px palette texture are under
  `Assets/Art/Weapons/TraditionalDoubleBarrel/`.

The weapon points down Blender +Y and imports pointing down Unity +Z. The origin is near
the receiver for convenient first-person positioning. Regenerate it with Blender 4.2+:

```powershell
blender --background --factory-startup --python ArtSource/TraditionalDoubleBarrel/build_traditional_double_barrel.py
```
