# Blackpine Ranger Cabin — low-poly

Created in Blender 5.2 via the live MCP connection on 2026-09-28.

- `BlackpineRangerCabin.blend`: editable reference scene, studio camera/lights, and a separate compact export scene.
- `BlackpineRangerCabin.fbx`: cabin only, 9 mesh objects, 5,994 triangles including the sign lettering. Door has a hinge origin. No studio ground/cameras/lights exported.
- `BlackpineCabin_reference.png`: 1600 × 1400 front three-quarter render.
- `validation.json`: counts and FBX reimport verification.
- `01_structure.py`, `02_roof_details_studio.py`, `03_export.py`: construction stages; run in order in a fresh Blender file if rebuilding. They are creation scripts, not idempotent update scripts.

Body footprint: 6 × 8 m. Porch deck: 6 × 2 m. Four square porch posts,
three entrance treads, a central door, two front windows and one right-side window.
Roof overhangs, chimney, stairs and trim extend beyond the nominal body footprint.
The measured exported bounds are approximately 6.94 × 11.48 × 6.27 m in Blender XYZ.

The shell has an empty interior and actual door/window openings. Materials use a
simple low-poly colour palette; weathering is sparse mesh detail. No external
texture downloads are required. Glass is a stylized opaque reflective material.
The sign is on the porch header above the entrance, visible in the reference view.

Verified: full building visible in render, four porch posts, three steps; exported
FBX reimports with 9 meshes, 5,994 triangles, material slots and metre-scale bounds.
Unity import, URP material setup, colliders, LOD/lightmap UVs and navigation are not
part of this delivery. Existing Unity cabin/scene assets have not been replaced.
