# ACE Pro 2 RFID Tag Writer v1.3a

## Release candidate

v1.3a is the first release-candidate package prepared after successful real-world testing with an ACE Pro 2, ACR122U reader/writer and third-party NFC tags.

### Highlights

- Normal `#RRGGBB` manufacturer HEX colours with automatic ACE ABGR conversion.
- Saved third-party filament profiles with manufacturer colour, material and temperature information.
- New reel gross and empty-spool weights stored with the profile.
- Reel Weight Calculator based on physical weighing rather than trusting the ACE depletion graphic as a scale.
- Write + Verify workflow and protection against writing unsaved profile changes.
- Duplicate-profile detection and case-insensitive profile matching.
- Loaded profiles update in place even if identity text/capitalisation changes (`SUNLU` -> `Sunlu`).
- Old imported GitHub profiles can be deleted normally.
- Dedicated filament-reel application icon.

### Important experimental finding

Testing provides strong experimental evidence that the ACE remaining-reel graphic is influenced by filament movement and spool rotation/effective diameter rather than being a simple remaining-weight value continually stored on the NFC tag. For important prints, physical reel weighing is recommended.

### Observed slicer behaviour

Third-party programmed tags may still appear with manufacturer **Anycubic** in the slicer even when the local profile records the real manufacturer. Material and colour recognition can still work correctly.

### Safety / practical note

When a spool is nearly empty, watch the filament end. Some manufacturers secure it with adhesive or tape. Do not allow a sticky end to be pulled into the ACE feed path.
