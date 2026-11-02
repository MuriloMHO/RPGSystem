RPGSystem — Approved Visual Assets (v1)
======================================

VISUAL APPROVAL
All 15 files were visually approved in the RPGSystem Sprite Creation & Visual Assets conversation.
3 player sprites, 6 enemy sprites, 6 area combat backgrounds.

TECHNICAL VERIFICATION
Player and enemy sprites: 256 x 256 RGBA PNG, fully transparent background pixels,
no partly transparent pixels, and no painted sprite pixels along the outer canvas edge.
Area backgrounds: 1536 x 864 RGB PNG (16:9), opaque.

IMPORTANT FILE CHOICES
- Necromancer uses the corrected left-facing file, not the original direction.
- Dark Lord uses the approved compact 'ReferenceStyle' version, not the taller variation.
- The filenames in this archive are organized for use, while the source/original filenames
  remain recorded in manifest.json.

DIRECTORY STRUCTURE
Assets/Characters/Players/
Assets/Characters/Enemies/
Assets/Backgrounds/

BLAZOR INTEGRATION NOTES (not yet implemented)
- Do not add new graphical backgrounds behind the transparent character PNGs.
- Keep original sprites immutable. Control their displayed scale and vertical baseline in UI layout.
- Don't automatically display all creatures at the same visual height: the Wolf is naturally wide,
  while Troll and Dark Lord should feel larger than Goblin.
- Use e.g. CSS image-rendering: pixelated where it suits the actual display scaling.
- Assess horizontal crop at narrow responsive widths; keep the central battle ground visible.
- This archive does not certify UI implementation, performance or responsive rendering.
