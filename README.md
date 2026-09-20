# ACE Pro 2 RFID Tag Writer

A Windows utility for reading, creating, editing and verifying filament NFC tags for the **Anycubic ACE Pro 2**, using an **ACS ACR122U USB NFC reader/writer**.

The project grew from practical reverse-engineering of genuine Anycubic tags, experiments with third-party NTAG tags, and repeated testing in a real ACE Pro 2. The aim is simple: make useful third-party filament tags without requiring users to type APDU commands by hand.

> **Current release:** v1.3b has been physically tested with real tags and an ACE Pro 2. This remains an independent community project, not official Anycubic software.

## Features

- Read compatible NFC filament tags.
- Create and save reusable filament profiles.
- Write and verify third-party filament tags.
- Enter ordinary manufacturer HEX colours as `#RRGGBB`; the program performs the ACE ABGR conversion internally.
- Store manufacturer colour names, material and nozzle/bed temperature ranges.
- Keep gross spool-weight information separate from the NFC protocol weight.
- Calculate actual filament remaining from physical reel weights.
- Add print requirement and safety margin checks before a long print.
- Maintain reel records and export reel data to CSV.
- Detect existing profiles while entering data and protect against accidental duplicates.
- Update a loaded profile even when its identifying text or capitalisation is edited.
- Deleted profiles remain deleted after restart; old development/reference profiles are no longer recreated at startup.
- Manufacturer and material dropdowns are deduplicated case-insensitively (for example, `SUNLU` and `Sunlu` appear only once).
- Built-in About box identifies v1.3b, MadeToFitUK, the MIT licence and the project repository.

## Hardware used during development

Development and physical testing used:

- Anycubic ACE Pro 2
- ACS ACR122U USB NFC reader/writer
- Genuine Anycubic NXP NTAG213 tags
- Third-party NXP NTAG215 stickers

NTAG213 and NTAG215 tags have both been successfully read during the project. Never attempt to overwrite the factory UID/manufacturer pages (pages 00-03).

## The ACE remaining-reel graphic

A major part of the project was investigating how the ACE Pro 2 decides how much filament is left.

Our testing provides **strong experimental evidence that the decreasing reel graphic is not simply a remaining-weight value stored and continually updated on the NFC tag**. Printing substantial quantities did not produce a corresponding depletion value in the tag data. In a separate experiment, changing spool rotation relative to filament movement caused the displayed reel level to change without rewriting the tag.

The behaviour is consistent with the ACE estimating remaining filament from **filament movement and spool rotation/effective wound diameter**. Because third-party spools can have different hub and flange geometry, the graphic should be treated as an estimate rather than an accurate measurement of grams remaining.

This is an experimental finding, not a claim about undocumented Anycubic firmware internals.

## A better way: weigh the reel

When the amount remaining actually matters, **physical weighing is the more dependable method**.

The profile stores:

- **New reel gross** - the complete new reel including spool (default 1200 g until measured).
- **Empty spool** - the weight of the empty spool.

The Reel Weight Calculator then only needs the changing information: **Current gross**, **Print needs**, and **Safety margin**.

The useful calculation is:

`remaining filament = current gross - empty spool`

For the best result, weigh a new reel before first use. When the first reel of that type is empty, weigh the empty spool and update the profile. The calculator can then compare the physical amount remaining with the slicer's estimated print requirement plus your chosen safety margin.

The calculator is deliberately separate from NFC depletion data and does **not** write calculated remaining quantity back to the tag.

## Colour handling

Enter the manufacturer's normal HEX value in conventional `#RRGGBB` format. The utility converts it internally to the ABGR byte order used by the ACE tag.

This was physically tested with multiple real manufacturer HEX colours and the ACE displayed the intended colours correctly.

**Manufacturer Colour** is the human-readable maker's name, such as `Red`, `Bone White` or `Sunny Orange`. **HEX** is the actual colour value.

## Tips & Tricks

### Weigh a new reel

Do not assume the advertised filament weight tells you the complete gross reel weight. Put a new reel on scales and save the actual gross value in its profile.

### Keep an empty spool

Once you finish the first reel of a particular type, weigh the empty spool. That makes future remaining-weight calculations much more useful.

### Do not rely solely on the ACE reel graphic

For a long print or a marginal reel, use scales and the Reel Weight Calculator. Spool geometry can affect the ACE graphical estimate.

### Use a safety margin

Slicer material requirements are estimates. Add a sensible margin rather than planning to finish with zero grams remaining.

### Use the manufacturer's actual HEX colour

When the manufacturer publishes a HEX value, use it rather than estimating by eye from a monitor. The utility handles the required ABGR conversion.

### Check temperatures carefully

A saved profile is convenient, but it also makes an incorrect value easy to reuse. Check nozzle and bed temperatures against the reel label or manufacturer's specifications before writing several tags.

### Save before writing

New or edited profile data must be saved before **Write + Verify**. This prevents an unfinished set of screen values being written accidentally.

### Use Write + Verify

Verification reads the written data back and compares it with the intended profile. Use it rather than assuming a write completed correctly.

### Watch the end of a nearly empty reel

Do not assume the final end of the filament will simply pull free from the spool.

During development, one reel was found to have the filament end fixed to the spool with **heavy-duty adhesive**. If it had been left unattended, the sticky end could have been pulled into the ACE feed path, potentially contaminating rollers, gears or tubes with adhesive.

When a reel is nearly empty, watch the final section and remove or cut the secured end before adhesive or tape can enter the ACE. Different manufacturers secure filament differently, so check unfamiliar spools.

## Third-party manufacturer display

During testing, the Anycubic slicer could display the manufacturer as **Anycubic** even when the Windows profile contained the actual third-party manufacturer. Material and colour recognition still worked. This is recorded as observed behaviour; the utility does not attempt to override the slicer's manufacturer display.

## Building on Windows

The project is intentionally small: one C# WinForms source file plus the application icon.

Extract/download the files, open Command Prompt in the project folder, and run:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /win32icon:ACE_Pro_2_Tag_Writer.ico /out:ACE_Pro_2_Tag_Writer.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ACE_Pro_2_Tag_Writer.cs
