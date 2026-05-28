Drop icon PNGs into this folder to make them available to the runtime UGUI shell.

Naming convention:

- `icon_home_brand`
- `icon_home_white`
- `icon_shop_brand`
- `icon_shop_white`
- `icon_map_brand`
- `icon_map_white`
- `icon_profile_brand`
- `icon_profile_white`
- `icon_settings_brand`
- `icon_settings_white`
- `icon_audio`
- `icon_notification`
- `icon_language`
- `icon_feedback`
- `icon_rating`
- `icon_share`
- `icon_nextarrow`
- `icon_pawprint_other`

Rules:

- Keep the files as transparent PNGs.
- The runtime loads them from `Resources/UI/Icons/<name>`.
- Missing icons do not crash the UI, but Unity will log a warning.
