# Review: content integrator (final enemy / environment / 2D-art revisions)

## Plan
1. [x] Freshness check (script mtimes vs staging outputs): enemies 06:44 >= scripts 06:44; environment 06:40:20 >= env_pieces 06:40:19
   (layouts.json is read in place); art2d/UI extra 06:39-06:44 >= scripts; heroprops 19:27 >= model scripts 19:27. Nothing stale.
2. [ ] Prop_Pillar re-author (Tools/Blender/heroprops) + rebuild
3. [ ] sync_staging.sh (content diff: 10 enemy/boss FBX, 5 env FBX, 11 enemy icons, Debris/Leaf, Bar_Fill_Hp/Boss, Frame_Panel, Logo, ui_slices.json)
4. [ ] ImportSettingsBuilder + Build All in the lock; fix builder breakage
5. [ ] Play-mode screenshots (title, act 1/2/3, boss) vs polish/shots/after_*.png
6. [ ] Commit (pathspec) with metas
