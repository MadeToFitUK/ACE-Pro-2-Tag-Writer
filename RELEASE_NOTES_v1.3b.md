# ACE Pro 2 Tag Writer v1.3b

Maintenance release following the first public v1.3a release.

## Fixes

- Removed three old development/reference profiles that were hard-coded at startup. Deleted profiles now remain deleted after the program is closed and reopened.
- Manufacturer and material dropdown entries are now deduplicated case-insensitively, preventing entries such as `SUNLU` and `Sunlu` from appearing separately.
- Added an **About** button showing the application version, copyright, MIT licence, independence notice and GitHub project identity.

## Regression checks

The v1.3b release candidate was tested after these changes. Saved profiles persisted across close/reopen, deleted unwanted profiles did not return, and duplicate manufacturer casing no longer appeared in the dropdown.

## Unchanged core behaviour

The NFC/tag-writing logic, ABGR colour conversion, profile update behaviour, reel-weight calculator and tag verification workflow are unchanged from v1.3a.

## Licence and independence

Copyright © 2026 MadeToFitUK. Released under the MIT License.

This is an independent community project and is not affiliated with, authorised by, sponsored by or endorsed by Anycubic.
